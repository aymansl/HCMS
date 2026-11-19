using HCMS4.Modules.Portal.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HCMS4.Shared.Identity.Authorization;

namespace HCMS4.Modules.Portal.Controllers
{
    // Part of the former PharmacistController; [ControllerName] keeps the /Pharmacist/... URLs.
    [Authorize(Policy = Permissions.Portal.PharmacistDashboard)]
    [ControllerName("Pharmacist")]
    public class PharmacistPortalController : HcmsControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<PharmacistPortalController> _logger;

        public PharmacistPortalController(IDashboardService dashboardService, ILogger<PharmacistPortalController> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                return View(await _dashboardService.GetPharmacistDashboardAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pharmacist dashboard");
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return View(new PharmacistDashboardViewModel());
            }
        }
    }
}
