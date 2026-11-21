using System.Net;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;

namespace HCMS4.Tests.Integration
{
    /// <summary>UC-L09 — the patient history page (search, history, trend, access).</summary>
    public class LabPatientHistoryPageTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public LabPatientHistoryPageTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient Lab() => _factory.CreateClientAs("LabTechnician", "history-lab");

        private static async Task<string> TextAsync(HttpResponseMessage response) =>
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        private async Task<(int PatientId, string LastName, int TestId, int NewestId, int OldestId)> SeedAsync(bool withResults = true)
        {
            (int, string, int, int, int) result = default;
            await _factory.WithDbAsync(async db =>
            {
                var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (patientUser, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                patientUser.LastName = $"Hist{Guid.NewGuid():N}"[..14];
                var test = new LabTest { Name = $"G-{Guid.NewGuid():N}"[..14], Unit = "mg/dL", ReferenceMin = 70, ReferenceMax = 110, Price = 100 };
                LabRequest Issued(DateTime at, decimal value, bool abnormal) => new()
                {
                    PatientId = patient.Id, DoctorId = doctor.Id, Status = LabRequestStatus.ResultIssued, ResultIssuedAt = at,
                    Items = { new LabRequestItem { LabTest = test, Price = 100, Result = new LabResult { NumericValue = value, IsAbnormal = abnormal } } }
                };
                var oldest = Issued(ClinicTime.Now.AddDays(-10), 95, false);
                var newest = Issued(ClinicTime.Now.AddDays(-1), 160, true);
                if (withResults)
                    db.LabRequests.AddRange(oldest, newest);
                else
                    db.LabTests.Add(test);
                await db.SaveChangesAsync();
                result = (patient.Id, patientUser.LastName, test.Id, newest.Id, oldest.Id);
            });
            return result;
        }

        [Fact]
        public async Task Search_ThenSelect_ShowsTheHistoryNewestFirst_WithFlags()
        {
            var s = await SeedAsync();
            var lab = Lab();

            var search = await TextAsync(await lab.GetAsync($"/Lab/PatientHistory?term={s.LastName.ToLowerInvariant()}"));
            Assert.Contains("id=\"patient-matches\"", search);
            Assert.Contains($"/Lab/PatientHistory?patientId={s.PatientId}", search);

            var history = await TextAsync(await lab.GetAsync($"/Lab/PatientHistory?patientId={s.PatientId}"));
            Assert.True(history.IndexOf($"data-request-id=\"{s.NewestId}\"", StringComparison.Ordinal)
                        < history.IndexOf($"data-request-id=\"{s.OldestId}\"", StringComparison.Ordinal), "Newest must come first.");
            Assert.Contains("Abnormal", history);
        }

        [Fact]
        public async Task TrendPage_ShowsTheChartAndTheValues()
        {
            var s = await SeedAsync();

            var html = await TextAsync(await Lab().GetAsync($"/Lab/PatientHistory?patientId={s.PatientId}&testId={s.TestId}"));

            Assert.Contains("id=\"trend-table\"", html);
            Assert.Contains("<polyline", html);
            Assert.Contains("160", html);
        }

        [Fact]
        public async Task NoResults_ShowsNoTestHistory_AndUnknownSearch_ShowsPatientNotFound()
        {
            var s = await SeedAsync(withResults: false);

            Assert.Contains(PatientHistoryViewModel.NoHistoryMessage, await TextAsync(await Lab().GetAsync($"/Lab/PatientHistory?patientId={s.PatientId}")));
            Assert.Contains(PatientHistoryViewModel.PatientNotFoundMessage, await TextAsync(await Lab().GetAsync("/Lab/PatientHistory?term=zz-nobody-zz")));
        }

        [Fact]
        public async Task OtherRoles_CannotOpenThePage()
        {
            foreach (var role in new[] { "Admin", "Doctor", "Patient", "Pharmacist" })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs(role).GetAsync("/Lab/PatientHistory")).StatusCode);
            }
        }
    }
}
