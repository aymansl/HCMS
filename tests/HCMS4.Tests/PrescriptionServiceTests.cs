using HCMS4.Shared.Common;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
                ExpiryDate = ClinicTime.Now.AddDays(-1)
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
        public async Task CreateAsync_DoesNotTakeStock_StockLeavesOnlyAtDispensing()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var drug = await TestDbContextFactory.CreateTestDrugAsync(context);
            var stockBefore = drug.Quantity;
            var service = new PrescriptionService(context, NullLogger<PrescriptionService>.Instance);

            var result = await service.CreateAsync(
                new Prescription { PatientId = patient.Id, DoctorId = doctor.Id },
                new List<PrescriptionItem> { new PrescriptionItem { DrugId = drug.Id, DrugName = "typed name", Quantity = 10, Dosage = "1", Duration = "5", Frequency = "daily" } });

            Assert.True(result.Success);
            Assert.Equal(stockBefore, (await context.Drugs.AsNoTracking().SingleAsync(d => d.Id == drug.Id)).Quantity);
            Assert.Equal(drug.Name, (await context.PrescriptionItems.SingleAsync()).DrugName);
        }

        [Fact]
        public async Task CancelAsync_PendingPrescription_IsCanceled_WithoutStockChange()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var drug = await TestDbContextFactory.CreateTestDrugAsync(context);
            var stockBefore = drug.Quantity;
            var prescription = new Prescription
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Status = PrescriptionStatus.Pending,
                PrescriptionItems = { new PrescriptionItem { DrugId = drug.Id, DrugName = drug.Name, Quantity = 10, Dosage = "1", Duration = "5", Frequency = "daily" } }
            };
            context.Prescriptions.Add(prescription);
            await context.SaveChangesAsync();
            var service = new PrescriptionService(context, NullLogger<PrescriptionService>.Instance);

            var result = await service.CancelAsync(prescription.Id);

            Assert.True(result.Success);
            Assert.Equal(PrescriptionStatus.Canceled, (await context.Prescriptions.AsNoTracking().SingleAsync()).Status);
            Assert.Equal(stockBefore, (await context.Drugs.AsNoTracking().SingleAsync(d => d.Id == drug.Id)).Quantity);
        }

        [Fact]
        public async Task CancelAsync_DispensedPrescription_IsRejected()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var prescription = new Prescription { PatientId = patient.Id, DoctorId = doctor.Id, Status = PrescriptionStatus.Completed };
            context.Prescriptions.Add(prescription);
            await context.SaveChangesAsync();
            var service = new PrescriptionService(context, NullLogger<PrescriptionService>.Instance);

            var result = await service.CancelAsync(prescription.Id);

            Assert.False(result.Success);
            Assert.Equal(PrescriptionStatus.Completed, (await context.Prescriptions.AsNoTracking().SingleAsync()).Status);
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
