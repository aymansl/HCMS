using HCMS4.Modules.Portal.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HCMS4.Shared.Identity.Authorization;

namespace HCMS4.Modules.Portal.Controllers
{
    // Part of the former DoctorController; [ControllerName] keeps the /Doctor/... URLs.
    [Authorize(Policy = Permissions.Portal.DoctorDashboard)]
    [ControllerName("Doctor")]
    public class DoctorPortalController : HcmsControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<DoctorPortalController> _logger;

        public DoctorPortalController(IDashboardService dashboardService, ILogger<DoctorPortalController> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                return View(await _dashboardService.GetDoctorDashboardAsync(doctorId.Value, CurrentUserId!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading doctor dashboard");
                TempData["ErrorMessage"] = "Error loading dashboard. Please try again.";
                return View(new DoctorDashboardViewModel());
            }
        }
    }
}
