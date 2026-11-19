using HCMS4.Controllers;
using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Billing.Services;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Feedback.Services;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HCMS4.Modules.Feedback.Controllers
{
    // Part of the former PatientController; [ControllerName] keeps the /Patient/... URLs.
    [Authorize(Roles = "Patient")]
    [ControllerName("Patient")]
    public class PatientFeedbackController : BaseController
    {
        private readonly IAppointmentService _appointmentService;
        private readonly ILeaveService _leaveService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;
        private readonly IComplaintAttachmentService _attachmentService;
        private readonly IInvoicePaymentService _paymentService;

        public PatientFeedbackController(
            ApplicationDbContext context,
            ILogger<PatientFeedbackController> logger,
            IAppointmentService appointmentService,
            ILeaveService leaveService,
            INotificationService notificationService,
            IActivityLogService activityLogService,
            IComplaintAttachmentService attachmentService,
            IInvoicePaymentService paymentService)
            : base(context, logger)
        {
            _paymentService = paymentService;
            _appointmentService = appointmentService;
            _leaveService = leaveService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
            _attachmentService = attachmentService;
        }

        [HttpGet]
        public async Task<IActionResult> RateVisit(int appointmentId)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId.Value);

            if (appointment == null || appointment.Status != AppointmentStatus.Completed)
            {
                TempData["ErrorMessage"] = "Only completed visits can be rated.";
                return RedirectToAction("ViewAppointmentHistory");
            }

            var existingRating = await _context.VisitRatings
                .AnyAsync(vr => vr.PatientId == patientId.Value && vr.AppointmentId == appointmentId);

            if (existingRating)
            {
                TempData["ErrorMessage"] = "You have already rated this visit.";
                return RedirectToAction("ViewAppointmentHistory");
            }

            return View(new VisitRatingFormViewModel
            {
                AppointmentId = appointment.Id,
                DoctorId = appointment.DoctorId,
                DoctorName = appointment.Doctor?.User?.FullName ?? "Doctor",
                AppointmentDateTime = appointment.AppointmentDateTime
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateVisit(VisitRatingFormViewModel model)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == model.AppointmentId && a.PatientId == patientId.Value);

            if (appointment == null || appointment.Status != AppointmentStatus.Completed)
            {
                TempData["ErrorMessage"] = "Only completed visits can be rated.";
                return RedirectToAction("ViewAppointmentHistory");
            }

            if (await _context.VisitRatings.AnyAsync(vr => vr.PatientId == patientId.Value && vr.AppointmentId == model.AppointmentId))
            {
                TempData["ErrorMessage"] = "You have already rated this visit.";
                return RedirectToAction("ViewAppointmentHistory");
            }

            if (!ModelState.IsValid)
            {
                model.DoctorName = appointment.Doctor?.FullName ?? model.DoctorName;
                model.DoctorId = appointment.DoctorId;
                model.AppointmentDateTime = appointment.AppointmentDateTime;
                return View(model);
            }

            try
            {
                var rating = new VisitRating
                {
                    AppointmentId = appointment.Id,
                    PatientId = patientId.Value,
                    DoctorId = appointment.DoctorId,
                    Rating = model.Rating,
                    Comment = model.Comment,
                    DoctorCooperative = model.DoctorCooperative,
                    WaitingTimeReasonable = model.WaitingTimeReasonable,
                    CreatedAt = ClinicTime.Now
                };

                _context.VisitRatings.Add(rating);
                await _context.SaveChangesAsync();

                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == appointment.DoctorId);
                if (doctor != null)
                {
                    doctor.RatingCount = await _context.VisitRatings.CountAsync(vr => vr.DoctorId == doctor.Id);
                    doctor.AverageRating = Math.Round((decimal)await _context.VisitRatings
                        .Where(vr => vr.DoctorId == doctor.Id)
                        .AverageAsync(vr => vr.Rating), 2);
                    await _context.SaveChangesAsync();
                }

                var currentUser = await GetCurrentUserAsync();
                await _activityLogService.LogAsync(
                    "VisitRated",
                    nameof(VisitRating),
                    $"Patient rated appointment #{appointment.Id} with {model.Rating} star(s).",
                    rating.Id.ToString(),
                    currentUser?.Id,
                    currentUser?.UserName);

                TempData["SuccessMessage"] = "Thank you for your rating, your feedback has been submitted successfully.";
                return RedirectToAction("ViewAppointmentHistory");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving visit rating for appointment {AppointmentId}", model.AppointmentId);
                ModelState.AddModelError(string.Empty, "Failed to save rating, please try again later.");
                model.DoctorName = appointment.Doctor?.FullName ?? model.DoctorName;
                model.DoctorId = appointment.DoctorId;
                model.AppointmentDateTime = appointment.AppointmentDateTime;
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Complaints()
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var complaints = await _context.Complaints
                .Where(c => c.PatientId == patientId.Value)
                .OrderByDescending(c => c.SubmissionDate)
                .ToListAsync();

            return View(complaints);
        }

        [HttpGet]
        public IActionResult CreateComplaint()
        {
            return View(new PatientComplaintFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateComplaint(PatientComplaintFormViewModel model)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string? attachmentPath = null;
            if (model.Attachment != null)
            {
                var upload = await _attachmentService.SaveAsync(model.Attachment);
                if (!upload.Success)
                {
                    ModelState.AddModelError(nameof(model.Attachment), upload.Message);
                    return View(model);
                }

                // Only the stored file name is kept; the file is served by ComplaintAttachmentsController.
                attachmentPath = upload.Data;
            }

            var complaint = new Complaint
            {
                PatientId = patientId.Value,
                Title = model.Title.Trim(),
                Type = model.Type,
                Description = model.Description.Trim(),
                AssociatedVisitDate = model.AssociatedVisitDate,
                AttachmentPath = attachmentPath,
                TrackingNumber = $"CMP-{ClinicTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                SubmissionDate = ClinicTime.Now,
                Status = ComplaintStatus.Submitted
            };

            _context.Complaints.Add(complaint);
            await _context.SaveChangesAsync();

            await _notificationService.CreateForRoleAsync(
                "Admin",
                "New patient complaint",
                $"A new complaint ({complaint.TrackingNumber}) requires review.",
                NotificationType.Complaint,
                "/Admin/Complaints",
                nameof(Complaint),
                complaint.Id);

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "ComplaintSubmitted",
                nameof(Complaint),
                $"Patient submitted complaint {complaint.TrackingNumber}.",
                complaint.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = $"Complaint submitted successfully. Tracking number: {complaint.TrackingNumber}";
            return RedirectToAction(nameof(Complaints));
        }

        [HttpGet]
        public async Task<IActionResult> Surveys()
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var assignments = await _context.SurveyAssignments
                .Include(sa => sa.Survey)
                .Where(sa => sa.PatientId == patientId.Value)
                .OrderByDescending(sa => sa.AssignedAt)
                .ToListAsync();

            return View(assignments);
        }

        [HttpGet]
        public async Task<IActionResult> TakeSurvey(int assignmentId)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await BuildSurveyResponseViewModelAsync(assignmentId, patientId.Value);
            if (viewModel == null)
            {
                TempData["ErrorMessage"] = "Survey not found or is no longer available.";
                return RedirectToAction(nameof(Surveys));
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TakeSurvey(SurveyResponseViewModel model)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var assignment = await _context.SurveyAssignments
                .Include(sa => sa.Survey)
                .FirstOrDefaultAsync(sa => sa.Id == model.SurveyAssignmentId &&
                                           sa.PatientId == patientId.Value);

            if (assignment == null || assignment.Status == SurveyAssignmentStatus.Completed)
            {
                TempData["ErrorMessage"] = "Survey not found or has already been completed.";
                return RedirectToAction(nameof(Surveys));
            }

            foreach (var question in model.Questions)
            {
                if (question.IsRequired)
                {
                    var hasAnswer = question.QuestionType switch
                    {
                        SurveyQuestionType.MultipleChoice => !string.IsNullOrWhiteSpace(question.AnswerText),
                        SurveyQuestionType.Rating => question.NumericValue.HasValue,
                        SurveyQuestionType.YesNo => question.BooleanValue.HasValue,
                        _ => !string.IsNullOrWhiteSpace(question.AnswerText)
                    };

                    if (!hasAnswer)
                    {
                        ModelState.AddModelError(string.Empty, $"Please answer question before proceeding: {question.QuestionText}");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                var refreshedModel = await BuildSurveyResponseViewModelAsync(model.SurveyAssignmentId, patientId.Value, model);
                return View(refreshedModel ?? model);
            }

            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM SurveyAnswers WHERE SurveyAssignmentId = {assignment.Id}");

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
            assignment.CompletedAt = ClinicTime.Now;
            await _context.SaveChangesAsync();

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "SurveyCompleted",
                nameof(Survey),
                $"Patient completed survey #{assignment.SurveyId}.",
                assignment.SurveyId.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Thank you for participating! Your answers have been recorded successfully.";
            return RedirectToAction("Dashboard");
        }

        private async Task<SurveyResponseViewModel?> BuildSurveyResponseViewModelAsync(int assignmentId, int patientId,
            SurveyResponseViewModel? submittedModel = null)
        {
            var assignment = await _context.SurveyAssignments
                .Include(sa => sa.Survey)
                .FirstOrDefaultAsync(sa => sa.Id == assignmentId &&
                                           sa.PatientId == patientId &&
                                           sa.Status == SurveyAssignmentStatus.Pending);

            if (assignment == null ||
                assignment.Survey.Status != SurveyStatus.Active ||
                assignment.Survey.StartDate > ClinicTime.Now.Date ||
                (assignment.Survey.EndDate.HasValue && assignment.Survey.EndDate.Value < ClinicTime.Now.Date))
            {
                return null;
            }

            var questions = await _context.SurveyQuestions
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
                    var submittedQuestion = submittedModel?.Questions
                        .FirstOrDefault(q => q.SurveyQuestionId == question.Id);

                    return new SurveyResponseQuestionViewModel
                    {
                        SurveyQuestionId = question.Id,
                        QuestionText = question.QuestionText,
                        QuestionType = question.QuestionType,
                        IsRequired = question.IsRequired,
                        Options = question.Options.OrderBy(o => o.DisplayOrder).ToList(),
                        AnswerText = submittedQuestion?.AnswerText,
                        NumericValue = submittedQuestion?.NumericValue,
                        BooleanValue = submittedQuestion?.BooleanValue
                    };
                }).ToList()
            };
        }
    }
}
