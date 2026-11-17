using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using HCMS4.Data;
using HCMS4.Models;

namespace HCMS4.Tests
{
    public static class TestDbContextFactory
    {
        public static ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        private static int _patientIdCounter = 1000;
        private static int _doctorIdCounter = 2000;
        private static int _pharmacistIdCounter = 3000;
        private static int _drugIdCounter = 4000;
        private static int _supplierIdCounter = 5000;

        public static async Task<(ApplicationUser user, Patient patient)> CreateTestPatientAsync(ApplicationDbContext context)
        {
            var id = _patientIdCounter++;
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = $"patient{id}@test.com",
                Email = $"patient{id}@test.com",
                FirstName = "Test",
                LastName = "Patient"
            };

            var patient = new Patient
            {
                Id = id,
                UserId = user.Id,
                User = user,
                DateOfBirth = DateTime.Now.AddYears(-30)
            };

            context.Users.Add(user);
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            return (user, patient);
        }

        public static async Task<(ApplicationUser user, Doctor doctor, Specialization specialization)> CreateTestDoctorAsync(ApplicationDbContext context)
        {
            var specId = _doctorIdCounter;
            var docId = _doctorIdCounter + 100;
            _doctorIdCounter += 200;

            var specialization = new Specialization
            {
                Id = specId,
                Name = "General Medicine",
                ConsultationFee = 100,
                IsActive = true
            };

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = $"doctor{docId}@test.com",
                Email = $"doctor{docId}@test.com",
                FirstName = "Test",
                LastName = "Doctor"
            };

            var doctor = new Doctor
            {
                Id = docId,
                UserId = user.Id,
                User = user,
                SpecializationId = specialization.Id,
                IsAvailable = true
            };

            context.Specializations.Add(specialization);
            context.Users.Add(user);
            context.Doctors.Add(doctor);
            await context.SaveChangesAsync();

            return (user, doctor, specialization);
        }

        public static async Task<(ApplicationUser user, Pharmacist pharmacist)> CreateTestPharmacistAsync(ApplicationDbContext context)
        {
            var id = _pharmacistIdCounter++;
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = $"pharmacist{id}@test.com",
                Email = $"pharmacist{id}@test.com",
                FirstName = "Test",
                LastName = "Pharmacist"
            };

            var pharmacist = new Pharmacist
            {
                Id = id,
                UserId = user.Id,
                User = user,
                IsActive = true
            };

            context.Users.Add(user);
            context.Pharmacists.Add(pharmacist);
            await context.SaveChangesAsync();

            return (user, pharmacist);
        }

        public static async Task<Drug> CreateTestDrugAsync(ApplicationDbContext context)
        {
            var drug = new Drug
            {
                Id = _drugIdCounter++,
                Name = "Amoxicillin",
                Price = 25.00m,
                Quantity = 100,
                ExpiryDate = DateTime.Now.AddMonths(12)
            };

            context.Drugs.Add(drug);
            await context.SaveChangesAsync();

            return drug;
        }

        public static async Task<Supplier> CreateTestSupplierAsync(ApplicationDbContext context)
        {
            var supplier = new Supplier
            {
                Id = _supplierIdCounter++,
                Name = "Test Supplier",
                Phone = "123456789",
                Email = "supplier@test.com",
                IsActive = true
            };

            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();

            return supplier;
        }
    }
}
