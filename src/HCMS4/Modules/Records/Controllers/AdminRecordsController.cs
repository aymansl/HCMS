using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Records.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HCMS4.Shared.Identity.Authorization;

namespace HCMS4.Modules.Records.Controllers
{
    // Part of the former AdminController; [ControllerName] keeps the /Admin/... URLs.
    [Authorize(Policy = Permissions.Records.ManagePatients)]
    [ControllerName("Admin")]
    public class AdminRecordsController : HcmsControllerBase
    {
        private readonly IPatientService _patientService;
        private readonly IAppointmentService _appointmentService;
        private readonly ILogger<AdminRecordsController> _logger;

        public AdminRecordsController(
            IPatientService patientService,
            IAppointmentService appointmentService,
            ILogger<AdminRecordsController> logger)
        {
            _patientService = patientService;
            _appointmentService = appointmentService;
            _logger = logger;
        }

        public async Task<IActionResult> Patients(int page = 1, int pageSize = 20)
        {
            try
            {
                var pagination = new PaginationParams
                {
                    PageNumber = page,
                    PageSize = pageSize
                };

                var result = await _patientService.GetAllAsync(pagination);

                var totalScheduledAppointments = await _appointmentService.CountByStatusAsync(AppointmentStatus.Scheduled);

                var totalPrescriptions = result.Items.Sum(p => p.Prescriptions?.Count ?? 0);
                var pendingInvoicesCount = result.Items
                    .SelectMany(p => p.Invoices ?? new List<Invoice>())
                    .Count(i => i.PaymentStatus == PaymentStatus.Pending);

                var viewModel = new PatientStatsViewModel
                {
                    Patients = result.Items,
                    TotalScheduledAppointments = totalScheduledAppointments,
                    TotalPrescriptions = totalPrescriptions,
                    PendingInvoicesCount = pendingInvoicesCount
                };

                ViewBag.TotalCount = result.TotalCount;
                ViewBag.TotalPages = result.TotalPages;
                ViewBag.CurrentPage = page;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading patients");
                return View(new PatientStatsViewModel());
            }
        }

        public async Task<IActionResult> EditPatient(int? id)
        {
            if (id == null)
                return NotFound();

            var patient = await _patientService.GetByIdAsync(id.Value);
            if (patient == null)
                return NotFound();

            return View(patient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPatient(int id, Patient patient)
        {
            if (id != patient.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                var result = await _patientService.UpdateAsync(id, patient);
                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Patients));
                }

                TempData["ErrorMessage"] = result.Message;
            }

            return View(patient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePatient(int id)
        {
            var result = await _patientService.DeleteAsync(id);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Patients));
        }

        public async Task<IActionResult> PatientDetails(int? id)
        {
            if (id == null)
                return NotFound();

            var patient = await _patientService.GetByIdWithDetailsAsync(id.Value);
            if (patient == null)
                return NotFound();

            return View(patient);
        }

        [HttpGet]
        public async Task<IActionResult> DisablePatient(int id)
        {
            var patient = await _patientService.GetByIdAsync(id);

            if (patient == null)
            {
                TempData["ErrorMessage"] = "Patient not found.";
                return RedirectToAction("Patients");
            }

            var viewModel = new DisablePatientViewModel
            {
                PatientId = patient.Id,
                PatientName = patient.User?.FullName ?? "Unknown"
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisablePatient(DisablePatientViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.DisableReason))
            {
                ModelState.AddModelError("DisableReason", "Please provide a reason for disabling the account.");
                return View(model);
            }

            var result = await _patientService.DisablePatientAsync(model.PatientId, model.DisableReason);

            if (result.Success)
            {
                TempData["SuccessMessage"] = "Patient account disabled successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Patients");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnablePatient(int id)
        {
            var result = await _patientService.EnablePatientAsync(id);

            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction("Patients");
        }
    }
}
