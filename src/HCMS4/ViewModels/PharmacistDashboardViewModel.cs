using HCMS4.Modules.Pharmacy.Models;
namespace HCMS4.ViewModels
{
    public class PharmacistDashboardViewModel
    {
        public int TotalDrugs { get; set; }
        public int ExpiringSoonCount { get; set; }
        public int ExpiredCount { get; set; }
        public int LowStockCount { get; set; }
        public decimal TotalInventoryValue { get; set; }

        public int PendingPrescriptions { get; set; }
        public int CompletedPrescriptions { get; set; }

        public List<Drug> ExpiringDrugs { get; set; } = new();
        public List<Drug> LowStockDrugs { get; set; } = new();
        public List<Prescription> RecentPrescriptions { get; set; } = new();

        public List<string> Alerts { get; set; } = new();
    }
}