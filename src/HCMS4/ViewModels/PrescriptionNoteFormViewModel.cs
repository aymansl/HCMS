using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class PrescriptionNoteFormViewModel
    {
        public int PrescriptionId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string NoteText { get; set; } = string.Empty;

        public bool NotifyDoctor { get; set; }
    }
}
