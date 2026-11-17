using HCMS4.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class Invoice
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Patient")]
        public int PatientId { get; set; }

        [ForeignKey("Appointment")]
        public int? AppointmentId { get; set; }

        [ForeignKey("Prescription")]
        public int? PrescriptionId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Range(0, 10000)]
        [DataType(DataType.Currency)]
        public decimal ConsultationFee { get; set; }

        [Required]
        [Range(0, 10000)]
        [DataType(DataType.Currency)]
        public decimal MedicationTotal { get; set; }

        [Required]
        [Range(0.01, 20000)]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        [Required]
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        [DataType(DataType.Date)]
        public DateTime? PaymentDate { get; set; }

        [DataType(DataType.Currency)]
        public decimal? AmountPaid { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


        public virtual Patient Patient { get; set; }
        public virtual Appointment? Appointment { get; set; }
        public virtual Prescription? Prescription { get; set; }

        [ForeignKey("Pharmacist")]
        public int? PharmacistId { get; set; }

        [Display(Name = "Dispensed By")]
        public virtual Pharmacist? Pharmacist { get; set; }

        [Display(Name = "Dispensed At")]
        public DateTime? DispensedAt { get; set; }
    }


    public enum PaymentStatus
    {
        [Display(Name = "Pending")]
        Pending,

        [Display(Name = "Paid")]
        Paid
    }
}