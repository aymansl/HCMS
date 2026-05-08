using System.ComponentModel.DataAnnotations;

namespace HCMS4.Models
{
    public class Specialization
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Specialization name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        [Display(Name = "Specialization Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Consultation fee is required")]
        [Range(0, 10000, ErrorMessage = "Consultation fee must be between 0 and 10,000")]
        [DataType(DataType.Currency)]
        [Display(Name = "Consultation Fee")]
        [DisplayFormat(DataFormatString = "{0:C2}")]
        public decimal ConsultationFee { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

       
        // edit class diagram
        public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
    }
}