using HCMS4.Modules.Lab.Models;

namespace HCMS4.Modules.Lab.ViewModels
{
    /// <summary>The issued results search (UC-L08). Empty fields do not filter.</summary>
    public class IssuedResultFilter
    {
        public string? PatientName { get; set; }
        public int? RequestId { get; set; }
        public int? TestId { get; set; }

        /// <summary>The day the result was issued (Damascus time).</summary>
        public DateOnly? Date { get; set; }

        public bool IsEmpty => string.IsNullOrWhiteSpace(PatientName) && RequestId == null && TestId == null && Date == null;
    }

    /// <summary>The issued results page (UC-L08).</summary>
    public class IssuedResultsViewModel
    {
        public const string NoMatchMessage = "No matching results.";

        public IssuedResultFilter Filter { get; set; } = new();
        public List<LabRequest> Results { get; set; } = new();
        public List<LabTest> Tests { get; set; } = new();

        /// <summary>The search had no criteria, so today's results are shown.</summary>
        public bool ShowingToday => Filter.IsEmpty;
    }

    /// <summary>The printable result report (browser "Save as PDF").</summary>
    public class LabResultReportViewModel
    {
        public LabRequest Request { get; set; } = null!;
        public string? IssuedByName { get; set; }
        public DateTime PrintedAt { get; set; }
    }
}
