using HCMS4.Models;
using HCMS4.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCMS4.Tests
{
    public class PharmacistServiceTests
    {
        private static async Task<Prescription> CreatePrescriptionAsync(HCMS4.Data.ApplicationDbContext context, PrescriptionStatus status)
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var prescription = new Prescription
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                Notes = "Test",
                Status = status
            };
            context.Prescriptions.Add(prescription);
            await context.SaveChangesAsync();
            return prescription;
        }

        [Fact]
        public async Task HoldPrescriptionAsync_Pending_SucceedsAndStaysPending()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var prescription = await CreatePrescriptionAsync(context, PrescriptionStatus.Pending);
            var service = new PharmacistService(context, NullLogger<PharmacistService>.Instance);

            var result = await service.HoldPrescriptionAsync(prescription.Id);

            Assert.True(result.Success);
            Assert.Equal(PrescriptionStatus.Pending, (await context.Prescriptions.FindAsync(prescription.Id))!.Status);
        }

        [Fact]
        public async Task HoldPrescriptionAsync_Completed_Fails()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var prescription = await CreatePrescriptionAsync(context, PrescriptionStatus.Completed);
            var service = new PharmacistService(context, NullLogger<PharmacistService>.Instance);

            var result = await service.HoldPrescriptionAsync(prescription.Id);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task HoldPrescriptionAsync_Missing_Fails()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var service = new PharmacistService(context, NullLogger<PharmacistService>.Instance);

            var result = await service.HoldPrescriptionAsync(999999);

            Assert.False(result.Success);
        }
    }
}
