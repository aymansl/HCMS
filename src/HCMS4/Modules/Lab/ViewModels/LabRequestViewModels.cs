using System.ComponentModel.DataAnnotations;
using HCMS4.Modules.Lab.Models;

namespace HCMS4.Modules.Lab.ViewModels
{
    /// <summary>The doctor's new / edit lab request form (UC-L03). Id 0 means a new request.</summary>
    public class LabRequestFormViewModel : IValidatableObject
    {
        public const string NoTestsError = "Select at least one test.";

        public int Id { get; set; }

        public int PatientId { get; set; }

        public List<int> SelectedTestIds { get; set; } = new();

        [StringLength(500)]
        [Display(Name = "Clinical Notes")]
        public string? ClinicalNotes { get; set; }

        // Display only; filled by the service.
        public string PatientName { get; set; } = string.Empty;
        public string? ReturnNote { get; set; }
        public List<LabTest> AvailableTests { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SelectedTestIds.Count == 0)
            {
                yield return new ValidationResult(NoTestsError, new[] { nameof(SelectedTestIds) });
            }
        }
    }

    /// <summary>The technician's "return to doctor" form (UC-L03 alternative flow).</summary>
    public class ReturnLabRequestViewModel
    {
        [Required(ErrorMessage = "Write a note for the doctor.")]
        [StringLength(500)]
        public string Note { get; set; } = string.Empty;
    }

    /// <summary>A doctor's lab requests, optionally for one patient.</summary>
    public class DoctorLabRequestsViewModel
    {
        public int? PatientId { get; set; }
        public string? PatientName { get; set; }
        public List<LabRequest> Requests { get; set; } = new();
    }
}
