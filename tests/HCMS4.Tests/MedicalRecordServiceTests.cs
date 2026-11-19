using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Records.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace HCMS4.Tests
{
    public class MedicalRecordServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();

        // 2026-11-18 09:00 UTC = 12:00 Damascus.
        private readonly ClinicClock _clock = new(
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero)), ClinicTime.TimeZone);

        private MedicalRecordService Service => new(_db, _clock);

        public void Dispose() => _db.Dispose();

        private async Task<(int doctorId, int patientId)> SeedDoctorWithPatientAsync()
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            _db.Appointments.Add(new Appointment
            {
                DoctorId = doctor.Id, PatientId = patient.Id,
                AppointmentDateTime = new DateTime(2026, 11, 10, 10, 0, 0), Status = AppointmentStatus.Completed
            });
            await _db.SaveChangesAsync();
            return (doctor.Id, patient.Id);
        }

        [Fact]
        public async Task DoctorHasAccessAsync_OnlyForPatientsWithAnAppointment()
        {
            var (doctorId, patientId) = await SeedDoctorWithPatientAsync();

            Assert.True(await Service.DoctorHasAccessAsync(doctorId, patientId));
            Assert.False(await Service.DoctorHasAccessAsync(doctorId, patientId + 1000));
            Assert.False(await Service.DoctorHasAccessAsync(doctorId + 1000, patientId));
        }

        [Fact]
        public async Task GetMedicalRecordAsync_UnknownPatient_ReturnsNull()
        {
            Assert.Null(await Service.GetMedicalRecordAsync(999999));
        }

        [Fact]
        public async Task GetMedicalRecordAsync_MapsAppointmentsNotesAndInvoices()
        {
            var (doctorId, patientId) = await SeedDoctorWithPatientAsync();
            _db.ClinicalNotes.Add(new ClinicalNote
            {
                PatientId = patientId, DoctorId = doctorId, NoteType = "Progress", Content = "Stable", Date = new DateTime(2026, 11, 10)
            });
            _db.Invoices.Add(new Invoice
            {
                PatientId = patientId, InvoiceDate = new DateTime(2026, 11, 10), TotalAmount = 50, PaymentStatus = PaymentStatus.Pending
            });
            await _db.SaveChangesAsync();

            var record = await Service.GetMedicalRecordAsync(patientId);

            Assert.NotNull(record);
            Assert.Equal(patientId, record!.PatientId);
            Assert.Equal("Completed", Assert.Single(record.AppointmentHistory).Status);
            Assert.Equal("Stable", Assert.Single(record.ClinicalNotes).Content);
            Assert.Equal(50, Assert.Single(record.Invoices).TotalAmount);
            Assert.Empty(record.Prescriptions);
        }

        [Fact]
        public async Task AddClinicalNoteAsync_StampsDamascusTime()
        {
            var (doctorId, patientId) = await SeedDoctorWithPatientAsync();

            var result = await Service.AddClinicalNoteAsync(doctorId, new CreateClinicalNoteViewModel
            {
                PatientId = patientId, NoteType = "Diagnosis", Content = "Flu", Diagnosis = "J11"
            });

            Assert.True(result.Success);
            var note = await _db.ClinicalNotes.SingleAsync();
            Assert.Equal(doctorId, note.DoctorId);
            Assert.Equal(new DateTime(2026, 11, 18, 12, 0, 0), note.Date);
        }

        [Fact]
        public async Task AddClinicalNoteAsync_DoctorWithoutAppointment_IsRejected()
        {
            var (doctorId, patientId) = await SeedDoctorWithPatientAsync();

            var result = await Service.AddClinicalNoteAsync(doctorId + 1000, new CreateClinicalNoteViewModel
            {
                PatientId = patientId, NoteType = "Diagnosis", Content = "Flu"
            });

            Assert.False(result.Success);
            Assert.False(await _db.ClinicalNotes.AnyAsync());
        }

        [Fact]
        public async Task AppointmentService_CountByStatusAsync_CountsOnlyThatStatus()
        {
            var (doctorId, patientId) = await SeedDoctorWithPatientAsync();
            _db.Appointments.Add(new Appointment
            {
                DoctorId = doctorId, PatientId = patientId,
                AppointmentDateTime = new DateTime(2026, 12, 1, 10, 0, 0), Status = AppointmentStatus.Scheduled
            });
            await _db.SaveChangesAsync();
            var appointments = new AppointmentService(_db, NullLogger<AppointmentService>.Instance, _clock);

            Assert.Equal(1, await appointments.CountByStatusAsync(AppointmentStatus.Scheduled));
            Assert.Equal(1, await appointments.CountByStatusAsync(AppointmentStatus.Completed));
            Assert.Equal(0, await appointments.CountByStatusAsync(AppointmentStatus.Canceled));
        }
    }
}
