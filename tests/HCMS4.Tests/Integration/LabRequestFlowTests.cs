using System.Globalization;
using System.Net;
using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    /// <summary>UC-L03 — the doctor and lab pages (TC-L3-01 … TC-L3-03, access).</summary>
    public class LabRequestFlowTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public LabRequestFlowTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed record Setup(string DoctorUserId, int PatientId, int TestId, string TestName);

        private async Task<Setup> SetupAsync()
        {
            Setup? setup = null;
            await _factory.WithDbAsync(async db =>
            {
                var (doctorUser, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                db.Appointments.Add(new Appointment
                {
                    DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = ClinicTime.Now.AddDays(-1),
                    Status = AppointmentStatus.Completed, ConsultationFee = 50
                });
                var test = new LabTest { Name = $"CBC-{Guid.NewGuid():N}"[..16], Unit = "10^9/L", ReferenceMin = 4, ReferenceMax = 11, Price = 5000 };
                db.LabTests.Add(test);
                await db.SaveChangesAsync();
                setup = new Setup(doctorUser.Id, patient.Id, test.Id, test.Name);
            });
            return setup!;
        }

        private HttpClient Doctor(Setup s) => _factory.CreateClientAs("Doctor", s.DoctorUserId);
        private HttpClient Technician() => _factory.CreateClientAs("LabTechnician", "flow-lab");

        private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

        private static Task<HttpResponseMessage> SendRequestAsync(HttpClient client, Setup s, params int[] testIds)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                new("PatientId", Id(s.PatientId)),
                new("ClinicalNotes", "Fatigue")
            };
            fields.AddRange(testIds.Select(t => new KeyValuePair<string, string>("SelectedTestIds", Id(t))));
            return PostAsync(client, $"/Doctor/CreateLabRequest?patientId={s.PatientId}", "/Doctor/CreateLabRequest", fields);
        }

        // FormHelper takes a dictionary; the test checkboxes need repeated keys.
        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string formUrl, string postUrl,
            List<KeyValuePair<string, string>> fields)
        {
            var token = await FormHelper.GetAntiforgeryTokenAsync(client, formUrl);
            fields.Add(new("__RequestVerificationToken", token));
            return await client.PostAsync(postUrl, new FormUrlEncodedContent(fields));
        }

        private async Task<LabRequest?> LatestRequestAsync(Setup s)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.LabRequests.AsNoTracking().Where(r => r.PatientId == s.PatientId)
                .OrderByDescending(r => r.Id).FirstOrDefaultAsync();
        }

        private static async Task<string> TextAsync(HttpResponseMessage response) =>
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        [Fact]
        public async Task TC_L3_01_DoctorSends_LabSeesIt_AndConfirmsReceipt()
        {
            var s = await SetupAsync();

            var sent = await SendRequestAsync(Doctor(s), s, s.TestId);
            Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
            var request = await LatestRequestAsync(s);
            Assert.NotNull(request);
            Assert.Equal(LabRequestStatus.Requested, request!.Status);

            var lab = Technician();
            var dashboard = await TextAsync(await lab.GetAsync("/Lab/Dashboard"));
            Assert.Contains($"/Lab/RequestDetails/{request.Id}", dashboard);
            var details = await TextAsync(await lab.GetAsync($"/Lab/RequestDetails/{request.Id}"));
            Assert.Contains(s.TestName, details);
            Assert.Contains("Confirm Receipt", details);

            var receive = await FormHelper.PostFormAsync(lab, $"/Lab/RequestDetails/{request.Id}",
                $"/Lab/ReceiveRequest/{request.Id}", new Dictionary<string, string>());
            Assert.Equal(HttpStatusCode.Redirect, receive.StatusCode);

            var received = await LatestRequestAsync(s);
            Assert.Equal(LabRequestStatus.Received, received!.Status);
            Assert.NotNull(received.ReceivedAt);
            Assert.Equal("flow-lab", received.ReceivedBy);
            Assert.Contains("id=\"request-received-at\"", await TextAsync(await lab.GetAsync($"/Lab/RequestDetails/{request.Id}")));
        }

        [Fact]
        public async Task TC_L3_02_UnknownPatient_IsRejected_WithTheMessage()
        {
            var s = await SetupAsync();
            var client = Doctor(s);

            var form = await client.GetAsync("/Doctor/CreateLabRequest?patientId=999999");
            Assert.Equal(HttpStatusCode.Redirect, form.StatusCode);

            var post = await PostAsync(client, $"/Doctor/CreateLabRequest?patientId={s.PatientId}", "/Doctor/CreateLabRequest",
                new List<KeyValuePair<string, string>> { new("PatientId", "999999"), new("SelectedTestIds", Id(s.TestId)) });
            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
            Assert.Null(await LatestRequestAsync(s with { PatientId = 999999 }));
        }

        [Fact]
        public async Task TC_L3_03_NoTestsSelected_ShowsTheError()
        {
            var s = await SetupAsync();

            var response = await SendRequestAsync(Doctor(s), s);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(LabRequestFormViewModel.NoTestsError, await TextAsync(response));
            Assert.Null(await LatestRequestAsync(s));
        }

        [Fact]
        public async Task TC_L3_03_LabReturnsWithANote_DoctorSeesIt_AndResends()
        {
            var s = await SetupAsync();
            var doctor = Doctor(s);
            await SendRequestAsync(doctor, s, s.TestId);
            var request = (await LatestRequestAsync(s))!;

            var lab = Technician();
            var returned = await FormHelper.PostFormAsync(lab, $"/Lab/RequestDetails/{request.Id}",
                $"/Lab/ReturnRequest/{request.Id}", new Dictionary<string, string> { ["Note"] = "Clinical information is missing." });
            Assert.Equal(HttpStatusCode.Redirect, returned.StatusCode);
            Assert.Equal(LabRequestStatus.ReturnedToDoctor, (await LatestRequestAsync(s))!.Status);

            var list = await TextAsync(await doctor.GetAsync($"/Doctor/LabRequests?patientId={s.PatientId}"));
            Assert.Contains("Clinical information is missing.", list);
            var edit = await TextAsync(await doctor.GetAsync($"/Doctor/EditLabRequest/{request.Id}"));
            Assert.Contains("id=\"lab-return-note\"", edit);

            var resend = await PostAsync(doctor, $"/Doctor/EditLabRequest/{request.Id}", $"/Doctor/EditLabRequest/{request.Id}",
                new List<KeyValuePair<string, string>>
                {
                    new("PatientId", Id(s.PatientId)), new("SelectedTestIds", Id(s.TestId)), new("ClinicalNotes", "Fatigue, fasting 12 h")
                });
            Assert.Equal(HttpStatusCode.Redirect, resend.StatusCode);
            var resent = (await LatestRequestAsync(s))!;
            Assert.Equal(LabRequestStatus.Requested, resent.Status);
            Assert.Equal("Fatigue, fasting 12 h", resent.ClinicalNotes);
        }

        [Fact]
        public async Task EditLabRequest_NotReturned_RedirectsToTheList()
        {
            var s = await SetupAsync();
            var doctor = Doctor(s);
            await SendRequestAsync(doctor, s, s.TestId);
            var request = (await LatestRequestAsync(s))!;

            var response = await doctor.GetAsync($"/Doctor/EditLabRequest/{request.Id}");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        [Fact]
        public async Task RequestDetails_UnknownId_IsNotFound()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Technician().GetAsync("/Lab/RequestDetails/999999")).StatusCode);
        }

        [Fact]
        public async Task PatientRecord_LinksToLabRequests()
        {
            var s = await SetupAsync();

            var html = await TextAsync(await Doctor(s).GetAsync($"/Doctor/PatientRecord?patientId={s.PatientId}"));

            Assert.Contains($"/Doctor/CreateLabRequest?patientId={s.PatientId}", html);
            Assert.Contains($"/Doctor/LabRequests?patientId={s.PatientId}", html);
        }

        [Fact]
        public async Task OnlyDoctorsSendRequests_AndOnlyTheLabReceivesThem()
        {
            foreach (var role in new[] { "Admin", "Patient", "Pharmacist", "LabTechnician" })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs(role).GetAsync("/Doctor/LabRequests")).StatusCode);
            }
            foreach (var role in new[] { "Admin", "Doctor", "Patient", "Pharmacist" })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClientAs(role).GetAsync("/Lab/RequestDetails/1")).StatusCode);
            }
        }
    }
}
