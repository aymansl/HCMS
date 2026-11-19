using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Billing.Services;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace HCMS4.Tests
{
    public class InvoiceServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();

        // 2026-11-18 09:00 UTC = 12:00 Damascus.
        private readonly ClinicClock _clock = new(
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero)), ClinicTime.TimeZone);

        private InvoiceService Service => new(_db, _clock);

        public void Dispose() => _db.Dispose();

        private async Task<(int patientId, int doctorId, Appointment recent, Prescription prescription)> SeedAsync()
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            var recent = new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2026, 11, 10, 10, 0, 0), Status = AppointmentStatus.Completed, ConsultationFee = 40 };
            _db.Appointments.AddRange(
                recent,
                new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2026, 8, 1, 10, 0, 0), Status = AppointmentStatus.Completed, ConsultationFee = 99 },
                new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2026, 11, 12, 10, 0, 0), Status = AppointmentStatus.Canceled, ConsultationFee = 77 });
            var prescription = new Prescription { DoctorId = doctor.Id, PatientId = patient.Id, PrescriptionDate = new DateTime(2026, 11, 11), Status = PrescriptionStatus.Completed, MedicationTotal = 15 };
            _db.Prescriptions.Add(prescription);
            await _db.SaveChangesAsync();
            return (patient.Id, doctor.Id, recent, prescription);
        }

        [Fact]
        public async Task GetGenerateForm_SumsLast30DaysOfCompletedWork()
        {
            var seed = await SeedAsync();

            var form = await Service.GetGenerateFormAsync(seed.patientId);

            Assert.NotNull(form);
            Assert.Equal(40, form!.ConsultationFee);
            Assert.Equal(15, form.MedicationTotal);
            Assert.Equal(55, form.TotalAmount);
            Assert.Single(form.RecentAppointments);
            Assert.Null(await Service.GetGenerateFormAsync(999999));
        }

        [Fact]
        public async Task FillOptions_ListsAllCompletedVisits()
        {
            var seed = await SeedAsync();
            var model = new GenerateInvoiceViewModel { PatientId = seed.patientId };

            await Service.FillGenerateFormOptionsAsync(model);

            Assert.Equal(2, model.RecentAppointments.Count);
            Assert.Single(model.RecentPrescriptions);
            Assert.False(string.IsNullOrEmpty(model.PatientName));
        }

        [Fact]
        public async Task Create_ValidatesTotalPatientAndOwnership_ThenCreatesPendingInvoice()
        {
            var seed = await SeedAsync();
            var (_, other) = await TestDbContextFactory.CreateTestPatientAsync(_db);

            Assert.False((await Service.CreateAsync(new GenerateInvoiceViewModel { PatientId = seed.patientId, ConsultationFee = 10, MedicationTotal = 5, TotalAmount = 20 })).Success);
            Assert.Equal(InvoiceService.PatientNotFoundMessage,
                (await Service.CreateAsync(new GenerateInvoiceViewModel { PatientId = 999999, ConsultationFee = 1, TotalAmount = 1 })).Message);
            Assert.False((await Service.CreateAsync(new GenerateInvoiceViewModel { PatientId = other.Id, AppointmentId = seed.recent.Id, ConsultationFee = 1, TotalAmount = 1 })).Success);

            var created = await Service.CreateAsync(new GenerateInvoiceViewModel
            {
                PatientId = seed.patientId, AppointmentId = seed.recent.Id, PrescriptionId = seed.prescription.Id,
                ConsultationFee = 40, MedicationTotal = 15, TotalAmount = 55
            });

            Assert.True(created.Success);
            var invoice = await _db.Invoices.AsNoTracking().SingleAsync();
            Assert.Equal(PaymentStatus.Pending, invoice.PaymentStatus);
            Assert.Equal(new DateTime(2026, 11, 18, 12, 0, 0), invoice.InvoiceDate);
        }

        [Fact]
        public async Task UpdatePaymentStatus_CoversInvalidPaidAndLockedCases()
        {
            var seed = await SeedAsync();
            var invoice = new Invoice { PatientId = seed.patientId, TotalAmount = 55, InvoiceDate = new DateTime(2026, 11, 18) };
            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync();

            Assert.Equal(PaymentUpdateOutcome.NotFound, (await Service.UpdatePaymentStatusAsync(999999, new("Paid", "55", null, null))).Outcome);
            Assert.Equal(PaymentUpdateOutcome.Invalid, (await Service.UpdatePaymentStatusAsync(invoice.Id, new("", null, null, null))).Outcome);
            Assert.Equal(PaymentUpdateOutcome.Invalid, (await Service.UpdatePaymentStatusAsync(invoice.Id, new("Bogus", null, null, null))).Outcome);
            Assert.Equal(PaymentUpdateOutcome.Invalid, (await Service.UpdatePaymentStatusAsync(invoice.Id, new("Paid", "", null, null))).Outcome);
            Assert.Equal(PaymentUpdateOutcome.Invalid, (await Service.UpdatePaymentStatusAsync(invoice.Id, new("Paid", "50", null, null))).Outcome);

            var paid = await Service.UpdatePaymentStatusAsync(invoice.Id, new("Paid", "55", null, "cash at desk"));

            Assert.Equal(PaymentUpdateOutcome.Updated, paid.Outcome);
            var stored = await _db.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id);
            Assert.Equal(PaymentStatus.Paid, stored.PaymentStatus);
            Assert.Equal(new DateTime(2026, 11, 18, 12, 0, 0), stored.PaymentDate);
            Assert.Contains("cash at desk", stored.Notes);
            Assert.Equal(PaymentUpdateOutcome.AlreadyPaid, (await Service.UpdatePaymentStatusAsync(invoice.Id, new("Pending", null, null, null))).Outcome);
        }

        [Fact]
        public async Task PatientQueries_OnlyReturnTheirOwnInvoices_WithDetails()
        {
            var seed = await SeedAsync();
            var (_, other) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            var mine = new Invoice { PatientId = seed.patientId, AppointmentId = seed.recent.Id, TotalAmount = 40, InvoiceDate = new DateTime(2026, 11, 10) };
            var theirs = new Invoice { PatientId = other.Id, TotalAmount = 10, InvoiceDate = new DateTime(2026, 11, 10) };
            _db.Invoices.AddRange(mine, theirs);
            await _db.SaveChangesAsync();

            Assert.Equal(mine.Id, Assert.Single(await Service.GetForPatientAsync(seed.patientId)).Id);
            Assert.NotNull((await Service.GetForPatientAsync(seed.patientId, mine.Id))!.Appointment!.Doctor);
            Assert.Null(await Service.GetForPatientAsync(seed.patientId, theirs.Id));
            Assert.NotNull(await Service.GetAsync(theirs.Id));
        }
    }
}
