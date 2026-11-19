using HCMS4.Modules.Lab.Models;

namespace HCMS4.Modules.Lab.ViewModels
{
    /// <summary>One test on the technician's result entry page.</summary>
    public class LabResultEntryRow
    {
        public int ItemId { get; set; }
        public LabTest Test { get; set; } = null!;
        public bool IsQualitative { get; set; }
        public string? Value { get; set; }
        public bool IsAbnormal { get; set; }
        public bool IsCritical { get; set; }
    }

    /// <summary>The technician's result entry page (UC-L10).</summary>
    public class LabResultEntryViewModel
    {
        public LabRequest Request { get; set; } = null!;
        public List<LabResultEntryRow> Rows { get; set; } = new();
    }
}
