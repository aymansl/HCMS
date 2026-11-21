using System.Globalization;
using HCMS4.Modules.Lab.Models;

namespace HCMS4.Modules.Lab.ViewModels
{
    /// <summary>
    /// The lab activity report of a day or a period (UC-L11). It covers the requests that reached the lab
    /// in the period (by <c>RequestedAt</c>), counted by their current status and by test type.
    /// </summary>
    public class LabActivityReport
    {
        public const string NoActivityMessage = "No activity";

        public DateOnly From { get; init; }
        public DateOnly To { get; init; }
        public int TotalRequests { get; init; }

        /// <summary>Every status, in workflow order, with its count (zero included).</summary>
        public IReadOnlyList<(LabRequestStatus Status, int Count)> ByStatus { get; init; } = Array.Empty<(LabRequestStatus, int)>();

        /// <summary>Requested tests by name, most requested first.</summary>
        public IReadOnlyList<(string Test, int Count)> ByTest { get; init; } = Array.Empty<(string, int)>();

        /// <summary>Median time from receipt to result issue of the issued requests; null when none.</summary>
        public TimeSpan? MedianTurnaround { get; init; }
        public int TurnaroundSampleSize { get; init; }

        public bool HasActivity => TotalRequests > 0;
        public bool IsSingleDay => From == To;

        public int CountOf(LabRequestStatus status) => ByStatus.FirstOrDefault(s => s.Status == status).Count;

        public static string FormatDuration(TimeSpan? value) => value is { } t
            ? (t.TotalHours >= 1
                ? string.Format(CultureInfo.InvariantCulture, "{0} h {1:D2} min", (int)t.TotalHours, t.Minutes)
                : string.Format(CultureInfo.InvariantCulture, "{0} min", (int)Math.Round(t.TotalMinutes)))
            : "-";
    }

    /// <summary>The report page: the chosen period, an error, or the report.</summary>
    public class LabActivityReportPageViewModel
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public string? Error { get; set; }
        public LabActivityReport? Report { get; set; }
    }
}
