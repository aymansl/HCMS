// ViewModels/PurchaseRequestDetailViewModel.cs
using HCMS4.Models;

namespace HCMS4.ViewModels
{
    public class PurchaseRequestDetailViewModel
    {
        public PurchaseRequest PurchaseRequest { get; set; } = null!;
        public List<PurchaseRequestItem> Items { get; set; } = new List<PurchaseRequestItem>();
        public bool CanApprove { get; set; }
        public bool CanReceive { get; set; }
    }
}