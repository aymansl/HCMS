// ViewModels/DailyInvoiceItem.cs
using HCMS4.Models;

namespace HCMS4.ViewModels
{
    public class DailyInvoiceItem
    {
        public int InvoiceId { get; set; }

        public string PatientName { get; set; } = string.Empty;

        public string? PharmacistName { get; set; }

        public decimal TotalAmount { get; set; }

        public DateTime InvoiceTime { get; set; }

        public PaymentStatus PaymentStatus { get; set; }

        public string? PrescriptionId { get; set; }
    }
}