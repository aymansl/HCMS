using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using System.Net;
using HCMS4.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace HCMS4.Tests.Integration
{
    // Regression test: when the AI service is unavailable (no key, or service down) the appointments
    // page must keep showing the local estimate ("Rule-Based Mode") instead of hiding the risk badges.
    public class NoShowFallbackPageTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public NoShowFallbackPageTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static async Task SeedAppointmentAsync(WebApplicationFactory<Program> factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            {
                var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                db.Appointments.Add(new Appointment
                {
                    PatientId = patient.Id,
                    DoctorId = doctor.Id,
                    AppointmentDateTime = DateTime.Now.AddDays(2),
                    Status = AppointmentStatus.Scheduled,
                    ConsultationFee = 100
                });
                await db.SaveChangesAsync();
            }
        }

        private static async Task AssertRuleBasedModeAsync(HttpClient client)
        {
            var response = await client.GetAsync("/Admin/ManageAppointments");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Rule-Based Mode", html);
            Assert.Contains("risk-indicator", html);
            Assert.DoesNotContain("Analytics service currently unavailable", html);
        }

        [Fact]
        public async Task ManageAppointments_AiServiceDown_ShowsLocalEstimate()
        {
            // The shared factory points AiService:BaseUrl at a closed port.
            await SeedAppointmentAsync(_factory);

            await AssertRuleBasedModeAsync(_factory.CreateClientAs("Admin"));
        }

        [Fact]
        public async Task ManageAppointments_NoApiKey_ShowsLocalEstimate()
        {
            var factory = _factory.WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?> { ["AiService:ApiKey"] = "" })));
            await SeedAppointmentAsync(factory);

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Admin");

            await AssertRuleBasedModeAsync(client);
        }
    }
}
