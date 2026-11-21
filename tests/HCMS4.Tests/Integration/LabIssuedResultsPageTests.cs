using System.Net;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;

namespace HCMS4.Tests.Integration
{
    /// <summary>UC-L08 — the issued results page and the printable report.</summary>
    public class LabIssuedResultsPageTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public LabIssuedResultsPageTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient Lab() => _factory.CreateClientAs("LabTechnician", "issued-lab");

        private static async Task<string> TextAsync(HttpResponseMessage response) =>
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        private async Task<(string LastName, int TodayId, int OlderId)> SeedAsync()
        {
            (string, int, int) result = default;
            await _factory.WithDbAsync(async db =>
            {
                var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (patientUser, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                patientUser.LastName = $"Iss{Guid.NewGuid():N}"[..14];
                var test = new LabTest { Name = $"I-{Guid.NewGuid():N}"[..14], Unit = "g/dL", ReferenceMin = 1, ReferenceMax = 2, Price = 100 };
                LabRequest Issued(DateTime at, bool critical) => new()
                {
                    PatientId = patient.Id, DoctorId = doctor.Id, Status = LabRequestStatus.ResultIssued, ResultIssuedAt = at, HasCriticalResult = critical,
                    Items = { new LabRequestItem { LabTest = test, Price = 100, Result = new LabResult { NumericValue = critical ? 9 : 1.5m, IsAbnormal = critical, IsCritical = critical } } }
                };
                var today = Issued(ClinicTime.Now.AddMinutes(-5), critical: true);
                var older = Issued(ClinicTime.Now.AddDays(-3), critical: false);
                db.LabRequests.AddRange(today, older);
                await db.SaveChangesAsync();
                result = (patientUser.LastName, today.Id, older.Id);
            });
            return result;
        }

        [Fact]
        public async Task TC_L8_01_SearchByName_ShowsBothResults_WithPrintLinks()
        {
            var s = await SeedAsync();

            var html = await TextAsync(await Lab().GetAsync($"/Lab/IssuedResults?PatientName={s.LastName}"));

            Assert.Contains($"/Lab/ResultReport/{s.TodayId}", html);
            Assert.Contains($"/Lab/ResultReport/{s.OlderId}", html);
            Assert.Contains("Critical", html);
        }

        [Fact]
        public async Task TC_L8_02_NoCriteria_ShowsTodaysResultsOnly()
        {
            var s = await SeedAsync();

            var html = await TextAsync(await Lab().GetAsync("/Lab/IssuedResults"));

            Assert.Contains("id=\"showing-today\"", html);
            Assert.Contains($"/Lab/ResultReport/{s.TodayId}", html);
            Assert.DoesNotContain($"/Lab/ResultReport/{s.OlderId}\"", html);
        }

        [Fact]
        public async Task TC_L8_03_UnknownRequestNumber_ShowsNoMatchingResults()
        {
            var html = await TextAsync(await Lab().GetAsync("/Lab/IssuedResults?RequestId=999999"));

            Assert.Contains(IssuedResultsViewModel.NoMatchMessage, html);
        }

        [Fact]
        public async Task Report_IsAPrintablePage_WithThePrintButton()
        {
            var s = await SeedAsync();

            var response = await Lab().GetAsync($"/Lab/ResultReport/{s.TodayId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await TextAsync(response);
            Assert.Contains("id=\"result-report\"", html);
            Assert.Contains("window.print()", html);
            Assert.Contains("@media print", html);
            Assert.Contains("This report contains a critical value.", html);
            Assert.Equal(HttpStatusCode.NotFound, (await Lab().GetAsync("/Lab/ResultReport/999999")).StatusCode);
        }

        [Fact]
        public async Task OtherRoles_CannotSearchOrPrint()
        {
            foreach (var role in new[] { "Admin", "Doctor", "Patient", "Pharmacist" })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs(role).GetAsync("/Lab/IssuedResults")).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs(role).GetAsync("/Lab/ResultReport/1")).StatusCode);
            }
        }
    }
}
