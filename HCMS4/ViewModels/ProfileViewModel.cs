using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class ProfileViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Roles")]
        public List<string> Roles { get; set; } = new List<string>();

        [Display(Name = "Has Doctor Profile")]
        public bool HasDoctorProfile { get; set; }

        [Display(Name = "Has Patient Profile")]
        public bool HasPatientProfile { get; set; }

        // Doctor-specific properties
        [Display(Name = "Specialization")]
        public string? Specialization { get; set; }

        [Display(Name = "Qualifications")]
        public string? Qualifications { get; set; }

        [Display(Name = "Contact Information")]
        public string? ContactInfo { get; set; }

        // Patient-specific properties
        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Display(Name = "Emergency Contact")]
        public string? EmergencyContact { get; set; }

        [Display(Name = "Member Since")]
        public DateTime CreatedAt { get; set; }
    }
}
