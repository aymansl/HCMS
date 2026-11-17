using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class PharmacistEditProfileViewModel
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [DataType(DataType.PhoneNumber)]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Qualifications")]
        public string? Qualifications { get; set; }

        [Display(Name = "Contact Info")]
        public string? ContactInfo { get; set; }

        [Display(Name = "Shift")]
        public string? Shift { get; set; }
    }
}