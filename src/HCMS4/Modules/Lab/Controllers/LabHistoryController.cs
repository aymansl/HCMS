using HCMS4.Modules.Lab.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // A patient's test history and the trend of one test (UC-L09); [ControllerName] keeps the /Lab/... URLs.
    [Authorize(Policy = Permissions.Lab.EnterResults)]
    [ControllerName("Lab")]
    public class LabHistoryController : HcmsControllerBase
    {
        private readonly ILabHistoryService _historyService;

        public LabHistoryController(ILabHistoryService historyService)
        {
            _historyService = historyService;
        }

        public async Task<IActionResult> PatientHistory(string? term, int? patientId, int? testId)
        {
            return View(await _historyService.BuildPageAsync(term, patientId, testId));
        }
    }
}
