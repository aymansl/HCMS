using HCMS4.Modules.Appointments.Services;
using HCMS4.Shared.Reporting.Services;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCMS4.Tests
{
    // Regression test for issue #15: past appointments were only completed when an admin opened a page.
    public class DailyBackgroundTasksTests
    {
        [Fact]
        public async Task RunDailyTasksAsync_CompletesPastScheduledAppointments()
        {
            var databaseName = Guid.NewGuid().ToString();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName));
            services.AddScoped<IAppointmentService, AppointmentService>();
            services.AddScoped<IDailyReportService, DailyReportService>();
            using var provider = services.BuildServiceProvider();

            int pastId, futureId;
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var past = new Appointment { PatientId = patient.Id, DoctorId = doctor.Id, AppointmentDateTime = DateTime.Now.AddDays(-2), Status = AppointmentStatus.Scheduled };
                var future = new Appointment { PatientId = patient.Id, DoctorId = doctor.Id, AppointmentDateTime = DateTime.Now.AddDays(2), Status = AppointmentStatus.Scheduled };
                db.Appointments.AddRange(past, future);
                await db.SaveChangesAsync();
                pastId = past.Id;
                futureId = future.Id;
            }

            var job = new DailyReportBackgroundService(provider, NullLogger<DailyReportBackgroundService>.Instance);
            await job.RunDailyTasksAsync();

            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Equal(AppointmentStatus.Completed, (await db.Appointments.FindAsync(pastId))!.Status);
                Assert.Equal(AppointmentStatus.Scheduled, (await db.Appointments.FindAsync(futureId))!.Status);
            }
        }
    }
}
