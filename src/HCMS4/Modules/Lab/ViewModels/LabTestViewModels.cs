using System.ComponentModel.DataAnnotations;

namespace HCMS4.Modules.Lab.ViewModels
{
    /// <summary>The add / edit form of a catalogue test (UC-L06). Id 0 means a new test.</summary>
    public class LabTestFormViewModel : IValidatableObject
    {
        public const string RangeError = "The minimum reference value cannot be greater than the maximum.";

        public int Id { get; set; }

        [Required(ErrorMessage = "Test name is required")]
        [StringLength(100)]
        [Display(Name = "Test Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Unit is required")]
        [StringLength(30)]
        public string Unit { get; set; } = string.Empty;

        [Display(Name = "Reference Min")]
        public decimal? ReferenceMin { get; set; }

        [Display(Name = "Reference Max")]
        public decimal? ReferenceMax { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0, 100_000_000, ErrorMessage = "The price cannot be negative.")]
        public decimal? Price { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ReferenceMin.HasValue && ReferenceMax.HasValue && ReferenceMin > ReferenceMax)
            {
                yield return new ValidationResult(RangeError, new[] { nameof(ReferenceMin), nameof(ReferenceMax) });
            }
        }
    }
}
