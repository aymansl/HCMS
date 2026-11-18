using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Shared.Common;
using HCMS4.Data;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Services
{
    public interface IInvoicePaymentService
    {
        /// <summary>Records a (partial) payment by the invoice's own patient; never more than the remaining balance.</summary>
        Task<ServiceResult> PayAsync(int invoiceId, int patientId, decimal amount, PaymentMethod paymentMethod, string? notes);
    }

    public class InvoicePaymentService : IInvoicePaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InvoicePaymentService> _logger;

        public InvoicePaymentService(ApplicationDbContext context, ILogger<InvoicePaymentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult> PayAsync(int invoiceId, int patientId, decimal amount, PaymentMethod paymentMethod, string? notes)
        {
            // Business rule: the center accepts cash payments only.
            if (paymentMethod != PaymentMethod.Cash)
            {
                return ServiceResult.Fail("Only cash payments are accepted.");
            }

            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.PatientId == patientId);

            if (invoice == null)
            {
                return ServiceResult.Fail("Invoice not found.");
            }

            var paid = invoice.AmountPaid ?? 0;
            var remaining = invoice.TotalAmount - paid;

            if (invoice.PaymentStatus == PaymentStatus.Paid || remaining <= 0)
            {
                return ServiceResult.Fail("This invoice is already fully paid.");
            }

            // Compare with the remaining balance, not the total (issue #30).
            if (amount <= 0 || amount > remaining)
            {
                return ServiceResult.Fail($"Invalid payment amount. The remaining balance is ${remaining:F2}.");
            }

            var now = ClinicTime.Now;
            invoice.AmountPaid = paid + amount;
            invoice.PaymentDate = now;
            invoice.PaymentMethod = paymentMethod; // persisted in its own column (issue #31)
            invoice.UpdatedAt = now;
            invoice.PaymentStatus = invoice.AmountPaid >= invoice.TotalAmount ? PaymentStatus.Paid : PaymentStatus.Pending;

            if (!string.IsNullOrEmpty(notes))
            {
                var paymentNote = $"Payment of ${amount:F2} via {paymentMethod} on {now:yyyy-MM-dd}. {notes}";
                invoice.Notes = string.IsNullOrEmpty(invoice.Notes)
                    ? paymentNote
                    : $"{invoice.Notes}\n{paymentNote}";
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Payment of {Amount} recorded for invoice {InvoiceId}", amount, invoiceId);

            return ServiceResult.Ok($"Payment of ${amount:F2} processed successfully for Invoice #{invoice.Id}.");
        }
    }
}
