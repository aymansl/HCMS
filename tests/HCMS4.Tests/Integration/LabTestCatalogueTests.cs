using System.Net;
using HCMS4.Data;
using HCMS4.Modules.Lab.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    /// <summary>UC-L06 — the catalogue pages (TC-L6-01, TC-L6-02, delete, access).</summary>
    public class LabTestCatalogueTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public LabTestCatalogueTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient Technician() => _factory.CreateClientAs("LabTechnician");

        private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

        private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string name, string min, string max, string price) =>
            FormHelper.PostFormAsync(client, "/Lab/CreateTest", "/Lab/CreateTest", new Dictionary<string, string>
            {
                ["Name"] = name, ["Unit"] = "10^9/L", ["ReferenceMin"] = min, ["ReferenceMax"] = max, ["Price"] = price
            });

        private async Task<T> InDbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private static async Task<string> TextAsync(HttpResponseMessage response) =>
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        [Fact]
        public async Task TC_L6_01_AddCbc_ShowsItInTheCatalogue()
        {
            var client = Technician();
            var name = UniqueName("CBC");

            var post = await CreateAsync(client, name, "4", "11", "5000");

            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
            Assert.Equal("/Lab/Tests", post.Headers.Location?.OriginalString);
            var html = await TextAsync(await client.GetAsync("/Lab/Tests"));
            Assert.Contains(name, html);
            Assert.Contains("4 – 11", html);
            Assert.Contains("5,000.00", html);
        }

        [Fact]
        public async Task TC_L6_02_MinGreaterThanMax_ShowsTheError_AndSavesNothing()
        {
            var name = UniqueName("Bad");

            var response = await CreateAsync(Technician(), name, "11", "4", "5000");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(LabTestFormViewModel.RangeError, await TextAsync(response));
            Assert.False(await InDbAsync(db => db.LabTests.AnyAsync(t => t.Name == name)));
        }

        [Fact]
        public async Task DuplicateName_ShowsTheError()
        {
            var client = Technician();
            var name = UniqueName("Dup");
            await CreateAsync(client, name, "", "", "100");

            var response = await CreateAsync(client, name.ToLowerInvariant(), "", "", "100");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("A test with this name already exists.", await TextAsync(response));
        }

        [Fact]
        public async Task Edit_ThenDelete_AnUnusedTest()
        {
            var client = Technician();
            var name = UniqueName("Edit");
            await CreateAsync(client, name, "1", "2", "100");
            var id = await InDbAsync(db => db.LabTests.Where(t => t.Name == name).Select(t => t.Id).SingleAsync());

            var edit = await FormHelper.PostFormAsync(client, $"/Lab/EditTest/{id}", $"/Lab/EditTest/{id}", new Dictionary<string, string>
            {
                ["Name"] = name, ["Unit"] = "mmol/L", ["ReferenceMin"] = "1", ["ReferenceMax"] = "3", ["Price"] = "250", ["IsActive"] = "true"
            });
            Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
            var edited = await InDbAsync(db => db.LabTests.AsNoTracking().SingleAsync(t => t.Id == id));
            Assert.Equal("mmol/L", edited.Unit);
            Assert.Equal(250, edited.Price);

            var delete = await FormHelper.PostFormAsync(client, "/Lab/Tests", $"/Lab/DeleteTest/{id}", new Dictionary<string, string>());
            Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);
            Assert.False(await InDbAsync(db => db.LabTests.AnyAsync(t => t.Id == id)));
        }

        [Fact]
        public async Task EditTest_UnknownId_IsNotFound()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Technician().GetAsync("/Lab/EditTest/999999")).StatusCode);
        }

        [Fact]
        public async Task Dashboard_LinksToTheCatalogue()
        {
            var html = await TextAsync(await Technician().GetAsync("/Lab/Dashboard"));

            Assert.Contains("id=\"lab-active-tests\"", html);
            Assert.Contains("href=\"/Lab/Tests\"", html);
        }

        [Fact]
        public async Task OtherRoles_CannotOpenTheCatalogue()
        {
            foreach (var role in new[] { "Admin", "Doctor", "Patient", "Pharmacist" })
            {
                var response = await _factory.CreateClientAs(role).GetAsync("/Lab/Tests");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
    }
}
