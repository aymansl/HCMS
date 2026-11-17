using System;
using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class AppointmentHistoryViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Appointment Date & Time")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm}")]
        public DateTime AppointmentDateTime { get; set; }

        [Display(Name = "Doctor")]
        public string DoctorName { get; set; } = string.Empty;

        [Display(Name = "Specialization")]
        public string Specialization { get; set; } = string.Empty;

        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;

        [Display(Name = "Fee")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal? ConsultationFee { get; set; }

        [Display(Name = "Booked On")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime? CreatedAt { get; set; }

        public bool CanRateVisit { get; set; }
        public bool HasVisitRating { get; set; }
        public int? VisitRatingId { get; set; }

        
        public string StatusBadgeClass
        {
            get
            {
                return Status.ToLower() switch
                {
                    "scheduled" => "bg-warning",
                    "completed" => "bg-success",
                    "canceled" => "bg-danger",
                    _ => "bg-secondary"
                };
            }
        }
    }
}
