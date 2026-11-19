using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // The admin creates and suspends lab technician accounts (UC-L01 precondition and exception);
    // [ControllerName] keeps the /Admin/... URLs.
    [Authorize(Policy = Permissions.Staff.Manage)]
    [ControllerName("Admin")]
    public class AdminLabTechniciansController : HcmsControllerBase
    {
        private readonly ILabTechnicianService _technicianService;
        private readonly ILogger<AdminLabTechniciansController> _logger;

        public AdminLabTechniciansController(ILabTechnicianService technicianService, ILogger<AdminLabTechniciansController> logger)
        {
            _technicianService = technicianService;
            _logger = logger;
        }

        public async Task<IActionResult> LabTechnicians()
        {
            return View(await _technicianService.GetAllAsync());
        }

        [HttpGet]
        public IActionResult CreateLabTechnician()
        {
            return View(new CreateLabTechnicianViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLabTechnician(CreateLabTechnicianViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _technicianService.CreateAsync(model);
            if (!result.Success)
            {
                foreach (var error in result.Errors.Count > 0 ? result.Errors : new List<string> { result.Message })
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return View(model);
            }

            _logger.LogInformation("Lab technician {TechnicianId} created by {User}", result.Data.Id, User.Identity?.Name);
            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(LabTechnicians));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendLabTechnician(int id)
        {
            var result = await _technicianService.SetActiveAsync(id, isActive: false);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(LabTechnicians));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateLabTechnician(int id)
        {
            var result = await _technicianService.SetActiveAsync(id, isActive: true);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(LabTechnicians));
        }
    }
}
