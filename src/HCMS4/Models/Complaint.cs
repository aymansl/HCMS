using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class Complaint
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Patient is required")]
        [ForeignKey("Patient")]
        [Display(Name = "Patient")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Complaint type is required")]
        [Display(Name = "Complaint Type")]
        public ComplaintType Type { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "Associated Visit Date")]
        public DateTime? AssociatedVisitDate { get; set; }

        [StringLength(500, ErrorMessage = "Attachment path cannot exceed 500 characters")]
        [Display(Name = "Attachment Path")]
        public string? AttachmentPath { get; set; }

        [Display(Name = "Tracking Number")]
        public string TrackingNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Submission date is required")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Submission Date")]
        public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Status")]
        public ComplaintStatus Status { get; set; } = ComplaintStatus.Submitted;

        [DataType(DataType.DateTime)]
        [Display(Name = "Resolved At")]
        public DateTime? ResolvedAt { get; set; }

        [StringLength(1000, ErrorMessage = "Resolution notes cannot exceed 1000 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Resolution Notes")]
        public string? ResolutionNotes { get; set; }

        [Display(Name = "Patient")]
        public Patient Patient { get; set; } = null!;
    }

    public enum ComplaintType
    {
        [Display(Name = "Service")]
        Service,

        [Display(Name = "Doctor")]
        Doctor,

        [Display(Name = "Appointments")]
        Appointments,

        [Display(Name = "Billing")]
        Billing,

        [Display(Name = "Other")]
        Other
    }

    public enum ComplaintStatus
    {
        [Display(Name = "Submitted")]
        Submitted,

        [Display(Name = "Under Review")]
        UnderReview,

        [Display(Name = "Resolved")]
        Resolved,

        [Display(Name = "Closed")]
        Closed
    }
}
