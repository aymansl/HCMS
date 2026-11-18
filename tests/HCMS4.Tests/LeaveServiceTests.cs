using HCMS4.Data;
using HCMS4.Models;
using HCMS4.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCMS4.Tests
{
    public class LeaveServiceTests
    {
        private static LeaveService CreateService(ApplicationDbContext context) =>
            new(context, NullLogger<LeaveService>.Instance, new NotificationService(context));

        private static async Task<string> CreateAdminAsync(ApplicationDbContext context)
        {
            var role = new IdentityRole("Admin") { Id = Guid.NewGuid().ToString(), NormalizedName = "ADMIN" };
            var admin = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "admin@test.local",
                Email = "admin@test.local",
                FirstName = "Admin",
                LastName = "User"
            };
            context.Roles.Add(role);
            context.Users.Add(admin);
            context.UserRoles.Add(new IdentityUserRole<string> { RoleId = role.Id, UserId = admin.Id });
            await context.SaveChangesAsync();
            return admin.Id;
        }

        // Regression test for issue #16: NotifyManagerAsync only wrote a log line.
        [Fact]
        public async Task CreateLeaveRequestAsync_NotifiesAdmins()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var adminId = await CreateAdminAsync(context);
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var service = CreateService(context);

            await service.CreateLeaveRequestAsync(new LeaveRequest
            {
                DoctorId = doctor.Id,
                StartDate = new DateTime(2026, 12, 1),
                EndDate = new DateTime(2026, 12, 3),
                LeaveType = LeaveType.Annual
            });

            var notification = Assert.Single(context.UserNotifications.Where(n => n.UserId == adminId));
            Assert.Contains("2026-12-01", notification.Message);
            Assert.Equal($"/Admin/ViewDoctorLeaves/{doctor.Id}", notification.LinkUrl);
        }
    }
}
