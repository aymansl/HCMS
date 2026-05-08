using HCMS4.Models;
using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class GenerateInvoiceViewModel
    {
        [Required]
        public int PatientId { get; set; }

        [Display(Name = "Patient Name")]
        public string PatientName { get; set; }

        [Display(Name = "Appointment")]
        public int? AppointmentId { get; set; }

        [Display(Name = "Prescription")]
        public int? PrescriptionId { get; set; }

        [Required(ErrorMessage = "Consultation fee is required")]
        [Range(0, 10000, ErrorMessage = "Consultation fee must be between 0 and 10,000")]
        [DataType(DataType.Currency)]
        [Display(Name = "Consultation Fee")]
        public decimal ConsultationFee { get; set; } = 0;

        [Required(ErrorMessage = "Medication total is required")]
        [Range(0, 10000, ErrorMessage = "Medication total must be between 0 and 10,000")]
        [DataType(DataType.Currency)]
        [Display(Name = "Medication Total")]
        public decimal MedicationTotal { get; set; } = 0;

        [Required(ErrorMessage = "Total amount is required")]
        [Range(0.01, 20000, ErrorMessage = "Total amount must be between 0.01 and 20,000")]
        [DataType(DataType.Currency)]
        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; } = 0;

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // For dropdown lists
        public List<AppointmentSelectDto>? RecentAppointments { get; set; }
        public List<PrescriptionSelectDto>? RecentPrescriptions { get; set; }

        // Patient's existing invoices for reference
        public List<Invoice>? ExistingInvoices { get; set; }

        public class AppointmentSelectDto
        {
            public int Id { get; set; }
            public string DisplayText { get; set; }
            public DateTime AppointmentDate { get; set; }
            public string DoctorName { get; set; }
            public decimal? ConsultationFee { get; set; }
        }

        public class PrescriptionSelectDto
        {
            public int Id { get; set; }
            public string DisplayText { get; set; }
            public DateTime PrescriptionDate { get; set; }
            public string DoctorName { get; set; }
            public decimal? MedicationTotal { get; set; }
        }
    }
}
