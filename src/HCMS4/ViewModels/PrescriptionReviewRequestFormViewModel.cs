using HCMS4.Models;
using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class PrescriptionReviewRequestFormViewModel
    {
        public int PrescriptionId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;

        [Required]
        public ReviewReason ReasonForReview { get; set; }

        [StringLength(1000)]
        public string? AdditionalComments { get; set; }

        [StringLength(500)]
        public string? SuggestedAlternative { get; set; }
    }
}
