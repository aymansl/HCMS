using HCMS4.Modules.Portal.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Services;
using HCMS4.Shared.Reporting.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HCMS4.Shared.Identity.Authorization;

namespace HCMS4.Modules.Portal.Controllers
{
    // Part of the former AdminController; [ControllerName] keeps the /Admin/... URLs.
    [Authorize(Policy = Permissions.Portal.AdminDashboard)]
    [ControllerName("Admin")]
    public class AdminPortalController : HcmsControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly IDailyReportService _reportService;
        private readonly INotificationService _notificationService;
        private readonly IClinicClock _clock;

        public AdminPortalController(
            IDashboardService dashboardService,
            IDailyReportService reportService,
            INotificationService notificationService,
            IClinicClock clock)
        {
            _dashboardService = dashboardService;
            _reportService = reportService;
            _notificationService = notificationService;
            _clock = clock;
        }

        public async Task<IActionResult> Dashboard()
        {
            var counts = await _dashboardService.GetAdminCountsAsync();
            ViewBag.DoctorCount = counts.Doctors;
            ViewBag.PatientCount = counts.Patients;
            ViewBag.AppointmentCount = counts.Appointments;
            ViewBag.PendingComplaintsCount = counts.PendingComplaints;
            ViewBag.ActiveSurveysCount = counts.ActiveSurveys;
            ViewBag.TodayReport = await _reportService.GetDailyReportAsync(_clock.Today);

            // Admins receive leave requests and other alerts as notifications; show the latest ones.
            ViewBag.RecentNotifications = await _notificationService.GetRecentForUserAsync(CurrentUserId ?? string.Empty, 8);

            return View();
        }
    }
}
