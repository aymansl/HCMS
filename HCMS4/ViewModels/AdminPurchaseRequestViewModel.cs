// ViewModels/AdminPurchaseRequestViewModel.cs
using HCMS4.Models;

namespace HCMS4.ViewModels
{
    public class AdminPurchaseRequestListViewModel
    {
        public List<PurchaseRequest> PurchaseRequests { get; set; } = new List<PurchaseRequest>();
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int OrderedCount { get; set; }
        public int ReceivedCount { get; set; }
        public string SelectedStatus { get; set; } = "all";
    }

    public class AdminPurchaseRequestDetailViewModel
    {
        public PurchaseRequest PurchaseRequest { get; set; } = null!;
        public List<PurchaseRequestItem> Items { get; set; } = new List<PurchaseRequestItem>();
    }
}