using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // A doctor sends lab requests from the patient file and resends returned ones (UC-L03);
    // [ControllerName] keeps the /Doctor/... URLs.
    [Authorize(Policy = Permissions.Lab.CreateRequest)]
    [ControllerName("Doctor")]
    public class DoctorLabRequestsController : HcmsControllerBase
    {
        private readonly ILabRequestService _requestService;
        private readonly ILogger<DoctorLabRequestsController> _logger;

        public DoctorLabRequestsController(ILabRequestService requestService, ILogger<DoctorLabRequestsController> logger)
        {
            _requestService = requestService;
            _logger = logger;
        }

        public async Task<IActionResult> LabRequests(int? patientId)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
                return RedirectToAction("Login", "Account");

            var requests = await _requestService.GetForDoctorAsync(doctorId.Value, patientId);
            string? patientName = null;
            if (patientId.HasValue)
            {
                patientName = (await _requestService.NewFormAsync(patientId.Value))?.PatientName;
            }

            return View(new DoctorLabRequestsViewModel { PatientId = patientId, PatientName = patientName, Requests = requests });
        }

        [HttpGet]
        public async Task<IActionResult> CreateLabRequest(int patientId)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
                return RedirectToAction("Login", "Account");

            var model = await _requestService.NewFormAsync(patientId);
            if (model == null)
            {
                TempData["ErrorMessage"] = LabRequestService.PatientNotFoundError;
                return RedirectToAction("ViewAppointments");
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLabRequest(LabRequestFormViewModel model)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
                return RedirectToAction("Login", "Account");

            model.Id = 0;
            if (!ModelState.IsValid)
            {
                await _requestService.FillFormAsync(model);
                return View(model);
            }

            var result = await _requestService.CreateAsync(doctorId.Value, model);
            if (!result.Success)
            {
                if (result.Message == LabRequestService.PatientNotFoundError)
                {
                    TempData["ErrorMessage"] = result.Message;
                    return RedirectToAction("ViewAppointments");
                }

                ModelState.AddModelError(string.Empty, result.Message);
                await _requestService.FillFormAsync(model);
                return View(model);
            }

            _logger.LogInformation("Lab request {RequestId} created by doctor {DoctorId}", result.Data.Id, doctorId);
            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(LabRequests), new { patientId = model.PatientId });
        }

        [HttpGet]
        public async Task<IActionResult> CancelLabRequest(int id)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
                return RedirectToAction("Login", "Account");

            var request = await _requestService.GetForDoctorByIdAsync(doctorId.Value, id);
            if (request == null)
                return NotFound();

            return View(new CancelLabRequestViewModel { Request = request });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelLabRequest(int id, CancelLabRequestViewModel model)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
                return RedirectToAction("Login", "Account");

            var request = await _requestService.GetForDoctorByIdAsync(doctorId.Value, id);
            if (request == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                model.Request = request;
                return View(model);
            }

            var result = await _requestService.CancelAsync(doctorId.Value, id, model.Reason);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                model.Request = request;
                return View(model);
            }

            _logger.LogInformation("Lab request {RequestId} cancelled by doctor {DoctorId}", id, doctorId);
            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(LabRequests), new { patientId = request.PatientId });
        }

        [HttpGet]
        public async Task<IActionResult> EditLabRequest(int id)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
                return RedirectToAction("Login", "Account");

            var model = await _requestService.EditFormAsync(doctorId.Value, id);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Only a request returned by the lab can be edited and resent.";
                return RedirectToAction(nameof(LabRequests));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLabRequest(int id, LabRequestFormViewModel model)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
                return RedirectToAction("Login", "Account");

            model.Id = id;
            if (!ModelState.IsValid)
            {
                await _requestService.FillFormAsync(model);
                return View(model);
            }

            var result = await _requestService.ResubmitAsync(doctorId.Value, model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                await _requestService.FillFormAsync(model);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(LabRequests), new { patientId = result.Data.PatientId });
        }
    }
}
