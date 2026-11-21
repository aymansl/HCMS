using System.Net;
using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
using HCMS4.Shared.Identity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    /// <summary>UC-L07 — the technician adds notes; the doctor sees them (TC-L7-01 … TC-L7-03).</summary>
    public class LabNotePagesTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public LabNotePagesTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(string DoctorUserId, string TechnicianUserId, int RequestId)> SeedAsync(LabRequestStatus status)
        {
            (string, string, int) result = default;
            await _factory.WithDbAsync(async db =>
            {
                var (doctorUser, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var technician = new ApplicationUser
                {
                    Id = $"note-lab-{Guid.NewGuid():N}", UserName = $"{Guid.NewGuid():N}@test.local", FirstName = "Lina", LastName = "Notes"
                };
                db.Users.Add(technician);
                var request = new LabRequest
                {
                    PatientId = patient.Id, DoctorId = doctor.Id, Status = status,
                    Items = { new LabRequestItem { LabTest = new LabTest { Name = $"N-{Guid.NewGuid():N}"[..16], Unit = "g/dL", Price = 100 }, Price = 100 } }
                };
                db.LabRequests.Add(request);
                await db.SaveChangesAsync();
                result = (doctorUser.Id, technician.Id, request.Id);
            });
            return result;
        }

        private static Task<HttpResponseMessage> AddNoteAsync(HttpClient client, int id, string text, bool urgent)
        {
            var fields = new Dictionary<string, string> { ["Text"] = text };
            if (urgent)
                fields["IsUrgent"] = "true";
            return FormHelper.PostFormAsync(client, $"/Lab/RequestDetails/{id}", $"/Lab/AddNote/{id}", fields);
        }

        private async Task<List<LabNote>> NotesAsync(int requestId)
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LabNotes.AsNoTracking()
                .Where(n => n.LabRequestId == requestId).ToListAsync();
        }

        private static async Task<string> TextAsync(HttpResponseMessage response) =>
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        [Fact]
        public async Task TC_L7_01_TheTechnicianAddsANote_AndTheDoctorSeesItWithTheAuthor()
        {
            var (doctorUserId, technicianUserId, id) = await SeedAsync(LabRequestStatus.Received);
            var lab = _factory.CreateClientAs("LabTechnician", technicianUserId);

            var response = await AddNoteAsync(lab, id, "Sample received at 09:40.", urgent: false);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("Sample received at 09:40.", await TextAsync(await lab.GetAsync($"/Lab/RequestDetails/{id}")));
            var doctor = _factory.CreateClientAs("Doctor", doctorUserId);
            Assert.Contains($"/Doctor/LabRequestDetails/{id}", await TextAsync(await doctor.GetAsync("/Doctor/LabRequests")));
            var details = await TextAsync(await doctor.GetAsync($"/Doctor/LabRequestDetails/{id}"));
            Assert.Contains("id=\"lab-notes-list\"", details);
            Assert.Contains("Sample received at 09:40.", details);
            Assert.Contains("Lina Notes", details);
        }

        [Fact]
        public async Task TC_L7_02_AnUrgentNote_ReachesTheDoctorsNotifications()
        {
            var (doctorUserId, technicianUserId, id) = await SeedAsync(LabRequestStatus.InProgress);

            await AddNoteAsync(_factory.CreateClientAs("LabTechnician", technicianUserId), id, "Please call the lab.", urgent: true);

            var notifications = await TextAsync(await _factory.CreateClientAs("Doctor", doctorUserId).GetAsync("/Notifications"));
            Assert.Contains("Urgent lab note", notifications);
            Assert.Contains("Please call the lab.", notifications);
            Assert.True(Assert.Single(await NotesAsync(id)).IsUrgent);
        }

        [Fact]
        public async Task TC_L7_03_AnEmptyNote_ShowsTheError_AndSavesNothing()
        {
            var (_, technicianUserId, id) = await SeedAsync(LabRequestStatus.Received);
            var lab = _factory.CreateClientAs("LabTechnician", technicianUserId);

            var response = await AddNoteAsync(lab, id, "", urgent: false);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains(LabRequestService.EmptyNoteError, await TextAsync(await lab.GetAsync($"/Lab/RequestDetails/{id}")));
            Assert.Empty(await NotesAsync(id));
        }

        [Fact]
        public async Task ACancelledRequest_HasNoNoteForm_AndRefusesNotes()
        {
            var (_, technicianUserId, id) = await SeedAsync(LabRequestStatus.Cancelled);
            var lab = _factory.CreateClientAs("LabTechnician", technicianUserId);

            Assert.DoesNotContain("/Lab/AddNote/", await TextAsync(await lab.GetAsync($"/Lab/RequestDetails/{id}")));
            await AddNoteAsync(lab, id, "Late note", urgent: false);

            Assert.Empty(await NotesAsync(id));
        }

        [Fact]
        public async Task DoctorDetails_OfAnotherDoctorsRequest_IsNotFound_AndOtherRolesAreForbidden()
        {
            var (_, _, id) = await SeedAsync(LabRequestStatus.Received);
            var (otherDoctorUserId, _, _) = await SeedAsync(LabRequestStatus.Received);

            Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClientAs("Doctor", otherDoctorUserId).GetAsync($"/Doctor/LabRequestDetails/{id}")).StatusCode);
            foreach (var role in new[] { "Admin", "Patient", "Pharmacist", "LabTechnician" })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs(role).GetAsync($"/Doctor/LabRequestDetails/{id}")).StatusCode);
            }
        }
    }
}
