using HCMS4.Modules.Billing.Services;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCMS4.Tests
{
    // Regression tests for issue #30: payments were checked against the total, not the remaining balance.
    public class InvoicePaymentServiceTests
    {
        private static async Task<(InvoicePaymentService Service, Invoice Invoice, int PatientId)> SetupAsync(
            ApplicationDbContext context, decimal total = 100m, decimal? paid = null)
        {
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var invoice = new Invoice
            {
                PatientId = patient.Id,
                TotalAmount = total,
                ConsultationFee = total,
                AmountPaid = paid,
                PaymentStatus = paid >= total ? PaymentStatus.Paid : PaymentStatus.Pending
            };
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();
            return (new InvoicePaymentService(context, NullLogger<InvoicePaymentService>.Instance), invoice, patient.Id);
        }

        [Fact]
        public async Task PayAsync_FullAmount_MarksPaid()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (service, invoice, patientId) = await SetupAsync(context);

            var result = await service.PayAsync(invoice.Id, patientId, 100m, PaymentMethod.Cash, null);

            Assert.True(result.Success);
            Assert.Equal(PaymentStatus.Paid, invoice.PaymentStatus);
            Assert.Equal(100m, invoice.AmountPaid);
        }

        [Fact]
        public async Task PayAsync_SecondPartialPayment_CompletesInvoice()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (service, invoice, patientId) = await SetupAsync(context);

            await service.PayAsync(invoice.Id, patientId, 40m, PaymentMethod.Cash, null);
            Assert.Equal(PaymentStatus.Pending, invoice.PaymentStatus);
            var second = await service.PayAsync(invoice.Id, patientId, 60m, PaymentMethod.Cash, null);

            Assert.True(second.Success);
            Assert.Equal(100m, invoice.AmountPaid);
            Assert.Equal(PaymentStatus.Paid, invoice.PaymentStatus);
        }

        [Fact]
        public async Task PayAsync_MoreThanRemainingBalance_IsRejected()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (service, invoice, patientId) = await SetupAsync(context, paid: 70m);

            var result = await service.PayAsync(invoice.Id, patientId, 50m, PaymentMethod.Cash, null);

            Assert.False(result.Success);
            Assert.Equal(70m, invoice.AmountPaid);
        }

        [Fact]
        public async Task PayAsync_AlreadyPaidInvoice_IsRejected()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (service, invoice, patientId) = await SetupAsync(context, paid: 100m);

            var result = await service.PayAsync(invoice.Id, patientId, 100m, PaymentMethod.Cash, null);

            Assert.False(result.Success);
            Assert.Equal(100m, invoice.AmountPaid);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task PayAsync_NonPositiveAmount_IsRejected(decimal amount)
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (service, invoice, patientId) = await SetupAsync(context);

            Assert.False((await service.PayAsync(invoice.Id, patientId, amount, PaymentMethod.Cash, null)).Success);
        }

        [Fact]
        public async Task PayAsync_OtherPatientsInvoice_IsRejected()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (service, invoice, _) = await SetupAsync(context);
            var (_, other) = await TestDbContextFactory.CreateTestPatientAsync(context);

            Assert.False((await service.PayAsync(invoice.Id, other.Id, 10m, PaymentMethod.Cash, null)).Success);
        }

        // Regression test for issue #31: the payment method was only kept inside the free-text notes.
        [Fact]
        public async Task PayAsync_StoresPaymentMethod_EvenWithoutNotes()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (service, invoice, patientId) = await SetupAsync(context);

            await service.PayAsync(invoice.Id, patientId, 30m, PaymentMethod.Cash, notes: null);

            Assert.Equal(PaymentMethod.Cash, invoice.PaymentMethod);
        }

        // Business rule: cash only.
        [Theory]
        [InlineData(PaymentMethod.CreditCard)]
        [InlineData(PaymentMethod.DebitCard)]
        [InlineData(PaymentMethod.BankTransfer)]
        [InlineData(PaymentMethod.Other)]
        public async Task PayAsync_NonCashMethod_IsRejected(PaymentMethod method)
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (service, invoice, patientId) = await SetupAsync(context);

            var result = await service.PayAsync(invoice.Id, patientId, 30m, method, notes: null);

            Assert.False(result.Success);
            Assert.Null(invoice.AmountPaid);
        }
    }
}