using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class PrescriptionReissueRequest
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Patient is required")]
        [ForeignKey("Patient")]
        [Display(Name = "Patient")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Prescription is required")]
        [ForeignKey("Prescription")]
        [Display(Name = "Prescription")]
        public int PrescriptionId { get; set; }

        [Required(ErrorMessage = "Doctor is required")]
        [ForeignKey("Doctor")]
        [Display(Name = "Attending Doctor")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Request date is required")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Request Date")]
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Status")]
        public ReissueRequestStatus Status { get; set; } = ReissueRequestStatus.Pending;

        [StringLength(500, ErrorMessage = "Doctor response cannot exceed 500 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Doctor Response")]
        public string? DoctorResponse { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Reviewed At")]
        public DateTime? ReviewedAt { get; set; }

        [Display(Name = "Patient")]
        public Patient Patient { get; set; } = null!;

        [Display(Name = "Prescription")]
        public Prescription Prescription { get; set; } = null!;

        [Display(Name = "Doctor")]
        public Doctor Doctor { get; set; } = null!;
    }

    public enum ReissueRequestStatus
    {
        [Display(Name = "Pending")]
        Pending,

        [Display(Name = "Approved")]
        Approved,

        [Display(Name = "Rejected")]
        Rejected,

        [Display(Name = "Expired")]
        Expired
    }
}
