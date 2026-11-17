// ViewModels/PurchaseRequestViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class CreatePurchaseRequestViewModel
    {
        [Display(Name = "Supplier")]
        public int? SupplierId { get; set; }

        [StringLength(500)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        public List<SupplierSelectDto> AvailableSuppliers { get; set; } = new List<SupplierSelectDto>();
        public List<DrugSelectDto> AvailableDrugs { get; set; } = new List<DrugSelectDto>();
        public List<PurchaseRequestItemDto> Items { get; set; } = new List<PurchaseRequestItemDto>();
    }

    public class SupplierSelectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
    }

    public class DrugSelectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int CurrentStock { get; set; }
        public string ExpiryStatus { get; set; } = string.Empty;
        public string DisplayText => $"{Name} - Stock: {CurrentStock} - ${Price:F2}";
    }

    public class PurchaseRequestItemDto
    {
        public int DrugId { get; set; }
        public string DrugName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal => Quantity * UnitPrice;
        public string? Notes { get; set; }
    }
}