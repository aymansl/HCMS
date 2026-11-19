using HCMS4.Shared.Common;

namespace HCMS4.Modules.Lab.Models
{
    /// <summary>The result of one test in a lab request (UC-L10).</summary>
    public class LabResult
    {
        public int Id { get; set; }

        public int LabRequestItemId { get; set; }
        public LabRequestItem LabRequestItem { get; set; } = null!;

        /// <summary>The value of a quantitative test, in the test's unit.</summary>
        public decimal? NumericValue { get; set; }

        /// <summary>The value of a qualitative test (no reference range), e.g. "Positive".</summary>
        public string? TextValue { get; set; }

        /// <summary>Outside the reference range.</summary>
        public bool IsAbnormal { get; set; }

        /// <summary>At or beyond a critical limit: the doctor is alerted urgently.</summary>
        public bool IsCritical { get; set; }

        public DateTime EnteredAt { get; set; } = ClinicTime.Now;
        public string? EnteredBy { get; set; }

        public string DisplayValue => NumericValue.HasValue ? NumericValue.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) : TextValue ?? "-";
    }

    /// <summary>How a value compares with the catalogue limits of its test.</summary>
    public static class LabResultRules
    {
        /// <summary>A test with no reference or critical limits takes a text value and is never flagged.</summary>
        public static bool IsQualitative(LabTest test) =>
            test.ReferenceMin == null && test.ReferenceMax == null && test.CriticalMin == null && test.CriticalMax == null;

        public static bool IsAbnormal(LabTest test, decimal value) =>
            (test.ReferenceMin.HasValue && value < test.ReferenceMin.Value) ||
            (test.ReferenceMax.HasValue && value > test.ReferenceMax.Value);

        public static bool IsCritical(LabTest test, decimal value) =>
            (test.CriticalMin.HasValue && value <= test.CriticalMin.Value) ||
            (test.CriticalMax.HasValue && value >= test.CriticalMax.Value);
    }
}
