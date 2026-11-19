using HCMS4.Modules.Lab.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // The doctor reads an issued result and acknowledges a critical one (UC-L10);
    // [ControllerName] keeps the /Doctor/... URLs.
    [Authorize(Policy = Permissions.Lab.CreateRequest)]
    [ControllerName("Doctor")]
    public class DoctorLabResultsController : HcmsControllerBase
    {
        private readonly ILabResultService _resultService;

        public DoctorLabResultsController(ILabResultService resultService)
        {
            _resultService = resultService;
        }

        public async Task<IActionResult> LabResult(int id)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
                return RedirectToAction("Login", "Account");

            var request = await _resultService.GetIssuedForDoctorAsync(doctorId.Value, id);
            return request == null ? NotFound() : View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcknowledgeCriticalResult(int id)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
                return RedirectToAction("Login", "Account");

            var result = await _resultService.AcknowledgeCriticalAsync(doctorId.Value, id);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(LabResult), new { id });
        }
    }
}
