using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class Doctor
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "User ID is required")]
        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty; 

        [Required(ErrorMessage = "User is required")]
        [Display(Name = "User Account")]
        public ApplicationUser User { get; set; } = null!;

        [ForeignKey("Specialization")]
        [Display(Name = "Specialization")]
        public int? SpecializationId { get; set; }
        public Specialization? Specialization { get; set; }

        [NotMapped]
        public string SpecializationName => Specialization?.Name ?? "Not specified";

        [StringLength(500, ErrorMessage = "Qualifications cannot exceed 500 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Qualifications")]
        public string? Qualifications { get; set; }

        [Phone(ErrorMessage = "Invalid phone number format")]
        [StringLength(50, ErrorMessage = "Contact info cannot exceed 50 characters")]
        [Display(Name = "Contact Information")]
        public string? ContactInfo { get; set; }
        [Display(Name = "Available for Appointments")]
        public bool IsAvailable { get; set; } = true;

        [Range(0, 5)]
        [Display(Name = "Average Rating")]
        public decimal AverageRating { get; set; }

        [Display(Name = "Rating Count")]
        public int RatingCount { get; set; }

        [Display(Name = "Can Publish Articles")]
        public bool CanPublishArticles { get; set; } = true;

        [Display(Name = "Appointments")]
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        [Display(Name = "Prescriptions")]
        public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

        [Display(Name = "Doctor Name")]
        public string FullName => User?.FullName ?? "Unknown";

        [Display(Name = "Doctor Leaves")]
        public ICollection<DoctorLeave> DoctorLeaves { get; set; } = new List<DoctorLeave>();

        [Display(Name = "Medical Articles")]
        public ICollection<MedicalArticle> MedicalArticles { get; set; } = new List<MedicalArticle>();
    }
}
