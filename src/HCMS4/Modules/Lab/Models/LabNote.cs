using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Models;

namespace HCMS4.Modules.Lab.Models
{
    /// <summary>A technician's note on a request, e.g. the sample's state or a delay reason (UC-L07).</summary>
    public class LabNote
    {
        public const int TextMaxLength = 500;

        public int Id { get; set; }

        public int LabRequestId { get; set; }
        public LabRequest LabRequest { get; set; } = null!;

        public string Text { get; set; } = string.Empty;

        /// <summary>An urgent note notifies the doctor at once.</summary>
        public bool IsUrgent { get; set; }

        public DateTime CreatedAt { get; set; } = ClinicTime.Now;

        public string CreatedBy { get; set; } = string.Empty;
        public ApplicationUser Author { get; set; } = null!;
    }
}
