using System.Text.RegularExpressions;

namespace HCMS4.Tests
{
    // The user interface is in English (decided 2026-11-19). Views and code must not contain Arabic text or RTL markup.
    public class UiLanguageTests
    {
        private static readonly Regex Arabic = new("[\\u0600-\\u06FF]", RegexOptions.Compiled);

        // Located from this file's path, so it works wherever the test binaries are built.
        private static string SourceRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HCMS4.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "src", "HCMS4");
        }

        private static IEnumerable<string> SourceFiles()
        {
            var root = SourceRoot();
            var skipped = new[] { "bin", "obj", "Migrations", Path.Combine("wwwroot", "lib") }
                .Select(s => Path.DirectorySeparatorChar + s + Path.DirectorySeparatorChar)
                .ToArray();

            return Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cshtml", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".js", StringComparison.Ordinal))
                .Where(f => !skipped.Any(s => f.Contains(s)));
        }

        [Fact]
        public void ViewsAndCode_HaveNoArabicText()
        {
            var offenders = SourceFiles()
                .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
                .Where(x => Arabic.IsMatch(x.Text))
                .Select(x => $"{Path.GetRelativePath(SourceRoot(), x.File)}:{x.Line}")
                .ToList();

            Assert.True(offenders.Count == 0, "Arabic text found:\n" + string.Join("\n", offenders));
        }

        [Fact]
        public void Views_HaveNoRightToLeftMarkup()
        {
            var offenders = SourceFiles()
                .Where(f => f.EndsWith(".cshtml", StringComparison.Ordinal))
                .Where(f => File.ReadAllText(f).Contains("dir=\"rtl\"") || File.ReadAllText(f).Contains("lang=\"ar\""))
                .Select(f => Path.GetRelativePath(SourceRoot(), f))
                .ToList();

            Assert.Empty(offenders);
        }
    }
}
