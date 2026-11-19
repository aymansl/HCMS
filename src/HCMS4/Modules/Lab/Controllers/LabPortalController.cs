using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using HCMS4.Shared.Notifications.Services;
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
        private readonly INotificationService _notificationService;
        private readonly ILabAppointmentService _appointmentService;
        private readonly IClinicClock _clock;

        public const int RecentNotificationCount = 8;

        public LabPortalController(ILabTechnicianService technicianService, ILabTestService testService,
            ILabRequestService requestService, INotificationService notificationService,
            ILabAppointmentService appointmentService, IClinicClock clock)
        {
            _appointmentService = appointmentService;
            _clock = clock;
            _technicianService = technicianService;
            _testService = testService;
            _requestService = requestService;
            _notificationService = notificationService;
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
                NewRequests = await _requestService.GetNewForLabAsync(),
                TodayAppointmentCount = (await _appointmentService.GetScheduleAsync(DateOnly.FromDateTime(_clock.Today))).Count,
                RecentNotifications = await _notificationService.GetRecentForUserAsync(CurrentUserId ?? string.Empty, RecentNotificationCount)
            });
        }
    }
}
