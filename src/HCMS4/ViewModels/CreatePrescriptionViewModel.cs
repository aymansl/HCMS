using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class CreatePrescriptionViewModel
    {
        [Required]
        [Display(Name = "Patient ID")]
        public int PatientId { get; set; }

        [Display(Name = "Appointment ID")]
        public int? AppointmentId { get; set; }

        [Required]
        [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
        [Display(Name = "Prescription Notes")]
        public string Notes { get; set; } = string.Empty;

        public List<PrescriptionItemCreateViewModel> Items { get; set; } = new();

        [Required(ErrorMessage = "Doctor's electronic signature is required")]
        [Display(Name = "Electronic Signature")]
        public string DoctorSignature { get; set; } = string.Empty;

        [Display(Name = "Total Cost")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal TotalCost => Items?.Sum(item => item.Quantity * item.Price) ?? 0;

        public List<DrugSelectionViewModel> AvailableDrugs { get; set; } = new();
    }
}
