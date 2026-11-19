using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Notifications.Services;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Common;
using HCMS4.Data;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Controllers
{
    [Authorize(Roles = "Doctor")]
    public class DoctorController : BaseController
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IPrescriptionService _prescriptionService;
        private readonly ILeaveService _leaveService;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public DoctorController(
            ApplicationDbContext context,
            ILogger<DoctorController> logger,
            IAppointmentService appointmentService,
            IPrescriptionService prescriptionService,
            ILeaveService leaveService,
            INoShowRiskService noShowRiskService,
            INotificationService notificationService,
            IActivityLogService activityLogService)
            : base(context, logger)
        {
            _appointmentService = appointmentService;
            _prescriptionService = prescriptionService;
            _leaveService = leaveService;
            _noShowRiskService = noShowRiskService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
        }

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                var today = ClinicTime.Today;

                var todayAppointments = await _context.Appointments
                    .Include(a => a.Patient)
                        .ThenInclude(p => p.User)
                    .Include(a => a.Doctor)
                    .Where(a => a.DoctorId == doctorId.Value &&
                           a.AppointmentDateTime.Date == today &&
                           a.Status == AppointmentStatus.Scheduled)
                    .OrderBy(a => a.AppointmentDateTime)
                    .ToListAsync();

                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId.Value);
                var pendingReissueCount = await _context.PrescriptionReissueRequests
                    .CountAsync(r => r.DoctorId == doctorId.Value && r.Status == ReissueRequestStatus.Pending);
                var pendingReviewCount = await _context.DoctorReviewRequests
                    .CountAsync(r => r.DoctorId == doctorId.Value && r.Status == ReviewRequestStatus.Pending);
                var recentRatings = await _context.VisitRatings
                    .Include(r => r.Patient)
                        .ThenInclude(p => p.User)
                    .Where(r => r.DoctorId == doctorId.Value)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(5)
                    .ToListAsync();
                var notificationUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                List<UserNotification> notifications;
                try
                {
                    notifications = await _context.UserNotifications
                        .Where(n => n.UserId == notificationUserId)
                        .OrderByDescending(n => n.CreatedAt)
                        .Take(5)
                        .ToListAsync();
                }
                catch
                {
                    notifications = new List<UserNotification>();
                }

                var viewModel = new DoctorDashboardViewModel
                {
                    TodayAppointmentsCount = todayAppointments.Count,
                    PendingReissueRequestsCount = pendingReissueCount,
                    PendingReviewRequestsCount = pendingReviewCount,
                    PublishedArticlesCount = await _context.MedicalArticles.CountAsync(a => a.DoctorId == doctorId.Value && a.Status == ArticleStatus.Published),
                    DraftArticlesCount = await _context.MedicalArticles.CountAsync(a => a.DoctorId == doctorId.Value && a.Status == ArticleStatus.Draft),
                    AverageRating = doctor?.AverageRating ?? 0,
                    RatingCount = doctor?.RatingCount ?? 0,
                    RecentRatings = recentRatings,
                    RecentNotifications = notifications
                };

                return View(viewModel);
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
