using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class PatientPrescriptionDetailViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Prescription Date")]
        [DisplayFormat(DataFormatString = "{0:dddd, MMMM dd, yyyy}")]
        public DateTime PrescriptionDate { get; set; }

        [Display(Name = "Patient Name")]
        public string PatientName { get; set; } = string.Empty;

        [Display(Name = "Doctor")]
        public string DoctorName { get; set; } = string.Empty;

        [Display(Name = "Specialization")]
        public string Specialization { get; set; } = string.Empty;

        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Total Cost")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal TotalCost { get; set; }

        [Display(Name = "Dispensed Date")]
        [DisplayFormat(DataFormatString = "{0:dddd, MMMM dd, yyyy HH:mm}")]
        public DateTime? DispensedDate { get; set; }

        [Display(Name = "Dispensed By")]
        public string? DispensedBy { get; set; }

        [Display(Name = "Created At")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Appointment Date")]
        [DisplayFormat(DataFormatString = "{0:dddd, MMMM dd, yyyy}")]
        public DateTime? AppointmentDate { get; set; }

        public List<PrescriptionItemDetailViewModel> PrescriptionItems { get; set; } = new();
        public List<PrescriptionNoteDisplayViewModel> PharmacistNotes { get; set; } = new();
        public DateTime? PrescriptionExpiryDate { get; set; }
        public bool CanRequestReissue { get; set; }
        public bool HasPendingReissueRequest { get; set; }
        public string? LatestReissueStatus { get; set; }

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
}
