using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Services;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Portal.Services
{
    public record AdminDashboardCounts(int Doctors, int Patients, int Appointments, int PendingComplaints, int ActiveSurveys);

    /// <summary>
    /// Read-only figures for the four role dashboards. The portal is the one place that summarises
    /// every module, so this service reads several modules' tables without changing them.
    /// </summary>
    public interface IDashboardService
    {
        Task<AdminDashboardCounts> GetAdminCountsAsync();
        Task<DoctorDashboardViewModel> GetDoctorDashboardAsync(int doctorId, string userId);
        Task<PatientDashboardViewModel> GetPatientDashboardAsync(int patientId, string userId);
        Task<PharmacistDashboardViewModel> GetPharmacistDashboardAsync();
    }

    public class DashboardService : IDashboardService
    {
        private const int RecentItems = 5;
        private const int PublishedArticlesShown = 6;

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IDrugService _drugService;
        private readonly IClinicClock _clock;

        public DashboardService(ApplicationDbContext context, INotificationService notificationService,
            IDrugService drugService, IClinicClock clock)
        {
            _context = context;
            _notificationService = notificationService;
            _drugService = drugService;
            _clock = clock;
        }

        public async Task<AdminDashboardCounts> GetAdminCountsAsync() => new(
            await _context.Doctors.CountAsync(),
            await _context.Patients.CountAsync(),
            await _context.Appointments.CountAsync(),
            await _context.Complaints.CountAsync(c => c.Status == ComplaintStatus.Submitted || c.Status == ComplaintStatus.UnderReview),
            await _context.Surveys.CountAsync(s => s.Status == SurveyStatus.Active));

        public async Task<DoctorDashboardViewModel> GetDoctorDashboardAsync(int doctorId, string userId)
        {
            var today = _clock.Today;
            var doctor = await _context.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doctorId);

            return new DoctorDashboardViewModel
            {
                TodayAppointmentsCount = await _context.Appointments.CountAsync(a =>
                    a.DoctorId == doctorId && a.AppointmentDateTime.Date == today && a.Status == AppointmentStatus.Scheduled),
                PendingReissueRequestsCount = await _context.PrescriptionReissueRequests
                    .CountAsync(r => r.DoctorId == doctorId && r.Status == ReissueRequestStatus.Pending),
                PendingReviewRequestsCount = await _context.DoctorReviewRequests
                    .CountAsync(r => r.DoctorId == doctorId && r.Status == ReviewRequestStatus.Pending),
                PublishedArticlesCount = await _context.MedicalArticles
                    .CountAsync(a => a.DoctorId == doctorId && a.Status == ArticleStatus.Published),
                DraftArticlesCount = await _context.MedicalArticles
                    .CountAsync(a => a.DoctorId == doctorId && a.Status == ArticleStatus.Draft),
                AverageRating = doctor?.AverageRating ?? 0,
                RatingCount = doctor?.RatingCount ?? 0,
                RecentRatings = await _context.VisitRatings.AsNoTracking()
                    .Include(r => r.Patient)
                        .ThenInclude(p => p.User)
                    .Where(r => r.DoctorId == doctorId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(RecentItems)
                    .ToListAsync(),
                RecentNotifications = await _notificationService.GetRecentForUserAsync(userId, RecentItems)
            };
        }

        public async Task<PatientDashboardViewModel> GetPatientDashboardAsync(int patientId, string userId)
        {
            var now = _clock.Now;
            var today = _clock.Today;

            var upcomingAppointments = await _context.Appointments.AsNoTracking()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Specialization)
                .Where(a => a.PatientId == patientId &&
                            a.Status == AppointmentStatus.Scheduled &&
                            a.AppointmentDateTime >= now)
                .OrderBy(a => a.AppointmentDateTime)
                .Take(RecentItems)
                .ToListAsync();

            return new PatientDashboardViewModel
            {
                UpcomingAppointmentsCount = upcomingAppointments.Count,
                CompletedVisitsCount = await _context.Appointments
                    .CountAsync(a => a.PatientId == patientId && a.Status == AppointmentStatus.Completed),
                ActiveSurveyCount = await _context.SurveyAssignments
                    .CountAsync(sa => sa.PatientId == patientId &&
                                      sa.Status == SurveyAssignmentStatus.Pending &&
                                      sa.Survey.Status == SurveyStatus.Active &&
                                      sa.Survey.StartDate <= today &&
                                      (!sa.Survey.EndDate.HasValue || sa.Survey.EndDate.Value >= today)),
                OpenComplaintCount = await _context.Complaints
                    .CountAsync(c => c.PatientId == patientId &&
                                     c.Status != ComplaintStatus.Resolved &&
                                     c.Status != ComplaintStatus.Closed),
                RecentNotifications = await _notificationService.GetRecentForUserAsync(userId, RecentItems),
                PublishedArticles = await _context.MedicalArticles.AsNoTracking()
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.User)
                    .Where(a => a.Status == ArticleStatus.Published)
                    .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
                    .Take(PublishedArticlesShown)
                    .ToListAsync(),
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
        }

        public async Task<PharmacistDashboardViewModel> GetPharmacistDashboardAsync()
        {
            var now = _clock.Now;
            var drugs = await _context.Drugs.AsNoTracking().ToListAsync();

            var viewModel = new PharmacistDashboardViewModel
            {
                TotalDrugs = drugs.Count,
                ExpiringSoonCount = drugs.Count(d => d.ExpiryDate <= now.AddDays(BusinessRules.DrugExpiryWarningDays) && d.ExpiryDate > now),
                ExpiredCount = drugs.Count(d => d.ExpiryDate <= now),
                LowStockCount = drugs.Count(d => d.Quantity < BusinessRules.LowStockThreshold),
                PendingPrescriptions = await _context.Prescriptions.CountAsync(p => p.Status == PrescriptionStatus.Pending),
                CompletedPrescriptions = await _context.Prescriptions.CountAsync(p => p.Status == PrescriptionStatus.Completed),
                ExpiringDrugs = await _drugService.GetExpiringDrugsAsync(),
                LowStockDrugs = await _drugService.GetLowStockDrugsAsync(),
                RecentPrescriptions = await _context.Prescriptions.AsNoTracking()
                    .Include(p => p.Patient)
                        .ThenInclude(pat => pat.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(doc => doc.User)
                    .OrderByDescending(p => p.PrescriptionDate)
                    .Take(RecentItems)
                    .ToListAsync(),
                Alerts = new List<string>()
            };

            if (viewModel.ExpiredCount > 0)
                viewModel.Alerts.Add($"⚠️ {viewModel.ExpiredCount} drug(s) have expired and need to be removed.");

            if (viewModel.ExpiringSoonCount > 0)
                viewModel.Alerts.Add($"⚠️ {viewModel.ExpiringSoonCount} drug(s) will expire within {BusinessRules.DrugExpiryWarningDays} days.");

            if (viewModel.LowStockCount > 0)
                viewModel.Alerts.Add($"⚠️ {viewModel.LowStockCount} drug(s) are running low on stock.");

            return viewModel;
        }
    }
}
