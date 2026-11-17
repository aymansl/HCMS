using System.ComponentModel.DataAnnotations;

public class ClinicalNoteViewModel
{
    public int Id { get; set; }

    [Display(Name = "Date")]
    [DisplayFormat(DataFormatString = "{0:MMMM dd, yyyy HH:mm}")]
    public DateTime Date { get; set; }

    [Display(Name = "Note Type")]
    public string NoteType { get; set; } = string.Empty;

    [Display(Name = "Content")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Diagnosis")]
    public string? Diagnosis { get; set; }

    public string DoctorName { get; set; } = string.Empty;
}