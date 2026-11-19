using HCMS4.Data;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Feedback.Services
{
    /// <summary>The created survey and the users who were sent it (empty for a draft).</summary>
    public record SurveyCreated(Survey Survey, List<string> AssignedUserIds);

    /// <summary>Surveys: created and sent by the admin, answered by patients.</summary>
    public interface ISurveyService
    {
        Task<List<Survey>> GetAllAsync();
        Task<List<PatientSelectDto>> GetPatientOptionsAsync();
        /// <summary>Form-level checks; each item is (field name or empty, message).</summary>
        List<(string Field, string Message)> Validate(SurveyEditorViewModel model);
        /// <summary>Saves the survey; when "send immediately" is set, assigns it to the target patients.</summary>
        Task<SurveyCreated> CreateAsync(SurveyEditorViewModel model, string? createdByUserId);

        Task<List<SurveyAssignment>> GetAssignmentsAsync(int patientId);
        /// <summary>The answer form of a pending assignment of an active survey; null when it is not available.</summary>
        Task<SurveyResponseViewModel?> GetResponseFormAsync(int assignmentId, int patientId, SurveyResponseViewModel? submitted = null);
        /// <summary>Records the answers. A failure with errors means unanswered required questions; without errors, the survey is not available.</summary>
        Task<ServiceResult<int>> SubmitAsync(int patientId, SurveyResponseViewModel model);
    }

    public class SurveyService : ISurveyService
    {
        private readonly ApplicationDbContext _context;
        private readonly IClinicClock _clock;

        public SurveyService(ApplicationDbContext context, IClinicClock clock)
        {
            _context = context;
            _clock = clock;
        }

        public Task<List<Survey>> GetAllAsync() =>
            _context.Surveys.AsNoTracking()
                .Include(s => s.Questions)
                .Include(s => s.Assignments)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

        public Task<List<PatientSelectDto>> GetPatientOptionsAsync() =>
            _context.Patients.AsNoTracking()
                .Where(p => p.Status == PatientStatus.Active)
                .Select(p => new PatientSelectDto
                {
                    Id = p.Id,
                    FullName = p.User.FullName,
                    Email = p.User.Email ?? "N/A"
                })
                .ToListAsync();

        public List<(string Field, string Message)> Validate(SurveyEditorViewModel model)
        {
            var errors = new List<(string, string)>();
            if (model.EndDate.HasValue && model.EndDate.Value < model.StartDate)
                errors.Add((nameof(model.EndDate), "End date must be after start date."));

            if (!model.Questions.Any(q => !string.IsNullOrWhiteSpace(q.QuestionText)))
                errors.Add((string.Empty, "You must add at least one question."));

            if (model.TargetAudience == SurveyTargetAudience.SpecificPatients && !model.SpecificPatientIds.Any())
                errors.Add((string.Empty, "Please select at least one patient for a specific-patient survey."));

            return errors;
        }

        public async Task<SurveyCreated> CreateAsync(SurveyEditorViewModel model, string? createdByUserId)
        {
            var now = _clock.Now;
            var survey = new Survey
            {
                Title = model.Title.Trim(),
                Description = model.Description?.Trim(),
                StartDate = model.StartDate.Date,
                EndDate = model.EndDate?.Date,
                TargetAudience = model.TargetAudience,
                TargetCriteria = model.TargetCriteria?.Trim(),
                Status = model.SendImmediately ? SurveyStatus.Active : SurveyStatus.Draft,
                CreatedByUserId = createdByUserId ?? string.Empty,
                CreatedAt = now,
                UpdatedAt = now,
                SentAt = model.SendImmediately ? now : null
            };

            var order = 0;
            foreach (var question in model.Questions.Where(q => !string.IsNullOrWhiteSpace(q.QuestionText)))
            {
                var surveyQuestion = new SurveyQuestion
                {
                    QuestionText = question.QuestionText.Trim(),
                    QuestionType = question.QuestionType,
                    IsRequired = question.IsRequired,
                    DisplayOrder = order++
                };

                if (question.QuestionType == SurveyQuestionType.MultipleChoice && !string.IsNullOrWhiteSpace(question.OptionsText))
                {
                    var options = question.OptionsText
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(o => o.Trim())
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .Distinct()
                        .ToList();
                    for (var i = 0; i < options.Count; i++)
                        surveyQuestion.Options.Add(new SurveyQuestionOption { OptionText = options[i], DisplayOrder = i });
                }

                survey.Questions.Add(surveyQuestion);
            }

            _context.Surveys.Add(survey);

            var assignedUserIds = new List<string>();
            if (model.SendImmediately)
            {
                foreach (var patient in await GetTargetPatientsAsync(model))
                {
                    survey.Assignments.Add(new SurveyAssignment
                    {
                        PatientId = patient.Id,
                        Status = SurveyAssignmentStatus.Pending,
                        AssignedAt = now
                    });
                    assignedUserIds.Add(patient.UserId);
                }
            }

            await _context.SaveChangesAsync();
            return new SurveyCreated(survey, assignedUserIds);
        }

        public Task<List<SurveyAssignment>> GetAssignmentsAsync(int patientId) =>
            _context.SurveyAssignments.AsNoTracking()
                .Include(sa => sa.Survey)
                .Where(sa => sa.PatientId == patientId)
                .OrderByDescending(sa => sa.AssignedAt)
                .ToListAsync();

        public async Task<SurveyResponseViewModel?> GetResponseFormAsync(int assignmentId, int patientId,
            SurveyResponseViewModel? submitted = null)
        {
            var today = _clock.Today;
            var assignment = await _context.SurveyAssignments.AsNoTracking()
                .Include(sa => sa.Survey)
                .FirstOrDefaultAsync(sa => sa.Id == assignmentId &&
                                           sa.PatientId == patientId &&
                                           sa.Status == SurveyAssignmentStatus.Pending);

            if (assignment == null ||
                assignment.Survey.Status != SurveyStatus.Active ||
                assignment.Survey.StartDate > today ||
                (assignment.Survey.EndDate.HasValue && assignment.Survey.EndDate.Value < today))
            {
                return null;
            }

            var questions = await _context.SurveyQuestions.AsNoTracking()
                .Include(q => q.Options)
                .Where(q => q.SurveyId == assignment.SurveyId)
                .OrderBy(q => q.DisplayOrder)
                .ToListAsync();

            return new SurveyResponseViewModel
            {
                SurveyId = assignment.SurveyId,
                SurveyAssignmentId = assignment.Id,
                Title = assignment.Survey.Title,
                Description = assignment.Survey.Description,
                Questions = questions.Select(question =>
                {
                    var answer = submitted?.Questions.FirstOrDefault(q => q.SurveyQuestionId == question.Id);
                    return new SurveyResponseQuestionViewModel
                    {
                        SurveyQuestionId = question.Id,
                        QuestionText = question.QuestionText,
                        QuestionType = question.QuestionType,
                        IsRequired = question.IsRequired,
                        Options = question.Options.OrderBy(o => o.DisplayOrder).ToList(),
                        AnswerText = answer?.AnswerText,
                        NumericValue = answer?.NumericValue,
                        BooleanValue = answer?.BooleanValue
                    };
                }).ToList()
            };
        }

        public async Task<ServiceResult<int>> SubmitAsync(int patientId, SurveyResponseViewModel model)
        {
            var assignment = await _context.SurveyAssignments
                .FirstOrDefaultAsync(sa => sa.Id == model.SurveyAssignmentId && sa.PatientId == patientId);

            if (assignment == null || assignment.Status == SurveyAssignmentStatus.Completed)
                return ServiceResult<int>.Fail("Survey not found or has already been completed.");

            var missing = model.Questions
                .Where(q => q.IsRequired && !HasAnswer(q))
                .Select(q => $"Please answer question before proceeding: {q.QuestionText}")
                .ToList();
            if (missing.Any())
                return ServiceResult<int>.Fail("Some required questions are not answered.", missing);

            _context.SurveyAnswers.RemoveRange(
                await _context.SurveyAnswers.Where(a => a.SurveyAssignmentId == assignment.Id).ToListAsync());

            foreach (var question in model.Questions)
            {
                _context.SurveyAnswers.Add(new SurveyAnswer
                {
                    SurveyAssignmentId = assignment.Id,
                    SurveyQuestionId = question.SurveyQuestionId,
                    AnswerText = question.AnswerText,
                    NumericValue = question.NumericValue,
                    BooleanValue = question.BooleanValue
                });
            }

            assignment.Status = SurveyAssignmentStatus.Completed;
            assignment.CompletedAt = _clock.Now;
            await _context.SaveChangesAsync();

            return ServiceResult<int>.Ok(assignment.SurveyId,
                "Thank you for participating! Your answers have been recorded successfully.");
        }

        private static bool HasAnswer(SurveyResponseQuestionViewModel question) => question.QuestionType switch
        {
            SurveyQuestionType.MultipleChoice => !string.IsNullOrWhiteSpace(question.AnswerText),
            SurveyQuestionType.Rating => question.NumericValue.HasValue,
            SurveyQuestionType.YesNo => question.BooleanValue.HasValue,
            _ => !string.IsNullOrWhiteSpace(question.AnswerText)
        };

        private Task<List<Patient>> GetTargetPatientsAsync(SurveyEditorViewModel model)
        {
            IQueryable<Patient> query = _context.Patients.AsNoTracking()
                .Include(p => p.User)
                .Where(p => p.Status == PatientStatus.Active);

            if (model.TargetAudience == SurveyTargetAudience.SpecificPatients)
            {
                query = query.Where(p => model.SpecificPatientIds.Contains(p.Id));
            }
            else if (model.TargetAudience == SurveyTargetAudience.SpecificCategory &&
                     !string.IsNullOrWhiteSpace(model.TargetCriteria))
            {
                var criteria = model.TargetCriteria.Trim().ToLower();
                query = query.Where(p =>
                    (p.ChronicConditions != null && p.ChronicConditions.ToLower().Contains(criteria)) ||
                    (p.Address != null && p.Address.ToLower().Contains(criteria)) ||
                    (p.User.Email != null && p.User.Email.ToLower().Contains(criteria)) ||
                    (p.User.FirstName + " " + p.User.LastName).ToLower().Contains(criteria));
            }

            return query.ToListAsync();
        }
    }
}
