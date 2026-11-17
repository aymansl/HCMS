using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class CreateClinicalNoteViewModel
    {
        [Required]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Note type is required")]
        [StringLength(50, ErrorMessage = "Note type cannot exceed 50 characters")]
        [Display(Name = "Note Type")]
        public string NoteType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Content is required")]
        [StringLength(2000, ErrorMessage = "Content cannot exceed 2000 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Content")]
        public string Content { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Diagnosis cannot exceed 500 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Diagnosis")]
        public string? Diagnosis { get; set; }
    }
}