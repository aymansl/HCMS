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

    /// <summary>The doctor's cancel form (UC-L04).</summary>
    public class CancelLabRequestViewModel
    {
        [Required(ErrorMessage = "Write the reason for the cancellation.")]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        // Display only.
        public LabRequest? Request { get; set; }
    }

    /// <summary>The technician's note form (UC-L07).</summary>
    public class LabNoteViewModel
    {
        [Required(ErrorMessage = "Write the note.")]
        [StringLength(500)]
        public string Text { get; set; } = string.Empty;

        public bool IsUrgent { get; set; }
    }

    /// <summary>The technician's note on a cancelled request (UC-L04 alternative flow).</summary>
    public class CancellationNoteViewModel
    {
        [Required(ErrorMessage = "Write the note.")]
        [StringLength(500)]
        public string Note { get; set; } = string.Empty;
    }

    /// <summary>The lab's incoming requests page (UC-L02).</summary>
    public class LabIncomingRequestsViewModel
    {
        public const string NoRequestsMessage = "No requests at the moment.";
        public const string NoMatchMessage = "No requests match the filter.";

        public HCMS4.Modules.Lab.Services.LabRequestFilter Filter { get; set; } = new();
        public List<LabRequest> Requests { get; set; } = new();
    }

    /// <summary>A doctor's lab requests, optionally for one patient.</summary>
    public class DoctorLabRequestsViewModel
    {
        public int? PatientId { get; set; }
        public string? PatientName { get; set; }
        public List<LabRequest> Requests { get; set; } = new();
    }
}
