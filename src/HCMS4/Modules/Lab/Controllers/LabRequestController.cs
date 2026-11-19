using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // The technician opens a request, confirms receipt or returns it to the doctor (UC-L03);
    // [ControllerName] keeps the /Lab/... URLs.
    [Authorize(Policy = Permissions.Lab.ReceiveRequest)]
    [ControllerName("Lab")]
    public class LabRequestController : HcmsControllerBase
    {
        private readonly ILabRequestService _requestService;

        public LabRequestController(ILabRequestService requestService)
        {
            _requestService = requestService;
        }

        public async Task<IActionResult> Requests(LabRequestFilter filter)
        {
            return View(new LabIncomingRequestsViewModel
            {
                Filter = filter,
                Requests = await _requestService.GetIncomingAsync(filter)
            });
        }

        public async Task<IActionResult> RequestDetails(int id)
        {
            var request = await _requestService.GetDetailsAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            return View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiveRequest(int id)
        {
            var result = await _requestService.ReceiveAsync(id, CurrentUserId ?? string.Empty);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(RequestDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnRequest(int id, ReturnLabRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Write a note for the doctor.";
                return RedirectToAction(nameof(RequestDetails), new { id });
            }

            var result = await _requestService.ReturnToDoctorAsync(id, CurrentUserId ?? string.Empty, model.Note);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(RequestDetails), new { id });
        }
    }
}
