using System.Net;
using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    /// <summary>UC-L04 — the doctor's cancel page and the lab's view of a cancelled request (TC-L4-01, TC-L4-02).</summary>
    public class LabRequestCancellationTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public LabRequestCancellationTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(string DoctorUserId, int RequestId)> SeedAsync(LabRequestStatus status)
        {
            (string, int) result = default;
            await _factory.WithDbAsync(async db =>
            {
                var (doctorUser, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                db.Appointments.Add(new Appointment
                {
                    DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = ClinicTime.Now.AddDays(-1),
                    Status = AppointmentStatus.Completed, ConsultationFee = 50
                });
                var test = new LabTest { Name = $"L4-{Guid.NewGuid():N}"[..16], Unit = "g/dL", Price = 100 };
                var request = new LabRequest
                {
                    PatientId = patient.Id, DoctorId = doctor.Id, Status = status,
                    Items = { new LabRequestItem { LabTest = test, Price = 100 } }
                };
                db.LabRequests.Add(request);
                await db.SaveChangesAsync();
                result = (doctorUser.Id, request.Id);
            });
            return result;
        }

        private async Task<LabRequest> ReloadAsync(int id)
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .LabRequests.AsNoTracking().SingleAsync(r => r.Id == id);
        }

        private static async Task<string> TextAsync(HttpResponseMessage response) =>
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        private static Task<HttpResponseMessage> CancelAsync(HttpClient client, int id, string reason) =>
            FormHelper.PostFormAsync(client, $"/Doctor/CancelLabRequest/{id}", $"/Doctor/CancelLabRequest/{id}",
                new Dictionary<string, string> { ["Reason"] = reason });

        [Fact]
        public async Task TC_L4_01_DoctorCancels_TheLabSeesIt_AndAddsANote()
        {
            var (doctorUserId, id) = await SeedAsync(LabRequestStatus.Received);
            var doctor = _factory.CreateClientAs("Doctor", doctorUserId);

            var list = await TextAsync(await doctor.GetAsync("/Doctor/LabRequests"));
            Assert.Contains($"/Doctor/CancelLabRequest/{id}", list);

            var cancel = await CancelAsync(doctor, id, "Patient was transferred.");
            Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
            var saved = await ReloadAsync(id);
            Assert.Equal(LabRequestStatus.Cancelled, saved.Status);
            Assert.Equal("Patient was transferred.", saved.CancelReason);

            var lab = _factory.CreateClientAs("LabTechnician", "cancel-lab");
            var details = await TextAsync(await lab.GetAsync($"/Lab/RequestDetails/{id}"));
            Assert.Contains("id=\"request-cancelled\"", details);
            Assert.Contains("Patient was transferred.", details);
            Assert.DoesNotContain("Confirm Receipt", details);

            var note = await FormHelper.PostFormAsync(lab, $"/Lab/RequestDetails/{id}", $"/Lab/AddCancellationNote/{id}",
                new Dictionary<string, string> { ["Note"] = "Sample taken - request cancelled" });
            Assert.Equal(HttpStatusCode.Redirect, note.StatusCode);
            Assert.Equal("Sample taken - request cancelled", (await ReloadAsync(id)).CancellationNote);
            Assert.Contains("id=\"request-cancellation-note\"", await TextAsync(await lab.GetAsync($"/Lab/RequestDetails/{id}")));
        }

        [Fact]
        public async Task TC_L4_01_TheTechnicianSeesTheCancellationNotification_OnTheLabDashboard()
        {
            // Regression: the notification was saved but no page showed it to the lab technician.
            var technicianUserId = $"lab-{Guid.NewGuid():N}";
            await _factory.WithDbAsync(async db =>
            {
                var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == "LabTechnician");
                if (role == null)
                {
                    role = new Microsoft.AspNetCore.Identity.IdentityRole("LabTechnician") { NormalizedName = "LABTECHNICIAN" };
                    db.Roles.Add(role);
                }
                db.Users.Add(new HCMS4.Shared.Identity.Models.ApplicationUser
                {
                    Id = technicianUserId, UserName = $"{technicianUserId}@test.local", FirstName = "Lina", LastName = "Lab"
                });
                db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { RoleId = role.Id, UserId = technicianUserId });
                await db.SaveChangesAsync();
            });
            var (doctorUserId, id) = await SeedAsync(LabRequestStatus.Received);

            await CancelAsync(_factory.CreateClientAs("Doctor", doctorUserId), id, "Patient was transferred.");

            var dashboard = await TextAsync(await _factory.CreateClientAs("LabTechnician", technicianUserId).GetAsync("/Lab/Dashboard"));
            Assert.Contains("id=\"lab-notifications\"", dashboard);
            Assert.Contains("Lab request cancelled", dashboard);
            Assert.Contains("Patient was transferred.", dashboard);
            Assert.Contains($"href=\"/Lab/RequestDetails/{id}\"", dashboard);
        }

        [Fact]
        public async Task TC_L4_02_CancelAfterTheResult_ShowsTheMessage_AndKeepsTheStatus()
        {
            var (doctorUserId, id) = await SeedAsync(LabRequestStatus.ResultIssued);
            var doctor = _factory.CreateClientAs("Doctor", doctorUserId);

            var response = await CancelAsync(doctor, id, "Too late");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(LabRequestService.ResultIssuedError, await TextAsync(response));
            Assert.Equal(LabRequestStatus.ResultIssued, (await ReloadAsync(id)).Status);
        }

        [Fact]
        public async Task Cancel_WithoutAReason_ShowsTheValidationError()
        {
            var (doctorUserId, id) = await SeedAsync(LabRequestStatus.Requested);

            var response = await CancelAsync(_factory.CreateClientAs("Doctor", doctorUserId), id, "");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Write the reason for the cancellation.", await TextAsync(response));
            Assert.Equal(LabRequestStatus.Requested, (await ReloadAsync(id)).Status);
        }

        [Fact]
        public async Task CancelPage_OfAnotherDoctorsRequest_IsNotFound()
        {
            var (_, id) = await SeedAsync(LabRequestStatus.Requested);
            var (otherDoctorUserId, _) = await SeedAsync(LabRequestStatus.Requested);

            var response = await _factory.CreateClientAs("Doctor", otherDoctorUserId).GetAsync($"/Doctor/CancelLabRequest/{id}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
