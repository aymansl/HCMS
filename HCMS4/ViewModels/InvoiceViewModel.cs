using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class InvoiceViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Invoice Date")]
        public DateTime InvoiceDate { get; set; }

        [Display(Name = "Invoice Number")]
        public string InvoiceNumber => $"INV-{Id.ToString().PadLeft(6, '0')}";

        [Display(Name = "Consultation Fee")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal ConsultationFee { get; set; }

        [Display(Name = "Medication Total")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal MedicationTotal { get; set; }

        [Display(Name = "Total Amount")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Payment Status")]
        public string PaymentStatus { get; set; }

        [Display(Name = "Status Badge Class")]
        public string StatusBadgeClass
        {
            get
            {
                return PaymentStatus switch
                {
                    "Paid" => "bg-success",
                    "Partially Paid" => "bg-warning",
                    _ => "bg-secondary" // Pending
                };
            }
        }

        [Display(Name = "Amount Paid")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal? AmountPaid { get; set; }

        [Display(Name = "Balance")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal Balance => TotalAmount - (AmountPaid ?? 0);

        [Display(Name = "Payment Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime? PaymentDate { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Doctor Name")]
        public string? DoctorName { get; set; }

        [Display(Name = "Appointment Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime? AppointmentDate { get; set; }

        [Display(Name = "Created At")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}")]
        public DateTime CreatedAt { get; set; }

    }
}