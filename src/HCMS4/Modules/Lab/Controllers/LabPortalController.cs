using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // The lab dashboard a technician lands on after signing in (UC-L01 step 5).
    [Authorize(Policy = Permissions.Portal.LabDashboard)]
    [ControllerName("Lab")]
    public class LabPortalController : HcmsControllerBase
    {
        private readonly ILabTechnicianService _technicianService;
        private readonly ILabTestService _testService;
        private readonly ILabRequestService _requestService;

        public LabPortalController(ILabTechnicianService technicianService, ILabTestService testService,
            ILabRequestService requestService)
        {
            _technicianService = technicianService;
            _testService = testService;
            _requestService = requestService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var technician = await _technicianService.GetByUserIdAsync(CurrentUserId ?? string.Empty);
            var user = technician == null ? await GetCurrentUserAsync() : null;

            return View(new LabDashboardViewModel
            {
                TechnicianName = technician?.FullName ?? user?.FullName ?? User.Identity?.Name ?? string.Empty,
                ActiveTestCount = await _testService.CountActiveAsync(),
                NewRequestCount = await _requestService.CountNewAsync(),
                NewRequests = await _requestService.GetNewForLabAsync()
            });
        }
    }
}
