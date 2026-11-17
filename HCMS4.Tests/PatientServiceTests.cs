using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HCMS4.Models;
using HCMS4.Models.Common;
using HCMS4.Services;

namespace HCMS4.Tests
{
    public class PatientServiceTests
    {
        [Fact]
        public async Task GetByIdAsync_ExistingPatient_ReturnsPatient()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (user, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var service = new PatientService(context, NullLogger<PatientService>.Instance);

            // Act
            var result = await service.GetByIdAsync(patient.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(patient.Id, result.Id);
            Assert.Equal(user.Email, result.User.Email);
        }

        [Fact]
        public async Task GetByIdAsync_NonExistingPatient_ReturnsNull()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var service = new PatientService(context, NullLogger<PatientService>.Instance);

            // Act
            var result = await service.GetByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsPaginatedResults()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            for (int i = 0; i < 25; i++)
            {
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = $"patient{i}@test.com",
                    Email = $"patient{i}@test.com",
                    FirstName = $"Patient{i}",
                    LastName = "Test"
                };
                var patient = new Patient
                {
                    Id = i + 1,
                    UserId = user.Id,
                    User = user,
                    DateOfBirth = DateTime.Now.AddYears(-30)
                };
                context.Users.Add(user);
                context.Patients.Add(patient);
            }
            await context.SaveChangesAsync();

            var service = new PatientService(context, NullLogger<PatientService>.Instance);
            var pagination = new PaginationParams { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await service.GetAllAsync(pagination);

            // Assert
            Assert.Equal(10, result.Items.Count);
            Assert.Equal(25, result.TotalCount);
            Assert.Equal(3, result.TotalPages);
            Assert.True(result.HasNextPage);
            Assert.False(result.HasPreviousPage);
        }

        [Fact]
        public async Task UpdateAsync_ValidData_UpdatesPatient()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var service = new PatientService(context, NullLogger<PatientService>.Instance);

            patient.Address = "123 Test Street";
            patient.EmergencyContact = "1234567890";

            // Act
            var result = await service.UpdateAsync(patient.Id, patient);

            // Assert
            Assert.True(result.Success);
            var updatedPatient = await context.Patients.FindAsync(patient.Id);
            Assert.Equal("123 Test Street", updatedPatient.Address);
        }

        [Fact]
        public async Task DeleteAsync_PatientWithNoRelatedRecords_DeletesSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var service = new PatientService(context, NullLogger<PatientService>.Instance);

            // Act
            var result = await service.DeleteAsync(patient.Id);

            // Assert
            Assert.True(result.Success);
            var deletedPatient = await context.Patients.FindAsync(patient.Id);
            Assert.Null(deletedPatient);
        }

        [Fact]
        public async Task DeleteAsync_PatientWithAppointments_FailsWithMessage()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var appointment = new Appointment
            {
                Id = 1,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AppointmentDateTime = DateTime.Now.AddDays(1),
                Status = AppointmentStatus.Scheduled
            };
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            var service = new PatientService(context, NullLogger<PatientService>.Instance);

            // Act
            var result = await service.DeleteAsync(patient.Id);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("related records", result.Message);
        }
    }
}
