using HCMS4.Models;
using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class DoctorRegisterViewModel
    {
        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, ErrorMessage = "Password must be at least {2} characters long", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Specialization")]
        [Required(ErrorMessage = "Please select a specialization")]
        public int? SpecializationId { get; set; }
        public string? SpecializationName { get; set; }

        [StringLength(500, ErrorMessage = "Qualifications cannot exceed 500 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Qualifications")]
        public string? Qualifications { get; set; }

        [Phone(ErrorMessage = "Invalid phone number format")]
        [StringLength(50, ErrorMessage = "Contact info cannot exceed 50 characters")]
        [Display(Name = "Contact Information")]
        public string? ContactInfo { get; set; }

    }
}
