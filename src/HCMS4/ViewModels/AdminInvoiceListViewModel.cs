using HCMS4.Modules.Billing.Models;
using HCMS4.Shared.Common;

namespace HCMS4.ViewModels
{
    /// <summary>Filters of the admin invoice list. Status is "all", "Pending" or "Paid"; dates are inclusive.</summary>
    public class InvoiceListFilter
    {
        public string Status { get; set; } = "all";
        public string? Search { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class AdminInvoiceListViewModel
    {
        public InvoiceListFilter Filter { get; set; } = new();
        public PaginatedResult<Invoice> Invoices { get; set; } = new() { PageNumber = 1, PageSize = 20 };

        // Totals of all invoices that match the filter (not only the current page).
        public int PendingCount { get; set; }
        public int PaidCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount => TotalAmount - PaidAmount;
    }
}
