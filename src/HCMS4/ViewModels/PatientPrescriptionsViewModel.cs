using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class PatientPrescriptionsViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Prescription Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime PrescriptionDate { get; set; }

        [Display(Name = "Doctor")]
        public string DoctorName { get; set; } = string.Empty;

        [Display(Name = "Specialization")]
        public string Specialization { get; set; } = string.Empty;

        [Display(Name = "Drug Name")]
        public string DrugName { get; set; } = string.Empty;

        [Display(Name = "Dosage")]
        public string Dosage { get; set; } = string.Empty;

        [Display(Name = "Duration")]
        public string Duration { get; set; } = string.Empty;

        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";

        [Display(Name = "Total Cost")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal TotalCost { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Dispensed Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm}")]
        public DateTime? DispensedDate { get; set; }

        [Display(Name = "Dispensed By")]
        public string? DispensedBy { get; set; }

        [Display(Name = "Prescription Expiry Date")]
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
