using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Notifications.Services;
using HCMS4.Shared.Reporting.Services;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Common;
using HCMS4.Data;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static HCMS4.ViewModels.GenerateInvoiceViewModel;

namespace HCMS4.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IDailyReportService _reportService;
        private readonly IPatientService _patientService;
        private readonly IAppointmentService _appointmentService;
        private readonly IPrescriptionService _prescriptionService;
        private readonly ApplicationDbContext _adminContext;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly ILeaveService _leaveService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AdminController> logger,
            IDailyReportService reportService,
            IPatientService patientService,
            IAppointmentService appointmentService,
            IPrescriptionService prescriptionService,
            INoShowRiskService noShowRiskService,
            ILeaveService leaveService,
            INotificationService notificationService,
            IActivityLogService activityLogService)
            : base(context, logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _reportService = reportService;
            _patientService = patientService;
            _appointmentService = appointmentService;
            _prescriptionService = prescriptionService;
            _adminContext = context;
            _noShowRiskService = noShowRiskService;
            _leaveService = leaveService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.DoctorCount = await _context.Doctors.CountAsync();
            ViewBag.PatientCount = await _context.Patients.CountAsync();
            ViewBag.AppointmentCount = await _context.Appointments.CountAsync();
            ViewBag.PendingComplaintsCount = await _context.Complaints.CountAsync(c => c.Status == ComplaintStatus.Submitted || c.Status == ComplaintStatus.UnderReview);
            ViewBag.ActiveSurveysCount = await _context.Surveys.CountAsync(s => s.Status == SurveyStatus.Active);
            var todayReport = await _reportService.GetDailyReportAsync(ClinicTime.Today);
            ViewBag.TodayReport = todayReport;

            // Admins receive leave requests and other alerts as notifications; show the latest ones.
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            ViewBag.RecentNotifications = await _notificationService.GetRecentForUserAsync(currentUserId, 8);

            return View();
        }



    }
}
