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

        public LabPortalController(ILabTechnicianService technicianService)
        {
            _technicianService = technicianService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var technician = await _technicianService.GetByUserIdAsync(CurrentUserId ?? string.Empty);
            var user = technician == null ? await GetCurrentUserAsync() : null;

            return View(new LabDashboardViewModel
            {
                TechnicianName = technician?.FullName ?? user?.FullName ?? User.Identity?.Name ?? string.Empty
            });
        }
    }
}
