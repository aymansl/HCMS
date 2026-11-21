using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Common;
using Microsoft.Extensions.Time.Testing;

namespace HCMS4.Tests
{
    /// <summary>UC-L11 — Daily lab activity report (TC-L11-01 … TC-L11-03).</summary>
    public class LabReportServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();
        // Thursday 2026-11-19, 12:00 Damascus.
        private readonly ClinicClock _clock = new(new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero)), ClinicTime.TimeZone);
        private static readonly DateOnly Day = new(2026, 11, 18);
        private Doctor _doctor = null!;
        private Patient _patient = null!;
        private LabTest _cbc = null!;
        private LabTest _glucose = null!;

        public LabReportServiceTests()
        {
            SeedAsync().GetAwaiter().GetResult();
        }

        public void Dispose() => _db.Dispose();

        private LabReportService Service => new(_db, _clock);

        private async Task SeedAsync()
        {
            (_, _doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            (_, _patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            _cbc = new LabTest { Name = "CBC", Unit = "10^9/L", Price = 5000 };
            _glucose = new LabTest { Name = "Glucose", Unit = "mg/dL", Price = 2000 };
            _db.LabTests.AddRange(_cbc, _glucose);
            await _db.SaveChangesAsync();
        }

        private void Add(DateOnly day, LabRequestStatus status, int turnaroundMinutes = 0, params LabTest[] tests)
        {
            var requested = day.ToDateTime(new TimeOnly(9, 0));
            var received = requested.AddMinutes(15);
            _db.LabRequests.Add(new LabRequest
            {
                PatientId = _patient.Id, DoctorId = _doctor.Id, Status = status, RequestedAt = requested,
                ReceivedAt = status is LabRequestStatus.Requested or LabRequestStatus.ReturnedToDoctor ? null : received,
                ResultIssuedAt = status == LabRequestStatus.ResultIssued ? received.AddMinutes(turnaroundMinutes) : null,
                CancelReason = status == LabRequestStatus.Cancelled ? "Not needed" : null,
                Items = (tests.Length == 0 ? new[] { _cbc } : tests).Select(t => new LabRequestItem { LabTestId = t.Id, Price = t.Price }).ToList()
            });
        }

        [Fact]
        public async Task TC_L11_01_ADayWithTenRequests_GivesTheRightCountsAndMedian()
        {
            int[] minutes = { 30, 60, 90, 120, 150, 180, 210 };
            foreach (var m in minutes)
                Add(Day, LabRequestStatus.ResultIssued, m, _cbc, _glucose);
            Add(Day, LabRequestStatus.Received);
            Add(Day, LabRequestStatus.Received);
            Add(Day, LabRequestStatus.Cancelled);
            Add(Day.AddDays(-1), LabRequestStatus.ResultIssued, 5);   // another day: not counted
            await _db.SaveChangesAsync();

            var result = await Service.BuildAsync(Day, Day);

            Assert.True(result.Success, result.Message);
            var report = result.Data;
            Assert.Equal(10, report.TotalRequests);
            Assert.Equal(7, report.CountOf(LabRequestStatus.ResultIssued));
            Assert.Equal(2, report.CountOf(LabRequestStatus.Received));
            Assert.Equal(1, report.CountOf(LabRequestStatus.Cancelled));
            Assert.Equal(0, report.CountOf(LabRequestStatus.InProgress));
            Assert.Equal(6, report.ByStatus.Count);
            Assert.Equal(TimeSpan.FromMinutes(120), report.MedianTurnaround);
            Assert.Equal(7, report.TurnaroundSampleSize);
            Assert.Equal(new[] { ("CBC", 10), ("Glucose", 7) }, report.ByTest);
            Assert.True(report.HasActivity);
            Assert.Equal("2 h 00 min", LabActivityReport.FormatDuration(report.MedianTurnaround));
        }

        [Fact]
        public async Task TC_L11_03_ADayWithoutRequests_IsAnEmptyReport()
        {
            Add(Day.AddDays(-3), LabRequestStatus.ResultIssued, 40);
            await _db.SaveChangesAsync();

            var report = (await Service.BuildAsync(Day, Day)).Data;

            Assert.False(report.HasActivity);
            Assert.Equal(0, report.TotalRequests);
            Assert.Null(report.MedianTurnaround);
            Assert.Empty(report.ByTest);
            Assert.All(report.ByStatus, s => Assert.Equal(0, s.Count));
            Assert.Equal("-", LabActivityReport.FormatDuration(report.MedianTurnaround));
        }

        [Fact]
        public async Task AlternativeFlow_APeriod_IncludesBothEndDays()
        {
            Add(Day.AddDays(-2), LabRequestStatus.Requested);
            Add(Day, LabRequestStatus.Requested);
            Add(Day.AddDays(-3), LabRequestStatus.Requested);   // before the period
            Add(Day.AddDays(1), LabRequestStatus.Requested);    // after the period
            await _db.SaveChangesAsync();

            var report = (await Service.BuildAsync(Day.AddDays(-2), Day)).Data;

            Assert.Equal(2, report.TotalRequests);
            Assert.False(report.IsSingleDay);
        }

        [Fact]
        public async Task APeriodEndingBeforeItStarts_OrLongerThanAYear_IsRefused()
        {
            Assert.Equal(LabReportService.RangeError, (await Service.BuildAsync(Day, Day.AddDays(-1))).Message);
            Assert.False((await Service.BuildAsync(Day.AddDays(-366), Day)).Success);
            Assert.True((await Service.BuildAsync(Day.AddDays(-365), Day)).Success);
        }

        [Fact]
        public void Median_OfAnEvenCount_IsTheMeanOfTheTwoMiddleValues()
        {
            var values = new[] { 10, 40, 20, 30 }.Select(m => TimeSpan.FromMinutes(m)).ToList();

            Assert.Equal(TimeSpan.FromMinutes(25), LabReportService.Median(values));
            Assert.Null(LabReportService.Median(Array.Empty<TimeSpan>()));
            Assert.Equal(TimeSpan.FromMinutes(7), LabReportService.Median(new[] { TimeSpan.FromMinutes(7) }));
        }

        [Fact]
        public async Task Turnaround_IgnoresIssuedRequestsWithoutAReceiptTime()
        {
            _db.LabRequests.Add(new LabRequest
            {
                PatientId = _patient.Id, DoctorId = _doctor.Id, Status = LabRequestStatus.ResultIssued,
                RequestedAt = Day.ToDateTime(new TimeOnly(9, 0)), ResultIssuedAt = Day.ToDateTime(new TimeOnly(10, 0)),
                Items = { new LabRequestItem { LabTestId = _cbc.Id, Price = 5000 } }
            });
            Add(Day, LabRequestStatus.ResultIssued, 45);
            await _db.SaveChangesAsync();

            var report = (await Service.BuildAsync(Day, Day)).Data;

            Assert.Equal(1, report.TurnaroundSampleSize);
            Assert.Equal(TimeSpan.FromMinutes(45), report.MedianTurnaround);
            Assert.Equal("45 min", LabActivityReport.FormatDuration(report.MedianTurnaround));
        }

        [Fact]
        public async Task Page_DefaultsToToday_AndShowsErrors()
        {
            var today = await Service.BuildPageAsync(null, null);
            Assert.Equal(new DateOnly(2026, 11, 19), today.From);
            Assert.Equal(today.From, today.To);
            Assert.NotNull(today.Report);

            var oneSide = await Service.BuildPageAsync(Day, null);
            Assert.Equal(Day, oneSide.To);

            var wrong = await Service.BuildPageAsync(Day, Day.AddDays(-1));
            Assert.Null(wrong.Report);
            Assert.Equal(LabReportService.RangeError, wrong.Error);
        }
    }
}
