using HCMS4.Models;
using HCMS4.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCMS4.Tests
{
    // Regression tests for issue #34: DrugId was "[Required] int?" while the column is NOT NULL.
    public class PrescriptionItemDrugIdTests
    {
        [Fact]
        public void DrugId_IsNonNullableInt_MatchingTheNotNullColumn()
        {
            Assert.Equal(typeof(int), typeof(PrescriptionItem).GetProperty(nameof(PrescriptionItem.DrugId))!.PropertyType);
        }

        [Fact]
        public async Task CreateAsync_ItemWithoutDrug_ReturnsValidationError()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var service = new PrescriptionService(context, NullLogger<PrescriptionService>.Instance);
            var prescription = new Prescription { PatientId = patient.Id, DoctorId = doctor.Id, Notes = "Test" };
            var items = new List<PrescriptionItem>
            {
                new() { DrugName = "Unknown", Dosage = "1", Duration = "1d", Frequency = "daily", Quantity = 1, Instructions = "-" }
            };

            var result = await service.CreateAsync(prescription, items);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("no drug selected"));
        }
    }
}
