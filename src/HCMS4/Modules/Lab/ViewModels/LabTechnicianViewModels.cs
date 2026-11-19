using System.ComponentModel.DataAnnotations;

namespace HCMS4.Modules.Lab.ViewModels
{
    /// <summary>The admin's form for a new lab technician account.</summary>
    public class CreateLabTechnicianViewModel
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "The password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Qualifications { get; set; }

        [StringLength(20)]
        [Display(Name = "Contact Number")]
        public string? ContactInfo { get; set; }
    }

    /// <summary>The lab dashboard (UC-L01 step 5). The full incoming list comes with L2.</summary>
    public class LabDashboardViewModel
    {
        public string TechnicianName { get; set; } = string.Empty;

        /// <summary>Requests waiting for receipt (UC-L03), oldest first.</summary>
        public int NewRequestCount { get; set; }
        public List<HCMS4.Modules.Lab.Models.LabRequest> NewRequests { get; set; } = new();

        /// <summary>Active tests in the catalogue (UC-L06).</summary>
        public int ActiveTestCount { get; set; }
    }
}
