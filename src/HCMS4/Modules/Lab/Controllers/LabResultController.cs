using HCMS4.Modules.Lab.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // The technician enters and issues results (UC-L10); [ControllerName] keeps the /Lab/... URLs.
    [Authorize(Policy = Permissions.Lab.EnterResults)]
    [ControllerName("Lab")]
    public class LabResultController : HcmsControllerBase
    {
        private readonly ILabResultService _resultService;
        private readonly ILogger<LabResultController> _logger;

        public LabResultController(ILabResultService resultService, ILogger<LabResultController> logger)
        {
            _resultService = resultService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> EnterResults(int id)
        {
            var model = await _resultService.GetEntryFormAsync(id);
            return model == null ? NotFound() : View(model);
        }

        /// <param name="values">One value per request item, posted as values[itemId].</param>
        /// <param name="issue">"true" for "Issue Result"; otherwise the values are only saved.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnterResults(int id, Dictionary<int, string?> values, bool issue)
        {
            var result = await _resultService.SaveAsync(id, CurrentUserId ?? string.Empty, values, issue);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            if (result.Success && issue)
            {
                _logger.LogInformation("Lab result {RequestId} issued by {User}", id, User.Identity?.Name);
                return RedirectToAction("RequestDetails", new { id });
            }

            return RedirectToAction(nameof(EnterResults), new { id });
        }
    }
}
