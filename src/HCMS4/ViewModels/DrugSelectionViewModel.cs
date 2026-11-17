namespace HCMS4.ViewModels
{
    public class DrugSelectionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Supplier { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string ExpiryStatus { get; set; } = string.Empty;
        public string? Description { get; set; }

        public string DisplayInfo => $"{Name} - {Supplier} | Stock: {Quantity} | Price: ${Price:F2} | Expiry: {ExpiryStatus}";
    }
}