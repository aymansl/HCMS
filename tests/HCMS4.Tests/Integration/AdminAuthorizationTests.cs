using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Identity.Authorization;
using System.Net;
using System.Reflection;
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

        // After Phase 5 the /Admin/... actions live in several module controllers named "Admin".
        public static IEnumerable<Type> AdminControllers() =>
            typeof(Program).Assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<HCMS4.Shared.Common.ControllerNameAttribute>()?.Name == "Admin");

        [Fact]
        public void AdminControllers_AllowOnlyAdminRole()
        {
            var controllers = AdminControllers().ToList();
            Assert.True(controllers.Count >= 6, "Expected the Admin actions to be split across the module controllers.");

            foreach (var controller in controllers)
            {
                var classAttribute = controller.GetCustomAttribute<AuthorizeAttribute>();
                Assert.NotNull(classAttribute);
                // Permission policy held by the Admin role only.
                Assert.Equal(new[] { Roles.Admin }, RolePermissions.RolesWith(classAttribute!.Policy!));

                var actions = controller
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(m => typeof(IActionResult).IsAssignableFrom(m.ReturnType)
                             || typeof(Task<IActionResult>).IsAssignableFrom(m.ReturnType));

                foreach (var action in actions)
                {
                    Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
                    Assert.Empty(action.GetCustomAttributes<AuthorizeAttribute>());
                }
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
