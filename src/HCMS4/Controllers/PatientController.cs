using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Billing.Services;
using HCMS4.Modules.Feedback.Services;
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
using System.Security.Claims;

namespace HCMS4.Controllers
{
    [Authorize(Roles = "Patient")]
    public class PatientController : BaseController
    {
        private readonly IAppointmentService _appointmentService;
        private readonly ILeaveService _leaveService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;
        private readonly IComplaintAttachmentService _attachmentService;
        private readonly IInvoicePaymentService _paymentService;

        public PatientController(
            ApplicationDbContext context,
            ILogger<PatientController> logger,
            IAppointmentService appointmentService,
            ILeaveService leaveService,
            INotificationService notificationService,
            IActivityLogService activityLogService,
            IComplaintAttachmentService attachmentService,
            IInvoicePaymentService paymentService)
            : base(context, logger)
        {
            _paymentService = paymentService;
            _appointmentService = appointmentService;
            _leaveService = leaveService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
            _attachmentService = attachmentService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var upcomingAppointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Specialization)
                .Where(a => a.PatientId == patientId.Value &&
                            a.Status == AppointmentStatus.Scheduled &&
                            a.AppointmentDateTime >= ClinicTime.Now)
                .OrderBy(a => a.AppointmentDateTime)
                .Take(5)
                .ToListAsync();

            var activeSurveyCount = await _context.SurveyAssignments
                .Include(sa => sa.Survey)
                .CountAsync(sa => sa.PatientId == patientId.Value &&
                                  sa.Status == SurveyAssignmentStatus.Pending &&
                                  sa.Survey.Status == SurveyStatus.Active &&
                                  sa.Survey.StartDate <= ClinicTime.Now.Date &&
                                  (!sa.Survey.EndDate.HasValue || sa.Survey.EndDate.Value >= ClinicTime.Now.Date));

            var openComplaintCount = await _context.Complaints
                .CountAsync(c => c.PatientId == patientId.Value &&
                                 c.Status != ComplaintStatus.Resolved &&
                                 c.Status != ComplaintStatus.Closed);

            var completedVisitsCount = await _context.Appointments
                .CountAsync(a => a.PatientId == patientId.Value && a.Status == AppointmentStatus.Completed);

            List<UserNotification> notifications;
            try
            {
                notifications = await _context.UserNotifications
                    .Where(n => n.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier))
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .ToListAsync();
            }
            catch
            {
                notifications = new List<UserNotification>();
            }

            var articles = await _context.MedicalArticles
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Where(a => a.Status == ArticleStatus.Published)
                .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
                .Take(6)
                .ToListAsync();

            var viewModel = new PatientDashboardViewModel
            {
                UpcomingAppointmentsCount = upcomingAppointments.Count,
                CompletedVisitsCount = completedVisitsCount,
                ActiveSurveyCount = activeSurveyCount,
                OpenComplaintCount = openComplaintCount,
                RecentNotifications = notifications,
                PublishedArticles = articles,
                UpcomingAppointments = upcomingAppointments.Select(a => new AppointmentHistoryViewModel
                {
                    Id = a.Id,
                    AppointmentDateTime = a.AppointmentDateTime,
                    DoctorName = a.Doctor?.User?.FullName ?? "Unknown Doctor",
                    Specialization = a.Doctor?.Specialization?.Name ?? "General",
                    Status = a.Status.ToString(),
                    ConsultationFee = a.ConsultationFee,
                    CreatedAt = a.CreatedAt
                }).ToList()
            };

            return View(viewModel);
        }
    }
}
