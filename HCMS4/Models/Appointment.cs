using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class Appointment
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

        [Required(ErrorMessage = "Appointment date and time is required")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Appointment Date & Time")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm}", ApplyFormatInEditMode = true)]
        [FutureDate(ErrorMessage = "Appointment must be in the future")]
        public DateTime AppointmentDateTime { get; set; }

        [Required(ErrorMessage = "Status is required")]
        [Display(Name = "Status")]
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

        [StringLength(500, ErrorMessage = "Cancellation reason cannot exceed 500 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Cancellation Reason")]
        public string? CancellationReason { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Created At")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}")]
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        [DataType(DataType.DateTime)]
        [Display(Name = "Updated At")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}")]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        [Range(0, 10000, ErrorMessage = "Consultation fee must be between 0 and 10,000")]
        [DataType(DataType.Currency)]
        [Display(Name = "Consultation Fee")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal ConsultationFee { get; set; }

        [Display(Name = "Was No-Show")]
        public bool WasNoShow { get; set; } = false;

        [DataType(DataType.DateTime)]
        [Display(Name = "No-Show Evaluated At")]
        public DateTime? NoShowEvaluatedAt { get; set; }

        [Display(Name = "No-Show Risk Score")]
        [Range(0, 1)]
        public double? NoShowRiskScore { get; set; }

        [Display(Name = "Patient")]
        public Patient Patient { get; set; }

        [Display(Name = "Doctor")]
        public Doctor Doctor { get; set; }
        

      
        public class FutureDateAttribute : ValidationAttribute
        {
            protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
            {
                if (value is DateTime dateTime)
                {
                    if (dateTime <= DateTime.Now)
                    {
                        return new ValidationResult("Appointment must be in the future");
                    }
                }
                return ValidationResult.Success;
            }
        }
    }

    public enum AppointmentStatus
    {
        [Display(Name = "Scheduled")]
        Scheduled,

        [Display(Name = "Completed")]
        Completed,

        [Display(Name = "Canceled")]
        Canceled
    }
}