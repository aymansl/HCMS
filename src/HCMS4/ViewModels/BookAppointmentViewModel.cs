using HCMS4.Models;
using System.ComponentModel.DataAnnotations;
using static HCMS4.Models.Appointment;

namespace HCMS4.ViewModels
{
    public class BookAppointmentViewModel
    {
        [Required]
        [Display(Name = "Patient")]
        public int PatientId { get; set; }

        [Required]
        [Display(Name = "Doctor")]
        public int DoctorId { get; set; }

        [Required]
        [Display(Name = "Appointment Date & Time")]
        [DataType(DataType.DateTime)]
        [FutureDate(ErrorMessage = "Appointment must be in the future")]
        public DateTime AppointmentDateTime { get; set; }

        public List<DoctorSelectDto> AvailableDoctors { get; set; } = new();
        public List<PatientSelectDto> AvailablePatients { get; set; } = new();

        
        [Display(Name = "Reason for Visit")]
        [StringLength(200, ErrorMessage = "Symptoms cannot exceed 200 characters")]
        [DataType(DataType.MultilineText)]
        public string? Symptoms { get; set; }
    }
}