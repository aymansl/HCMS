using HCMS4.Models;

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

        public List<Drug> ExpiringDrugs { get; set; }
        public List<Drug> LowStockDrugs { get; set; }
        public List<Prescription> RecentPrescriptions { get; set; }

        public List<string> Alerts { get; set; }
    }
}