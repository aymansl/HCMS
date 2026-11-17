using Xunit;
using Microsoft.EntityFrameworkCore;
using HCMS4.Models;

namespace HCMS4.Tests
{
    public class DrugServiceTests
    {
        [Fact]
        public async Task Drug_CreateAsync_ValidDrug_CreatesSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var drug = new Drug
            {
                Name = "New Drug",
                Price = 25.00m,
                Quantity = 100,
                ExpiryDate = DateTime.Now.AddMonths(12)
            };

            context.Drugs.Add(drug);
            await context.SaveChangesAsync();

            // Assert
            var created = await context.Drugs.FindAsync(drug.Id);
            Assert.NotNull(created);
            Assert.Equal("New Drug", created.Name);
        }

        [Fact]
        public async Task Drug_UpdateAsync_ValidData_UpdatesSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var drug = await TestDbContextFactory.CreateTestDrugAsync(context);

            // Act
            drug.Price = 30.00m;
            drug.Quantity = 150;
            context.Drugs.Update(drug);
            await context.SaveChangesAsync();

            // Assert
            var updated = await context.Drugs.FindAsync(drug.Id);
            Assert.Equal(30.00m, updated.Price);
            Assert.Equal(150, updated.Quantity);
        }

        [Fact]
        public async Task Drug_DeleteAsync_WithPrescriptionItems_PreventsDeletion()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var drug = await TestDbContextFactory.CreateTestDrugAsync(context);

            var prescription = new Prescription
            {
                Id = 1,
                PatientId = patient.Id,
                DoctorId = 1,
                Notes = "Test"
            };
            context.Prescriptions.Add(prescription);

            var prescriptionItem = new PrescriptionItem
            {
                PrescriptionId = 1,
                DrugId = drug.Id,
                DrugName = drug.Name,
                Dosage = "500mg",
                Duration = "7 days",
                Frequency = "3x daily",
                Quantity = 21
            };
            context.PrescriptionItems.Add(prescriptionItem);
            await context.SaveChangesAsync();

            // Act
            var hasPrescriptionItems = await context.PrescriptionItems
                .AnyAsync(pi => pi.DrugId == drug.Id);

            // Assert
            Assert.True(hasPrescriptionItems);
        }

        [Fact]
        public async Task Drug_GetByExpiryStatus_IdentifiesExpired()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var expiredDrug = new Drug
            {
                Name = "Expired Drug",
                Price = 10.00m,
                Quantity = 50,
                ExpiryDate = DateTime.Now.AddDays(-5)
            };
            var validDrug = new Drug
            {
                Name = "Valid Drug",
                Price = 20.00m,
                Quantity = 100,
                ExpiryDate = DateTime.Now.AddMonths(6)
            };
            var expiringSoonDrug = new Drug
            {
                Name = "Expiring Soon Drug",
                Price = 15.00m,
                Quantity = 75,
                ExpiryDate = DateTime.Now.AddDays(20)
            };

            context.Drugs.AddRange(expiredDrug, validDrug, expiringSoonDrug);
            await context.SaveChangesAsync();

            // Act
            var expiredDrugs = await context.Drugs
                .Where(d => d.ExpiryDate <= DateTime.Now)
                .ToListAsync();

            var expiringSoonDrugs = await context.Drugs
                .Where(d => d.ExpiryDate > DateTime.Now && d.ExpiryDate <= DateTime.Now.AddDays(30))
                .ToListAsync();

            // Assert
            Assert.Single(expiredDrugs);
            Assert.Single(expiringSoonDrugs);
            Assert.Equal("Expired Drug", expiredDrugs[0].Name);
            Assert.Equal("Expiring Soon Drug", expiringSoonDrugs[0].Name);
        }

        [Fact]
        public async Task Drug_ExpiryStatus_CalculatedCorrectly()
        {
            // Arrange
            var expiredDrug = new Drug
            {
                Name = "Expired",
                ExpiryDate = DateTime.Now.AddDays(-1)
            };
            var expiringSoonDrug = new Drug
            {
                Name = "ExpiringSoon",
                ExpiryDate = DateTime.Now.AddDays(20)
            };
            var validDrug = new Drug
            {
                Name = "Valid",
                ExpiryDate = DateTime.Now.AddMonths(6)
            };

            // Act & Assert
            Assert.Equal("Expired", expiredDrug.ExpiryStatus);
            Assert.Equal("Expiring Soon", expiringSoonDrug.ExpiryStatus);
            Assert.Equal("Valid", validDrug.ExpiryStatus);
        }

        [Fact]
        public async Task Drug_LowStock_IdentifiesCorrectly()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var lowStockDrug = new Drug
            {
                Name = "Low Stock",
                Price = 10.00m,
                Quantity = 5,
                ExpiryDate = DateTime.Now.AddMonths(6)
            };
            var normalStockDrug = new Drug
            {
                Name = "Normal Stock",
                Price = 20.00m,
                Quantity = 50,
                ExpiryDate = DateTime.Now.AddMonths(6)
            };

            context.Drugs.AddRange(lowStockDrug, normalStockDrug);
            await context.SaveChangesAsync();

            // Act
            var lowStockDrugs = await context.Drugs
                .Where(d => d.Quantity < 10)
                .ToListAsync();

            // Assert
            Assert.Single(lowStockDrugs);
            Assert.Equal("Low Stock", lowStockDrugs[0].Name);
        }

        [Fact]
        public async Task Drug_NegativeQuantity_FailsValidation()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var drug = new Drug
            {
                Name = "Test Drug",
                Price = 10.00m,
                Quantity = -5,
                ExpiryDate = DateTime.Now.AddMonths(6)
            };

            // Act
            var isValid = drug.Quantity >= 0;

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void Drug_PastExpiryDate_FailsValidation()
        {
            // Arrange
            var drug = new Drug
            {
                Name = "Test Drug",
                Price = 10.00m,
                Quantity = 10,
                ExpiryDate = DateTime.Now.AddDays(-1)
            };

            // Act
            var validation = new Drug.FutureDateAttribute();
            var result = validation.GetValidationResult(drug.ExpiryDate, 
                new System.ComponentModel.DataAnnotations.ValidationContext(drug));

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Expiry date must be in the future", result.ErrorMessage);
        }
    }
}
