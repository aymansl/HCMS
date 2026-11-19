using HCMS4.Modules.Lab.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // The patient reads their own issued result in the portal (UC-L10 step 6);
    // [ControllerName] keeps the /Patient/... URLs.
    [Authorize(Policy = Permissions.Lab.ViewOwnResults)]
    [ControllerName("Patient")]
    public class PatientLabResultsController : HcmsControllerBase
    {
        private readonly ILabResultService _resultService;

        public PatientLabResultsController(ILabResultService resultService)
        {
            _resultService = resultService;
        }

        public async Task<IActionResult> LabResult(int id)
        {
            var request = await _resultService.GetIssuedForPatientAsync(CurrentUserId ?? string.Empty, id);
            return request == null ? NotFound() : View(request);
        }
    }
}
