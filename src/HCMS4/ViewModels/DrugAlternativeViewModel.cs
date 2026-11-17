namespace HCMS4.ViewModels
{
    public class DrugAlternativeViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public int Stock { get; set; }
        public decimal Price { get; set; }
        public string ExpiryStatus { get; set; } = string.Empty;
    }
}