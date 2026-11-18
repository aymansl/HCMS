using HCMS4.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCMS4.Tests
{
    // Issue #35: the admin and patient fee endpoints now share one service method.
    public class ConsultationFeeTests
    {
        [Fact]
        public async Task GetConsultationFeeAsync_ReturnsSpecializationFee()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, doctor, specialization) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var service = new AppointmentService(context, NullLogger<AppointmentService>.Instance);

            var fee = await service.GetConsultationFeeAsync(doctor.Id);

            Assert.Equal(specialization.ConsultationFee, fee);
        }

        [Fact]
        public async Task GetConsultationFeeAsync_UnknownDoctor_ReturnsZero()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var service = new AppointmentService(context, NullLogger<AppointmentService>.Instance);

            Assert.Equal(0m, await service.GetConsultationFeeAsync(424242));
        }
    }
}
