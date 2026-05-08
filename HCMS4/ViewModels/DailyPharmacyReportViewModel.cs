// ViewModels/DailyPharmacyReportViewModel.cs
namespace HCMS4.ViewModels
{
    public class DailyPharmacyReportViewModel
    {
        public DateTime ReportDate { get; set; }

        public List<DailyInvoiceItem> Invoices { get; set; } = new List<DailyInvoiceItem>();

        public decimal TotalAmount { get; set; }

        public int TotalInvoices { get; set; }

        public bool HasInvoices { get; set; }

        public string? ErrorMessage { get; set; }
    }
}