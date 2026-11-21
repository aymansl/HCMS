using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Models;
using Microsoft.Extensions.Time.Testing;

namespace HCMS4.Tests
{
    /// <summary>UC-L08 — Search issued results and print a report (TC-L8-01 … TC-L8-03).</summary>
    public class LabResultSearchServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();
        // Thursday 2026-11-19, 12:00 Damascus.
        private readonly ClinicClock _clock = new(new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero)), ClinicTime.TimeZone);
        private Doctor _doctor = null!;
        private Patient _rami = null!;
        private Patient _sara = null!;
        private LabTest _cbc = null!;
        private LabTest _glucose = null!;

        public LabResultSearchServiceTests()
        {
            SeedAsync().GetAwaiter().GetResult();
        }

        public void Dispose() => _db.Dispose();

        private LabResultSearchService Service => new(_db, _clock);
        private DateTime Today => _clock.Today;

        private async Task SeedAsync()
        {
            (_, _doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            _rami = await PatientAsync("Rami", "Haddad");
            _sara = await PatientAsync("Sara", "Khoury");
            _cbc = new LabTest { Name = "CBC", Unit = "10^9/L", ReferenceMin = 4, ReferenceMax = 11, Price = 5000 };
            _glucose = new LabTest { Name = "Glucose", Unit = "mg/dL", ReferenceMin = 70, ReferenceMax = 110, Price = 2000 };
            _db.LabTests.AddRange(_cbc, _glucose);
            _db.Users.Add(new ApplicationUser { Id = "tech-user", UserName = "tech@test.local", FirstName = "Lina", LastName = "Lab" });
            await _db.SaveChangesAsync();
        }

        private async Task<Patient> PatientAsync(string first, string last)
        {
            var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = $"{first}@test.local", FirstName = first, LastName = last };
            var patient = new Patient { UserId = user.Id, User = user, DateOfBirth = new DateTime(1990, 5, 1) };
            _db.Patients.Add(patient);
            await _db.SaveChangesAsync();
            return patient;
        }

        private async Task<LabRequest> RequestAsync(Patient patient, DateTime? issuedAt, LabTest test, LabRequestStatus status = LabRequestStatus.ResultIssued)
        {
            var request = new LabRequest
            {
                PatientId = patient.Id, DoctorId = _doctor.Id, Status = status, ResultIssuedAt = issuedAt, ResultIssuedBy = "tech-user",
                Items = { new LabRequestItem { LabTestId = test.Id, Price = test.Price, Result = new LabResult { NumericValue = 5 } } }
            };
            _db.LabRequests.Add(request);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
            return request;
        }

        [Fact]
        public async Task TC_L8_01_SearchByPatientName_ShowsBothIssuedResults()
        {
            var older = await RequestAsync(_rami, Today.AddDays(-7).AddHours(9), _cbc);
            var newer = await RequestAsync(_rami, Today.AddDays(-2).AddHours(9), _glucose);
            await RequestAsync(_rami, null, _cbc, LabRequestStatus.InProgress);   // not issued
            await RequestAsync(_sara, Today.AddHours(9), _cbc);                    // another patient

            var results = await Service.SearchIssuedAsync(new IssuedResultFilter { PatientName = " ramI HAD " });

            Assert.Equal(new[] { newer.Id, older.Id }, results.Select(r => r.Id));
            Assert.All(results, r => Assert.NotNull(r.Patient.User));
            Assert.All(results, r => Assert.NotNull(Assert.Single(r.Items).Result));
        }

        [Fact]
        public async Task TC_L8_02_NoCriteria_ShowsTodaysResults()
        {
            var today1 = await RequestAsync(_rami, Today.AddHours(8), _cbc);
            var today2 = await RequestAsync(_sara, Today.AddHours(11), _glucose);
            await RequestAsync(_rami, Today.AddDays(-1).AddHours(23), _cbc);

            var results = await Service.SearchIssuedAsync(new IssuedResultFilter());

            Assert.Equal(new[] { today2.Id, today1.Id }, results.Select(r => r.Id));
            Assert.True(new IssuedResultsViewModel().ShowingToday);
        }

        [Fact]
        public async Task TC_L8_03_AnUnknownRequestNumber_FindsNothing()
        {
            await RequestAsync(_rami, Today.AddHours(8), _cbc);

            Assert.Empty(await Service.SearchIssuedAsync(new IssuedResultFilter { RequestId = 999_999 }));
        }

        [Fact]
        public async Task SearchByRequestNumber_FindsItOnAnyDay()
        {
            var old = await RequestAsync(_rami, Today.AddDays(-30), _cbc);

            Assert.Equal(old.Id, Assert.Single(await Service.SearchIssuedAsync(new IssuedResultFilter { RequestId = old.Id })).Id);
        }

        [Fact]
        public async Task SearchByTestAndByDate_Combine()
        {
            var match = await RequestAsync(_rami, Today.AddDays(-3).AddHours(10), _glucose);
            await RequestAsync(_rami, Today.AddDays(-3).AddHours(11), _cbc);
            await RequestAsync(_rami, Today.AddDays(-4).AddHours(10), _glucose);

            var results = await Service.SearchIssuedAsync(new IssuedResultFilter
            {
                TestId = _glucose.Id, Date = DateOnly.FromDateTime(Today.AddDays(-3))
            });

            Assert.Equal(match.Id, Assert.Single(results).Id);
        }

        [Fact]
        public async Task Report_HasTheRequestResultsAndTheIssuer_OnlyForIssuedRequests()
        {
            var issued = await RequestAsync(_rami, Today.AddHours(8), _cbc);
            var pending = await RequestAsync(_rami, null, _cbc, LabRequestStatus.InProgress);

            var report = await Service.GetReportAsync(issued.Id);

            Assert.NotNull(report);
            Assert.Equal("Lina Lab", report!.IssuedByName);
            Assert.Equal(_clock.Now, report.PrintedAt);
            Assert.NotNull(report.Request.Patient.User);
            Assert.NotNull(report.Request.Items.Single().Result);
            Assert.Null(await Service.GetReportAsync(pending.Id));
            Assert.Null(await Service.GetReportAsync(999_999));
        }

        [Fact]
        public void Filter_IsEmpty_OnlyWithoutAnyCriterion()
        {
            Assert.True(new IssuedResultFilter { PatientName = " " }.IsEmpty);
            Assert.False(new IssuedResultFilter { RequestId = 1 }.IsEmpty);
            Assert.False(new IssuedResultFilter { TestId = 1 }.IsEmpty);
            Assert.False(new IssuedResultFilter { Date = new DateOnly(2026, 11, 1) }.IsEmpty);
        }
    }
}
