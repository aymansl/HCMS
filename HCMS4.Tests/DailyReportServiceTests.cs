using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HCMS4.Models;
using HCMS4.Services;
using HCMS4.Data;

namespace HCMS4.Tests
{
    public class DailyReportServiceTests
    {
        [Fact]
        public async Task GetDailyReportAsync_WithPrescriptionInvoices_ReturnsCorrectData()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (user, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var (docUser, doctor, spec) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (pharmUser, pharmacist) = await TestDbContextFactory.CreateTestPharmacistAsync(context);

            var prescription = new Prescription
            {
                Id = 1100,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Test",
                Status = PrescriptionStatus.Completed
            };
            context.Prescriptions.Add(prescription);

            var invoice1 = new Invoice
            {
                Id = 1100,
                PatientId = patient.Id,
                Patient = patient,
                PharmacistId = pharmacist.Id,
                Pharmacist = pharmacist,
                PrescriptionId = 1100,
                Prescription = prescription,
                InvoiceDate = DateTime.Today.AddHours(10),
                TotalAmount = 100,
                PaymentStatus = PaymentStatus.Paid
            };
            var invoice2 = new Invoice
            {
                Id = 1101,
                PatientId = patient.Id,
                Patient = patient,
                PharmacistId = pharmacist.Id,
                Pharmacist = pharmacist,
                PrescriptionId = 1100,
                Prescription = prescription,
                InvoiceDate = DateTime.Today.AddHours(14),
                TotalAmount = 200,
                PaymentStatus = PaymentStatus.Pending
            };
            context.Invoices.AddRange(invoice1, invoice2);
            await context.SaveChangesAsync();

            var service = new DailyReportService(context, NullLogger<DailyReportService>.Instance);

            // Act
            var result = await service.GetDailyReportAsync(DateTime.Today);

            // Assert
            Assert.True(result.HasInvoices);
            Assert.Equal(2, result.TotalInvoices);
            Assert.Equal(300, result.TotalAmount);
            Assert.Equal(2, result.Invoices.Count);
        }

        [Fact]
        public async Task GetDailyReportAsync_NoInvoices_ReturnsEmptyReport()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var service = new DailyReportService(context, NullLogger<DailyReportService>.Instance);

            // Act
            var result = await service.GetDailyReportAsync(DateTime.Today);

            // Assert
            Assert.False(result.HasInvoices);
            Assert.Empty(result.Invoices);
            Assert.Equal(0, result.TotalInvoices);
            Assert.Equal(0, result.TotalAmount);
        }

        [Fact]
        public async Task GetFilteredReportAsync_ByPaymentStatus_FiltersCorrectly()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (user, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var (docUser, doctor, spec) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (pharmUser, pharmacist) = await TestDbContextFactory.CreateTestPharmacistAsync(context);

            var prescription = new Prescription
            {
                Id = 2100,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Test",
                Status = PrescriptionStatus.Completed
            };
            context.Prescriptions.Add(prescription);

            var invoice1 = new Invoice
            {
                Id = 2100,
                PatientId = patient.Id,
                Patient = patient,
                PharmacistId = pharmacist.Id,
                Pharmacist = pharmacist,
                PrescriptionId = 2100,
                Prescription = prescription,
                InvoiceDate = DateTime.Now,
                TotalAmount = 100,
                PaymentStatus = PaymentStatus.Paid
            };
            var invoice2 = new Invoice
            {
                Id = 2101,
                PatientId = patient.Id,
                Patient = patient,
                PharmacistId = pharmacist.Id,
                Pharmacist = pharmacist,
                PrescriptionId = 2100,
                Prescription = prescription,
                InvoiceDate = DateTime.Now,
                TotalAmount = 200,
                PaymentStatus = PaymentStatus.Pending
            };
            context.Invoices.AddRange(invoice1, invoice2);
            await context.SaveChangesAsync();

            var service = new DailyReportService(context, NullLogger<DailyReportService>.Instance);

            // Act
            var result = await service.GetFilteredReportAsync(
                startDate: null,
                endDate: null,
                status: PaymentStatus.Paid,
                searchTerm: null);

            // Assert
            Assert.True(result.HasInvoices);
            Assert.Single(result.Invoices);
            Assert.Equal(PaymentStatus.Paid, result.Invoices[0].PaymentStatus);
        }

        [Fact]
        public async Task GenerateDailyReportAsync_NewReport_CreatesSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (user, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var (docUser, doctor, spec) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (pharmUser, pharmacist) = await TestDbContextFactory.CreateTestPharmacistAsync(context);

            var prescription = new Prescription
            {
                Id = 3100,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Test",
                Status = PrescriptionStatus.Completed
            };
            context.Prescriptions.Add(prescription);

            var invoice = new Invoice
            {
                Id = 3100,
                PatientId = patient.Id,
                Patient = patient,
                PharmacistId = pharmacist.Id,
                Pharmacist = pharmacist,
                PrescriptionId = 3100,
                Prescription = prescription,
                InvoiceDate = DateTime.Today.AddHours(10),
                TotalAmount = 150,
                PaymentStatus = PaymentStatus.Paid
            };
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            var service = new DailyReportService(context, NullLogger<DailyReportService>.Instance);

            // Act
            var result = await service.GenerateDailyReportAsync(DateTime.Today);

            // Assert
            Assert.True(result);
            var report = await context.DailyReports
                .FirstOrDefaultAsync(r => r.ReportDate.Date == DateTime.Today);
            Assert.NotNull(report);
            Assert.Equal(1, report.TotalInvoices);
            Assert.Equal(150, report.TotalAmount);
        }

        [Fact]
        public async Task GenerateDailyReportAsync_ExistingReport_ReturnsTrueWithoutDuplication()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            
            var existingReport = new DailyReport
            {
                Id = 1,
                ReportDate = DateTime.Today,
                TotalInvoices = 1,
                TotalAmount = 100,
                GeneratedAt = DateTime.Now
            };
            context.DailyReports.Add(existingReport);
            await context.SaveChangesAsync();

            var service = new DailyReportService(context, NullLogger<DailyReportService>.Instance);

            // Act
            var result = await service.GenerateDailyReportAsync(DateTime.Today);

            // Assert
            Assert.True(result);
            var reportCount = await context.DailyReports
                .Where(r => r.ReportDate.Date == DateTime.Today)
                .CountAsync();
            Assert.Equal(1, reportCount);
        }
    }
}
