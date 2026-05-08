using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class Prescription
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

        [ForeignKey("Appointment")]
        [Display(Name = "Appointment")]
        public int? AppointmentId { get; set; }

        [Required(ErrorMessage = "Prescription date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Prescription Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime PrescriptionDate { get; set; } = DateTime.UtcNow;

        [Required(ErrorMessage = "Notes is required")]
        [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
        [Display(Name = "Notes")]
        public string Notes { get; set; } = string.Empty;

        [Required(ErrorMessage = "Status is required")]
        [Display(Name = "Status")]
        public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Pending;

        
        [Display(Name = "Patient")]
        public Patient Patient { get; set; }

        [Display(Name = "Doctor")]
        public Doctor Doctor { get; set; }

        [Display(Name = "Appointment")]
        public Appointment? Appointment { get; set; }

        [Display(Name = "Prescription Items")]
        public ICollection<PrescriptionItem> PrescriptionItems { get; set; } = new List<PrescriptionItem>();

        [DataType(DataType.DateTime)]
        [Display(Name = "Dispensed Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}")]
        public DateTime? DispensedDate { get; set; }

        [StringLength(100, ErrorMessage = "Dispensed by cannot exceed 100 characters")]
        [Display(Name = "Dispensed By")]
        public string? DispensedBy { get; set; }

        [Display(Name = "Total Cost")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal TotalCost => PrescriptionItems?.Sum(item => item.Quantity * item.Drug?.Price ?? 0) ?? 0;

        [Range(0, 10000, ErrorMessage = "Medication total must be between 0 and 10,000")]
        [DataType(DataType.Currency)]
        [Display(Name = "Medication Total")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal? MedicationTotal { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [DataType(DataType.DateTime)]
        [Display(Name = "Updated At")]

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(500)]
        [Display(Name = "Dispensing Notes")]
        public string? DispensingNotes { get; set; }

    }

    public enum PrescriptionStatus
    {
        Pending,
        Completed,
        Canceled
    }
}