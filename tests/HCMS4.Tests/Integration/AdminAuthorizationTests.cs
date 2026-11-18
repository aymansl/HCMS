using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using System.Net;
using System.Reflection;
using HCMS4.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Tests.Integration
{
    // Regression tests for issue #1: doctors could reach most /Admin/* endpoints.
    public class AdminAuthorizationTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public AdminAuthorizationTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/Admin/Dashboard")]
        [InlineData("/Admin/Patients")]
        [InlineData("/Admin/CreateDoctor")]
        [InlineData("/Admin/ManageAppointments")]
        [InlineData("/Admin/PurchaseRequests")]
        [InlineData("/Admin/PriceList")]
        public async Task AdminPages_AsDoctor_AreForbidden(string url)
        {
            var client = _factory.CreateClientAs("Doctor");

            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task AdminDashboard_AsAdmin_ReturnsOk()
        {
            var client = _factory.CreateClientAs("Admin");

            var response = await client.GetAsync("/Admin/Dashboard");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void AdminController_AllowsOnlyAdminRole()
        {
            var classAttribute = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(classAttribute);
            Assert.Equal("Admin", classAttribute!.Roles);

            var actions = typeof(AdminController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => typeof(IActionResult).IsAssignableFrom(m.ReturnType)
                         || typeof(Task<IActionResult>).IsAssignableFrom(m.ReturnType));

            foreach (var action in actions)
            {
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
                var roles = action.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles ?? string.Empty);
                Assert.DoesNotContain(roles, r => r.Contains("Doctor", StringComparison.Ordinal));
            }
        }

        [Fact]
        public async Task DailyVisitingPatientsReport_AsDoctor_ReturnsOk()
        {
            var client = _factory.CreateClientAs("Doctor");

            var response = await client.GetAsync("/Doctor/DailyVisitingPatientsReport");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task RescheduleAppointment_OwnAppointment_ShowsForm()
        {
            var (doctorUserId, appointmentId) = await CreateAppointmentAsync();
            var client = _factory.CreateClientAs("Doctor", doctorUserId);

            var response = await client.GetAsync($"/Doctor/RescheduleAppointment/{appointmentId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("/Doctor/RescheduleAppointment", html);
            Assert.Contains("/Doctor/ViewAppointments", html);
        }

        [Fact]
        public async Task RescheduleAppointment_OtherDoctorsAppointment_RedirectsToList()
        {
            var (_, appointmentId) = await CreateAppointmentAsync();
            var (otherDoctorUserId, _) = await CreateAppointmentAsync();
            var client = _factory.CreateClientAs("Doctor", otherDoctorUserId);

            var response = await client.GetAsync($"/Doctor/RescheduleAppointment/{appointmentId}");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Doctor/ViewAppointments", response.Headers.Location?.OriginalString);
        }

        private async Task<(string DoctorUserId, int AppointmentId)> CreateAppointmentAsync()
        {
            string doctorUserId = string.Empty;
            int appointmentId = 0;
            await _factory.WithDbAsync(async db =>
            {
                var (doctorUser, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var appointment = new Appointment
                {
                    PatientId = patient.Id,
                    DoctorId = doctor.Id,
                    AppointmentDateTime = DateTime.UtcNow.AddDays(3),
                    Status = AppointmentStatus.Scheduled,
                    ConsultationFee = 100
                };
                db.Appointments.Add(appointment);
                await db.SaveChangesAsync();
                doctorUserId = doctorUser.Id;
                appointmentId = appointment.Id;
            });
            return (doctorUserId, appointmentId);
        }
    }
}
