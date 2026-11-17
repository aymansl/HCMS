using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class VisitRating
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Patient is required")]
        [ForeignKey("Patient")]
        [Display(Name = "Patient")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Doctor is required")]
        [ForeignKey("Doctor")]
        [Display(Name = "Doctor")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Appointment is required")]
        [ForeignKey("Appointment")]
        [Display(Name = "Appointment")]
        public int AppointmentId { get; set; }

        [Required(ErrorMessage = "Rating is required")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars")]
        [Display(Name = "Rating")]
        public int Rating { get; set; }

        [StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Comment")]
        public string? Comment { get; set; }

        [Display(Name = "Was Doctor Cooperative?")]
        public bool? DoctorCooperative { get; set; }

        [Display(Name = "Was Waiting Time Reasonable?")]
        public bool? WaitingTimeReasonable { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Patient")]
        public Patient Patient { get; set; } = null!;

        [Display(Name = "Doctor")]
        public Doctor Doctor { get; set; } = null!;

        [Display(Name = "Appointment")]
        public Appointment Appointment { get; set; } = null!;
    }
}
