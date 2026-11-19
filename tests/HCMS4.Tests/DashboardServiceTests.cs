using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Modules.Portal.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace HCMS4.Tests
{
    public class DashboardServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();

        // 2026-11-18 09:00 UTC = 12:00 Damascus.
        private readonly ClinicClock _clock = new(
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero)), ClinicTime.TimeZone);

        private DashboardService Service => new(_db, new NotificationService(_db),
            new DrugService(_db, NullLogger<DrugService>.Instance), _clock);

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task AdminCounts_CountOpenComplaintsAndActiveSurveysOnly()
        {
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            await TestDbContextFactory.CreateTestDoctorAsync(_db);
            _db.Complaints.AddRange(
                new Complaint { PatientId = patient.Id, Title = "a", Description = "a", TrackingNumber = "C1", Status = ComplaintStatus.Submitted },
                new Complaint { PatientId = patient.Id, Title = "b", Description = "b", TrackingNumber = "C2", Status = ComplaintStatus.Resolved });
            _db.Surveys.AddRange(
                new Survey { Title = "s1", Status = SurveyStatus.Active, CreatedByUserId = "x" },
                new Survey { Title = "s2", Status = SurveyStatus.Draft, CreatedByUserId = "x" });
            await _db.SaveChangesAsync();

            var counts = await Service.GetAdminCountsAsync();

            Assert.Equal(1, counts.Doctors);
            Assert.Equal(1, counts.Patients);
            Assert.Equal(1, counts.PendingComplaints);
            Assert.Equal(1, counts.ActiveSurveys);
        }

        [Fact]
        public async Task DoctorDashboard_CountsTodayAndPendingWork()
        {
            var (doctorUser, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            _db.Appointments.AddRange(
                new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2026, 11, 18, 15, 0, 0), Status = AppointmentStatus.Scheduled },
                new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2026, 11, 19, 15, 0, 0), Status = AppointmentStatus.Scheduled });
            _db.MedicalArticles.Add(new MedicalArticle { DoctorId = doctor.Id, Title = "t", Content = "c", Status = ArticleStatus.Draft });
            _db.UserNotifications.Add(new UserNotification { UserId = doctorUser.Id, Title = "n", Message = "m" });
            await _db.SaveChangesAsync();

            var dashboard = await Service.GetDoctorDashboardAsync(doctor.Id, doctorUser.Id);

            Assert.Equal(1, dashboard.TodayAppointmentsCount);
            Assert.Equal(1, dashboard.DraftArticlesCount);
            Assert.Equal(0, dashboard.PublishedArticlesCount);
            Assert.Single(dashboard.RecentNotifications);
        }

        [Fact]
        public async Task PatientDashboard_ShowsUpcomingVisitsAndPublishedArticles()
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (patientUser, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            _db.Appointments.AddRange(
                new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2026, 11, 20, 10, 0, 0), Status = AppointmentStatus.Scheduled },
                new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2026, 11, 1, 10, 0, 0), Status = AppointmentStatus.Completed });
            _db.MedicalArticles.AddRange(
                new MedicalArticle { DoctorId = doctor.Id, Title = "pub", Content = "c", Status = ArticleStatus.Published },
                new MedicalArticle { DoctorId = doctor.Id, Title = "draft", Content = "c", Status = ArticleStatus.Draft });
            await _db.SaveChangesAsync();

            var dashboard = await Service.GetPatientDashboardAsync(patient.Id, patientUser.Id);

            Assert.Equal(1, dashboard.UpcomingAppointmentsCount);
            Assert.Equal(1, dashboard.CompletedVisitsCount);
            Assert.Equal("pub", Assert.Single(dashboard.PublishedArticles).Title);
        }

        [Fact]
        public async Task PharmacistDashboard_RaisesStockAndExpiryAlerts()
        {
            _db.Drugs.AddRange(
                new Drug { Name = "Expired", Price = 1, Quantity = 50, ExpiryDate = new DateTime(2026, 11, 1) },
                new Drug { Name = "Low", Price = 1, Quantity = 1, ExpiryDate = new DateTime(2028, 1, 1) });
            await _db.SaveChangesAsync();

            var dashboard = await Service.GetPharmacistDashboardAsync();

            Assert.Equal(2, dashboard.TotalDrugs);
            Assert.Equal(1, dashboard.ExpiredCount);
            Assert.Equal(1, dashboard.LowStockCount);
            Assert.Equal(2, dashboard.Alerts.Count);
        }
    }
}
