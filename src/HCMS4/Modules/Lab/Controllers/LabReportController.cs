using HCMS4.Modules.Lab.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // The daily lab activity report and its printable page (UC-L11); [ControllerName] keeps the /Lab/... URLs.
    [Authorize(Policy = Permissions.Lab.EnterResults)]
    [ControllerName("Lab")]
    public class LabReportController : HcmsControllerBase
    {
        private readonly ILabReportService _reportService;

        public LabReportController(ILabReportService reportService)
        {
            _reportService = reportService;
        }

        public async Task<IActionResult> ActivityReport(DateOnly? from, DateOnly? to)
        {
            return View(await _reportService.BuildPageAsync(from, to));
        }

        /// <summary>The same summary on a print-friendly page; the browser's "Save as PDF" makes the PDF.</summary>
        public async Task<IActionResult> ActivityReportPrint(DateOnly? from, DateOnly? to)
        {
            return View(await _reportService.BuildPageAsync(from, to));
        }
    }
}
