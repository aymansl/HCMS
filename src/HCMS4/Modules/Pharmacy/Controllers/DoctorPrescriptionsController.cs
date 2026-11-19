using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Modules.Records.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Pharmacy.Controllers
{
    // Part of the former DoctorController; [ControllerName] keeps the /Doctor/... URLs.
    [Authorize(Roles = "Doctor")]
    [ControllerName("Doctor")]
    public class DoctorPrescriptionsController : HcmsControllerBase
    {
        private readonly IPrescriptionService _prescriptionService;
        private readonly IPrescriptionRequestService _requestService;
        private readonly IDrugService _drugService;
        private readonly IPatientService _patientService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;
        private readonly IClinicClock _clock;
        private readonly ILogger<DoctorPrescriptionsController> _logger;

        public DoctorPrescriptionsController(
            IPrescriptionService prescriptionService,
            IPrescriptionRequestService requestService,
            IDrugService drugService,
            IPatientService patientService,
            INotificationService notificationService,
            IActivityLogService activityLogService,
            IClinicClock clock,
            ILogger<DoctorPrescriptionsController> logger)
        {
            _prescriptionService = prescriptionService;
            _requestService = requestService;
            _drugService = drugService;
            _patientService = patientService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
            _clock = clock;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ReissueRequests()
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            return View(await _requestService.GetReissueRequestsForDoctorAsync(doctorId.Value));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewReissueRequest(int id, bool approve, string? doctorResponse)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var result = await _requestService.ReviewReissueRequestAsync(doctorId.Value, id, approve, doctorResponse);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(ReissueRequests));
            }

            await _notificationService.CreateForUserAsync(
                result.Data.NotifyUserId,
                "Prescription re-issuance updated",
                $"Your re-issuance request for prescription #{result.Data.PrescriptionId} was {(approve ? "approved" : "rejected")}.",
                NotificationType.PrescriptionReissue,
                "/Patient/Prescriptions",
                "PrescriptionReissueRequest",
                id);

            await LogActivityAsync(
                approve ? "PrescriptionReissueApproved" : "PrescriptionReissueRejected",
                "PrescriptionReissueRequest",
                $"Doctor {(approve ? "approved" : "rejected")} re-issuance request #{id}.",
                id);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(ReissueRequests));
        }

        [HttpGet]
        public async Task<IActionResult> DoctorReviewRequests()
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            return View(await _requestService.GetReviewRequestsForDoctorAsync(doctorId.Value));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondToReviewRequest(int id, string doctorResponse)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var result = await _requestService.RespondToReviewRequestAsync(doctorId.Value, id, doctorResponse);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(DoctorReviewRequests));
            }

            await _notificationService.CreateForUserAsync(
                result.Data.NotifyUserId,
                "Doctor review response",
                $"Doctor responded to review request for prescription #{result.Data.PrescriptionId}.",
                NotificationType.DoctorReview,
                "/Pharmacist/Prescriptions",
                "DoctorReviewRequest",
                id);

            await LogActivityAsync("DoctorReviewResponded", "DoctorReviewRequest",
                $"Doctor responded to doctor review request #{id}.", id);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(DoctorReviewRequests));
        }

        [HttpGet]
        public async Task<IActionResult> CreatePrescription(int patientId, int? appointmentId = null)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                var patient = await _patientService.GetByIdAsync(patientId);
                if (patient == null)
                {
                    TempData["ErrorMessage"] = "Patient not found.";
                    return RedirectToAction("ViewAppointments");
                }

                var viewModel = new CreatePrescriptionViewModel
                {
                    PatientId = patientId,
                    AppointmentId = appointmentId,
                    AvailableDrugs = await _drugService.GetPrescribableDrugsAsync()
                };

                ViewBag.PatientName = patient.User != null ? patient.User.FullName : "Unknown Patient";
                ViewBag.PatientInfo = $"ID: {patient.Id} | DOB: {patient.DateOfBirth?.ToString("yyyy-MM-dd") ?? "N/A"}";

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create prescription form");
                TempData["ErrorMessage"] = "Error loading form. Please try again.";
                return RedirectToAction("PatientRecord", new { patientId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePrescription(CreatePrescriptionViewModel viewModel)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                if (ModelState.IsValid && string.IsNullOrWhiteSpace(viewModel.DoctorSignature))
                {
                    ModelState.AddModelError("DoctorSignature", "Electronic signature is required.");
                }

                if (ModelState.IsValid)
                {
                    var prescription = new Prescription
                    {
                        PatientId = viewModel.PatientId,
                        DoctorId = doctorId.Value,
                        AppointmentId = viewModel.AppointmentId,
                        PrescriptionDate = _clock.Now,
                        Notes = viewModel.Notes,
                        Status = PrescriptionStatus.Pending
                    };

                    // The service checks stock and expiry and takes the drug names from the catalog.
                    var items = viewModel.Items.Select(item => new PrescriptionItem
                    {
                        DrugId = item.SelectedDrugId,
                        DrugName = item.DrugName,
                        Dosage = item.Dosage,
                        Duration = item.Duration,
                        Frequency = item.Frequency,
                        Quantity = item.Quantity,
                        Instructions = item.Instructions ?? string.Empty
                    }).ToList();

                    var result = await _prescriptionService.CreateAsync(prescription, items);
                    if (result.Success)
                    {
                        TempData["SuccessMessage"] = result.Message;
                        return RedirectToAction("PatientRecord", new { patientId = viewModel.PatientId });
                    }

                    if (result.Errors?.Any() == true)
                    {
                        foreach (var error in result.Errors)
                            ModelState.AddModelError(string.Empty, error);
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, result.Message);
                    }
                }

                viewModel.AvailableDrugs = await _drugService.GetPrescribableDrugsAsync();
                var patient = await _patientService.GetByIdAsync(viewModel.PatientId);
                ViewBag.PatientName = patient?.User != null ? patient.User.FullName : "Unknown Patient";
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating prescription");
                TempData["ErrorMessage"] = "Error creating prescription. Please try again.";
            }

            return RedirectToAction("PatientRecord", new { patientId = viewModel.PatientId });
        }

        private async Task LogActivityAsync(string action, string entityName, string description, int entityId)
        {
            var user = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(action, entityName, description, entityId.ToString(), user?.Id, user?.UserName);
        }
    }
}
