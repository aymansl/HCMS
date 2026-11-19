using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Feedback.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HCMS4.Shared.Identity.Authorization;

namespace HCMS4.Modules.Feedback.Controllers
{
    // Part of the former PatientController; [ControllerName] keeps the /Patient/... URLs.
    [Authorize(Policy = Permissions.Feedback.Submit)]
    [ControllerName("Patient")]
    public class PatientFeedbackController : HcmsControllerBase
    {
        private readonly IVisitRatingService _visitRatingService;
        private readonly IComplaintService _complaintService;
        private readonly ISurveyService _surveyService;
        private readonly IComplaintAttachmentService _attachmentService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;
        private readonly ILogger<PatientFeedbackController> _logger;

        public PatientFeedbackController(
            IVisitRatingService visitRatingService,
            IComplaintService complaintService,
            ISurveyService surveyService,
            IComplaintAttachmentService attachmentService,
            INotificationService notificationService,
            IActivityLogService activityLogService,
            ILogger<PatientFeedbackController> logger)
        {
            _visitRatingService = visitRatingService;
            _complaintService = complaintService;
            _surveyService = surveyService;
            _attachmentService = attachmentService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> RateVisit(int appointmentId)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var form = await _visitRatingService.GetRatingFormAsync(patientId.Value, appointmentId);
            if (!form.Success)
            {
                TempData["ErrorMessage"] = form.Message;
                return RedirectToAction("ViewAppointmentHistory");
            }

            return View(form.Data);
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

            if (!ModelState.IsValid)
            {
                return await RateVisitFormAsync(patientId.Value, model);
            }

            try
            {
                var result = await _visitRatingService.RateAsync(patientId.Value, model);
                if (!result.Success)
                {
                    TempData["ErrorMessage"] = result.Message;
                    return RedirectToAction("ViewAppointmentHistory");
                }

                await LogAsync("VisitRated", nameof(VisitRating),
                    $"Patient rated appointment #{model.AppointmentId} with {model.Rating} star(s).", result.Data);

                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("ViewAppointmentHistory");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving visit rating for appointment {AppointmentId}", model.AppointmentId);
                ModelState.AddModelError(string.Empty, "Failed to save rating, please try again later.");
                return await RateVisitFormAsync(patientId.Value, model);
            }
        }

        // Shows the form again with the submitted answers; leaves when the visit can no longer be rated.
        private async Task<IActionResult> RateVisitFormAsync(int patientId, VisitRatingFormViewModel model)
        {
            var form = await _visitRatingService.GetRatingFormAsync(patientId, model.AppointmentId);
            if (!form.Success)
            {
                TempData["ErrorMessage"] = form.Message;
                return RedirectToAction("ViewAppointmentHistory");
            }

            model.DoctorName = form.Data.DoctorName;
            model.DoctorId = form.Data.DoctorId;
            model.AppointmentDateTime = form.Data.AppointmentDateTime;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Complaints()
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            return View(await _complaintService.GetForPatientAsync(patientId.Value));
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

            string? attachmentFileName = null;
            if (model.Attachment != null)
            {
                var upload = await _attachmentService.SaveAsync(model.Attachment);
                if (!upload.Success)
                {
                    ModelState.AddModelError(nameof(model.Attachment), upload.Message);
                    return View(model);
                }

                // Only the stored file name is kept; the file is served by ComplaintAttachmentsController.
                attachmentFileName = upload.Data;
            }

            var complaint = await _complaintService.CreateAsync(patientId.Value, model, attachmentFileName);

            await _notificationService.CreateForRoleAsync(
                "Admin",
                "New patient complaint",
                $"A new complaint ({complaint.TrackingNumber}) requires review.",
                NotificationType.Complaint,
                "/Admin/Complaints",
                nameof(Complaint),
                complaint.Id);

            await LogAsync("ComplaintSubmitted", nameof(Complaint),
                $"Patient submitted complaint {complaint.TrackingNumber}.", complaint.Id);

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

            return View(await _surveyService.GetAssignmentsAsync(patientId.Value));
        }

        [HttpGet]
        public async Task<IActionResult> TakeSurvey(int assignmentId)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _surveyService.GetResponseFormAsync(assignmentId, patientId.Value);
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

            var result = await _surveyService.SubmitAsync(patientId.Value, model);
            if (!result.Success)
            {
                if (result.Errors?.Any() != true)
                {
                    TempData["ErrorMessage"] = result.Message;
                    return RedirectToAction(nameof(Surveys));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                var refreshed = await _surveyService.GetResponseFormAsync(model.SurveyAssignmentId, patientId.Value, model);
                return View(refreshed ?? model);
            }

            await LogAsync("SurveyCompleted", nameof(Survey), $"Patient completed survey #{result.Data}.", result.Data);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Dashboard");
        }

        private async Task LogAsync(string action, string entityName, string description, int entityId)
        {
            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(action, entityName, description, entityId.ToString(),
                currentUser?.Id, currentUser?.UserName);
        }
    }
}
