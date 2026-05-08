// Models/PurchaseRequest.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class PurchaseRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Request Number")]
        public string RequestNumber { get; set; } = string.Empty;

        [Required]
        [ForeignKey("Pharmacist")]
        [Display(Name = "Requested By")]
        public int PharmacistId { get; set; }

        [ForeignKey("Supplier")]
        [Display(Name = "Supplier")]
        public int? SupplierId { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [Display(Name = "Request Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm}")]
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Display(Name = "Status")]
        public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Pending;

        [DataType(DataType.Currency)]
        [Display(Name = "Total Amount")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal TotalAmount { get; set; }

        [StringLength(500)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Approved Date")]
        public DateTime? ApprovedDate { get; set; }

        [StringLength(100)]
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [StringLength(500)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Received Date")]
        public DateTime? ReceivedDate { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [DataType(DataType.DateTime)]
        [Display(Name = "Updated At")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Pharmacist Pharmacist { get; set; } = null!;
        public virtual Supplier? Supplier { get; set; }
        public virtual ICollection<PurchaseRequestItem> Items { get; set; } = new List<PurchaseRequestItem>();
    }

    public enum PurchaseRequestStatus
    {
        [Display(Name = "Pending Review")]
        Pending,
        [Display(Name = "Approved")]
        Approved,
        [Display(Name = "Rejected")]
        Rejected,
        [Display(Name = "Ordered")]
        Ordered,
        [Display(Name = "Received")]
        Received,
        [Display(Name = "Cancelled")]
        Cancelled
    }
}