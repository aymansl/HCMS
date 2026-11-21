using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Identity.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCMS4.Tests
{
    /// <summary>UC-L09 — View a patient's test history (TC-L9-01 … TC-L9-03) and the patient search.</summary>
    public class LabHistoryServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();
        private Doctor _doctor = null!;
        private Patient _patient = null!;
        private LabTest _glucose = null!;
        private LabTest _culture = null!;

        public LabHistoryServiceTests()
        {
            SeedAsync().GetAwaiter().GetResult();
        }

        public void Dispose() => _db.Dispose();

        private LabHistoryService Service => new(_db, new PatientService(_db, NullLogger<PatientService>.Instance));

        private async Task SeedAsync()
        {
            (_, _doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            _patient = await PatientAsync("Rami", "Haddad", "rami@test.local", "0991234567");
            _glucose = new LabTest { Name = "Glucose", Unit = "mg/dL", ReferenceMin = 70, ReferenceMax = 110, Price = 2000 };
            _culture = new LabTest { Name = "Urine Culture", Unit = "-", Price = 7500 };
            _db.LabTests.AddRange(_glucose, _culture);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }

        private async Task<Patient> PatientAsync(string first, string last, string email, string? phone = null)
        {
            var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = email, Email = email, FirstName = first, LastName = last, PhoneNumber = phone };
            var patient = new Patient { UserId = user.Id, User = user, DateOfBirth = new DateTime(1990, 1, 1) };
            _db.Patients.Add(patient);
            await _db.SaveChangesAsync();
            return patient;
        }

        private async Task<LabRequest> IssuedAsync(DateTime issuedAt, decimal? glucose, bool abnormal = false, LabRequestStatus status = LabRequestStatus.ResultIssued, int? patientId = null)
        {
            var request = new LabRequest
            {
                PatientId = patientId ?? _patient.Id, DoctorId = _doctor.Id, Status = status, RequestedAt = issuedAt.AddHours(-3),
                ResultIssuedAt = status == LabRequestStatus.ResultIssued ? issuedAt : null,
                Items =
                {
                    new LabRequestItem
                    {
                        LabTestId = _glucose.Id, Price = 2000,
                        Result = glucose.HasValue ? new LabResult { NumericValue = glucose, IsAbnormal = abnormal } : null
                    }
                }
            };
            _db.LabRequests.Add(request);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
            return request;
        }

        private static readonly DateTime Day = new(2026, 11, 1, 10, 0, 0);

        [Fact]
        public async Task TC_L9_01_FourIssuedResults_AreShownNewestFirst()
        {
            var r1 = await IssuedAsync(Day, 90);
            var r3 = await IssuedAsync(Day.AddDays(10), 95);
            var r2 = await IssuedAsync(Day.AddDays(5), 100);
            var r4 = await IssuedAsync(Day.AddDays(15), 85);
            await IssuedAsync(Day.AddDays(20), null, status: LabRequestStatus.InProgress);   // not issued: excluded
            await IssuedAsync(Day.AddDays(21), null, status: LabRequestStatus.Cancelled);    // not issued: excluded

            var history = await Service.GetHistoryAsync(_patient.Id);

            Assert.Equal(new[] { r4.Id, r3.Id, r2.Id, r1.Id }, history.Select(r => r.Id));
            Assert.All(history, r => Assert.NotNull(Assert.Single(r.Items).Result));
            Assert.All(history, r => Assert.NotNull(r.Doctor.User));
        }

        [Fact]
        public async Task TC_L9_02_AnOutOfRangeValue_IsMarked()
        {
            await IssuedAsync(Day, 180, abnormal: true);

            var result = Assert.Single(Assert.Single(await Service.GetHistoryAsync(_patient.Id)).Items).Result!;

            Assert.True(result.IsAbnormal);
            Assert.Equal(180, result.NumericValue);
        }

        [Fact]
        public async Task TC_L9_03_APatientWithoutResults_HasNoHistory()
        {
            var fresh = await PatientAsync("New", "Patient", "new@test.local");

            var page = await Service.BuildPageAsync(null, fresh.Id, null);

            Assert.NotNull(page.Patient);
            Assert.Empty(page.History);
            Assert.False(page.NotFound);
        }

        [Fact]
        public async Task Exception_AnUnknownPatient_IsNotFound()
        {
            var byId = await Service.BuildPageAsync(null, 999_999, null);
            var bySearch = await Service.BuildPageAsync("nobody-here", null, null);

            Assert.True(byId.NotFound);
            Assert.Null(byId.Patient);
            Assert.True(bySearch.NotFound);
            Assert.Empty(bySearch.Matches);
        }

        [Theory]
        [InlineData("rami")]
        [InlineData("AMI HAD")]
        [InlineData("rami@TEST")]
        [InlineData("0991234")]
        public async Task Search_FindsThePatientByPartOfNameEmailOrPhone(string term)
        {
            await PatientAsync("Sara", "Khoury", "sara@test.local");

            var page = await Service.BuildPageAsync(term, null, null);

            Assert.Equal(_patient.Id, Assert.Single(page.Matches).Id);
            Assert.False(page.NotFound);
        }

        [Fact]
        public async Task Search_WithoutATerm_ShowsNothingAndIsNotAnError()
        {
            var page = await Service.BuildPageAsync("  ", null, null);

            Assert.Empty(page.Matches);
            Assert.False(page.NotFound);
            Assert.Empty(await new PatientService(_db, NullLogger<PatientService>.Instance).SearchAsync(""));
        }

        [Fact]
        public async Task Search_ReturnsAtMostTwentyPatients_ByName()
        {
            for (var i = 0; i < 25; i++)
                await PatientAsync($"Many{i:D2}", "Same", $"many{i}@test.local");

            var matches = await new PatientService(_db, NullLogger<PatientService>.Instance).SearchAsync("many");

            Assert.Equal(20, matches.Count);
            Assert.Equal("Many00", matches[0].User.FirstName);
        }

        [Fact]
        public async Task AlternativeFlow_TheTrendOfOneTest_IsOldestFirst_WithAChart()
        {
            await IssuedAsync(Day.AddDays(5), 120, abnormal: true);
            await IssuedAsync(Day, 90);
            await IssuedAsync(Day.AddDays(9), 100);

            var page = await Service.BuildPageAsync(null, _patient.Id, _glucose.Id);

            Assert.Equal("Glucose", page.TrendTest!.Name);
            Assert.Equal(new decimal?[] { 90, 120, 100 }, page.Trend.Select(p => p.Result.NumericValue));
            Assert.Equal(Day, page.Trend[0].IssuedAt);
            Assert.NotNull(page.Chart);
            Assert.Equal(3, page.Chart!.Dots.Count);
            Assert.True(page.Chart.Dots[1].Abnormal);
            Assert.NotNull(page.Chart.BandY);
            Assert.Equal(new[] { "Glucose" }, page.Tests.Select(t => t.Name));
        }

        [Fact]
        public async Task Trend_OfATestNotInTheHistory_IsEmpty()
        {
            await IssuedAsync(Day, 90);

            var page = await Service.BuildPageAsync(null, _patient.Id, _culture.Id);

            Assert.Null(page.TrendTest);
            Assert.Empty(page.Trend);
        }

        [Fact]
        public async Task History_IsOnlyForTheSelectedPatient()
        {
            var other = await PatientAsync("Other", "Person", "other@test.local");
            await IssuedAsync(Day, 90, patientId: other.Id);

            Assert.Empty(await Service.GetHistoryAsync(_patient.Id));
            Assert.Single(await Service.GetHistoryAsync(other.Id));
        }

        [Fact]
        public void Chart_ForQualitativeValuesOnly_IsNull_AndASingleValueIsCentred()
        {
            var point = new LabTrendPoint(1, Day, new LabResult { TextValue = "Negative" });
            Assert.Null(LabTrendChart.Build(_culture, new[] { point }));

            var single = LabTrendChart.Build(_glucose, new[] { new LabTrendPoint(1, Day, new LabResult { NumericValue = 90 }) });
            Assert.Equal("300", single!.Dots[0].X);

            var flat = LabTrendChart.Build(new LabTest(), new[] { new LabTrendPoint(1, Day, new LabResult { NumericValue = 5 }) });
            Assert.NotNull(flat);
            Assert.Null(flat!.BandY);
        }
    }
}
