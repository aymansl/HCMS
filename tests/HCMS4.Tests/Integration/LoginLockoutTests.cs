using HCMS4.Shared.Identity.Models;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    // Regression tests for issue #6 (lockout never armed) and the open returnUrl redirect.
    public class LoginLockoutTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private const string Password = "Valid@123";
        private const string LockedMessage = "تم قفل الحساب مؤقتاً";
        private readonly HcmsWebApplicationFactory _factory;

        public LoginLockoutTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<string> CreateUserAsync()
        {
            var email = $"user{Guid.NewGuid():N}@test.local";
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await userManager.CreateAsync(
                new ApplicationUser { UserName = email, Email = email, FirstName = "Lock", LastName = "Test" },
                Password);
            Assert.True(result.Succeeded);
            return email;
        }

        private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, string? returnUrl = null)
        {
            var postUrl = returnUrl == null
                ? "/Account/Login"
                : $"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";
            return FormHelper.PostFormAsync(client, "/Account/Login", postUrl, new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password
            });
        }

        [Fact]
        public async Task Login_CorrectPassword_RedirectsToHome()
        {
            var email = await CreateUserAsync();
            var client = _factory.CreateClientAs(role: null);

            var response = await LoginAsync(client, email, Password);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        [Fact]
        public async Task Login_FiveFailedAttempts_LocksAccountAndRejectsCorrectPassword()
        {
            var email = await CreateUserAsync();
            var client = _factory.CreateClientAs(role: null);

            for (var i = 0; i < 5; i++)
            {
                await LoginAsync(client, email, "Wrong@123");
            }
            var response = await LoginAsync(client, email, Password);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // Razor HTML-encodes Arabic text, so decode before searching.
            Assert.Contains(LockedMessage, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));

            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.True(await userManager.IsLockedOutAsync(user!));
        }

        [Fact]
        public async Task Login_ExternalReturnUrl_IsIgnored()
        {
            var email = await CreateUserAsync();
            var client = _factory.CreateClientAs(role: null);

            var response = await LoginAsync(client, email, Password, "https://evil.example/phish");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.DoesNotContain("evil.example", response.Headers.Location?.OriginalString);
        }
    }
}
