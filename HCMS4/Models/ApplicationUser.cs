using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace HCMS4.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        public Doctor? Doctor { get; set; }
        public Patient? Patient { get; set; }

        public Pharmacist? Pharmacist { get; set; }
    }
}
