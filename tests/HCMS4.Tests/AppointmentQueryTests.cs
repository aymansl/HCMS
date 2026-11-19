using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Feedback.Services;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Models;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace HCMS4.Tests
{
    // Queries added while moving data access out of the appointment controllers (cleanup phase 5b).
    public class AppointmentQueryTests : IDisposable
    {
        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();
        private readonly ClinicClock _clock = new(
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero)), ClinicTime.TimeZone);

        private AppointmentService Appointments => new(_db, NullLogger<AppointmentService>.Instance, _clock);
        private PrescriptionService Prescriptions => new(_db, NullLogger<PrescriptionService>.Instance);

        public void Dispose() => _db.Dispose();

        private Appointment AddAppointment(int doctorId, int patientId, DateTime at, AppointmentStatus status)
        {
            var appointment = new Appointment { DoctorId = doctorId, PatientId = patientId, AppointmentDateTime = at, Status = status };
            _db.Appointments.Add(appointment);
            return appointment;
        }

        [Fact]
        public async Task GetDoctorAppointmentsAsync_FiltersByDoctorDateRangeAndStatus()
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            AddAppointment(doctor.Id, patient.Id, new DateTime(2026, 11, 20, 15, 0, 0), AppointmentStatus.Scheduled);
            AddAppointment(doctor.Id, patient.Id, new DateTime(2026, 11, 20, 9, 0, 0), AppointmentStatus.Scheduled);
            AddAppointment(doctor.Id, patient.Id, new DateTime(2026, 11, 21, 9, 0, 0), AppointmentStatus.Canceled);
            AddAppointment(doctor.Id, patient.Id, new DateTime(2026, 11, 25, 9, 0, 0), AppointmentStatus.Scheduled);
            await _db.SaveChangesAsync();

            var scheduled = await Appointments.GetDoctorAppointmentsAsync(doctor.Id, new DateTime(2026, 11, 20), new DateTime(2026, 11, 21), true);
            var all = await Appointments.GetDoctorAppointmentsAsync(doctor.Id, new DateTime(2026, 11, 20), new DateTime(2026, 11, 21), false);
            var firstOnly = await Appointments.GetDoctorAppointmentsAsync(doctor.Id, new DateTime(2026, 11, 1), DateTime.MaxValue, true, take: 1);
            var otherDoctor = await Appointments.GetDoctorAppointmentsAsync(doctor.Id + 1000, new DateTime(2026, 11, 1), DateTime.MaxValue, false);

            Assert.Equal(new[] { 9, 15 }, scheduled.Select(a => a.AppointmentDateTime.Hour));
            Assert.Equal(3, all.Count);
            Assert.Equal(new DateTime(2026, 11, 20, 9, 0, 0), Assert.Single(firstOnly).AppointmentDateTime);
            Assert.Empty(otherDoctor);
        }

        [Fact]
        public async Task DailyVisitReport_MarksFollowUpsAndPrescriptions()
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (_, returning) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            var drug = await TestDbContextFactory.CreateTestDrugAsync(_db);
            var day = new DateTime(2026, 11, 17);
            AddAppointment(doctor.Id, returning.Id, new DateTime(2026, 10, 1, 10, 0, 0), AppointmentStatus.Completed);
            var visit = AddAppointment(doctor.Id, returning.Id, day.AddHours(10), AppointmentStatus.Completed);
            AddAppointment(doctor.Id, returning.Id, day.AddHours(12), AppointmentStatus.Canceled);
            await _db.SaveChangesAsync();
            _db.Prescriptions.Add(new Prescription
            {
                PatientId = returning.Id, DoctorId = doctor.Id, AppointmentId = visit.Id,
                PrescriptionItems = { new PrescriptionItem { DrugId = drug.Id, DrugName = "Amoxicillin with clavulanic acid", Quantity = 1 } }
            });
            await _db.SaveChangesAsync();

            var report = await new DailyVisitReportService(Appointments, Prescriptions).GetReportAsync(day);

            var item = Assert.Single(report.Patients);
            Assert.Equal(1, report.TotalPatients);
            Assert.Equal("Follow-up", item.VisitType);
            Assert.True(item.HasPrescription);
            Assert.Equal("Amoxicillin with cla...", item.Diagnosis);
            Assert.Null(report.ErrorMessage);
        }

        [Fact]
        public async Task DailyVisitReport_FirstVisit_IsConsultation_AndEmptyDayHasMessage()
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            AddAppointment(doctor.Id, patient.Id, new DateTime(2026, 11, 17, 10, 0, 0), AppointmentStatus.Completed);
            await _db.SaveChangesAsync();
            var service = new DailyVisitReportService(Appointments, Prescriptions);

            var item = Assert.Single((await service.GetReportAsync(new DateTime(2026, 11, 17))).Patients);
            Assert.Equal("Consultation", item.VisitType);
            Assert.False(item.HasPrescription);
            Assert.NotNull((await service.GetReportAsync(new DateTime(2026, 11, 16))).ErrorMessage);
        }

        [Fact]
        public async Task GetBookableDoctorsAsync_ExcludesUnavailableAndDoctorsOnApprovedLeave()
        {
            var (_, onDuty, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (_, onLeave, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (_, pendingLeave, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (_, unavailable, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            unavailable.IsAvailable = false;
            var day = new DateTime(2026, 11, 18);
            _db.DoctorLeaves.AddRange(
                new DoctorLeave { DoctorId = onLeave.Id, StartDate = day.AddDays(-1), EndDate = day.AddDays(1), Status = LeaveStatus.Approved },
                new DoctorLeave { DoctorId = pendingLeave.Id, StartDate = day, EndDate = day, Status = LeaveStatus.Pending });
            await _db.SaveChangesAsync();
            var staff = new StaffService(_db, null!, _clock, NullLogger<StaffService>.Instance);

            var ids = (await staff.GetBookableDoctorsAsync(day.AddHours(14))).Select(d => d.Id).ToList();

            Assert.Contains(onDuty.Id, ids);
            Assert.Contains(pendingLeave.Id, ids);
            Assert.DoesNotContain(onLeave.Id, ids);
            Assert.DoesNotContain(unavailable.Id, ids);
        }

        [Fact]
        public async Task VisitRatingsAndPatientList_AreReturnedForSelection()
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            var visit = AddAppointment(doctor.Id, patient.Id, new DateTime(2026, 11, 1, 10, 0, 0), AppointmentStatus.Completed);
            await _db.SaveChangesAsync();
            var rating = new VisitRating { PatientId = patient.Id, DoctorId = doctor.Id, AppointmentId = visit.Id, Rating = 5 };
            _db.VisitRatings.Add(rating);
            await _db.SaveChangesAsync();

            var ratings = await new VisitRatingService(_db, _clock).GetRatingIdsByAppointmentAsync(patient.Id);
            var patients = await new PatientService(_db, NullLogger<PatientService>.Instance).GetAllWithUserAsync();

            Assert.Equal(rating.Id, ratings[visit.Id]);
            Assert.Empty(await new VisitRatingService(_db, _clock).GetRatingIdsByAppointmentAsync(patient.Id + 1000));
            Assert.NotNull(Assert.Single(patients).User);
        }
    }
}
