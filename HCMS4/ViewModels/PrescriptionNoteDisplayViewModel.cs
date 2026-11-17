namespace HCMS4.ViewModels
{
    public class PrescriptionNoteDisplayViewModel
    {
        public string PharmacistName { get; set; } = string.Empty;
        public string NoteText { get; set; } = string.Empty;
        public bool NotifyDoctor { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
