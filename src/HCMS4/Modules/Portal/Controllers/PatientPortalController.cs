using HCMS4.Modules.Portal.Services;
using HCMS4.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HCMS4.Shared.Identity.Authorization;

namespace HCMS4.Modules.Portal.Controllers
{
    // Part of the former PatientController; [ControllerName] keeps the /Patient/... URLs.
    [Authorize(Policy = Permissions.Portal.PatientDashboard)]
    [ControllerName("Patient")]
    public class PatientPortalController : HcmsControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public PatientPortalController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            return View(await _dashboardService.GetPatientDashboardAsync(patientId.Value, CurrentUserId!));
        }
    }
}
