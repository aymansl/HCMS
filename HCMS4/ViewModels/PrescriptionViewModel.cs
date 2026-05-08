using HCMS4.ViewModels;
using System.ComponentModel.DataAnnotations;

public class PrescriptionViewModel
{
    public int Id { get; set; }

    [Display(Name = "Prescription Date")]
    [DisplayFormat(DataFormatString = "{0:MMMM dd, yyyy}")]
    public DateTime PrescriptionDate { get; set; }

    public string DoctorName { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;

    [Display(Name = "Status")]
    public string Status { get; set; } = string.Empty;

    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Total Cost")]
    [DisplayFormat(DataFormatString = "{0:C2}")]
    public decimal TotalCost { get; set; }

    public List<PrescriptionItemViewModel> Items { get; set; } = new();
    public List<PrescriptionNoteDisplayViewModel> PharmacistNotes { get; set; } = new();

    // Helper property for styling
    public string StatusBadgeClass
    {
        get
        {
            return Status.ToLower() switch
            {
                "pending" => "bg-warning",
                "completed" => "bg-success",
                "canceled" => "bg-danger",
                _ => "bg-secondary"
            };
        }
    }
}
