using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
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

namespace HCMS4.Tests
{
    /// <summary>UC-L05 — Receive a sample-collection booking from the patient (TC-L5-01 … TC-L5-03).</summary>
    public class LabAppointmentServiceTests : IDisposable
    {
        private const string TechnicianUserId = "tech-user";

        // Thursday 2026-11-19, 09:00 UTC = 12:00 Damascus. Friday 20 is closed; Saturday 21 is open.
        private static readonly DateOnly Thursday = new(2026, 11, 19);
        private static readonly DateOnly Friday = new(2026, 11, 20);
        private static readonly DateOnly Saturday = new(2026, 11, 21);

        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero));
        private readonly ClinicClock _clock;

        private Doctor _doctor = null!;
        private ApplicationUser _patientUser = null!;
        private Patient _patient = null!;
        private ApplicationUser _otherPatientUser = null!;
        private Patient _otherPatient = null!;
        private LabTest _cbc = null!;

        public LabAppointmentServiceTests()
        {
            _clock = new ClinicClock(_time, ClinicTime.TimeZone);
            SeedAsync().GetAwaiter().GetResult();
        }

        public void Dispose() => _db.Dispose();

        private LabAppointmentService Service => new(_db, new NotificationService(_db), _clock);

        private static DateTime At(DateOnly day, int hour, int minute) => day.ToDateTime(new TimeOnly(hour, minute));

        private async Task SeedAsync()
        {
            (_, _doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            (_patientUser, _patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            (_otherPatientUser, _otherPatient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            _cbc = new LabTest { Name = "CBC", Unit = "10^9/L", Price = 5000 };
            _db.LabTests.Add(_cbc);

            var role = new IdentityRole(Roles.LabTechnician) { Id = "role-lab", NormalizedName = Roles.LabTechnician.ToUpperInvariant() };
            _db.Roles.Add(role);
            _db.Users.Add(new ApplicationUser { Id = TechnicianUserId, UserName = "tech@test.local", FirstName = "Lina", LastName = "Lab" });
            _db.UserRoles.Add(new IdentityUserRole<string> { RoleId = role.Id, UserId = TechnicianUserId });
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }

        private async Task<LabRequest> RequestAsync(Patient? patient = null, LabRequestStatus status = LabRequestStatus.Requested)
        {
            var request = new LabRequest
            {
                PatientId = (patient ?? _patient).Id, DoctorId = _doctor.Id, Status = status, RequestedAt = _clock.Now,
                Items = { new LabRequestItem { LabTestId = _cbc.Id, Price = 5000 } }
            };
            _db.LabRequests.Add(request);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
            return request;
        }

        private async Task<LabAppointment> BookAsync(LabRequest request, DateTime slot, ApplicationUser? user = null)
        {
            var result = await Service.BookAsync((user ?? _patientUser).Id, request.Id, slot);
            Assert.True(result.Success, result.Message);
            _db.ChangeTracker.Clear();
            return result.Data;
        }

        private Task<List<UserNotification>> NotificationsForAsync(string userId) =>
            _db.UserNotifications.AsNoTracking().Where(n => n.UserId == userId).ToListAsync();

        // ---- Slot rules ----

        [Fact]
        public void Schedule_HasTwentyFourSlotsFrom0800To1345_AndNoneOnFriday()
        {
            var slots = LabSchedule.SlotsFor(Saturday).ToList();

            Assert.Equal(24, slots.Count);
            Assert.Equal(At(Saturday, 8, 0), slots[0]);
            Assert.Equal(At(Saturday, 13, 45), slots[^1]);
            Assert.Empty(LabSchedule.SlotsFor(Friday));
        }

        [Theory]
        [InlineData(8, 0, true)]
        [InlineData(13, 45, true)]
        [InlineData(7, 45, false)]
        [InlineData(14, 0, false)]
        [InlineData(8, 10, false)]
        public void IsValidSlot_ChecksHoursAndQuarterHours(int hour, int minute, bool valid)
        {
            Assert.Equal(valid, LabSchedule.IsValidSlot(At(Saturday, hour, minute)));
        }

        [Fact]
        public void IsValidSlot_RefusesFridayAndSeconds()
        {
            Assert.False(LabSchedule.IsValidSlot(At(Friday, 9, 0)));
            Assert.False(LabSchedule.IsValidSlot(At(Saturday, 9, 0).AddSeconds(30)));
        }

        // ---- TC-L5-01: book a free slot ----

        [Fact]
        public async Task TC_L5_01_BookAFreeSlot_SavesIt_NotifiesTheLab_AndConfirmsToThePatient()
        {
            var request = await RequestAsync();

            var appointment = await BookAsync(request, At(Saturday, 8, 15));

            var saved = await _db.LabAppointments.AsNoTracking().SingleAsync();
            Assert.Equal(At(Saturday, 8, 15), saved.SlotStart);
            Assert.Equal(LabAppointmentStatus.Booked, saved.Status);
            Assert.Equal(_clock.Now, saved.BookedAt);
            Assert.Equal(request.Id, saved.LabRequestId);

            var lab = Assert.Single(await NotificationsForAsync(TechnicianUserId));
            Assert.Equal("New lab appointment", lab.Title);
            Assert.Equal("/Lab/Schedule?date=2026-11-21", lab.LinkUrl);
            var patient = Assert.Single(await NotificationsForAsync(_patientUser.Id));
            Assert.Equal("Lab appointment confirmed", patient.Title);
            Assert.Contains("Saturday 2026-11-21 08:15", patient.Message);

            var schedule = await Service.GetScheduleAsync(Saturday);
            Assert.Equal(appointment.Id, Assert.Single(schedule).Id);
            Assert.NotNull(schedule[0].LabRequest.Patient.User);
        }

        // ---- TC-L5-02: the slot is already taken ----

        [Fact]
        public async Task TC_L5_02_SecondBookingOfTheSameSlot_IsRefused_AndAskedToChooseAnother()
        {
            await BookAsync(await RequestAsync(), At(Saturday, 9, 0));
            var other = await RequestAsync(_otherPatient);

            var result = await Service.BookAsync(_otherPatientUser.Id, other.Id, At(Saturday, 9, 0));

            Assert.False(result.Success);
            Assert.Equal(LabAppointmentService.SlotTakenError, result.Message);
            Assert.Single(await _db.LabAppointments.ToListAsync());
            Assert.DoesNotContain(At(Saturday, 9, 0), await Service.GetFreeSlotsAsync(Saturday));
            Assert.Empty(await NotificationsForAsync(_otherPatientUser.Id));
        }

        // ---- TC-L5-03: change the booking ----

        [Fact]
        public async Task TC_L5_03_ChangeTheBooking_MovesIt_FreesTheOldSlot_AndNotifiesTheLab()
        {
            var request = await RequestAsync();
            var first = await BookAsync(request, At(Saturday, 8, 0));
            _time.Advance(TimeSpan.FromMinutes(10));

            var result = await Service.BookAsync(_patientUser.Id, request.Id, At(Saturday, 10, 30));

            Assert.True(result.Success, result.Message);
            var saved = await _db.LabAppointments.AsNoTracking().SingleAsync();
            Assert.Equal(first.Id, saved.Id);
            Assert.Equal(At(Saturday, 10, 30), saved.SlotStart);
            Assert.Equal(_clock.Now, saved.UpdatedAt);
            Assert.Contains(At(Saturday, 8, 0), await Service.GetFreeSlotsAsync(Saturday));
            Assert.Equal(At(Saturday, 10, 30), Assert.Single(await Service.GetScheduleAsync(Saturday)).SlotStart);
            Assert.Contains(await NotificationsForAsync(TechnicianUserId), n => n.Title == "Lab appointment changed");
        }

        [Fact]
        public async Task Change_ToATakenSlot_IsRefused_AndKeepsTheOldTime()
        {
            await BookAsync(await RequestAsync(_otherPatient), At(Saturday, 11, 0), _otherPatientUser);
            var request = await RequestAsync();
            await BookAsync(request, At(Saturday, 8, 0));

            var result = await Service.BookAsync(_patientUser.Id, request.Id, At(Saturday, 11, 0));

            Assert.Equal(LabAppointmentService.SlotTakenError, result.Message);
            Assert.Equal(At(Saturday, 8, 0), (await _db.LabAppointments.AsNoTracking().SingleAsync(a => a.LabRequestId == request.Id)).SlotStart);
        }

        [Fact]
        public async Task Booking_TheSameTimeAgain_ChangesNothing()
        {
            var request = await RequestAsync();
            await BookAsync(request, At(Saturday, 8, 0));

            var result = await Service.BookAsync(_patientUser.Id, request.Id, At(Saturday, 8, 0));

            Assert.True(result.Success);
            Assert.Equal(2, (await NotificationsForAsync(TechnicianUserId)).Count + (await NotificationsForAsync(_patientUser.Id)).Count);
        }

        // ---- Refusals ----

        public static IEnumerable<object[]> InvalidSlots() => new[]
        {
            new object[] { At(Friday, 9, 0) },
            new object[] { At(Saturday, 7, 45) },
            new object[] { At(Saturday, 14, 0) },
            new object[] { At(Saturday, 8, 10) },
            new object[] { At(Thursday, 11, 45) },              // already past (now is 12:00)
            new object[] { At(Thursday, 12, 0) },               // now
            new object[] { At(Thursday.AddDays(15), 9, 0) },    // beyond the 14-day limit
        };

        [Theory]
        [MemberData(nameof(InvalidSlots))]
        public async Task Book_AnInvalidSlot_IsRefused(DateTime slot)
        {
            var request = await RequestAsync();

            var result = await Service.BookAsync(_patientUser.Id, request.Id, slot);

            Assert.False(result.Success);
            Assert.Equal(LabAppointmentService.InvalidSlotError, result.Message);
            Assert.Empty(await _db.LabAppointments.ToListAsync());
        }

        [Fact]
        public async Task Book_TheLastDayOfTheLimit_IsAllowed()
        {
            var lastDay = Thursday.AddDays(LabSchedule.BookingDays); // Thursday 2026-12-03
            Assert.True((await Service.BookAsync(_patientUser.Id, (await RequestAsync()).Id, At(lastDay, 13, 45))).Success);
        }

        [Fact]
        public async Task Book_AnotherPatientsRequest_IsNotFound()
        {
            var request = await RequestAsync(_otherPatient);

            Assert.False((await Service.BookAsync(_patientUser.Id, request.Id, At(Saturday, 9, 0))).Success);
            Assert.Null(await Service.GetBookingFormAsync(_patientUser.Id, request.Id, null));
        }

        [Theory]
        [InlineData(LabRequestStatus.InProgress)]
        [InlineData(LabRequestStatus.ResultIssued)]
        [InlineData(LabRequestStatus.Cancelled)]
        [InlineData(LabRequestStatus.ReturnedToDoctor)]
        public async Task Book_ARequestThatIsNotWaitingForItsSample_IsRefused(LabRequestStatus status)
        {
            var request = await RequestAsync(status: status);

            var result = await Service.BookAsync(_patientUser.Id, request.Id, At(Saturday, 9, 0));

            Assert.Equal(LabAppointmentService.NotBookableError, result.Message);
        }

        [Fact]
        public async Task Book_AReceivedRequest_IsAllowed()
        {
            Assert.True((await Service.BookAsync(_patientUser.Id, (await RequestAsync(status: LabRequestStatus.Received)).Id, At(Saturday, 9, 0))).Success);
        }

        // ---- Free slots and the booking page ----

        [Fact]
        public async Task FreeSlots_Today_AreOnlyTheOnesStillAhead()
        {
            var slots = await Service.GetFreeSlotsAsync(Thursday);

            Assert.Equal(At(Thursday, 12, 15), slots[0]);
            Assert.Equal(7, slots.Count);
            Assert.Empty(await Service.GetFreeSlotsAsync(Friday));
            Assert.Empty(await Service.GetFreeSlotsAsync(Thursday.AddDays(-1)));
        }

        [Fact]
        public async Task BookingForm_OffersTheNext14DaysWithoutFridays_StartingToday()
        {
            var request = await RequestAsync();

            var form = await Service.GetBookingFormAsync(_patientUser.Id, request.Id, null);

            Assert.NotNull(form);
            Assert.Equal(Thursday, form!.SelectedDate);
            Assert.Equal(Thursday, form.BookableDates[0]);
            Assert.Equal(Thursday.AddDays(LabSchedule.BookingDays), form.BookableDates[^1]);
            Assert.DoesNotContain(form.BookableDates, d => d.DayOfWeek == DayOfWeek.Friday);
            Assert.Equal(13, form.BookableDates.Count); // 15 days, two Fridays
            Assert.Equal(7, form.FreeSlots.Count);
            Assert.Null(form.Appointment);
        }

        [Fact]
        public async Task BookingForm_ShowsTheChosenDay_AndTheCurrentBooking()
        {
            var request = await RequestAsync();
            await BookAsync(request, At(Saturday, 8, 0));

            var form = await Service.GetBookingFormAsync(_patientUser.Id, request.Id, Saturday);

            Assert.Equal(Saturday, form!.SelectedDate);
            Assert.Equal(At(Saturday, 8, 0), form.Appointment!.SlotStart);
            Assert.Equal(23, form.FreeSlots.Count);
            Assert.Equal(Thursday, (await Service.GetBookingFormAsync(_patientUser.Id, request.Id, Friday))!.SelectedDate);
        }

        [Fact]
        public async Task BookingForm_WhenTodayIsOver_StartsTomorrowOrLater()
        {
            _time.Advance(TimeSpan.FromHours(2)); // 14:00 Thursday: no slot left today; Friday closed
            var form = await Service.GetBookingFormAsync(_patientUser.Id, (await RequestAsync()).Id, null);

            Assert.Equal(Saturday, form!.SelectedDate);
        }

        [Fact]
        public async Task PatientRequests_ShowTheirBookingAndWhetherTheyCanBook()
        {
            var booked = await RequestAsync();
            await BookAsync(booked, At(Saturday, 8, 0));
            await RequestAsync(status: LabRequestStatus.InProgress);
            await RequestAsync(_otherPatient);

            var rows = await Service.GetPatientRequestsAsync(_patientUser.Id);

            Assert.Equal(2, rows.Count);
            var bookedRow = Assert.Single(rows, r => r.Request.Id == booked.Id);
            Assert.True(bookedRow.CanBook);
            Assert.Equal(At(Saturday, 8, 0), bookedRow.Appointment!.SlotStart);
            Assert.False(Assert.Single(rows, r => r.Request.Id != booked.Id).CanBook);
            Assert.NotNull(rows[0].Request.Doctor.User);
        }

        [Fact]
        public async Task Schedule_IsOrderedByTime_AndOnlyForThatDay()
        {
            await BookAsync(await RequestAsync(), At(Saturday, 10, 0));
            await BookAsync(await RequestAsync(), At(Saturday, 8, 30));
            await BookAsync(await RequestAsync(), At(Saturday.AddDays(1), 8, 30));

            Assert.Equal(new[] { At(Saturday, 8, 30), At(Saturday, 10, 0) }, (await Service.GetScheduleAsync(Saturday)).Select(a => a.SlotStart));
        }

        // ---- UC-L04 + UC-L05: a cancelled request frees its slot ----

        [Fact]
        public async Task CancellingTheRequest_FreesItsSlot_AndRemovesItFromTheSchedule()
        {
            var appointmentDb = _db;
            _db.Appointments.Add(new Appointment
            {
                DoctorId = _doctor.Id, PatientId = _patient.Id, AppointmentDateTime = _clock.Now.AddDays(-1),
                Status = AppointmentStatus.Completed, ConsultationFee = 50
            });
            await _db.SaveChangesAsync();
            var request = await RequestAsync();
            await BookAsync(request, At(Saturday, 9, 0));
            var requests = new LabRequestService(appointmentDb, new MedicalRecordService(appointmentDb, _clock), new NotificationService(appointmentDb), _clock);

            Assert.True((await requests.CancelAsync(_doctor.Id, request.Id, "Not needed")).Success);

            Assert.Equal(LabAppointmentStatus.Cancelled, (await _db.LabAppointments.AsNoTracking().SingleAsync()).Status);
            Assert.Empty(await Service.GetScheduleAsync(Saturday));
            Assert.Contains(At(Saturday, 9, 0), await Service.GetFreeSlotsAsync(Saturday));
            Assert.True((await Service.BookAsync(_otherPatientUser.Id, (await RequestAsync(_otherPatient)).Id, At(Saturday, 9, 0))).Success);
        }
    }
}
