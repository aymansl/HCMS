using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class PrescriptionItemCreateViewModel
    {
        [Required]
        [Display(Name = "Drug")]
        public int SelectedDrugId { get; set; } // تغيير من DrugId إلى SelectedDrugId

        [Display(Name = "Drug Name")]
        public string DrugName { get; set; } = string.Empty;

        [Required]
        [StringLength(50, ErrorMessage = "Dosage cannot exceed 50 characters")]
        [Display(Name = "Dosage")]
        public string Dosage { get; set; } = string.Empty;

        [Required]
        [StringLength(50, ErrorMessage = "Duration cannot exceed 50 characters")]
        [Display(Name = "Duration")]
        public string Duration { get; set; } = string.Empty;

        [Required]
        [StringLength(50, ErrorMessage = "Frequency cannot exceed 50 characters")]
        [Display(Name = "Frequency")]
        public string Frequency { get; set; } = string.Empty;

        [Required]
        [Range(1, 10000, ErrorMessage = "Quantity must be between 1 and 10,000")]
        [Display(Name = "Quantity")]
        public int Quantity { get; set; }

        [StringLength(500, ErrorMessage = "Instructions cannot exceed 500 characters")]
        [Display(Name = "Instructions")]
        public string Instructions { get; set; } = string.Empty;

        [Display(Name = "Price")]
        public decimal Price { get; set; }

        [Display(Name = "Available Stock")]
        public int AvailableStock { get; set; }

        [Display(Name = "Expiry Status")]
        public string ExpiryStatus { get; set; } = string.Empty;
    }
}