using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HCMS4.Models;
using HCMS4.Services;

namespace HCMS4.Tests
{
    public class PrescriptionServiceTests
    {
        [Fact]
        public async Task CreateAsync_ValidPrescription_CreatesSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var drug = await TestDbContextFactory.CreateTestDrugAsync(context);

            var service = new PrescriptionService(context, NullLogger<PrescriptionService>.Instance);

            var prescription = new Prescription
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Test prescription",
                Status = PrescriptionStatus.Pending
            };

            var items = new List<PrescriptionItem>
            {
                new PrescriptionItem
                {
                    DrugId = drug.Id,
                    DrugName = drug.Name,
                    Dosage = "500mg",
                    Duration = "7 days",
                    Frequency = "3 times daily",
                    Quantity = 21,
                    Instructions = "Take with food"
                }
            };

            // Act
            var result = await service.CreateAsync(prescription, items);

            // Assert
            Assert.True(result.Success);
            var createdPrescription = await context.Prescriptions
                .Include(p => p.PrescriptionItems)
                .FirstOrDefaultAsync();
            Assert.NotNull(createdPrescription);
            Assert.Single(createdPrescription.PrescriptionItems);
            Assert.Equal(PrescriptionStatus.Pending, createdPrescription.Status);
        }

        [Fact]
        public async Task CreateAsync_InsufficientStock_FailsWithMessage()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var drug = await TestDbContextFactory.CreateTestDrugAsync(context);

            var service = new PrescriptionService(context, NullLogger<PrescriptionService>.Instance);

            var prescription = new Prescription
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Test prescription"
            };

            var items = new List<PrescriptionItem>
            {
                new PrescriptionItem
                {
                    DrugId = drug.Id,
                    DrugName = drug.Name,
                    Dosage = "500mg",
                    Duration = "30 days",
                    Frequency = "3 times daily",
                    Quantity = 500,
                    Instructions = "Take with food"
                }
            };

            // Act
            var result = await service.CreateAsync(prescription, items);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Validation failed.", result.Message);
            Assert.Contains(result.Errors, e => e.Contains("Insufficient stock"));
        }

        [Fact]
        public async Task CreateAsync_ExpiredDrug_FailsWithMessage()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var expiredDrug = new Drug
            {
                Id = 5100,
                Name = "Expired Drug",
                Price = 10.00m,
                Quantity = 100,
                ExpiryDate = DateTime.Now.AddDays(-1)
            };
            context.Drugs.Add(expiredDrug);
            await context.SaveChangesAsync();

            var service = new PrescriptionService(context, NullLogger<PrescriptionService>.Instance);

            var prescription = new Prescription
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Test prescription"
            };

            var items = new List<PrescriptionItem>
            {
                new PrescriptionItem
                {
                    DrugId = expiredDrug.Id,
                    DrugName = expiredDrug.Name,
                    Dosage = "100mg",
                    Duration = "7 days",
                    Frequency = "Once daily",
                    Quantity = 7,
                    Instructions = "Take in morning"
                }
            };

            // Act
            var result = await service.CreateAsync(prescription, items);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Validation failed.", result.Message);
            Assert.Contains(result.Errors, e => e.Contains("expired"));
        }

        [Fact]
        public async Task MarkAsCompletedAsync_PendingPrescription_MarksAsCompleted()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var prescriptionId = 5101;
            var prescription = new Prescription
            {
                Id = prescriptionId,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Test prescription",
                Status = PrescriptionStatus.Pending
            };
            context.Prescriptions.Add(prescription);
            await context.SaveChangesAsync();

            var service = new PrescriptionService(context, NullLogger<PrescriptionService>.Instance);

            // Act
            var result = await service.MarkAsCompletedAsync(prescriptionId);

            // Assert
            Assert.True(result.Success);
            var completedPrescription = await context.Prescriptions.FindAsync(prescriptionId);
            Assert.Equal(PrescriptionStatus.Completed, completedPrescription.Status);
        }

        [Fact]
        public async Task MarkAsCompletedAsync_AlreadyCompleted_FailsWithMessage()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var prescriptionId = 5102;
            var prescription = new Prescription
            {
                Id = prescriptionId,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Test prescription",
                Status = PrescriptionStatus.Completed
            };
            context.Prescriptions.Add(prescription);
            await context.SaveChangesAsync();

            var service = new PrescriptionService(context, NullLogger<PrescriptionService>.Instance);

            // Act
            var result = await service.MarkAsCompletedAsync(prescriptionId);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Only pending", result.Message);
        }

        [Fact]
        public async Task CancelAsync_PrescriptionWithItems_RestoresStock()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var drug = await TestDbContextFactory.CreateTestDrugAsync(context);
            var initialStock = drug.Quantity;

            var prescriptionId = 5103;
            var prescription = new Prescription
            {
                Id = prescriptionId,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Test prescription",
                Status = PrescriptionStatus.Pending
            };
            context.Prescriptions.Add(prescription);

            var itemQuantity = 10;
            var prescriptionItem = new PrescriptionItem
            {
                Id = 5103,
                PrescriptionId = prescriptionId,
                DrugId = drug.Id,
                DrugName = drug.Name,
                Dosage = "500mg",
                Duration = "7 days",
                Frequency = "3 times daily",
                Quantity = itemQuantity
            };
            context.PrescriptionItems.Add(prescriptionItem);
            await context.SaveChangesAsync();

            // Simulate stock being deducted (as would happen in CreateAsync)
            drug.Quantity -= itemQuantity;
            context.Drugs.Update(drug);
            await context.SaveChangesAsync();

            var service = new PrescriptionService(context, NullLogger<PrescriptionService>.Instance);

            // Act
            var result = await service.CancelAsync(prescriptionId);

            // Assert
            Assert.True(result.Success);
            var canceledPrescription = await context.Prescriptions.FindAsync(prescriptionId);
            Assert.Equal(PrescriptionStatus.Canceled, canceledPrescription.Status);

            var updatedDrug = await context.Drugs.FindAsync(drug.Id);
            Assert.Equal(initialStock, updatedDrug.Quantity);
        }

        [Fact]
        public async Task GetPendingAsync_ReturnsOnlyPendingPrescriptions()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);

            var pendingPrescription1 = new Prescription
            {
                Id = 5104,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Pending 1",
                Status = PrescriptionStatus.Pending
            };
            var pendingPrescription2 = new Prescription
            {
                Id = 5105,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Pending 2",
                Status = PrescriptionStatus.Pending
            };
            var completedPrescription = new Prescription
            {
                Id = 5106,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Completed",
                Status = PrescriptionStatus.Completed
            };

            context.Prescriptions.AddRange(pendingPrescription1, pendingPrescription2, completedPrescription);
            await context.SaveChangesAsync();

            var service = new PrescriptionService(context, NullLogger<PrescriptionService>.Instance);

            // Act
            var result = await service.GetPendingAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, p => Assert.Equal(PrescriptionStatus.Pending, p.Status));
        }
    }
}
