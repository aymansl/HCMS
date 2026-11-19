using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HCMS4.Shared.Identity.Authorization;

namespace HCMS4.Modules.Pharmacy.Controllers
{
    // Part of the former PatientController; [ControllerName] keeps the /Patient/... URLs.
    [Authorize(Policy = Permissions.Pharmacy.ViewOwnPrescriptions)]
    [ControllerName("Patient")]
    public class PatientPrescriptionsController : HcmsControllerBase
    {
        private readonly IPrescriptionRequestService _requestService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;
        private readonly ILogger<PatientPrescriptionsController> _logger;

        public PatientPrescriptionsController(
            IPrescriptionRequestService requestService,
            INotificationService notificationService,
            IActivityLogService activityLogService,
            ILogger<PatientPrescriptionsController> logger)
        {
            _requestService = requestService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
            _logger = logger;
        }

        public async Task<IActionResult> Prescriptions()
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                return View(new PatientPrescriptionsListViewModel
                {
                    Prescriptions = await _requestService.GetPatientPrescriptionsAsync(patientId.Value)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prescriptions for patient");
                TempData["ErrorMessage"] = "Error loading prescriptions.";
                return RedirectToAction("Dashboard", "Patient");
            }
        }

        public async Task<IActionResult> PrescriptionDetails(int id)
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var viewModel = await _requestService.GetPatientPrescriptionDetailAsync(patientId.Value, id);
                if (viewModel == null)
                {
                    TempData["ErrorMessage"] = "No prescription found.";
                    return RedirectToAction(nameof(Prescriptions));
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prescription {PrescriptionId}", id);
                TempData["ErrorMessage"] = "Error loading prescription.";
                return RedirectToAction(nameof(Prescriptions));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPrescriptionReissue(int prescriptionId)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var result = await _requestService.RequestReissueAsync(patientId.Value, prescriptionId);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
            }

            if (result.Data.NotifyUserId != null)
            {
                await _notificationService.CreateForUserAsync(
                    result.Data.NotifyUserId,
                    "Prescription re-issuance request",
                    $"A patient requested re-issuance for prescription #{prescriptionId}.",
                    NotificationType.PrescriptionReissue,
                    "/Doctor/ReissueRequests",
                    "PrescriptionReissueRequest",
                    result.Data.Id);
            }

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "PrescriptionReissueRequested",
                "PrescriptionReissueRequest",
                $"Patient requested re-issuance for prescription #{prescriptionId}.",
                result.Data.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
        }
    }
}
