using System.Net;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;

namespace HCMS4.Tests.Integration
{
    /// <summary>UC-L02 — the incoming requests page (TC-L2-01, TC-L2-02, filters, access).</summary>
    public class LabIncomingRequestsTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public LabIncomingRequestsTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(int Requested, int Received, int Returned, string TestName)> SeedAsync()
        {
            (int, int, int, string) ids = default;
            await _factory.WithDbAsync(async db =>
            {
                var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var test = new LabTest { Name = $"L2-{Guid.NewGuid():N}"[..16], Unit = "g/dL", Price = 100 };
                var now = ClinicTime.Now;
                LabRequest Make(LabRequestStatus status, DateTime at) => new()
                {
                    PatientId = patient.Id, DoctorId = doctor.Id, Status = status, RequestedAt = at, StatusChangedAt = at,
                    Items = { new LabRequestItem { LabTest = test, Price = 100 } }
                };
                var requested = Make(LabRequestStatus.Requested, now.AddMinutes(-10));
                var received = Make(LabRequestStatus.Received, now.AddMinutes(-20));
                var returned = Make(LabRequestStatus.ReturnedToDoctor, now.AddMinutes(-30));
                db.LabRequests.AddRange(requested, received, returned);
                await db.SaveChangesAsync();
                ids = (requested.Id, received.Id, returned.Id, test.Name);
            });
            return ids;
        }

        private HttpClient Technician() => _factory.CreateClientAs("LabTechnician", "incoming-lab");

        private static async Task<string> TextAsync(HttpResponseMessage response) =>
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        [Fact]
        public async Task TC_L2_01_Page_ListsIncomingRequests_OldestFirst_WithLinksToDetails()
        {
            var s = await SeedAsync();

            var response = await Technician().GetAsync("/Lab/Requests");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await TextAsync(response);
            var receivedAt = html.IndexOf($"/Lab/RequestDetails/{s.Received}\"", StringComparison.Ordinal);
            var requestedAt = html.IndexOf($"/Lab/RequestDetails/{s.Requested}\"", StringComparison.Ordinal);
            Assert.True(receivedAt > 0 && requestedAt > receivedAt, "The older (received) request must come first.");
            Assert.DoesNotContain($"/Lab/RequestDetails/{s.Returned}\"", html);
            Assert.Contains(s.TestName, html);
        }

        [Fact]
        public async Task TC_L2_02_FilterByStatus_ShowsOnlyRequested()
        {
            var s = await SeedAsync();

            var html = await TextAsync(await Technician().GetAsync("/Lab/Requests?Status=Requested"));

            Assert.Contains($"/Lab/RequestDetails/{s.Requested}\"", html);
            Assert.DoesNotContain($"/Lab/RequestDetails/{s.Received}\"", html);
            Assert.Contains("<option value=\"Requested\" selected=\"selected\">", html);
        }

        [Fact]
        public async Task FilterWithoutMatches_ShowsTheNoMatchMessage()
        {
            await SeedAsync();

            var html = await TextAsync(await Technician().GetAsync("/Lab/Requests?PatientName=nobody-at-all&Date=2000-01-01"));

            Assert.Contains(LabIncomingRequestsViewModel.NoMatchMessage, html);
            Assert.Contains("value=\"2000-01-01\"", html);
        }

        [Fact]
        public async Task Dashboard_And_Menu_LinkToThePage()
        {
            var html = await TextAsync(await Technician().GetAsync("/Lab/Dashboard"));

            Assert.Contains("href=\"/Lab/Requests\"", html);
        }

        [Fact]
        public async Task OtherRoles_CannotOpenThePage()
        {
            foreach (var role in new[] { "Admin", "Doctor", "Patient", "Pharmacist" })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs(role).GetAsync("/Lab/Requests")).StatusCode);
            }
        }
    }

    /// <summary>TC-L2-03 needs a database with no incoming request, so it has its own factory.</summary>
    public class LabIncomingRequestsEmptyTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public LabIncomingRequestsEmptyTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TC_L2_03_NoRequests_ShowsTheNoRequestsMessage()
        {
            var response = await _factory.CreateClientAs("LabTechnician", "empty-lab").GetAsync("/Lab/Requests");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(LabIncomingRequestsViewModel.NoRequestsMessage, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        }
    }
}
