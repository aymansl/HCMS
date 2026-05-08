using HCMS4.Models;

namespace HCMS4.ViewModels
{
    public class ExpiryAlertsViewModel
    {
        public List<Drug> ExpiredDrugs { get; set; } = new();
        public List<Drug> ExpiringSoonDrugs { get; set; } = new();
        public List<Drug> ExpiringShortlyDrugs { get; set; } = new();

        public decimal TotalExpiredValue { get; set; }
        public decimal TotalExpiringSoonValue { get; set; }

        public DateTime LastChecked { get; set; }

        public int TotalExpired => ExpiredDrugs.Count;
        public int TotalExpiringSoon => ExpiringSoonDrugs.Count;
        public int TotalExpiringShortly => ExpiringShortlyDrugs.Count;
    }
}