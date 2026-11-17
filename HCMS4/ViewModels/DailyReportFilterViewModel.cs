// ViewModels/DailyReportFilterViewModel.cs
using HCMS4.Models;
using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class DailyReportFilterViewModel
    {
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        public PaymentStatus? PaymentStatus { get; set; }

        public string? SearchTerm { get; set; }
    }
}