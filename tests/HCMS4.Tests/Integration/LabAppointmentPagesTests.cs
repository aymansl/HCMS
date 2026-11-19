using System.Globalization;
using System.Net;
using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    /// <summary>UC-L05 — the patient's booking pages and the lab's schedule (TC-L5-01 … TC-L5-03).</summary>
    public class LabAppointmentPagesTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public LabAppointmentPagesTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        /// <summary>The next Saturday (always within the booking limit) at the given time.</summary>
        private static DateTime NextSaturdayAt(int hour, int minute)
        {
            var day = DateOnly.FromDateTime(ClinicTime.Today).AddDays(1);
            while (day.DayOfWeek != DayOfWeek.Saturday)
                day = day.AddDays(1);
            return day.ToDateTime(new TimeOnly(hour, minute));
        }

        private static string Iso(DateTime slot) => slot.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        private static string Day(DateTime slot) => slot.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private async Task<(string PatientUserId, int RequestId, string PatientName)> SeedAsync(LabRequestStatus status = LabRequestStatus.Requested)
        {
            (string, int, string) result = default;
            await _factory.WithDbAsync(async db =>
            {
                var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (patientUser, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var test = new LabTest { Name = $"L5-{Guid.NewGuid():N}"[..16], Unit = "g/dL", Price = 100 };
                var request = new LabRequest
                {
                    PatientId = patient.Id, DoctorId = doctor.Id, Status = status,
                    Items = { new LabRequestItem { LabTest = test, Price = 100 } }
                };
                db.LabRequests.Add(request);
                await db.SaveChangesAsync();
                result = (patientUser.Id, request.Id, patientUser.FullName);
            });
            return result;
        }

        private static Task<HttpResponseMessage> BookAsync(HttpClient client, int requestId, DateTime slot) =>
            FormHelper.PostFormAsync(client, $"/Patient/BookLabAppointment?requestId={requestId}", "/Patient/BookLabAppointment",
                new Dictionary<string, string> { ["requestId"] = requestId.ToString(CultureInfo.InvariantCulture), ["slot"] = Iso(slot) });

        private async Task<LabAppointment?> AppointmentAsync(int requestId)
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LabAppointments.AsNoTracking()
                .FirstOrDefaultAsync(a => a.LabRequestId == requestId && a.Status == LabAppointmentStatus.Booked);
        }

        private static async Task<string> TextAsync(HttpResponseMessage response) =>
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        [Fact]
        public async Task TC_L5_01_PatientBooksAFreeSlot_AndTheLabSeesItInTheSchedule()
        {
            var (userId, requestId, name) = await SeedAsync();
            var patient = _factory.CreateClientAs("Patient", userId);
            var slot = NextSaturdayAt(8, 0);

            var list = await TextAsync(await patient.GetAsync("/Patient/LabRequests"));
            Assert.Contains($"/Patient/BookLabAppointment?requestId={requestId}", list);
            var form = await TextAsync(await patient.GetAsync($"/Patient/BookLabAppointment?requestId={requestId}&date={Day(slot)}"));
            Assert.Contains($"value=\"{Iso(slot)}\"", form);

            var response = await BookAsync(patient, requestId, slot);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Patient/LabRequests", response.Headers.Location?.OriginalString);
            Assert.Equal(slot, (await AppointmentAsync(requestId))!.SlotStart);

            var schedule = await TextAsync(await _factory.CreateClientAs("LabTechnician", "schedule-lab").GetAsync($"/Lab/Schedule?date={Day(slot)}"));
            Assert.Contains("id=\"lab-schedule\"", schedule);
            Assert.Contains(name, schedule);
            Assert.Contains($"#{requestId}", schedule);
        }

        [Fact]
        public async Task TC_L5_02_SecondPatient_GetsTheSlotTakenMessage()
        {
            var slot = NextSaturdayAt(9, 0);
            var first = await SeedAsync();
            Assert.Equal(HttpStatusCode.Redirect, (await BookAsync(_factory.CreateClientAs("Patient", first.PatientUserId), first.RequestId, slot)).StatusCode);
            var second = await SeedAsync();
            var client = _factory.CreateClientAs("Patient", second.PatientUserId);

            var response = await BookAsync(client, second.RequestId, slot);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Patient/BookLabAppointment", response.Headers.Location?.OriginalString);
            Assert.Null(await AppointmentAsync(second.RequestId));
            var page = await TextAsync(await client.GetAsync(response.Headers.Location!.OriginalString));
            Assert.Contains(LabAppointmentService.SlotTakenError, page);
            Assert.DoesNotContain($"value=\"{Iso(slot)}\"", page);
        }

        [Fact]
        public async Task TC_L5_03_PatientChangesTheTime_AndTheScheduleShowsTheNewTime()
        {
            var (userId, requestId, _) = await SeedAsync();
            var patient = _factory.CreateClientAs("Patient", userId);
            await BookAsync(patient, requestId, NextSaturdayAt(10, 0));

            var form = await TextAsync(await patient.GetAsync($"/Patient/BookLabAppointment?requestId={requestId}"));
            Assert.Contains("id=\"current-appointment\"", form);
            var response = await BookAsync(patient, requestId, NextSaturdayAt(12, 30));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(NextSaturdayAt(12, 30), (await AppointmentAsync(requestId))!.SlotStart);
            var schedule = await TextAsync(await _factory.CreateClientAs("LabTechnician", "schedule-lab").GetAsync($"/Lab/Schedule?date={Day(NextSaturdayAt(0, 0))}"));
            Assert.Contains("12:30", schedule);
            Assert.Contains("Change", await TextAsync(await patient.GetAsync("/Patient/LabRequests")));
        }

        [Fact]
        public async Task BookingPage_OfAnotherPatientsRequest_IsNotFound()
        {
            var (_, requestId, _) = await SeedAsync();
            var (otherUserId, _, _) = await SeedAsync();

            var response = await _factory.CreateClientAs("Patient", otherUserId).GetAsync($"/Patient/BookLabAppointment?requestId={requestId}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Schedule_OnAFriday_SaysTheLabIsClosed()
        {
            var friday = NextSaturdayAt(0, 0).AddDays(-1);

            var html = await TextAsync(await _factory.CreateClientAs("LabTechnician", "schedule-lab").GetAsync($"/Lab/Schedule?date={Day(friday)}"));

            Assert.Contains("does not collect samples on Fridays", html);
        }

        [Fact]
        public async Task Dashboard_LinksToTheSchedule()
        {
            var html = await TextAsync(await _factory.CreateClientAs("LabTechnician", "schedule-lab").GetAsync("/Lab/Dashboard"));

            Assert.Contains("id=\"lab-today-appointments\"", html);
            Assert.Contains("href=\"/Lab/Schedule\"", html);
        }

        [Fact]
        public async Task OnlyPatientsBook_AndOnlyTheLabSeesTheSchedule()
        {
            foreach (var role in new[] { "Admin", "Doctor", "Pharmacist", "LabTechnician" })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs(role).GetAsync("/Patient/LabRequests")).StatusCode);
            }
            foreach (var role in new[] { "Admin", "Doctor", "Patient", "Pharmacist" })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs(role).GetAsync("/Lab/Schedule")).StatusCode);
            }
        }
    }
}
