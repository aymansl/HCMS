using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class PharmacistPrescriptionNote
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Prescription is required")]
        [ForeignKey("Prescription")]
        [Display(Name = "Prescription")]
        public int PrescriptionId { get; set; }

        [Required(ErrorMessage = "Pharmacist is required")]
        [ForeignKey("Pharmacist")]
        [Display(Name = "Pharmacist")]
        public int PharmacistId { get; set; }

        [Required(ErrorMessage = "Note text is required")]
        [StringLength(2000, ErrorMessage = "Note text cannot exceed 2000 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Note Text")]
        public string NoteText { get; set; } = string.Empty;

        [Display(Name = "Notify Doctor")]
        public bool NotifyDoctor { get; set; } = false;

        [DataType(DataType.DateTime)]
        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Prescription")]
        public Prescription Prescription { get; set; } = null!;

        [Display(Name = "Pharmacist")]
        public Pharmacist Pharmacist { get; set; } = null!;
    }
}
