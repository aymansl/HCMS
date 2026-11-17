using HCMS4.Models;
using System.ComponentModel.DataAnnotations;
using static HCMS4.Models.Appointment;

namespace HCMS4.ViewModels
{
    // In BookAppointmentViewModel.cs, create a separate view model for patients
    // Or modify the existing one with conditional logic

    // Option 1: Create a new PatientBookAppointmentViewModel
    public class PatientBookAppointmentViewModel
    {
        [Required]
        [Display(Name = "Doctor")]
        public int DoctorId { get; set; }

        [Required]
        [Display(Name = "Appointment Date & Time")]
        [DataType(DataType.DateTime)]
        [FutureDate(ErrorMessage = "Appointment must be in the future")]
        public DateTime AppointmentDateTime { get; set; }

        public List<DoctorSelectDto> AvailableDoctors { get; set; } = new();

        [Display(Name = "Reason for Visit")]
        [StringLength(200, ErrorMessage = "Symptoms cannot exceed 200 characters")]
        [DataType(DataType.MultilineText)]
        public string? Symptoms { get; set; }

        public List<Doctor> RecommendedDoctors { get; set; } = new();
        public bool IsUrgent { get; set; }

        // Hidden field for patient ID
        public int PatientId { get; set; }
    }

    // Option 2: Alternatively, modify the BookAppointment.cshtml view to hide the patient selection
    // for patient users
}
