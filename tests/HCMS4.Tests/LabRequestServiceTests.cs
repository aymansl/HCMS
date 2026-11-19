using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using System.ComponentModel.DataAnnotations;

namespace HCMS4.Tests
{
    /// <summary>UC-L03 — Receive a lab test request from the doctor (TC-L3-01 … TC-L3-03), and TC-L6-03.</summary>
    public class LabRequestServiceTests : IDisposable
    {
        private const string TechnicianUserId = "tech-user";

        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero)); // 12:00 Damascus
        private readonly ClinicClock _clock;

        private ApplicationUser _doctorUser = null!;
        private Doctor _doctor = null!;
        private Patient _patient = null!;
        private LabTest _cbc = null!;
        private LabTest _glucose = null!;

        public LabRequestServiceTests()
        {
            _clock = new ClinicClock(_time, ClinicTime.TimeZone);
            SeedAsync().GetAwaiter().GetResult();
        }

        public void Dispose() => _db.Dispose();

        private LabRequestService Service =>
            new(_db, new MedicalRecordService(_db, _clock), new NotificationService(_db), _clock);

        private async Task SeedAsync()
        {
            (_doctorUser, _doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            (_, _patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            _db.Appointments.Add(new Appointment
            {
                DoctorId = _doctor.Id, PatientId = _patient.Id, AppointmentDateTime = _clock.Now.AddDays(-1),
                Status = AppointmentStatus.Completed, ConsultationFee = 50
            });

            _cbc = new LabTest { Name = "CBC", Unit = "10^9/L", ReferenceMin = 4, ReferenceMax = 11, Price = 5000 };
            _glucose = new LabTest { Name = "Glucose", Unit = "mg/dL", ReferenceMin = 70, ReferenceMax = 110, Price = 2000 };
            _db.LabTests.AddRange(_cbc, _glucose);

            // One lab technician, so role notifications have a recipient.
            var role = new IdentityRole(Roles.LabTechnician) { Id = "role-lab", NormalizedName = Roles.LabTechnician.ToUpperInvariant() };
            _db.Roles.Add(role);
            _db.Users.Add(new ApplicationUser { Id = TechnicianUserId, UserName = "tech@test.local", FirstName = "Lina", LastName = "Lab" });
            _db.UserRoles.Add(new IdentityUserRole<string> { RoleId = role.Id, UserId = TechnicianUserId });

            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }

        private LabRequestFormViewModel Form(params int[] testIds) => new()
        {
            PatientId = _patient.Id, SelectedTestIds = testIds.ToList(), ClinicalNotes = " Fatigue for two weeks "
        };

        private async Task<LabRequest> CreateAsync(params int[] testIds)
        {
            var result = await Service.CreateAsync(_doctor.Id, Form(testIds));
            Assert.True(result.Success, result.Message);
            _db.ChangeTracker.Clear();
            return result.Data;
        }

        private Task<LabRequest> ReloadAsync(int id) =>
            _db.LabRequests.AsNoTracking().Include(r => r.Items).SingleAsync(r => r.Id == id);

        private Task<List<UserNotification>> NotificationsForAsync(string userId) =>
            _db.UserNotifications.AsNoTracking().Where(n => n.UserId == userId).ToListAsync();

        // ---- TC-L3-01: a valid request reaches the lab and is received ----

        [Fact]
        public async Task TC_L3_01_Create_SavesARequestedRequest_WithTestPrices_AndNotifiesTheLab()
        {
            var request = await CreateAsync(_cbc.Id, _glucose.Id);

            var saved = await ReloadAsync(request.Id);
            Assert.Equal(LabRequestStatus.Requested, saved.Status);
            Assert.Equal(_clock.Now, saved.RequestedAt);
            Assert.Equal(_doctorUser.Id, saved.StatusChangedBy);
            Assert.Equal("Fatigue for two weeks", saved.ClinicalNotes);
            Assert.Equal(new[] { 2000m, 5000m }, saved.Items.Select(i => i.Price).Order());

            var notification = Assert.Single(await NotificationsForAsync(TechnicianUserId));
            Assert.Equal(NotificationType.LabRequest, notification.Type);
            Assert.Equal($"/Lab/RequestDetails/{request.Id}", notification.LinkUrl);
            Assert.Contains("CBC, Glucose", notification.Message);
        }

        [Fact]
        public async Task TC_L3_01_NewRequest_IsListedForTheLab()
        {
            var request = await CreateAsync(_cbc.Id);

            Assert.Equal(1, await Service.CountNewAsync());
            var listed = Assert.Single(await Service.GetNewForLabAsync());
            Assert.Equal(request.Id, listed.Id);
            Assert.NotNull(listed.Patient.User);
            Assert.Equal("CBC", Assert.Single(listed.Items).LabTest.Name);
        }

        [Fact]
        public async Task TC_L3_01_Receive_SetsReceived_WithTheTimeAndTheTechnician()
        {
            var request = await CreateAsync(_cbc.Id);
            _time.Advance(TimeSpan.FromMinutes(10));

            var result = await Service.ReceiveAsync(request.Id, TechnicianUserId);

            Assert.True(result.Success, result.Message);
            var saved = await ReloadAsync(request.Id);
            Assert.Equal(LabRequestStatus.Received, saved.Status);
            Assert.Equal(_clock.Now, saved.ReceivedAt);
            Assert.Equal(TechnicianUserId, saved.ReceivedBy);
            Assert.Equal(TechnicianUserId, saved.StatusChangedBy);
            Assert.Equal(0, await Service.CountNewAsync());
        }

        [Fact]
        public async Task Receive_Twice_IsRefused()
        {
            var request = await CreateAsync(_cbc.Id);
            await Service.ReceiveAsync(request.Id, TechnicianUserId);

            var again = await Service.ReceiveAsync(request.Id, TechnicianUserId);

            Assert.False(again.Success);
            Assert.Contains("Received", again.Message);
        }

        [Fact]
        public async Task Receive_UnknownRequest_Fails()
        {
            Assert.False((await Service.ReceiveAsync(999, TechnicianUserId)).Success);
        }

        // ---- TC-L3-02: unknown patient ----

        [Fact]
        public async Task TC_L3_02_UnknownPatient_IsRejected_NothingIsSaved_AndTheDoctorIsAlerted()
        {
            var model = Form(_cbc.Id);
            model.PatientId = 999_999;

            var result = await Service.CreateAsync(_doctor.Id, model);

            Assert.False(result.Success);
            Assert.Equal(LabRequestService.PatientNotFoundError, result.Message);
            Assert.Empty(await _db.LabRequests.ToListAsync());
            var alert = Assert.Single(await NotificationsForAsync(_doctorUser.Id));
            Assert.Equal(LabRequestService.PatientNotFoundError, alert.Message);
            Assert.Empty(await NotificationsForAsync(TechnicianUserId));
        }

        [Fact]
        public async Task NewForm_UnknownPatient_IsNull()
        {
            Assert.Null(await Service.NewFormAsync(999_999));
        }

        // ---- TC-L3-03: no tests is rejected; an incomplete request is returned and resent ----

        [Fact]
        public void TC_L3_03_FormValidation_RequiresAtLeastOneTest()
        {
            var model = Form();
            var results = new List<ValidationResult>();

            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

            Assert.Contains(results, r => r.ErrorMessage == LabRequestFormViewModel.NoTestsError);
        }

        [Fact]
        public async Task TC_L3_03_Create_WithoutTests_IsRejected()
        {
            var result = await Service.CreateAsync(_doctor.Id, Form());

            Assert.False(result.Success);
            Assert.Equal(LabRequestFormViewModel.NoTestsError, result.Message);
            Assert.Empty(await _db.LabRequests.ToListAsync());
        }

        [Fact]
        public async Task TC_L3_03_ReturnToDoctor_SavesTheNote_AndNotifiesTheDoctor()
        {
            var request = await CreateAsync(_cbc.Id);

            var result = await Service.ReturnToDoctorAsync(request.Id, TechnicianUserId, "  Fasting state is missing. ");

            Assert.True(result.Success, result.Message);
            var saved = await ReloadAsync(request.Id);
            Assert.Equal(LabRequestStatus.ReturnedToDoctor, saved.Status);
            Assert.Equal("Fasting state is missing.", saved.ReturnNote);
            Assert.Equal(_clock.Now, saved.ReturnedAt);
            var notification = Assert.Single(await NotificationsForAsync(_doctorUser.Id));
            Assert.Contains("Fasting state is missing.", notification.Message);
            Assert.Equal($"/Doctor/EditLabRequest/{request.Id}", notification.LinkUrl);
        }

        [Fact]
        public async Task ReturnToDoctor_WithoutANote_IsRefused()
        {
            var request = await CreateAsync(_cbc.Id);

            Assert.False((await Service.ReturnToDoctorAsync(request.Id, TechnicianUserId, "  ")).Success);
            Assert.Equal(LabRequestStatus.Requested, (await ReloadAsync(request.Id)).Status);
        }

        [Fact]
        public async Task ReturnToDoctor_AfterReceipt_IsRefused()
        {
            var request = await CreateAsync(_cbc.Id);
            await Service.ReceiveAsync(request.Id, TechnicianUserId);

            Assert.False((await Service.ReturnToDoctorAsync(request.Id, TechnicianUserId, "Too late")).Success);
        }

        [Fact]
        public async Task ReturnToDoctor_UnknownRequest_Fails()
        {
            Assert.False((await Service.ReturnToDoctorAsync(999, TechnicianUserId, "Note")).Success);
        }

        [Fact]
        public async Task Resubmit_ReturnedRequest_GoesBackToRequested_WithTheNewTests_AndNotifiesTheLab()
        {
            var request = await CreateAsync(_cbc.Id);
            await Service.ReturnToDoctorAsync(request.Id, TechnicianUserId, "Add glucose instead.");
            _db.ChangeTracker.Clear();
            _time.Advance(TimeSpan.FromHours(1));

            var form = await Service.EditFormAsync(_doctor.Id, request.Id);
            Assert.NotNull(form);
            Assert.Equal("Add glucose instead.", form!.ReturnNote);
            Assert.Equal(new[] { _cbc.Id }, form.SelectedTestIds);
            form.SelectedTestIds = new List<int> { _glucose.Id };
            form.ClinicalNotes = "Fasting 12 hours";

            var result = await Service.ResubmitAsync(_doctor.Id, form);

            Assert.True(result.Success, result.Message);
            var saved = await ReloadAsync(request.Id);
            Assert.Equal(LabRequestStatus.Requested, saved.Status);
            Assert.Equal(_clock.Now, saved.RequestedAt);
            Assert.Equal("Fasting 12 hours", saved.ClinicalNotes);
            Assert.Equal(_glucose.Id, Assert.Single(saved.Items).LabTestId);
            Assert.Equal(2, (await NotificationsForAsync(TechnicianUserId)).Count);
        }

        [Fact]
        public async Task Resubmit_RequestThatWasNotReturned_IsRefused()
        {
            var request = await CreateAsync(_cbc.Id);
            var form = Form(_glucose.Id);
            form.Id = request.Id;

            var result = await Service.ResubmitAsync(_doctor.Id, form);

            Assert.False(result.Success);
            Assert.Equal(_cbc.Id, Assert.Single((await ReloadAsync(request.Id)).Items).LabTestId);
            Assert.Null(await Service.EditFormAsync(_doctor.Id, request.Id));
        }

        [Fact]
        public async Task Resubmit_AnotherDoctorsRequest_IsNotFound()
        {
            var request = await CreateAsync(_cbc.Id);
            await Service.ReturnToDoctorAsync(request.Id, TechnicianUserId, "Note");
            var form = Form(_cbc.Id);
            form.Id = request.Id;

            Assert.False((await Service.ResubmitAsync(_doctor.Id + 1, form)).Success);
            Assert.Null(await Service.EditFormAsync(_doctor.Id + 1, request.Id));
        }

        [Fact]
        public async Task Resubmit_WithoutTests_IsRefused()
        {
            var request = await CreateAsync(_cbc.Id);
            await Service.ReturnToDoctorAsync(request.Id, TechnicianUserId, "Note");
            var form = Form();
            form.Id = request.Id;

            Assert.False((await Service.ResubmitAsync(_doctor.Id, form)).Success);
            Assert.Equal(LabRequestStatus.ReturnedToDoctor, (await ReloadAsync(request.Id)).Status);
        }

        // ---- Other rules ----

        [Fact]
        public async Task Create_ForAPatientWithoutAppointment_IsRefused()
        {
            var (_, stranger) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            var model = Form(_cbc.Id);
            model.PatientId = stranger.Id;

            var result = await Service.CreateAsync(_doctor.Id, model);

            Assert.False(result.Success);
            Assert.Equal(LabRequestService.NoAccessError, result.Message);
        }

        [Fact]
        public async Task Create_WithAnInactiveOrUnknownTest_IsRefused()
        {
            var inactive = await _db.LabTests.SingleAsync(t => t.Id == _glucose.Id);
            inactive.IsActive = false;
            await _db.SaveChangesAsync();

            Assert.Equal(LabRequestService.InactiveTestError, (await Service.CreateAsync(_doctor.Id, Form(_cbc.Id, _glucose.Id))).Message);
            Assert.Equal(LabRequestService.InactiveTestError, (await Service.CreateAsync(_doctor.Id, Form(_cbc.Id, 999))).Message);
            Assert.Empty(await _db.LabRequests.ToListAsync());
        }

        [Fact]
        public async Task Create_DuplicateTestIds_AreStoredOnce()
        {
            var request = await CreateAsync(_cbc.Id, _cbc.Id);

            Assert.Single((await ReloadAsync(request.Id)).Items);
        }

        [Fact]
        public async Task Create_UnknownDoctor_Fails()
        {
            Assert.False((await Service.CreateAsync(999_999, Form(_cbc.Id))).Success);
        }

        [Fact]
        public async Task NewForm_ListsOnlyActiveTests()
        {
            var glucose = await _db.LabTests.SingleAsync(t => t.Id == _glucose.Id);
            glucose.IsActive = false;
            await _db.SaveChangesAsync();

            var form = await Service.NewFormAsync(_patient.Id);

            Assert.Equal(new[] { "CBC" }, form!.AvailableTests.Select(t => t.Name));
            Assert.False(string.IsNullOrWhiteSpace(form.PatientName));
        }

        [Fact]
        public async Task GetForDoctor_FiltersByPatient_NewestFirst()
        {
            var first = await CreateAsync(_cbc.Id);
            _time.Advance(TimeSpan.FromMinutes(5));
            var second = await CreateAsync(_glucose.Id);

            Assert.Equal(new[] { second.Id, first.Id }, (await Service.GetForDoctorAsync(_doctor.Id)).Select(r => r.Id));
            Assert.Equal(2, (await Service.GetForDoctorAsync(_doctor.Id, _patient.Id)).Count);
            Assert.Empty(await Service.GetForDoctorAsync(_doctor.Id, 999_999));
        }

        [Fact]
        public async Task GetDetails_IncludesPatientDoctorAndTests()
        {
            var request = await CreateAsync(_cbc.Id);

            var details = await Service.GetDetailsAsync(request.Id);

            Assert.NotNull(details!.Patient.User);
            Assert.NotNull(details.Doctor.User);
            Assert.Equal("CBC", Assert.Single(details.Items).LabTest.Name);
            Assert.Null(await Service.GetDetailsAsync(999));
        }

        // ---- TC-L6-03: a test used by a request is deactivated, not deleted ----

        [Fact]
        public async Task TC_L6_03_DeletingATestUsedByARequest_DeactivatesIt()
        {
            await CreateAsync(_cbc.Id);
            var catalogue = new LabTestService(_db, new LabRequestTestUsage(_db), _clock);

            var result = await catalogue.DeleteAsync(_cbc.Id);

            Assert.True(result.Success, result.Message);
            Assert.Equal(LabTestDeleteOutcome.Deactivated, result.Data);
            Assert.False((await _db.LabTests.AsNoTracking().SingleAsync(t => t.Id == _cbc.Id)).IsActive);
            Assert.Equal(LabTestDeleteOutcome.Deleted, (await catalogue.DeleteAsync(_glucose.Id)).Data);
        }

        // ---- The state machine ----

        [Theory]
        [InlineData(LabRequestStatus.Requested, LabRequestStatus.Received, true)]
        [InlineData(LabRequestStatus.Requested, LabRequestStatus.ReturnedToDoctor, true)]
        [InlineData(LabRequestStatus.ReturnedToDoctor, LabRequestStatus.Requested, true)]
        [InlineData(LabRequestStatus.Received, LabRequestStatus.InProgress, true)]
        [InlineData(LabRequestStatus.InProgress, LabRequestStatus.ResultIssued, true)]
        [InlineData(LabRequestStatus.Received, LabRequestStatus.ReturnedToDoctor, false)]
        [InlineData(LabRequestStatus.ReturnedToDoctor, LabRequestStatus.Received, false)]
        [InlineData(LabRequestStatus.Requested, LabRequestStatus.ResultIssued, false)]
        public void Workflow_AllowsOnlyTheAgreedTransitions(LabRequestStatus from, LabRequestStatus to, bool allowed)
        {
            Assert.Equal(allowed, LabRequestWorkflow.Machine.CanTransition(from, to));
        }

        [Fact]
        public void Workflow_ResultIssued_IsTerminal()
        {
            Assert.True(LabRequestWorkflow.Machine.IsTerminal(LabRequestStatus.ResultIssued));
        }
    }
}
