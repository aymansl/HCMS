using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace HCMS4.Tests
{
    /// <summary>UC-L10 — Issue a result and alert the doctor and the patient (TC-L10-01 … TC-L10-03, TC-L4-03).</summary>
    public class LabResultServiceTests : IDisposable
    {
        private const string TechnicianUserId = "tech-user";

        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero));
        private readonly ClinicClock _clock;

        private ApplicationUser _doctorUser = null!;
        private Doctor _doctor = null!;
        private ApplicationUser _patientUser = null!;
        private Patient _patient = null!;
        private LabTest _cbc = null!;       // 4–11
        private LabTest _glucose = null!;   // 70–110
        private LabTest _potassium = null!; // 3.5–5.1, critical ≤ 2.5 or ≥ 6.5
        private LabTest _culture = null!;   // qualitative

        public LabResultServiceTests()
        {
            _clock = new ClinicClock(_time, ClinicTime.TimeZone);
            SeedAsync().GetAwaiter().GetResult();
        }

        public void Dispose() => _db.Dispose();

        private LabResultService Service => new(_db, new NotificationService(_db), _clock);

        private async Task SeedAsync()
        {
            (_doctorUser, _doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            (_patientUser, _patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            _cbc = new LabTest { Name = "CBC", Unit = "10^9/L", ReferenceMin = 4, ReferenceMax = 11, Price = 5000 };
            _glucose = new LabTest { Name = "Glucose", Unit = "mg/dL", ReferenceMin = 70, ReferenceMax = 110, Price = 2000 };
            _potassium = new LabTest { Name = "Potassium", Unit = "mmol/L", ReferenceMin = 3.5m, ReferenceMax = 5.1m, CriticalMin = 2.5m, CriticalMax = 6.5m, Price = 3000 };
            _culture = new LabTest { Name = "Urine Culture", Unit = "-", Price = 7500 };
            _db.LabTests.AddRange(_cbc, _glucose, _potassium, _culture);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }

        private async Task<LabRequest> RequestAsync(LabRequestStatus status, params LabTest[] tests)
        {
            var request = new LabRequest
            {
                PatientId = _patient.Id, DoctorId = _doctor.Id, Status = status, RequestedAt = _clock.Now,
                ReceivedAt = status == LabRequestStatus.Received ? _clock.Now : null,
                Items = tests.Select(t => new LabRequestItem { LabTestId = t.Id, Price = t.Price }).ToList()
            };
            _db.LabRequests.Add(request);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
            return request;
        }

        private Dictionary<int, string?> Values(LabRequest request, params (LabTest Test, string? Value)[] values) =>
            values.ToDictionary(v => request.Items.Single(i => i.LabTestId == v.Test.Id).Id, v => v.Value);

        private Task<LabRequest> ReloadAsync(int id) => _db.LabRequests.AsNoTracking()
            .Include(r => r.Items).ThenInclude(i => i.Result).Include(r => r.Items).ThenInclude(i => i.LabTest)
            .SingleAsync(r => r.Id == id);

        private LabResult ResultOf(LabRequest request, LabTest test) => request.Items.Single(i => i.LabTestId == test.Id).Result!;

        private Task<List<UserNotification>> NotificationsForAsync(string userId) =>
            _db.UserNotifications.AsNoTracking().Where(n => n.UserId == userId).ToListAsync();

        // ---- TC-L10-01: a normal result ----

        [Fact]
        public async Task TC_L10_01_IssueANormalResult_SetsResultIssued_AndNotifiesTheDoctorAndThePatient()
        {
            var request = await RequestAsync(LabRequestStatus.Received, _cbc, _glucose);
            _time.Advance(TimeSpan.FromHours(2));

            var result = await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_cbc, "7.2"), (_glucose, "95")), issue: true);

            Assert.True(result.Success, result.Message);
            var saved = await ReloadAsync(request.Id);
            Assert.Equal(LabRequestStatus.ResultIssued, saved.Status);
            Assert.Equal(_clock.Now, saved.ResultIssuedAt);
            Assert.Equal(TechnicianUserId, saved.ResultIssuedBy);
            Assert.False(saved.HasCriticalResult);
            Assert.Equal(7.2m, ResultOf(saved, _cbc).NumericValue);
            Assert.False(ResultOf(saved, _cbc).IsAbnormal);
            Assert.Equal(TechnicianUserId, ResultOf(saved, _glucose).EnteredBy);

            var doctor = Assert.Single(await NotificationsForAsync(_doctorUser.Id));
            Assert.Equal("Lab result issued", doctor.Title);
            Assert.Equal($"/Doctor/LabResult/{request.Id}", doctor.LinkUrl);
            var patient = Assert.Single(await NotificationsForAsync(_patientUser.Id));
            Assert.Equal("Your lab result is ready", patient.Title);
            Assert.Equal($"/Patient/LabResult/{request.Id}", patient.LinkUrl);
        }

        [Fact]
        public async Task Save_TheFirstTime_MovesReceivedToInProgress_AndFlagsAbnormalValues()
        {
            var request = await RequestAsync(LabRequestStatus.Received, _cbc, _glucose);

            var result = await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_cbc, "12.5")), issue: false);

            Assert.True(result.Success, result.Message);
            var saved = await ReloadAsync(request.Id);
            Assert.Equal(LabRequestStatus.InProgress, saved.Status);
            Assert.Equal(TechnicianUserId, saved.StatusChangedBy);
            Assert.True(ResultOf(saved, _cbc).IsAbnormal);
            Assert.Null(saved.Items.Single(i => i.LabTestId == _glucose.Id).Result);
            Assert.Empty(await NotificationsForAsync(_doctorUser.Id));
        }

        [Fact]
        public async Task Save_ThenIssue_FromInProgress_Works_AndKeepsSavedValues()
        {
            var request = await RequestAsync(LabRequestStatus.Received, _cbc, _glucose);
            await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_cbc, "5")), issue: false);
            _db.ChangeTracker.Clear();

            var result = await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_cbc, "5"), (_glucose, "60")), issue: true);

            Assert.True(result.Success, result.Message);
            var saved = await ReloadAsync(request.Id);
            Assert.Equal(LabRequestStatus.ResultIssued, saved.Status);
            Assert.True(ResultOf(saved, _glucose).IsAbnormal);
            Assert.Single(await _db.LabResults.Where(r => r.LabRequestItem.LabTestId == _cbc.Id).ToListAsync());
        }

        // ---- TC-L10-02: a critical value ----

        [Fact]
        public async Task TC_L10_02_CriticalPotassium_IsMarkedCritical_AndTheDoctorGetsAnUrgentAlert()
        {
            var request = await RequestAsync(LabRequestStatus.Received, _potassium);

            var result = await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_potassium, "7.0")), issue: true);

            Assert.True(result.Success, result.Message);
            Assert.Contains("CRITICAL", result.Message);
            var saved = await ReloadAsync(request.Id);
            Assert.True(saved.HasCriticalResult);
            Assert.True(ResultOf(saved, _potassium).IsCritical);
            Assert.True(ResultOf(saved, _potassium).IsAbnormal);
            var alert = Assert.Single(await NotificationsForAsync(_doctorUser.Id));
            Assert.Equal("CRITICAL lab result", alert.Title);
            Assert.Contains("Potassium 7 mmol/L", alert.Message);
            Assert.Equal(request.Id, Assert.Single(await Service.GetUnacknowledgedCriticalAsync(_doctorUser.Id)).Id);
        }

        [Fact]
        public async Task TC_L10_02_Acknowledge_RecordsTimeAndDoctor_AndClearsTheAlert()
        {
            var request = await RequestAsync(LabRequestStatus.Received, _potassium);
            await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_potassium, "2.1")), issue: true);
            _time.Advance(TimeSpan.FromMinutes(20));

            var result = await Service.AcknowledgeCriticalAsync(_doctor.Id, request.Id);

            Assert.True(result.Success, result.Message);
            var saved = await ReloadAsync(request.Id);
            Assert.Equal(_clock.Now, saved.CriticalAcknowledgedAt);
            Assert.Equal(_doctorUser.Id, saved.CriticalAcknowledgedBy);
            Assert.Empty(await Service.GetUnacknowledgedCriticalAsync(_doctorUser.Id));
            Assert.True((await Service.AcknowledgeCriticalAsync(_doctor.Id, request.Id)).Success); // again: no change
            Assert.Equal(saved.CriticalAcknowledgedAt, (await ReloadAsync(request.Id)).CriticalAcknowledgedAt);
        }

        [Fact]
        public async Task Acknowledge_ANormalResult_OrAnotherDoctorsResult_Fails()
        {
            var normal = await RequestAsync(LabRequestStatus.Received, _cbc);
            await Service.SaveAsync(normal.Id, TechnicianUserId, Values(normal, (_cbc, "6")), issue: true);
            var critical = await RequestAsync(LabRequestStatus.Received, _potassium);
            await Service.SaveAsync(critical.Id, TechnicianUserId, Values(critical, (_potassium, "7")), issue: true);

            Assert.False((await Service.AcknowledgeCriticalAsync(_doctor.Id, normal.Id)).Success);
            Assert.False((await Service.AcknowledgeCriticalAsync(_doctor.Id + 1, critical.Id)).Success);
            Assert.False((await Service.AcknowledgeCriticalAsync(_doctor.Id, 999)).Success);
            Assert.Single(await Service.GetUnacknowledgedCriticalAsync(_doctorUser.Id));
        }

        // ---- TC-L10-03: an incomplete result ----

        [Fact]
        public async Task TC_L10_03_IssueWithAMissingValue_IsRefused_AndNothingChanges()
        {
            var request = await RequestAsync(LabRequestStatus.Received, _cbc, _glucose);

            var result = await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_cbc, "6"), (_glucose, " ")), issue: true);

            Assert.False(result.Success);
            Assert.StartsWith(LabResultService.IncompleteError, result.Message);
            Assert.Contains("Glucose", result.Message);
            var saved = await ReloadAsync(request.Id);
            Assert.Equal(LabRequestStatus.Received, saved.Status);
            Assert.Empty(await _db.LabResults.ToListAsync());
            Assert.Empty(await NotificationsForAsync(_doctorUser.Id));
        }

        // ---- TC-L4-03 and other status rules ----

        [Fact]
        public async Task TC_L4_03_ACancelledRequest_RefusesResults()
        {
            var request = await RequestAsync(LabRequestStatus.Cancelled, _cbc);

            var result = await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_cbc, "6")), issue: false);

            Assert.False(result.Success);
            Assert.Equal(LabResultService.CancelledError, result.Message);
            Assert.Empty(await _db.LabResults.ToListAsync());
        }

        [Theory]
        [InlineData(LabRequestStatus.Requested, LabResultService.NotReceivedError)]
        [InlineData(LabRequestStatus.ReturnedToDoctor, LabResultService.NotReceivedError)]
        [InlineData(LabRequestStatus.ResultIssued, LabResultService.AlreadyIssuedError)]
        public async Task Results_AreOnlyEnteredForReceivedOrInProgressRequests(LabRequestStatus status, string error)
        {
            var request = await RequestAsync(status, _cbc);

            Assert.Equal(error, (await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_cbc, "6")), issue: true)).Message);
        }

        [Fact]
        public async Task Save_AnUnknownRequest_Fails()
        {
            Assert.False((await Service.SaveAsync(999, TechnicianUserId, new Dictionary<int, string?>(), issue: false)).Success);
            Assert.Null(await Service.GetEntryFormAsync(999));
        }

        // ---- Values ----

        [Fact]
        public async Task ANumberIsRequired_ForAQuantitativeTest_AndNothingIsSaved()
        {
            var request = await RequestAsync(LabRequestStatus.Received, _cbc, _glucose);

            var result = await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_cbc, "5"), (_glucose, "high")), issue: false);

            Assert.False(result.Success);
            Assert.Contains("Glucose must be a number", result.Message);
            Assert.Empty(await _db.LabResults.ToListAsync());
            Assert.Equal(LabRequestStatus.Received, (await ReloadAsync(request.Id)).Status);
        }

        [Fact]
        public async Task AQualitativeTest_StoresText_AndIsNeverFlagged()
        {
            var request = await RequestAsync(LabRequestStatus.Received, _culture);

            Assert.True((await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_culture, " Positive ")), issue: true)).Success);

            var result = ResultOf(await ReloadAsync(request.Id), _culture);
            Assert.Equal("Positive", result.TextValue);
            Assert.Null(result.NumericValue);
            Assert.False(result.IsAbnormal);
            Assert.False(result.IsCritical);
            Assert.Equal("Positive", result.DisplayValue);
        }

        [Fact]
        public async Task AQualitativeValue_IsLimitedTo200Characters()
        {
            var request = await RequestAsync(LabRequestStatus.Received, _culture);

            Assert.False((await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_culture, new string('x', 201))), issue: false)).Success);
        }

        [Fact]
        public async Task ClearingAValue_RemovesItsResult()
        {
            var request = await RequestAsync(LabRequestStatus.Received, _cbc);
            await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_cbc, "6")), issue: false);
            _db.ChangeTracker.Clear();

            await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_cbc, "")), issue: false);

            Assert.Empty(await _db.LabResults.ToListAsync());
        }

        [Fact]
        public async Task EntryForm_ShowsSavedValuesAndFlags_AndMarksQualitativeTests()
        {
            var request = await RequestAsync(LabRequestStatus.Received, _potassium, _culture);
            await Service.SaveAsync(request.Id, TechnicianUserId, Values(request, (_potassium, "6.5")), issue: false);

            var form = await Service.GetEntryFormAsync(request.Id);

            Assert.Equal(new[] { "Potassium", "Urine Culture" }, form!.Rows.Select(r => r.Test.Name));
            Assert.Equal("6.5", form.Rows[0].Value);
            Assert.True(form.Rows[0].IsCritical); // the limit itself is critical
            Assert.False(form.Rows[0].IsQualitative);
            Assert.True(form.Rows[1].IsQualitative);
            Assert.Null(form.Rows[1].Value);
        }

        // ---- Reading results ----

        [Fact]
        public async Task IssuedResults_AreVisibleOnlyToTheirDoctorAndPatient()
        {
            var issued = await RequestAsync(LabRequestStatus.Received, _cbc);
            await Service.SaveAsync(issued.Id, TechnicianUserId, Values(issued, (_cbc, "6")), issue: true);
            var pending = await RequestAsync(LabRequestStatus.InProgress, _cbc);

            Assert.NotNull((await Service.GetIssuedForDoctorAsync(_doctor.Id, issued.Id))!.Items.Single().Result);
            Assert.NotNull(await Service.GetIssuedForPatientAsync(_patientUser.Id, issued.Id));
            Assert.Null(await Service.GetIssuedForDoctorAsync(_doctor.Id + 1, issued.Id));
            Assert.Null(await Service.GetIssuedForPatientAsync("someone-else", issued.Id));
            Assert.Null(await Service.GetIssuedForDoctorAsync(_doctor.Id, pending.Id));
            Assert.Null(await Service.GetIssuedForPatientAsync(_patientUser.Id, pending.Id));
        }

        // ---- Rules and catalogue limits ----

        [Theory]
        [InlineData(3.4, true, false)]
        [InlineData(3.5, false, false)]
        [InlineData(5.1, false, false)]
        [InlineData(5.2, true, false)]
        [InlineData(6.5, true, true)]
        [InlineData(2.5, true, true)]
        [InlineData(2.6, true, false)]
        public void Rules_FlagAbnormalAndCriticalValues(double value, bool abnormal, bool critical)
        {
            var potassium = new LabTest { ReferenceMin = 3.5m, ReferenceMax = 5.1m, CriticalMin = 2.5m, CriticalMax = 6.5m };

            Assert.Equal(abnormal, LabResultRules.IsAbnormal(potassium, (decimal)value));
            Assert.Equal(critical, LabResultRules.IsCritical(potassium, (decimal)value));
        }

        [Fact]
        public void Rules_ATestWithoutLimits_IsQualitative()
        {
            Assert.True(LabResultRules.IsQualitative(new LabTest()));
            Assert.False(LabResultRules.IsQualitative(new LabTest { CriticalMax = 1 }));
            Assert.Equal("≤ 2.5 or ≥ 6.5", new LabTest { CriticalMin = 2.5m, CriticalMax = 6.5m }.CriticalLimits);
            Assert.Equal("-", new LabTest().CriticalLimits);
        }

        [Theory]
        [InlineData(3.0, 6.0, true)]   // outside the 3.5–5.1 range
        [InlineData(3.5, 5.1, true)]   // on the limits
        [InlineData(4.0, null, false)] // critical min inside the range
        [InlineData(null, 5.0, false)] // critical max inside the range
        public void Catalogue_CriticalLimits_MustLieOutsideTheReferenceRange(double? min, double? max, bool valid)
        {
            var form = new LabTestFormViewModel
            {
                Name = "Potassium", Unit = "mmol/L", ReferenceMin = 3.5m, ReferenceMax = 5.1m,
                CriticalMin = (decimal?)min, CriticalMax = (decimal?)max, Price = 3000
            };

            Assert.Equal(valid, form.CriticalLimitsAreValid());
        }

        [Fact]
        public async Task Catalogue_SavesCriticalLimits_AndRefusesInvalidOnes()
        {
            var catalogue = new LabTestService(_db, new LabRequestTestUsage(_db), _clock);
            var ok = await catalogue.SaveAsync(new LabTestFormViewModel
            {
                Name = "Sodium", Unit = "mmol/L", ReferenceMin = 135, ReferenceMax = 145, CriticalMin = 120, CriticalMax = 160, Price = 3000
            });
            var bad = await catalogue.SaveAsync(new LabTestFormViewModel
            {
                Name = "Calcium", Unit = "mg/dL", ReferenceMin = 8.5m, ReferenceMax = 10.5m, CriticalMax = 9, Price = 3000
            });

            Assert.True(ok.Success, ok.Message);
            Assert.Equal(160, ok.Data.CriticalMax);
            Assert.Equal(LabTestFormViewModel.CriticalError, bad.Message);
        }
    }
}
