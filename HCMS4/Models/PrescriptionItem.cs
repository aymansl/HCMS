using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class PrescriptionItem
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Prescription is required")]
        [ForeignKey("Prescription")]
        [Display(Name = "Prescription")]
        public int PrescriptionId { get; set; }

        [Required(ErrorMessage = "Drug is required")]
        [ForeignKey("Drug")]
        [Display(Name = "Drug")]
        public int? DrugId { get; set; }

        [Required(ErrorMessage = "Drug name is required")]
        [StringLength(100, ErrorMessage = "Drug name cannot exceed 100 characters")]
        [Display(Name = "Drug Name")]
        public string DrugName { get; set; } = string.Empty; 

        [Required(ErrorMessage = "Dosage is required")]
        [StringLength(50, ErrorMessage = "Dosage cannot exceed 50 characters")]
        [Display(Name = "Dosage")]
        public string Dosage { get; set; } = string.Empty; 

        [Required(ErrorMessage = "Duration is required")]
        [StringLength(50, ErrorMessage = "Duration cannot exceed 50 characters")]
        [Display(Name = "Duration")]
        public string Duration { get; set; } = string.Empty; 

        [Required(ErrorMessage = "Frequency is required")]
        [StringLength(50, ErrorMessage = "Frequency cannot exceed 50 characters")]
        [Display(Name = "Frequency")]
        public string Frequency { get; set; } = string.Empty; 

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000")]
        [Display(Name = "Quantity")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Instructions is required")]
        [StringLength(500, ErrorMessage = "Instructions cannot exceed 500 characters")]
        [Display(Name = "Instructions")]

        public string Instructions { get; set; } = string.Empty;



        
        [Display(Name = "Prescription")]
        public Prescription Prescription { get; set; } = null!;

        [Display(Name = "Drug")]
        public Drug Drug { get; set; } = null!;
    }
    
}