using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HCMS4.Models;
using HCMS4.Services;

namespace HCMS4.Tests
{
    public class AppointmentServiceTests
    {
        [Fact]
        public async Task CreateAsync_ValidAppointment_CreatesSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var service = new AppointmentService(context, NullLogger<AppointmentService>.Instance);

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = DateTime.Now.AddDays(1),
                Status = AppointmentStatus.Scheduled
            };

            // Act
            var result = await service.CreateAsync(appointment);

            // Assert
            Assert.True(result.Success);
            var createdAppointment = await context.Appointments.FirstOrDefaultAsync();
            Assert.NotNull(createdAppointment);
            Assert.Equal(AppointmentStatus.Scheduled, createdAppointment.Status);
        }

        [Fact]
        public async Task CreateAsync_DuplicateTimeSlot_FailsWithMessage()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var appointmentTime = DateTime.Now.AddDays(1).AddHours(10);

            var existingAppointment = new Appointment
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = appointmentTime,
                Status = AppointmentStatus.Scheduled
            };
            context.Appointments.Add(existingAppointment);
            await context.SaveChangesAsync();

            var service = new AppointmentService(context, NullLogger<AppointmentService>.Instance);

            var newAppointment = new Appointment
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = appointmentTime,
                Status = AppointmentStatus.Scheduled
            };

            // Act
            var result = await service.CreateAsync(newAppointment);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not available", result.Message);
        }

        [Fact]
        public async Task CancelAsync_ScheduledAppointment_CancelsSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var appointmentId = 6101;
            var appointment = new Appointment
            {
                Id = appointmentId,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = DateTime.Now.AddDays(2),
                Status = AppointmentStatus.Scheduled
            };
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            var service = new AppointmentService(context, NullLogger<AppointmentService>.Instance);

            // Act
            var result = await service.CancelAsync(appointmentId, "Patient requested cancellation");

            // Assert
            Assert.True(result.Success);
            var canceledAppointment = await context.Appointments.FindAsync(appointmentId);
            Assert.Equal(AppointmentStatus.Canceled, canceledAppointment.Status);
            Assert.Equal("Patient requested cancellation", canceledAppointment.CancellationReason);
        }

        [Fact]
        public async Task CancelAsync_LessThan24Hours_FailsWithMessage()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var appointmentId = 6102;
            var appointment = new Appointment
            {
                Id = appointmentId,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = DateTime.Now.AddHours(12),
                Status = AppointmentStatus.Scheduled
            };
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            var service = new AppointmentService(context, NullLogger<AppointmentService>.Instance);

            // Act
            var result = await service.CancelAsync(appointmentId, "Late cancellation");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("24 hours", result.Message);
        }

        [Fact]
        public async Task CancelAsync_AlreadyCanceled_FailsWithMessage()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var appointmentId = 6103;
            var appointment = new Appointment
            {
                Id = appointmentId,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = DateTime.Now.AddDays(2),
                Status = AppointmentStatus.Canceled
            };
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            var service = new AppointmentService(context, NullLogger<AppointmentService>.Instance);

            // Act
            var result = await service.CancelAsync(appointmentId, "Test");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Only scheduled", result.Message);
        }

        [Fact]
        public async Task RescheduleAsync_ValidTimeSlot_ReschedulesSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var appointmentId = 6104;
            var appointment = new Appointment
            {
                Id = appointmentId,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = DateTime.Now.AddDays(1),
                Status = AppointmentStatus.Scheduled
            };
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            var service = new AppointmentService(context, NullLogger<AppointmentService>.Instance);
            var newDateTime = DateTime.Now.AddDays(3);

            // Act
            var result = await service.RescheduleAsync(appointmentId, newDateTime);

            // Assert
            Assert.True(result.Success);
            var rescheduledAppointment = await context.Appointments.FindAsync(appointmentId);
            Assert.Equal(newDateTime, rescheduledAppointment.AppointmentDateTime);
        }

        [Fact]
        public async Task GetTodayAppointmentsAsync_ReturnsTodayAppointments()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var todayAppointment = new Appointment
            {
                Id = 6105,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = DateTime.Today.AddHours(10),
                Status = AppointmentStatus.Scheduled
            };
            var tomorrowAppointment = new Appointment
            {
                Id = 6106,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = DateTime.Today.AddDays(1),
                Status = AppointmentStatus.Scheduled
            };
            var completedAppointment = new Appointment
            {
                Id = 6107,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = DateTime.Today.AddHours(9),
                Status = AppointmentStatus.Completed
            };

            context.Appointments.AddRange(todayAppointment, tomorrowAppointment, completedAppointment);
            await context.SaveChangesAsync();

            var service = new AppointmentService(context, NullLogger<AppointmentService>.Instance);

            // Act
            var result = await service.GetTodayAppointmentsAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal(AppointmentStatus.Scheduled, result[0].Status);
        }

        [Fact]
        public async Task IsTimeSlotAvailableAsync_AvailableSlot_ReturnsTrue()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var appointmentTime = DateTime.Now.AddDays(1).AddHours(10);
            var existingAppointment = new Appointment
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = appointmentTime,
                Status = AppointmentStatus.Scheduled
            };
            context.Appointments.Add(existingAppointment);
            await context.SaveChangesAsync();

            var service = new AppointmentService(context, NullLogger<AppointmentService>.Instance);

            // Act - Different time slot
            var result = await service.IsTimeSlotAvailableAsync(doctor.Id, appointmentTime.AddHours(2));

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsTimeSlotAvailableAsync_ConflictingSlot_ReturnsFalse()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var appointmentTime = DateTime.Now.AddDays(1).AddHours(10);
            var existingAppointment = new Appointment
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = appointmentTime,
                Status = AppointmentStatus.Scheduled
            };
            context.Appointments.Add(existingAppointment);
            await context.SaveChangesAsync();

            var service = new AppointmentService(context, NullLogger<AppointmentService>.Instance);

            // Act - Same time slot (within 30 min)
            var result = await service.IsTimeSlotAvailableAsync(doctor.Id, appointmentTime.AddMinutes(15));

            // Assert
            Assert.False(result);
        }
    }
}
