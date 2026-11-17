using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class DoctorReviewRequest
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Pharmacist is required")]
        [ForeignKey("Pharmacist")]
        [Display(Name = "Pharmacist")]
        public int PharmacistId { get; set; }

        [Required(ErrorMessage = "Prescription is required")]
        [ForeignKey("Prescription")]
        [Display(Name = "Prescription")]
        public int PrescriptionId { get; set; }

        [Required(ErrorMessage = "Doctor is required")]
        [ForeignKey("Doctor")]
        [Display(Name = "Doctor")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Reason for review is required")]
        [Display(Name = "Reason for Review")]
        public ReviewReason ReasonForReview { get; set; }

        [StringLength(1000, ErrorMessage = "Additional comments cannot exceed 1000 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Additional Comments")]
        public string? AdditionalComments { get; set; }

        [StringLength(500, ErrorMessage = "Suggested alternative cannot exceed 500 characters")]
        [Display(Name = "Suggested Alternative")]
        public string? SuggestedAlternative { get; set; }

        [Required(ErrorMessage = "Request date is required")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Request Date")]
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Status")]
        public ReviewRequestStatus Status { get; set; } = ReviewRequestStatus.Pending;

        [StringLength(1000, ErrorMessage = "Doctor response cannot exceed 1000 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Doctor Response")]
        public string? DoctorResponse { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Reviewed At")]
        public DateTime? ReviewedAt { get; set; }

        [Display(Name = "Pharmacist")]
        public Pharmacist Pharmacist { get; set; } = null!;

        [Display(Name = "Prescription")]
        public Prescription Prescription { get; set; } = null!;

        [Display(Name = "Doctor")]
        public Doctor Doctor { get; set; } = null!;
    }

    public enum ReviewReason
    {
        [Display(Name = "Unclear Dosage")]
        UnclearDosage,

        [Display(Name = "Drug Interaction")]
        DrugInteraction,

        [Display(Name = "Medication Unavailable")]
        MedicationUnavailable,

        [Display(Name = "Alternative Medication")]
        AlternativeMedication,

        [Display(Name = "Other")]
        Other
    }

    public enum ReviewRequestStatus
    {
        [Display(Name = "Pending")]
        Pending,

        [Display(Name = "Responded")]
        Responded,

        [Display(Name = "Closed")]
        Closed
    }
}
