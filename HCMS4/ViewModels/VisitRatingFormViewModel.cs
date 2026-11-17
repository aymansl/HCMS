using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class VisitRatingFormViewModel
    {
        public int AppointmentId { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public DateTime AppointmentDateTime { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }

        public bool DoctorCooperative { get; set; }
        public bool WaitingTimeReasonable { get; set; }
    }
}
