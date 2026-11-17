using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class PharmacistProfileViewModel
    {
        public int Id { get; set; }

        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        public string Email { get; set; }

        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Qualifications")]
        public string Qualifications { get; set; }

        [Display(Name = "Contact Info")]
        public string ContactInfo { get; set; }

        [Display(Name = "Shift")]
        public string Shift { get; set; }

        [Display(Name = "Account Created")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Last Login")]
        public DateTime? LastLoginAt { get; set; }

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";
    }
}