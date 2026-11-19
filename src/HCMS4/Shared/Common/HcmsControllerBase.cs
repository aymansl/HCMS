using System.Security.Claims;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Identity.Services;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Shared.Common
{
    /// <summary>
    /// Base class for module controllers: "who is signed in" helpers without any database access.
    /// The lookups are implemented by the Records and Staff modules.
    /// </summary>
    public abstract class HcmsControllerBase : Controller
    {
        protected string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        protected async Task<int?> GetCurrentPatientIdAsync()
        {
            var userId = CurrentUserId;
            return string.IsNullOrEmpty(userId)
                ? null
                : await HttpContext.RequestServices.GetRequiredService<IPatientLookup>().GetPatientIdAsync(userId);
        }

        protected async Task<int?> GetCurrentDoctorIdAsync()
        {
            var userId = CurrentUserId;
            return string.IsNullOrEmpty(userId)
                ? null
                : await HttpContext.RequestServices.GetRequiredService<IDoctorLookup>().GetDoctorIdAsync(userId);
        }

        protected Task<ApplicationUser?> GetCurrentUserAsync() =>
            HttpContext.RequestServices.GetRequiredService<ICurrentUserService>().GetUserAsync();
    }
}
