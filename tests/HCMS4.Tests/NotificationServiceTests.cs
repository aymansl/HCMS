using HCMS4.Data;
using HCMS4.Shared.Identity.Authorization;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Tests
{
    /// <summary>A user's notifications: unread count and mark as read.</summary>
    public class NotificationServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();
        private NotificationService Service => new(_db);

        public void Dispose() => _db.Dispose();

        private async Task<int> AddAsync(string userId, bool isRead = false, string? link = null)
        {
            var notification = new UserNotification { UserId = userId, Title = "T", Message = "M", IsRead = isRead, LinkUrl = link };
            _db.UserNotifications.Add(notification);
            await _db.SaveChangesAsync();
            return notification.Id;
        }

        [Fact]
        public async Task CountUnread_CountsOnlyTheUsersUnreadNotifications()
        {
            await AddAsync("a");
            await AddAsync("a");
            await AddAsync("a", isRead: true);
            await AddAsync("b");

            Assert.Equal(2, await Service.CountUnreadAsync("a"));
            Assert.Equal(0, await Service.CountUnreadAsync(""));
        }

        [Fact]
        public async Task MarkAsRead_MarksTheUsersNotification_AndReturnsIt()
        {
            var id = await AddAsync("a", link: "/Lab/RequestDetails/1");

            var notification = await Service.MarkAsReadAsync("a", id);

            Assert.NotNull(notification);
            Assert.Equal("/Lab/RequestDetails/1", notification!.LinkUrl);
            Assert.True((await _db.UserNotifications.AsNoTracking().SingleAsync(n => n.Id == id)).IsRead);
            Assert.NotNull(await Service.MarkAsReadAsync("a", id)); // already read: still returned
        }

        [Fact]
        public async Task MarkAsRead_AnotherUsersNotification_IsNull_AndUnchanged()
        {
            var id = await AddAsync("b");

            Assert.Null(await Service.MarkAsReadAsync("a", id));
            Assert.False((await _db.UserNotifications.AsNoTracking().SingleAsync(n => n.Id == id)).IsRead);
        }

        [Fact]
        public async Task MarkAllAsRead_ChangesOnlyTheUsersUnreadNotifications()
        {
            await AddAsync("a");
            await AddAsync("a");
            await AddAsync("a", isRead: true);
            await AddAsync("b");

            Assert.Equal(2, await Service.MarkAllAsReadAsync("a"));
            Assert.Equal(0, await Service.CountUnreadAsync("a"));
            Assert.Equal(1, await Service.CountUnreadAsync("b"));
            Assert.Equal(0, await Service.MarkAllAsReadAsync("a"));
        }

        [Fact]
        public void EveryRole_HoldsTheNotificationsPermission()
        {
            Assert.All(Roles.All, role => Assert.True(RolePermissions.Has(role, Permissions.Notifications.View), role));
        }
    }
}
