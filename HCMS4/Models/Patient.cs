using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HCMS4.Models.Common;

namespace HCMS4.Models
{
    public class Patient
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "User ID is required")]
        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "User is required")]
        [Display(Name = "User Account")]
        public ApplicationUser User { get; set; } = null!;

        [Required(ErrorMessage = "Date of birth is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Phone(ErrorMessage = "Invalid phone number format")]
        [StringLength(20, ErrorMessage = "Emergency contact cannot exceed 20 characters")]
        [Display(Name = "Emergency Contact")]
        public string? EmergencyContact { get; set; }

       
        [Display(Name = "Appointments")]
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        [Display(Name = "Prescriptions")]
        public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

        [Display(Name = "Invoices")]
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

        [StringLength(500, ErrorMessage = "Chronic conditions cannot exceed 500 characters")]
        [Display(Name = "Chronic Conditions")]
        public string? ChronicConditions { get; set; }

        [Display(Name = "Status")]
        public PatientStatus Status { get; set; } = PatientStatus.Active;

        [Display(Name = "No-Show Count")]
        public int NoShowCount { get; set; } = 0;

        [Display(Name = "Disable Reason")]
        public string? DisableReason { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Disabled At")]
        public DateTime? DisabledAt { get; set; }
    }

    public enum PatientStatus
    {
        [Display(Name = "Active")]
        Active,
        [Display(Name = "Disabled")]
        Disabled
    }
}
