using System.Globalization;
using System.Net;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;

namespace HCMS4.Tests.Integration
{
    /// <summary>UC-L11 — the activity report page and its printable page (TC-L11-01 … TC-L11-03).</summary>
    public class LabActivityReportPageTests : IClassFixture<HcmsWebApplicationFactory>
    {
        // A fixed past day, so other tests in this class do not change the counts.
        private static readonly DateOnly Day = new(2026, 10, 7);
        private static readonly string DayText = Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private readonly HcmsWebApplicationFactory _factory;

        public LabActivityReportPageTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient Lab() => _factory.CreateClientAs("LabTechnician", "report-lab");

        private static async Task<string> TextAsync(HttpResponseMessage response) =>
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        private static readonly SemaphoreSlim SeedLock = new(1, 1);
        private static bool _seeded;

        private async Task SeedAsync()
        {
            await SeedLock.WaitAsync();
            try
            {
                if (_seeded)
                    return;
                await _factory.WithDbAsync(async db =>
                {
                    var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                    var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                    var test = new LabTest { Name = $"R-{Guid.NewGuid():N}"[..14], Unit = "g/dL", Price = 100 };
                    var requested = Day.ToDateTime(new TimeOnly(9, 0));
                    LabRequest Make(LabRequestStatus status, int minutes) => new()
                    {
                        PatientId = patient.Id, DoctorId = doctor.Id, Status = status, RequestedAt = requested,
                        ReceivedAt = requested.AddMinutes(10),
                        ResultIssuedAt = status == LabRequestStatus.ResultIssued ? requested.AddMinutes(10 + minutes) : null,
                        CancelReason = status == LabRequestStatus.Cancelled ? "Not needed" : null,
                        Items = { new LabRequestItem { LabTest = test, Price = 100 } }
                    };
                    foreach (var minutes in new[] { 30, 60, 90, 120, 150, 180, 210 })
                        db.LabRequests.Add(Make(LabRequestStatus.ResultIssued, minutes));
                    db.LabRequests.Add(Make(LabRequestStatus.Received, 0));
                    db.LabRequests.Add(Make(LabRequestStatus.Received, 0));
                    db.LabRequests.Add(Make(LabRequestStatus.Cancelled, 0));
                    await db.SaveChangesAsync();
                });
                _seeded = true;
            }
            finally
            {
                SeedLock.Release();
            }
        }

        [Fact]
        public async Task TC_L11_01_TheDayReport_ShowsCountsAndMedian()
        {
            await SeedAsync();

            var html = await TextAsync(await Lab().GetAsync($"/Lab/ActivityReport?from={DayText}&to={DayText}"));

            Assert.Contains("id=\"total-requests\">10<", html);
            Assert.Contains("id=\"count-issued\">7<", html);
            Assert.Contains("id=\"count-cancelled\">1<", html);
            Assert.Contains("id=\"median-turnaround\">2 h 00 min<", html);
            Assert.Contains($"/Lab/ActivityReportPrint?from={DayText}&to={DayText}", html.Replace("&amp;", "&", StringComparison.Ordinal));
        }

        [Fact]
        public async Task TC_L11_02_ThePrintPage_HasTheSameSummary_AndThePrintButton()
        {
            await SeedAsync();

            var html = await TextAsync(await Lab().GetAsync($"/Lab/ActivityReportPrint?from={DayText}&to={DayText}"));

            Assert.Contains("id=\"activity-report-print\"", html);
            Assert.Contains("window.print()", html);
            Assert.Contains("@media print", html);
            Assert.Contains("id=\"total-requests\">10<", html);
            Assert.Contains("id=\"median-turnaround\">2 h 00 min<", html);
        }

        [Fact]
        public async Task TC_L11_03_ADayWithoutRequests_ShowsNoActivity()
        {
            var html = await TextAsync(await Lab().GetAsync("/Lab/ActivityReport?from=2026-09-01&to=2026-09-01"));

            Assert.Contains(LabActivityReport.NoActivityMessage, html);
            Assert.Contains("id=\"total-requests\">0<", html);
        }

        [Fact]
        public async Task AWrongPeriod_ShowsTheError()
        {
            var html = await TextAsync(await Lab().GetAsync("/Lab/ActivityReport?from=2026-10-10&to=2026-10-01"));

            Assert.Contains(LabReportService.RangeError, html);
        }

        [Fact]
        public async Task OtherRoles_CannotOpenTheReport()
        {
            foreach (var role in new[] { "Admin", "Doctor", "Patient", "Pharmacist" })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs(role).GetAsync("/Lab/ActivityReport")).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs(role).GetAsync("/Lab/ActivityReportPrint")).StatusCode);
            }
        }
    }
}
