using System.Net;
using HCMS4.Data;
using HCMS4.Shared.Identity.Authorization;
using HCMS4.Shared.Notifications.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    /// <summary>The notifications page and the menu bell, for every role.</summary>
    public class NotificationsPageTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public NotificationsPageTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        public static IEnumerable<object[]> AllRoles() => Roles.All.Select(r => new object[] { r });

        private async Task<int> AddAsync(string userId, string title, string? link = null)
        {
            var id = 0;
            await _factory.WithDbAsync(async db =>
            {
                var notification = new UserNotification { UserId = userId, Title = title, Message = $"{title} message", LinkUrl = link };
                db.UserNotifications.Add(notification);
                await db.SaveChangesAsync();
                id = notification.Id;
            });
            return id;
        }

        private async Task<bool> IsReadAsync(int id)
        {
            using var scope = _factory.Services.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .UserNotifications.AsNoTracking().SingleAsync(n => n.Id == id)).IsRead;
        }

        private static async Task<string> TextAsync(HttpResponseMessage response) =>
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        [Theory]
        [MemberData(nameof(AllRoles))]
        public async Task EveryRole_SeesItsOwnNotifications_AndTheBellLinksToThem(string role)
        {
            var userId = $"notif-{role}-{Guid.NewGuid():N}";
            await AddAsync(userId, $"Mine {role}");
            await AddAsync("someone-else", $"Not mine {role}");
            var client = _factory.CreateClientAs(role, userId);

            var response = await client.GetAsync("/Notifications");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await TextAsync(response);
            Assert.Contains($"Mine {role}", html);
            Assert.DoesNotContain($"Not mine {role}", html);
            var bell = System.Text.RegularExpressions.Regex.Match(html, "<a[^>]*id=\"notifications-bell\"[^>]*>").Value;
            Assert.Contains("href=\"/Notifications\"", bell);
            Assert.Contains("id=\"unread-notifications\">1<", html);
        }

        [Fact]
        public async Task TheBell_NoLongerLinksToThePatientDashboard()
        {
            var html = await TextAsync(await _factory.CreateClientAs("Doctor", "bell-doctor").GetAsync("/Notifications"));

            var bell = System.Text.RegularExpressions.Regex.Match(html, "<a[^>]*id=\"notifications-bell\"[^>]*>").Value;
            Assert.NotEmpty(bell);
            Assert.DoesNotContain("/Patient/Dashboard", bell);
        }

        [Fact]
        public async Task Open_MarksAsRead_AndGoesToTheLink()
        {
            var userId = $"open-{Guid.NewGuid():N}";
            var id = await AddAsync(userId, "Lab request cancelled", "/Lab/RequestDetails/5");
            var client = _factory.CreateClientAs("LabTechnician", userId);

            var response = await FormHelper.PostFormAsync(client, "/Notifications", $"/Notifications/Open/{id}", new Dictionary<string, string>());

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Lab/RequestDetails/5", response.Headers.Location?.OriginalString);
            Assert.True(await IsReadAsync(id));
            Assert.DoesNotContain("id=\"unread-notifications\"", await TextAsync(await client.GetAsync("/Notifications")));
        }

        [Fact]
        public async Task Open_AnExternalLink_StaysOnTheNotificationsPage()
        {
            var userId = $"ext-{Guid.NewGuid():N}";
            var id = await AddAsync(userId, "External", "https://example.com/phish");
            var client = _factory.CreateClientAs("Patient", userId);

            var response = await FormHelper.PostFormAsync(client, "/Notifications", $"/Notifications/Open/{id}", new Dictionary<string, string>());

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Notifications", response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task Open_AnotherUsersNotification_IsNotFound_AndStaysUnread()
        {
            var id = await AddAsync("owner-user", "Private");
            var client = _factory.CreateClientAs("Patient", $"intruder-{Guid.NewGuid():N}");

            var response = await FormHelper.PostFormAsync(client, "/Notifications", $"/Notifications/Open/{id}", new Dictionary<string, string>());

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.False(await IsReadAsync(id));
        }

        [Fact]
        public async Task MarkAllAsRead_ClearsTheUnreadBadge()
        {
            var userId = $"all-{Guid.NewGuid():N}";
            var first = await AddAsync(userId, "One");
            var second = await AddAsync(userId, "Two");
            var client = _factory.CreateClientAs("Doctor", userId);
            Assert.Contains("id=\"unread-notifications\">2<", await TextAsync(await client.GetAsync("/Notifications")));

            var response = await FormHelper.PostFormAsync(client, "/Notifications", "/Notifications/MarkAllAsRead", new Dictionary<string, string>());

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.True(await IsReadAsync(first));
            Assert.True(await IsReadAsync(second));
            Assert.DoesNotContain("id=\"unread-notifications\"", await TextAsync(await client.GetAsync("/Notifications")));
        }

        [Fact]
        public async Task SignedOut_IsSentToLogin()
        {
            var response = await _factory.CreateClientAs(role: null).GetAsync("/Notifications");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/Login", response.Headers.Location?.PathAndQuery ?? response.Headers.Location?.OriginalString);
        }
    }
}
