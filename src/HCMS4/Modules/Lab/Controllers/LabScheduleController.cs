using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // The lab's daily sample-collection schedule (UC-L05 step 6); [ControllerName] keeps the /Lab/... URLs.
    [Authorize(Policy = Permissions.Lab.ReceiveRequest)]
    [ControllerName("Lab")]
    public class LabScheduleController : HcmsControllerBase
    {
        private readonly ILabAppointmentService _appointmentService;
        private readonly IClinicClock _clock;

        public LabScheduleController(ILabAppointmentService appointmentService, IClinicClock clock)
        {
            _appointmentService = appointmentService;
            _clock = clock;
        }

        public async Task<IActionResult> Schedule(DateOnly? date)
        {
            var day = date ?? DateOnly.FromDateTime(_clock.Today);
            return View(new LabScheduleViewModel { Date = day, Appointments = await _appointmentService.GetScheduleAsync(day) });
        }
    }
}
