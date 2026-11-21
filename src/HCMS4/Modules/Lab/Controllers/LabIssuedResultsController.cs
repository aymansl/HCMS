using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // Search issued results and print a result report (UC-L08); [ControllerName] keeps the /Lab/... URLs.
    [Authorize(Policy = Permissions.Lab.EnterResults)]
    [ControllerName("Lab")]
    public class LabIssuedResultsController : HcmsControllerBase
    {
        private readonly ILabResultSearchService _searchService;
        private readonly ILabTestService _testService;

        public LabIssuedResultsController(ILabResultSearchService searchService, ILabTestService testService)
        {
            _searchService = searchService;
            _testService = testService;
        }

        public async Task<IActionResult> IssuedResults(IssuedResultFilter filter)
        {
            return View(new IssuedResultsViewModel
            {
                Filter = filter,
                Results = await _searchService.SearchIssuedAsync(filter),
                Tests = await _testService.GetAllAsync()
            });
        }

        /// <summary>A print-friendly report; the browser's "Save as PDF" makes the PDF.</summary>
        public async Task<IActionResult> ResultReport(int id)
        {
            var model = await _searchService.GetReportAsync(id);
            return model == null ? NotFound() : View(model);
        }
    }
}
