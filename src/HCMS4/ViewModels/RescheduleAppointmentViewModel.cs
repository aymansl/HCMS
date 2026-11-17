using System.ComponentModel.DataAnnotations;
using static HCMS4.Models.Appointment;

namespace HCMS4.ViewModels
{
    public class RescheduleAppointmentViewModel
    {
        public int AppointmentId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;

        [Display(Name = "Current Appointment Time")]
        public DateTime CurrentAppointmentDateTime { get; set; }

        [Required]
        [Display(Name = "New Appointment Time")]
        [DataType(DataType.DateTime)]
        [FutureDate(ErrorMessage = "New appointment must be in the future")]
        public DateTime NewAppointmentDateTime { get; set; }


    }
}
