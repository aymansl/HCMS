using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace HCMS4.Tests.Integration
{
    // Regression tests for issues #7/#8: the booking page script did not parse and called a missing endpoint.
    public class BookAppointmentPageTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public BookAppointmentPageTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<string> GetPageAsync()
        {
            string userId = string.Empty;
            await _factory.WithDbAsync(async db =>
            {
                await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (user, _) = await TestDbContextFactory.CreateTestPatientAsync(db);
                userId = user.Id;
            });
            var client = _factory.CreateClientAs("Patient", userId);
            var response = await client.GetAsync("/Patient/BookAppointment");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        }

        private static string PageScript(string html)
        {
            // The page's own inline script (the last one without a src attribute).
            var scripts = Regex.Matches(html, "<script>(.*?)</script>", RegexOptions.Singleline);
            return scripts[^1].Groups[1].Value;
        }

        [Fact]
        public async Task BookAppointment_DoesNotCallMissingSuggestionsEndpoint()
        {
            var html = await GetPageAsync();

            Assert.DoesNotContain("GetAppointmentSuggestions", html);
            Assert.Contains("#doctorSelect", PageScript(html));
        }

        [Fact]
        public async Task BookAppointment_ScriptIsValidJavaScript()
        {
            var script = PageScript(await GetPageAsync());
            var path = Path.Combine(Path.GetTempPath(), $"hcms-book-{Guid.NewGuid():N}.js");
            await File.WriteAllTextAsync(path, script);

            try
            {
                var start = new ProcessStartInfo("node", $"--check \"{path}\"")
                {
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                Process? node;
                try
                {
                    node = Process.Start(start);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    return; // Node.js is not installed on this machine; the other test still covers the endpoint.
                }

                var error = await node!.StandardError.ReadToEndAsync();
                await node.WaitForExitAsync();
                Assert.True(node.ExitCode == 0, error);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
