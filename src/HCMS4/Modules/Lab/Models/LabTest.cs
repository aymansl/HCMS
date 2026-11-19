using HCMS4.Shared.Common;

namespace HCMS4.Modules.Lab.Models
{
    /// <summary>
    /// A test in the lab catalogue (UC-L06): name, unit, reference range and price.
    /// A test used by past requests is deactivated instead of deleted.
    /// </summary>
    public class LabTest
    {
        public int Id { get; set; }

        /// <summary>Unique across the catalogue (active and inactive tests).</summary>
        public string Name { get; set; } = string.Empty;

        public string Unit { get; set; } = string.Empty;

        /// <summary>Lower bound of the normal range; null for qualitative tests.</summary>
        public decimal? ReferenceMin { get; set; }

        /// <summary>Upper bound of the normal range; null for qualitative tests.</summary>
        public decimal? ReferenceMax { get; set; }

        /// <summary>A value at or below this is critical (UC-L10); optional.</summary>
        public decimal? CriticalMin { get; set; }

        /// <summary>A value at or above this is critical (UC-L10); optional.</summary>
        public decimal? CriticalMax { get; set; }

        public decimal Price { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = ClinicTime.Now;
        public DateTime UpdatedAt { get; set; } = ClinicTime.Now;

        public string ReferenceRange => (ReferenceMin, ReferenceMax) switch
        {
            (null, null) => "-",
            (decimal min, null) => $"≥ {min:0.####}",
            (null, decimal max) => $"≤ {max:0.####}",
            (decimal min, decimal max) => $"{min:0.####} – {max:0.####}"
        };

        public string CriticalLimits => (CriticalMin, CriticalMax) switch
        {
            (null, null) => "-",
            (decimal min, null) => $"≤ {min:0.####}",
            (null, decimal max) => $"≥ {max:0.####}",
            (decimal min, decimal max) => $"≤ {min:0.####} or ≥ {max:0.####}"
        };
    }
}
