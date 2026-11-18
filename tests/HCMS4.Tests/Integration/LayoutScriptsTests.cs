using System.Net;
using System.Text.RegularExpressions;

namespace HCMS4.Tests.Integration
{
    // Regression tests for issues #21/#22: jQuery and the validation plugins were loaded several times.
    public class LayoutScriptsTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public LayoutScriptsTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static List<string> ScriptSources(string html) =>
            Regex.Matches(html, "<script[^>]*src=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();

        [Fact]
        public async Task AuthenticatedPageWithValidation_LoadsJQueryAndPluginsOnce()
        {
            string userId = string.Empty;
            await _factory.WithDbAsync(async db =>
            {
                var (user, _) = await TestDbContextFactory.CreateTestPatientAsync(db);
                userId = user.Id;
            });
            var client = _factory.CreateClientAs("Patient", userId);

            // This page also renders _ValidationScriptsPartial.
            var html = await client.GetStringAsync("/Patient/BookAppointment");
            var sources = ScriptSources(html);

            Assert.Single(sources, s => Regex.IsMatch(s, @"jquery(-\d[\d.]*)?(\.min)?\.js$"));
            Assert.Single(sources, s => s.Contains("jquery.validate.min.js"));
            Assert.Single(sources, s => s.Contains("jquery.validate.unobtrusive.min.js"));

            foreach (var src in sources.Where(s => s.StartsWith('/')))
            {
                var response = await client.GetAsync(src.Split('?')[0]);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{src} returned {(int)response.StatusCode}");
            }
        }

        [Fact]
        public async Task OrphanedDashboardStylesheet_IsRemoved()
        {
            var client = _factory.CreateClientAs("Admin");

            var response = await client.GetAsync("/assets/css/dashboard.css");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
