using HCMS4.Modules.Pharmacy.Models;
// ViewModels/PurchaseRequestListViewModel.cs
namespace HCMS4.ViewModels
{
    public class PurchaseRequestListViewModel
    {
        public List<PurchaseRequest> PurchaseRequests { get; set; } = new List<PurchaseRequest>();
        public int TotalCount { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int ReceivedCount { get; set; }
        public string CurrentFilter { get; set; } = "all";
    }
}