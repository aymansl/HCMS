using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class PharmacistEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [DataType(DataType.PhoneNumber)]
        [Display(Name = "Phone Number")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        [RegularExpression(@"^[0-9+\-\s]+$", ErrorMessage = "Please enter a valid phone number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Qualifications")]
        [StringLength(500, ErrorMessage = "Qualifications cannot exceed 500 characters")]
        [DataType(DataType.MultilineText)]
        public string? Qualifications { get; set; }

        [Display(Name = "Contact Information")]
        [StringLength(200, ErrorMessage = "Contact information cannot exceed 200 characters")]
        public string? ContactInfo { get; set; }

        [Display(Name = "Shift")]
        public string? Shift { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

    }
}