using System.ComponentModel.DataAnnotations;

namespace HCMS4.Models
{
    public class Drug
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Drug name is required")]
        [StringLength(100, ErrorMessage = "Drug name cannot exceed 100 characters")]
        [Display(Name = "Drug Name")]
        public string Name { get; set; }

        [StringLength(100, ErrorMessage = "Supplier name cannot exceed 100 characters")]
        [Display(Name = "Supplier")]
        public string? Supplier { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, 10000.00, ErrorMessage = "Price must be between 0.01 and 10,000")]
        [DataType(DataType.Currency)]
        [Display(Name = "Price")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(0, 10000, ErrorMessage = "Quantity must be between 0 and 10,000")]
        [Display(Name = "Quantity in Stock")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Expiry date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Expiry Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [FutureDate(ErrorMessage = "Expiry date must be in the future")]
        public DateTime ExpiryDate { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Created At")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [DataType(DataType.DateTime)]
        [Display(Name = "Updated At")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


        [Display(Name = "Prescription Items")]
        public ICollection<PrescriptionItem> PrescriptionItems { get; set; } = new List<PrescriptionItem>();

        public class FutureDateAttribute : ValidationAttribute
        {
            protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
            {
                if (value is DateTime dateTime)
                {
                    if (dateTime <= DateTime.Now)
                    {
                        return new ValidationResult("Expiry date must be in the future");
                    }
                }
                return ValidationResult.Success;
            }
        }

        [Display(Name = "Expiry Status")]
        public string ExpiryStatus
        {
            get
            {
                var daysUntilExpiry = (ExpiryDate - DateTime.Now).Days;
                if (daysUntilExpiry <= 0) return "Expired";
                if (daysUntilExpiry <= 30) return "Expiring Soon";
                if (daysUntilExpiry <= 60) return "Expiring Shortly";
                return "Valid";
            }
        }
    }
}