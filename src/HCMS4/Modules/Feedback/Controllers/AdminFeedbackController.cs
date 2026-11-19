using System.Globalization;
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
    // Part of the former AdminController; [ControllerName] keeps the /Admin/... URLs.
    [Authorize(Policy = Permissions.Feedback.Manage)]
    [ControllerName("Admin")]
    public class AdminFeedbackController : HcmsControllerBase
    {
        private readonly IComplaintService _complaintService;
        private readonly ISurveyService _surveyService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public AdminFeedbackController(
            IComplaintService complaintService,
            ISurveyService surveyService,
            INotificationService notificationService,
            IActivityLogService activityLogService)
        {
            _complaintService = complaintService;
            _surveyService = surveyService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
        }

        [HttpGet]
        public async Task<IActionResult> Complaints()
        {
            return View(await _complaintService.GetAllAsync());
        }

        [HttpGet]
        public async Task<IActionResult> ComplaintDetails(int id)
        {
            var complaint = await _complaintService.OpenForReviewAsync(id);
            if (complaint == null)
            {
                TempData["ErrorMessage"] = "Complaint not found.";
                return RedirectToAction(nameof(Complaints));
            }

            return View(complaint);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateComplaintStatus(int id, ComplaintStatus status, string? resolutionNotes)
        {
            var result = await _complaintService.UpdateStatusAsync(id, status, resolutionNotes);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(Complaints));
            }

            var complaint = result.Data;
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
                complaint.Id.ToString(CultureInfo.InvariantCulture),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(ComplaintDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Surveys()
        {
            return View(await _surveyService.GetAllAsync());
        }

        [HttpGet]
        public async Task<IActionResult> CreateSurvey()
        {
            return View(new SurveyEditorViewModel
            {
                AvailablePatients = await _surveyService.GetPatientOptionsAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSurvey(SurveyEditorViewModel model)
        {
            foreach (var (field, message) in _surveyService.Validate(model))
            {
                ModelState.AddModelError(field, message);
            }

            if (!ModelState.IsValid)
            {
                model.AvailablePatients = await _surveyService.GetPatientOptionsAsync();
                return View(model);
            }

            var currentUser = await GetCurrentUserAsync();
            var created = await _surveyService.CreateAsync(model, currentUser?.Id);
            var survey = created.Survey;

            foreach (var userId in created.AssignedUserIds)
            {
                await _notificationService.CreateForUserAsync(
                    userId,
                    "New survey available",
                    $"A new survey '{survey.Title}' is available in your account.",
                    NotificationType.Survey,
                    "/Patient/Surveys",
                    nameof(Survey),
                    survey.Id);
            }

            await _activityLogService.LogAsync(
                model.SendImmediately ? "SurveyCreatedAndSent" : "SurveyDraftCreated",
                nameof(Survey),
                $"Survey '{survey.Title}' was {(model.SendImmediately ? "created and sent" : "saved as draft")}.",
                survey.Id.ToString(CultureInfo.InvariantCulture),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Survey created successfully.";
            return RedirectToAction(nameof(Surveys));
        }
    }
}
