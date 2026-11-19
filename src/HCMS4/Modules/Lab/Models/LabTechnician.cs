using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Models;

namespace HCMS4.Modules.Lab.Models
{
    /// <summary>The profile of a user in the LabTechnician role (UC-L01). A suspended technician cannot sign in.</summary>
    public class LabTechnician
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public string? Qualifications { get; set; }
        public string? ContactInfo { get; set; }

        public bool IsActive { get; set; } = true;
        /// <summary>When the technician was last suspended (Damascus time); null while active.</summary>
        public DateTime? DeactivatedAt { get; set; }

        public DateTime CreatedAt { get; set; } = ClinicTime.Now;

        public string FullName => User?.FullName ?? "Unknown";
    }
}
