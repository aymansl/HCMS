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

        private static async Task<LeaveRequest> CreatePendingRequestAsync(ApplicationDbContext context)
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var request = new LeaveRequest
            {
                DoctorId = doctor.Id,
                StartDate = new DateTime(2026, 12, 1),
                EndDate = new DateTime(2026, 12, 3),
                LeaveType = LeaveType.Annual
            };
            context.LeaveRequests.Add(request);
            await context.SaveChangesAsync();
            return request;
        }

        // Regression tests for issue #17: the rejection reason was discarded.
        [Fact]
        public async Task RejectLeaveRequestAsync_StoresReason()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var request = await CreatePendingRequestAsync(context);

            var result = await CreateService(context).RejectLeaveRequestAsync(request.Id, "  Clinic is understaffed that week ");

            Assert.True(result.Success);
            Assert.Equal(LeaveStatus.Rejected, request.Status);
            Assert.Equal("Clinic is understaffed that week", request.RejectionReason);
        }

        // Regression tests: the doctor never saw the rejection because the profile loaded pending requests only.
        [Fact]
        public async Task RejectLeaveRequestAsync_NotifiesDoctor_AndRequestStaysVisibleToDoctor()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var request = await CreatePendingRequestAsync(context);
            var service = CreateService(context);
            var doctorUserId = (await context.Doctors.FindAsync(request.DoctorId))!.UserId;

            await service.RejectLeaveRequestAsync(request.Id, "No cover available");
            var visible = await service.GetRequestsForDoctorAsync(request.DoctorId);

            var shown = Assert.Single(visible);
            Assert.Equal("No cover available", shown.RejectionReason);
            var notification = Assert.Single(context.UserNotifications.Where(n => n.UserId == doctorUserId));
            Assert.Contains("No cover available", notification.Message);
        }

        [Fact]
        public async Task RejectLeaveRequestAsync_WithoutReason_IsRejected()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var request = await CreatePendingRequestAsync(context);

            var result = await CreateService(context).RejectLeaveRequestAsync(request.Id, " ");

            Assert.False(result.Success);
            Assert.Equal(LeaveStatus.Pending, request.Status);
        }
    }
}
