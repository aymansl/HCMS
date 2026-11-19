using System.Text.RegularExpressions;

namespace HCMS4.Tests
{
    /// <summary>
    /// The application and its tests read the time only through IClinicClock / ClinicTime (Damascus time).
    /// Reading the machine clock made tests depend on the machine's time zone: they passed on a UTC+3 PC
    /// and failed on the UTC CI runner (DrugExpiryThresholdTests, 2026-11-19).
    /// </summary>
    public class ClockUsageTests
    {
        private static readonly Regex MachineClock = new(
            @"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b|\bTimeZoneInfo\.Local\b", RegexOptions.Compiled);

        private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HCMS4.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return dir!.FullName;
        }

        private static List<string> MachineClockUsages(string folder)
        {
            var root = RepositoryRoot();
            var skipped = new[] { "bin", "obj", "Migrations" }
                .Select(s => Path.DirectorySeparatorChar + s + Path.DirectorySeparatorChar)
                .ToArray();

            return Directory.EnumerateFiles(Path.Combine(root, folder), "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".cshtml", StringComparison.Ordinal))
                .Where(f => !skipped.Any(s => f.Contains(s, StringComparison.Ordinal)))
                .Where(f => !f.EndsWith(nameof(ClockUsageTests) + ".cs", StringComparison.Ordinal))
                .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
                .Where(x => MachineClock.IsMatch(x.Text) && !x.Text.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Select(x => $"{Path.GetRelativePath(root, x.File)}:{x.Line}: {x.Text.Trim()}")
                .ToList();
        }

        [Theory]
        [InlineData("src")]
        [InlineData("tests")]
        public void NoCode_ReadsTheMachineClock(string folder)
        {
            var usages = MachineClockUsages(folder);

            Assert.True(usages.Count == 0, "Use IClinicClock / ClinicTime instead of:\n" + string.Join("\n", usages));
        }
    }
}
