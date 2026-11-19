using System.Net;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    // Regression: a patient disabled by the admin, or an inactive pharmacist, could still sign in.
    public class SuspendedAccountLoginTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private const string Password = "Valid@123";
        private readonly HcmsWebApplicationFactory _factory;

        public SuspendedAccountLoginTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(string Email, string UserId)> CreateUserAsync(string role)
        {
            var email = $"{role.ToLowerInvariant()}{Guid.NewGuid():N}@test.local";
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Status", LastName = "Test" };
            Assert.True((await userManager.CreateAsync(user, Password)).Succeeded);
            return (email, user.Id);
        }

        private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
            FormHelper.PostFormAsync(client, "/Account/Login", "/Account/Login", new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password
            });

        private static async Task AssertRefusedAsSuspended(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(AccountStatusMessages.Suspended, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
            Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies)
                         && cookies.Any(c => c.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal)
                                             && !c.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase)),
                "No sign-in cookie may be issued for a suspended account.");
        }

        [Fact]
        public async Task DisabledPatient_CannotSignIn()
        {
            var (email, userId) = await CreateUserAsync("Patient");
            await _factory.WithDbAsync(async db =>
            {
                db.Patients.Add(new Patient { UserId = userId, DateOfBirth = new DateTime(1990, 1, 1), Status = PatientStatus.Disabled, DisableReason = "test" });
                await db.SaveChangesAsync();
            });

            await AssertRefusedAsSuspended(await LoginAsync(_factory.CreateClientAs(role: null), email, Password));
        }

        [Fact]
        public async Task InactivePharmacist_CannotSignIn()
        {
            var (email, userId) = await CreateUserAsync("Pharmacist");
            await _factory.WithDbAsync(async db =>
            {
                db.Pharmacists.Add(new Pharmacist { UserId = userId, IsActive = false });
                await db.SaveChangesAsync();
            });

            await AssertRefusedAsSuspended(await LoginAsync(_factory.CreateClientAs(role: null), email, Password));
        }

        [Fact]
        public async Task ActivePatient_StillSignsIn()
        {
            var (email, userId) = await CreateUserAsync("Patient");
            await _factory.WithDbAsync(async db =>
            {
                db.Patients.Add(new Patient { UserId = userId, DateOfBirth = new DateTime(1990, 1, 1), Status = PatientStatus.Active });
                await db.SaveChangesAsync();
            });

            var response = await LoginAsync(_factory.CreateClientAs(role: null), email, Password);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        [Fact]
        public async Task SuspendedAccount_WithWrongPassword_GetsTheNormalError()
        {
            // The suspension is only revealed to someone who knows the password.
            var (email, userId) = await CreateUserAsync("Patient");
            await _factory.WithDbAsync(async db =>
            {
                db.Patients.Add(new Patient { UserId = userId, DateOfBirth = new DateTime(1990, 1, 1), Status = PatientStatus.Disabled });
                await db.SaveChangesAsync();
            });

            var response = await LoginAsync(_factory.CreateClientAs(role: null), email, "Wrong@123");
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

            Assert.DoesNotContain(AccountStatusMessages.Suspended, html);
        }
    }
}
