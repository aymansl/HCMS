using System.Security.Claims;
using HCMS4.Modules.Lab.Services;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Components
{
    /// <summary>
    /// The doctor's unacknowledged critical lab results (UC-L10). The doctor dashboard renders it with
    /// <c>Component.InvokeAsync("CriticalLabResults")</c>, so the Portal module does not read lab data itself.
    /// </summary>
    public class CriticalLabResultsViewComponent : ViewComponent
    {
        private readonly ILabResultService _resultService;

        public CriticalLabResultsViewComponent(ILabResultService resultService)
        {
            _resultService = resultService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var critical = await _resultService.GetUnacknowledgedCriticalAsync(userId);
            return View("~/Modules/Lab/Views/Shared/Components/CriticalLabResults/Default.cshtml", critical);
        }
    }
}
