
namespace HCMS4.Models
{
   
    public class DailyReport
    {
       
        public int Id { get; set; }        
        public DateTime ReportDate { get; set; }
        public int TotalInvoices { get; set; }

        public decimal TotalAmount { get; set; }

        public DateTime GeneratedAt { get; set; }
        public string? ReportData { get; set; } // JSON data
    }
}