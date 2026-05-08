// Models/PurchaseRequestItem.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class PurchaseRequestItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("PurchaseRequest")]
        public int PurchaseRequestId { get; set; }

        [Required]
        [ForeignKey("Drug")]
        [Display(Name = "Drug")]
        public int DrugId { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Drug Name")]
        public string DrugName { get; set; } = string.Empty;

        [Required]
        [Range(1, 10000)]
        [Display(Name = "Quantity")]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, 10000)]
        [DataType(DataType.Currency)]
        [Display(Name = "Unit Price")]
        public decimal UnitPrice { get; set; }

        [DataType(DataType.Currency)]
        [Display(Name = "Subtotal")]
        public decimal Subtotal => Quantity * UnitPrice;

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // Navigation properties
        public virtual PurchaseRequest PurchaseRequest { get; set; } = null!;
        public virtual Drug Drug { get; set; } = null!;
    }
}