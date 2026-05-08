using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class PrescriptionItemDetailViewModel
    {
        [Display(Name = "Drug Name")]
        public string DrugName { get; set; } = string.Empty;

        [Display(Name = "Dosage")]
        public string Dosage { get; set; } = string.Empty;

        [Display(Name = "Duration")]
        public string Duration { get; set; } = string.Empty;

        [Display(Name = "Frequency")]
        public string Frequency { get; set; } = string.Empty;

        [Display(Name = "Quantity")]
        public int Quantity { get; set; }

        [Display(Name = "Instructions")]
        public string Instructions { get; set; } = string.Empty;

        [Display(Name = "Price")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal Price { get; set; }

        [Display(Name = "Subtotal")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal Subtotal { get; set; }
    }
}
