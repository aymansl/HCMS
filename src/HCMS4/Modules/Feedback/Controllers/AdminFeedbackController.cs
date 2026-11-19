using HCMS4.Controllers;
using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Models;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using HCMS4.Shared.Reporting.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static HCMS4.ViewModels.GenerateInvoiceViewModel;
using System.Security.Claims;

namespace HCMS4.Modules.Feedback.Controllers
{
    // Part of the former AdminController; [ControllerName] keeps the /Admin/... URLs.
    [Authorize(Roles = "Admin")]
    [ControllerName("Admin")]
    public class AdminFeedbackController : BaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IDailyReportService _reportService;
        private readonly IPatientService _patientService;
        private readonly IAppointmentService _appointmentService;
        private readonly IPrescriptionService _prescriptionService;
        private readonly ApplicationDbContext _adminContext;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly ILeaveService _leaveService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public AdminFeedbackController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AdminFeedbackController> logger,
            IDailyReportService reportService,
            IPatientService patientService,
            IAppointmentService appointmentService,
            IPrescriptionService prescriptionService,
            INoShowRiskService noShowRiskService,
            ILeaveService leaveService,
            INotificationService notificationService,
            IActivityLogService activityLogService)
            : base(context, logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _reportService = reportService;
            _patientService = patientService;
            _appointmentService = appointmentService;
            _prescriptionService = prescriptionService;
            _adminContext = context;
            _noShowRiskService = noShowRiskService;
            _leaveService = leaveService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Complaints()
        {
            var complaints = await _context.Complaints
                .Include(c => c.Patient)
                    .ThenInclude(p => p.User)
                .OrderByDescending(c => c.SubmissionDate)
                .ToListAsync();

            return View(complaints);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ComplaintDetails(int id)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Patient)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
            {
                TempData["ErrorMessage"] = "Complaint not found.";
                return RedirectToAction(nameof(Complaints));
            }

            if (complaint.Status == ComplaintStatus.Submitted)
            {
                complaint.Status = ComplaintStatus.UnderReview;
                await _context.SaveChangesAsync();
            }

            return View(complaint);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateComplaintStatus(int id, ComplaintStatus status, string? resolutionNotes)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Patient)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
            {
                TempData["ErrorMessage"] = "Complaint not found.";
                return RedirectToAction(nameof(Complaints));
            }

            complaint.Status = status;
            complaint.ResolutionNotes = resolutionNotes;
            complaint.ResolvedAt = status == ComplaintStatus.Resolved || status == ComplaintStatus.Closed
                ? ClinicTime.Now
                : null;

            await _context.SaveChangesAsync();

            await _notificationService.CreateForUserAsync(
                complaint.Patient.UserId,
                "Complaint status updated",
                $"Your complaint {complaint.TrackingNumber} status is now {status}.",
                NotificationType.Complaint,
                "/Patient/Complaints",
                nameof(Complaint),
                complaint.Id);

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "ComplaintStatusUpdated",
                nameof(Complaint),
                $"Complaint {complaint.TrackingNumber} updated to {status}.",
                complaint.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Complaint updated successfully.";
            return RedirectToAction(nameof(ComplaintDetails), new { id });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Surveys()
        {
            var surveys = await _context.Surveys
                .Include(s => s.Questions)
                .Include(s => s.Assignments)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return View(surveys);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> CreateSurvey()
        {
            var viewModel = new SurveyEditorViewModel
            {
                AvailablePatients = await _context.Patients
                    .Include(p => p.User)
                    .Where(p => p.Status == PatientStatus.Active)
                    .Select(p => new PatientSelectDto
                    {
                        Id = p.Id,
                        FullName = p.User.FullName,
                        Email = p.User.Email ?? "N/A"
                    })
                    .ToListAsync()
            };

            return View(viewModel);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSurvey(SurveyEditorViewModel model)
        {
            if (model.EndDate.HasValue && model.EndDate.Value < model.StartDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "End date must be after start date.");
            }

            var validQuestions = model.Questions
                .Where(q => !string.IsNullOrWhiteSpace(q.QuestionText))
                .ToList();

            if (!validQuestions.Any())
            {
                ModelState.AddModelError(string.Empty, "You must add at least one question.");
            }

            if (model.TargetAudience == SurveyTargetAudience.SpecificPatients && !model.SpecificPatientIds.Any())
            {
                ModelState.AddModelError(string.Empty, "Please select at least one patient for a specific-patient survey.");
            }

            if (!ModelState.IsValid)
            {
                model.AvailablePatients = await _context.Patients
                    .Include(p => p.User)
                    .Where(p => p.Status == PatientStatus.Active)
                    .Select(p => new PatientSelectDto
                    {
                        Id = p.Id,
                        FullName = p.User.FullName,
                        Email = p.User.Email ?? "N/A"
                    })
                    .ToListAsync();
                return View(model);
            }

            var currentUser = await GetCurrentUserAsync();
            var survey = new Survey
            {
                Title = model.Title.Trim(),
                Description = model.Description?.Trim(),
                StartDate = model.StartDate.Date,
                EndDate = model.EndDate?.Date,
                TargetAudience = model.TargetAudience,
                TargetCriteria = model.TargetCriteria?.Trim(),
                Status = model.SendImmediately ? SurveyStatus.Active : SurveyStatus.Draft,
                CreatedByUserId = currentUser?.Id ?? string.Empty,
                CreatedAt = ClinicTime.Now,
                UpdatedAt = ClinicTime.Now,
                SentAt = model.SendImmediately ? ClinicTime.Now : null
            };

            _context.Surveys.Add(survey);
            await _context.SaveChangesAsync();

            var questionOrder = 0;
            foreach (var question in validQuestions)
            {
                var surveyQuestion = new SurveyQuestion
                {
                    SurveyId = survey.Id,
                    QuestionText = question.QuestionText.Trim(),
                    QuestionType = question.QuestionType,
                    IsRequired = question.IsRequired,
                    DisplayOrder = questionOrder++
                };

                _context.SurveyQuestions.Add(surveyQuestion);
                await _context.SaveChangesAsync();

                if (question.QuestionType == SurveyQuestionType.MultipleChoice &&
                    !string.IsNullOrWhiteSpace(question.OptionsText))
                {
                    var options = question.OptionsText
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(o => o.Trim())
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .Distinct()
                        .ToList();

                    for (var i = 0; i < options.Count; i++)
                    {
                        _context.SurveyQuestionOptions.Add(new SurveyQuestionOption
                        {
                            SurveyQuestionId = surveyQuestion.Id,
                            OptionText = options[i],
                            DisplayOrder = i
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            if (model.SendImmediately)
            {
                var targetPatients = await GetSurveyTargetPatientsAsync(model);
                foreach (var patient in targetPatients)
                {
                    _context.SurveyAssignments.Add(new SurveyAssignment
                    {
                        SurveyId = survey.Id,
                        PatientId = patient.Id,
                        Status = SurveyAssignmentStatus.Pending,
                        AssignedAt = ClinicTime.Now
                    });

                    await _notificationService.CreateForUserAsync(
                        patient.UserId,
                        "New survey available",
                        $"A new survey '{survey.Title}' is available in your account.",
                        NotificationType.Survey,
                        "/Patient/Surveys",
                        nameof(Survey),
                        survey.Id);
                }

                await _context.SaveChangesAsync();
            }

            await _activityLogService.LogAsync(
                model.SendImmediately ? "SurveyCreatedAndSent" : "SurveyDraftCreated",
                nameof(Survey),
                $"Survey '{survey.Title}' was {(model.SendImmediately ? "created and sent" : "saved as draft")}.",
                survey.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Survey created successfully.";
            return RedirectToAction(nameof(Surveys));
        }

        private async Task<List<Patient>> GetSurveyTargetPatientsAsync(SurveyEditorViewModel model)
        {
            IQueryable<Patient> query = _context.Patients
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

            return await query.ToListAsync();
        }
    }
}
