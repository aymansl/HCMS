using HCMS4.Shared.Notifications.Services;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Notifications.Models;
using System.Net;
namespace HCMS4.Tests.Integration
{
    // Regression tests: leave-request notifications were created for admins, but no page showed them.
    public class AdminNotificationsTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public AdminNotificationsTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task AdminDashboard_ShowsTheAdminsNotifications()
        {
            var adminId = Guid.NewGuid().ToString();
            await _factory.WithDbAsync(async db =>
            {
                db.UserNotifications.Add(new UserNotification
                {
                    UserId = adminId,
                    Title = "Doctor leave request",
                    Message = "New leave request from 2026-12-01 to 2026-12-03.",
                    Type = NotificationType.General,
                    LinkUrl = "/Admin/ViewDoctorLeaves/5"
                });
                await db.SaveChangesAsync();
            });
            var client = _factory.CreateClientAs("Admin", adminId);

            var response = await client.GetAsync("/Admin/Dashboard");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Doctor leave request", html);
            Assert.Contains("/Admin/ViewDoctorLeaves/5", html);
        }

        [Fact]
        public async Task GetRecentForUserAsync_ReturnsOnlyThatUsersNewestFirst()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var service = new NotificationService(context);
            context.UserNotifications.AddRange(
                new UserNotification { UserId = "a", Title = "old", Message = "m", CreatedAt = new DateTime(2026, 1, 1) },
                new UserNotification { UserId = "a", Title = "new", Message = "m", CreatedAt = new DateTime(2026, 2, 1) },
                new UserNotification { UserId = "b", Title = "other", Message = "m", CreatedAt = new DateTime(2026, 3, 1) });
            await context.SaveChangesAsync();

            var result = await service.GetRecentForUserAsync("a");

            Assert.Equal(new[] { "new", "old" }, result.Select(n => n.Title));
        }
    }
}
