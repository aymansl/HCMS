using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Services;

namespace HCMS4.Tests
{
    // The "current patient / doctor" lookups used by HcmsControllerBase.
    public class UserLookupTests
    {
        [Fact]
        public async Task PatientLookup_ReturnsPatientIdForUser_OrNull()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (user, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var lookup = new PatientLookup(context);

            Assert.Equal(patient.Id, await lookup.GetPatientIdAsync(user.Id));
            Assert.Null(await lookup.GetPatientIdAsync("unknown-user"));
        }

        [Fact]
        public async Task DoctorLookup_ReturnsDoctorIdForUser_OrNull()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (user, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var lookup = new DoctorLookup(context);

            Assert.Equal(doctor.Id, await lookup.GetDoctorIdAsync(user.Id));
            Assert.Null(await lookup.GetDoctorIdAsync("unknown-user"));
        }
    }
}
