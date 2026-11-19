using HCMS4.Modules.Records.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HCMS4.Shared.Identity.Authorization;

namespace HCMS4.Modules.Records.Controllers
{
    // Part of the former DoctorController; [ControllerName] keeps the /Doctor/... URLs.
    [Authorize(Policy = Permissions.Records.ViewMedicalRecords)]
    [ControllerName("Doctor")]
    public class DoctorRecordsController : HcmsControllerBase
    {
        private readonly IMedicalRecordService _medicalRecordService;
        private readonly ILogger<DoctorRecordsController> _logger;

        public DoctorRecordsController(IMedicalRecordService medicalRecordService, ILogger<DoctorRecordsController> logger)
        {
            _medicalRecordService = medicalRecordService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> PatientRecord(int patientId)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                if (!await _medicalRecordService.DoctorHasAccessAsync(doctorId.Value, patientId))
                {
                    TempData["ErrorMessage"] = "You do not have access to this patient's record.";
                    return RedirectToAction("ViewAppointments");
                }

                var viewModel = await _medicalRecordService.GetMedicalRecordAsync(patientId);
                if (viewModel == null)
                {
                    TempData["ErrorMessage"] = "Patient not found.";
                    return RedirectToAction("ViewAppointments");
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading patient record for patient ID: {PatientId}", patientId);
                TempData["ErrorMessage"] = "Unable to load patient record. Please try again.";
                return RedirectToAction("ViewAppointments");
            }
        }

        [HttpGet]
        public async Task<IActionResult> CreateClinicalNote(int patientId)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
                return RedirectToAction("Login", "Account");

            ViewBag.PatientId = patientId;
            return View(new CreateClinicalNoteViewModel { PatientId = patientId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClinicalNote(CreateClinicalNoteViewModel viewModel)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                if (ModelState.IsValid)
                {
                    var result = await _medicalRecordService.AddClinicalNoteAsync(doctorId.Value, viewModel);
                    if (!result.Success)
                    {
                        TempData["ErrorMessage"] = result.Message;
                        return RedirectToAction("ViewAppointments");
                    }

                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction("PatientRecord", new { patientId = viewModel.PatientId });
                }

                ViewBag.PatientId = viewModel.PatientId;
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating clinical note");
                TempData["ErrorMessage"] = "Error creating clinical note. Please try again.";
                return RedirectToAction("PatientRecord", new { patientId = viewModel.PatientId });
            }
        }
    }
}
