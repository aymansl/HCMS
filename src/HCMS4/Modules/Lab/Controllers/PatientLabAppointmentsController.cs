using HCMS4.Modules.Lab.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Lab.Controllers
{
    // A patient sees their lab requests and books or changes the sample-collection time (UC-L05);
    // [ControllerName] keeps the /Patient/... URLs.
    [Authorize(Policy = Permissions.Lab.BookAppointment)]
    [ControllerName("Patient")]
    public class PatientLabAppointmentsController : HcmsControllerBase
    {
        private readonly ILabAppointmentService _appointmentService;
        private readonly ILogger<PatientLabAppointmentsController> _logger;

        public PatientLabAppointmentsController(ILabAppointmentService appointmentService, ILogger<PatientLabAppointmentsController> logger)
        {
            _appointmentService = appointmentService;
            _logger = logger;
        }

        public async Task<IActionResult> LabRequests()
        {
            return View(await _appointmentService.GetPatientRequestsAsync(CurrentUserId ?? string.Empty));
        }

        [HttpGet]
        public async Task<IActionResult> BookLabAppointment(int requestId, DateOnly? date)
        {
            var model = await _appointmentService.GetBookingFormAsync(CurrentUserId ?? string.Empty, requestId, date);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookLabAppointment(int requestId, DateTime slot)
        {
            var result = await _appointmentService.BookAsync(CurrentUserId ?? string.Empty, requestId, slot);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(BookLabAppointment),
                    new { requestId, date = DateOnly.FromDateTime(slot).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) });
            }

            _logger.LogInformation("Lab appointment {AppointmentId} booked for request {RequestId}", result.Data.Id, requestId);
            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(LabRequests));
        }
    }
}
