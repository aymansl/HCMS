using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class ClinicalNote
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Patient is required")]
        [ForeignKey("Patient")]
        [Display(Name = "Patient")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Doctor is required")]
        [ForeignKey("Doctor")]
        [Display(Name = "Doctor")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}")]
        public DateTime Date { get; set; } = DateTime.UtcNow;

        [Required(ErrorMessage = "Note type is required")]
        [StringLength(50, ErrorMessage = "Note type cannot exceed 50 characters")]
        [Display(Name = "Note Type")]
        public string NoteType { get; set; } = string.Empty; // Initialize

        [Required(ErrorMessage = "Content is required")]
        [StringLength(2000, ErrorMessage = "Content cannot exceed 2000 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Content")]
        public string Content { get; set; } = string.Empty; // Initialize

        [StringLength(500, ErrorMessage = "Diagnosis cannot exceed 500 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Diagnosis")]
        public string? Diagnosis { get; set; }

        
        [Display(Name = "Patient")]
        public Patient Patient { get; set; } = null!;

        [Display(Name = "Doctor")]
        public Doctor Doctor { get; set; } = null!;
    }
}