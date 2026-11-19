using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // The lab test catalogue (UC-L06); [ControllerName] keeps the /Lab/... URLs.
    [Authorize(Policy = Permissions.Lab.ManageTests)]
    [ControllerName("Lab")]
    public class LabTestController : HcmsControllerBase
    {
        private readonly ILabTestService _testService;
        private readonly ILogger<LabTestController> _logger;

        public LabTestController(ILabTestService testService, ILogger<LabTestController> logger)
        {
            _testService = testService;
            _logger = logger;
        }

        public async Task<IActionResult> Tests()
        {
            return View(await _testService.GetAllAsync());
        }

        [HttpGet]
        public IActionResult CreateTest()
        {
            return View(new LabTestFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTest(LabTestFormViewModel model)
        {
            model.Id = 0;
            return await SaveAsync(model, nameof(CreateTest));
        }

        [HttpGet]
        public async Task<IActionResult> EditTest(int id)
        {
            var test = await _testService.GetByIdAsync(id);
            if (test == null)
            {
                return NotFound();
            }

            return View(new LabTestFormViewModel
            {
                Id = test.Id,
                Name = test.Name,
                Unit = test.Unit,
                ReferenceMin = test.ReferenceMin,
                ReferenceMax = test.ReferenceMax,
                Price = test.Price,
                IsActive = test.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTest(int id, LabTestFormViewModel model)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            model.Id = id;
            return await SaveAsync(model, nameof(EditTest));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTest(int id)
        {
            var result = await _testService.DeleteAsync(id);
            if (result.Success)
            {
                _logger.LogInformation("Lab test {TestId} {Outcome} by {User}", id, result.Data, User.Identity?.Name);
            }

            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Tests));
        }

        private async Task<IActionResult> SaveAsync(LabTestFormViewModel model, string viewName)
        {
            if (!ModelState.IsValid)
            {
                return View(viewName, model);
            }

            var result = await _testService.SaveAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(viewName, model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Tests));
        }
    }
}
