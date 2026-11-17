using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class Pharmacist
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } 

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [StringLength(500)]
        [Display(Name = "Qualifications")]
        public string? Qualifications { get; set; }

        [StringLength(20)]
        [Display(Name = "Contact Number")]
        public string? ContactInfo { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [DataType(DataType.DateTime)]
        [Display(Name = "Last Login")]
        public DateTime? LastLoginAt { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Shift")]
        public string? Shift { get; set; }

       

        [Display(Name = "Full Name")]
        public string FullName => User?.FullName ?? "Unknown";
    }
}