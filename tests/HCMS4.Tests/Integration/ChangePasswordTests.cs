using System.Net;
using HCMS4.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    // Regression tests: a failed password change rendered the non-existent "ChangePassword" view (500 error).
    public class ChangePasswordTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private const string Password = "Valid@123";
        private readonly HcmsWebApplicationFactory _factory;

        public ChangePasswordTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<HttpClient> LoginAsync()
        {
            var email = $"user{Guid.NewGuid():N}@test.local";
            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var created = await userManager.CreateAsync(
                    new ApplicationUser { UserName = email, Email = email, FirstName = "Pass", LastName = "Change" }, Password);
                Assert.True(created.Succeeded);
            }

            var client = _factory.CreateClientAs(role: null);
            await FormHelper.PostFormAsync(client, "/Account/Login", "/Account/Login",
                new Dictionary<string, string> { ["Email"] = email, ["Password"] = Password });
            return client;
        }

        private static Task<HttpResponseMessage> ChangeAsync(HttpClient client, string current, string next, string confirm) =>
            FormHelper.PostFormAsync(client, "/Account/Profile", "/Account/ChangePassword", new Dictionary<string, string>
            {
                ["CurrentPassword"] = current,
                ["NewPassword"] = next,
                ["ConfirmPassword"] = confirm
            });

        [Fact]
        public async Task ChangePassword_WrongCurrentPassword_ReturnsToProfileWithError()
        {
            var client = await LoginAsync();

            var response = await ChangeAsync(client, "Wrong@123", "Newer@456", "Newer@456");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Account/Profile", response.Headers.Location?.OriginalString);
            var profile = await client.GetStringAsync("/Account/Profile");
            Assert.Contains("alert-danger", profile);
        }

        [Fact]
        public async Task ChangePassword_MismatchedConfirmation_ReturnsToProfileWithError()
        {
            var client = await LoginAsync();

            var response = await ChangeAsync(client, Password, "Newer@456", "Other@789");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var profile = await client.GetStringAsync("/Account/Profile");
            Assert.Contains("Passwords do not match", profile);
        }

        [Fact]
        public async Task ChangePassword_Valid_ShowsSuccess()
        {
            var client = await LoginAsync();

            var response = await ChangeAsync(client, Password, "Newer@456", "Newer@456");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var profile = await client.GetStringAsync("/Account/Profile");
            Assert.Contains("Password changed successfully!", profile);
        }
    }
}
