using System.Globalization;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Records.Models;

namespace HCMS4.Modules.Lab.ViewModels
{
    /// <summary>The patient test history page (UC-L09).</summary>
    public class PatientHistoryViewModel
    {
        public const string PatientNotFoundMessage = "Patient not found.";
        public const string NoHistoryMessage = "No test history.";

        public string? Term { get; set; }

        /// <summary>Patients matching <see cref="Term"/>; empty when nothing matched.</summary>
        public List<Patient> Matches { get; set; } = new();

        /// <summary>True when a search was made and found no patient (UC-L09 exception).</summary>
        public bool NotFound { get; set; }

        public Patient? Patient { get; set; }

        /// <summary>The patient's issued requests, newest first.</summary>
        public List<LabRequest> History { get; set; } = new();

        /// <summary>The tests that appear in the history, for the trend selector.</summary>
        public List<LabTest> Tests { get; set; } = new();

        public LabTest? TrendTest { get; set; }
        public List<LabTrendPoint> Trend { get; set; } = new();
        public LabTrendChart? Chart { get; set; }
    }

    /// <summary>One value of a test over time (UC-L09 alternative flow).</summary>
    public record LabTrendPoint(int RequestId, DateTime IssuedAt, LabResult Result);

    /// <summary>A small hand-drawn SVG line chart of numeric values with the reference band.</summary>
    public class LabTrendChart
    {
        public const int Width = 600, Height = 180, Pad = 24;

        public string Points { get; init; } = string.Empty;
        public List<(string X, string Y, bool Abnormal)> Dots { get; init; } = new();
        public string? BandY { get; init; }
        public string? BandHeight { get; init; }

        private static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>Null when fewer than one numeric value exists.</summary>
        public static LabTrendChart? Build(LabTest test, IReadOnlyList<LabTrendPoint> points)
        {
            var numeric = points.Where(p => p.Result.NumericValue.HasValue).ToList();
            if (numeric.Count == 0)
                return null;

            var values = numeric.Select(p => (double)p.Result.NumericValue!.Value).ToList();
            if (test.ReferenceMin.HasValue) values.Add((double)test.ReferenceMin.Value);
            if (test.ReferenceMax.HasValue) values.Add((double)test.ReferenceMax.Value);
            var min = values.Min();
            var max = values.Max();
            if (Math.Abs(max - min) < 1e-9) { min -= 1; max += 1; }

            double X(int i) => numeric.Count == 1 ? Width / 2.0 : Pad + i * (Width - 2.0 * Pad) / (numeric.Count - 1);
            double Y(double v) => Height - Pad - (v - min) * (Height - 2.0 * Pad) / (max - min);

            var dots = numeric.Select((p, i) => (F(X(i)), F(Y((double)p.Result.NumericValue!.Value)), p.Result.IsAbnormal)).ToList();
            string? bandY = null, bandHeight = null;
            if (test.ReferenceMin.HasValue && test.ReferenceMax.HasValue)
            {
                var top = Y((double)test.ReferenceMax.Value);
                bandY = F(top);
                bandHeight = F(Y((double)test.ReferenceMin.Value) - top);
            }

            return new LabTrendChart
            {
                Points = string.Join(" ", dots.Select(d => $"{d.Item1},{d.Item2}")),
                Dots = dots,
                BandY = bandY,
                BandHeight = bandHeight
            };
        }
    }
}
