using HCMS4.Shared.Common;
using HCMS4.Models;
using HCMS4.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace HCMS4.Tests
{
    // The whole system works in Damascus time (UTC+3), not UTC (issue #14, project decision).
    public class ClinicTimeTests
    {
        [Fact]
        public void ClinicClock_ConvertsUtcToDamascus()
        {
            var fake = new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 22, 30, 0, TimeSpan.Zero));
            var clock = new ClinicClock(fake, ClinicTime.TimeZone);

            Assert.Equal(new DateTime(2026, 11, 19, 1, 30, 0), clock.Now);
            Assert.Equal(new DateTime(2026, 11, 19), clock.Today);
        }

        [Fact]
        public void ClinicTime_UsesDamascusTimeZone()
        {
            Assert.Equal(TimeSpan.FromHours(3), ClinicTime.TimeZone.GetUtcOffset(new DateTime(2026, 7, 1)));
            Assert.Equal(TimeSpan.FromHours(3), ClinicTime.TimeZone.GetUtcOffset(new DateTime(2026, 1, 1)));
        }

        // Regression tests for issue #18: CreateAsync accepted appointments in the past.
        private static async Task<(AppointmentService Service, Appointment Appointment)> SetupAsync(DateTime appointmentTime)
        {
            var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            // Damascus "now" = 2026-11-18 12:00.
            var clock = new ClinicClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero)), ClinicTime.TimeZone);
            var service = new AppointmentService(context, NullLogger<AppointmentService>.Instance, clock);
            var appointment = new Appointment { PatientId = patient.Id, DoctorId = doctor.Id, AppointmentDateTime = appointmentTime, ConsultationFee = 100 };
            return (service, appointment);
        }

        [Fact]
        public async Task CreateAsync_PastAppointment_IsRejected()
        {
            var (service, appointment) = await SetupAsync(new DateTime(2026, 11, 18, 11, 0, 0));

            var result = await service.CreateAsync(appointment);

            Assert.False(result.Success);
            Assert.Contains("future", result.Message);
        }

        [Fact]
        public async Task CreateAsync_FutureAppointment_IsAccepted_AndStampedInDamascusTime()
        {
            var (service, appointment) = await SetupAsync(new DateTime(2026, 11, 18, 13, 0, 0));

            var result = await service.CreateAsync(appointment);

            Assert.True(result.Success);
            Assert.Equal(new DateTime(2026, 11, 18, 12, 0, 0), appointment.CreatedAt);
        }
    }
}
