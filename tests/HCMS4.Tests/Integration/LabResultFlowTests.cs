using System.Globalization;
using System.Net;
using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    /// <summary>UC-L10 — result entry, doctor and patient result pages, critical acknowledgement.</summary>
    public class LabResultFlowTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public LabResultFlowTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed record Setup(string DoctorUserId, string PatientUserId, int RequestId, int ItemId);

        private async Task<Setup> SeedAsync(LabRequestStatus status, decimal? criticalMax = null)
        {
            Setup? setup = null;
            await _factory.WithDbAsync(async db =>
            {
                var (doctorUser, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (patientUser, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var test = new LabTest
                {
                    Name = $"K-{Guid.NewGuid():N}"[..16], Unit = "mmol/L", ReferenceMin = 3.5m, ReferenceMax = 5.1m,
                    CriticalMax = criticalMax, Price = 3000
                };
                var item = new LabRequestItem { LabTest = test, Price = 3000 };
                var request = new LabRequest { PatientId = patient.Id, DoctorId = doctor.Id, Status = status, Items = { item } };
                db.LabRequests.Add(request);
                await db.SaveChangesAsync();
                setup = new Setup(doctorUser.Id, patientUser.Id, request.Id, item.Id);
            });
            return setup!;
        }

        private HttpClient Lab() => _factory.CreateClientAs("LabTechnician", "results-lab");

        private static Task<HttpResponseMessage> EnterAsync(HttpClient client, Setup s, string value, bool issue) =>
            FormHelper.PostFormAsync(client, $"/Lab/RequestDetails/{s.RequestId}", $"/Lab/EnterResults/{s.RequestId}",
                new Dictionary<string, string>
                {
                    [$"values[{s.ItemId.ToString(CultureInfo.InvariantCulture)}]"] = value,
                    ["issue"] = issue ? "true" : "false"
                });

        private async Task<LabRequest> ReloadAsync(int id)
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LabRequests.AsNoTracking()
                .Include(r => r.Items).ThenInclude(i => i.Result).SingleAsync(r => r.Id == id);
        }

        private static async Task<string> TextAsync(HttpResponseMessage response) =>
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        [Fact]
        public async Task TC_L10_01_TechnicianIssues_DoctorAndPatientSeeTheResult()
        {
            var s = await SeedAsync(LabRequestStatus.Received);
            var lab = Lab();

            Assert.Contains("id=\"enter-results\"", await TextAsync(await lab.GetAsync($"/Lab/RequestDetails/{s.RequestId}")));
            Assert.Contains($"name=\"values[{s.ItemId}]\"", await TextAsync(await lab.GetAsync($"/Lab/EnterResults/{s.RequestId}")));

            var issue = await EnterAsync(lab, s, "4.2", issue: true);

            Assert.Equal(HttpStatusCode.Redirect, issue.StatusCode);
            Assert.Equal(LabRequestStatus.ResultIssued, (await ReloadAsync(s.RequestId)).Status);

            var doctorPage = await TextAsync(await _factory.CreateClientAs("Doctor", s.DoctorUserId).GetAsync($"/Doctor/LabResult/{s.RequestId}"));
            Assert.Contains("id=\"lab-result-table\"", doctorPage);
            Assert.Contains("4.2", doctorPage);
            Assert.Contains("Normal", doctorPage);
            Assert.DoesNotContain("id=\"critical-alert\"", doctorPage);

            var patientPage = await TextAsync(await _factory.CreateClientAs("Patient", s.PatientUserId).GetAsync($"/Patient/LabResult/{s.RequestId}"));
            Assert.Contains("4.2", patientPage);
            Assert.Contains($"/Patient/LabResult/{s.RequestId}", await TextAsync(await _factory.CreateClientAs("Patient", s.PatientUserId).GetAsync("/Patient/LabRequests")));
        }

        [Fact]
        public async Task TC_L10_02_CriticalResult_ShowsOnTheDoctorDashboard_UntilAcknowledged()
        {
            var s = await SeedAsync(LabRequestStatus.Received, criticalMax: 6.5m);
            await EnterAsync(Lab(), s, "7.0", issue: true);
            var doctor = _factory.CreateClientAs("Doctor", s.DoctorUserId);

            var dashboard = await TextAsync(await doctor.GetAsync("/Doctor/Dashboard"));
            Assert.Contains("id=\"critical-lab-results\"", dashboard);
            Assert.Contains($"/Doctor/LabResult/{s.RequestId}", dashboard);
            var page = await TextAsync(await doctor.GetAsync($"/Doctor/LabResult/{s.RequestId}"));
            Assert.Contains("id=\"critical-alert\"", page);
            Assert.Contains("Critical", page);

            var ack = await FormHelper.PostFormAsync(doctor, $"/Doctor/LabResult/{s.RequestId}",
                $"/Doctor/AcknowledgeCriticalResult/{s.RequestId}", new Dictionary<string, string>());

            Assert.Equal(HttpStatusCode.Redirect, ack.StatusCode);
            Assert.NotNull((await ReloadAsync(s.RequestId)).CriticalAcknowledgedAt);
            Assert.Contains("id=\"critical-acknowledged\"", await TextAsync(await doctor.GetAsync($"/Doctor/LabResult/{s.RequestId}")));
            Assert.DoesNotContain("id=\"critical-lab-results\"", await TextAsync(await doctor.GetAsync("/Doctor/Dashboard")));
        }

        [Fact]
        public async Task TC_L10_03_IssueWithoutAValue_ShowsTheError_AndKeepsTheStatus()
        {
            var s = await SeedAsync(LabRequestStatus.Received);
            var lab = Lab();

            var response = await EnterAsync(lab, s, "", issue: true);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal($"/Lab/EnterResults/{s.RequestId}", response.Headers.Location?.OriginalString);
            Assert.Contains(LabResultService.IncompleteError, await TextAsync(await lab.GetAsync(response.Headers.Location!.OriginalString)));
            Assert.Equal(LabRequestStatus.Received, (await ReloadAsync(s.RequestId)).Status);
        }

        [Fact]
        public async Task Save_MovesTheRequestToInProgress()
        {
            var s = await SeedAsync(LabRequestStatus.Received);

            await EnterAsync(Lab(), s, "6", issue: false);

            var saved = await ReloadAsync(s.RequestId);
            Assert.Equal(LabRequestStatus.InProgress, saved.Status);
            Assert.True(saved.Items.Single().Result!.IsAbnormal);
        }

        [Fact]
        public async Task TC_L4_03_ACancelledRequest_BlocksResultEntry()
        {
            var s = await SeedAsync(LabRequestStatus.Cancelled);
            var lab = Lab();

            Assert.Contains("id=\"results-blocked\"", await TextAsync(await lab.GetAsync($"/Lab/EnterResults/{s.RequestId}")));
            await EnterAsync(lab, s, "4.2", issue: true);

            var saved = await ReloadAsync(s.RequestId);
            Assert.Equal(LabRequestStatus.Cancelled, saved.Status);
            Assert.Null(saved.Items.Single().Result);
        }

        [Fact]
        public async Task ResultPages_OfOthers_AreNotFound()
        {
            var s = await SeedAsync(LabRequestStatus.Received);
            await EnterAsync(Lab(), s, "4.2", issue: true);
            var other = await SeedAsync(LabRequestStatus.Requested);

            Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClientAs("Doctor", other.DoctorUserId).GetAsync($"/Doctor/LabResult/{s.RequestId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClientAs("Patient", other.PatientUserId).GetAsync($"/Patient/LabResult/{s.RequestId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Lab().GetAsync("/Lab/EnterResults/999999")).StatusCode);
        }

        [Fact]
        public async Task OnlyTheLabEntersResults()
        {
            foreach (var role in new[] { "Admin", "Doctor", "Patient", "Pharmacist" })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs(role).GetAsync("/Lab/EnterResults/1")).StatusCode);
            }
            Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs("Patient").GetAsync("/Doctor/LabResult/1")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs("Doctor").GetAsync("/Patient/LabResult/1")).StatusCode);
        }
    }
}
