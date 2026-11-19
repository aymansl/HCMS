using System.Net;
using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Identity.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    /// <summary>UC-L01 — Lab technician log in / log out (TC-L1-01 … TC-L1-04 and the suspended-account exception).</summary>
    public class LabLoginTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private const string Password = "Lab@1234";
        private readonly HcmsWebApplicationFactory _factory;

        public LabLoginTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(string Email, int Id)> CreateTechnicianAsync()
        {
            var email = $"tech{Guid.NewGuid():N}@test.local";
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ILabTechnicianService>();
            var result = await service.CreateAsync(new CreateLabTechnicianViewModel
            {
                FirstName = "Lina", LastName = "Lab", Email = email, Password = Password, ConfirmPassword = Password
            });
            Assert.True(result.Success, result.Message);
            return (email, result.Data.Id);
        }

        private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
            FormHelper.PostFormAsync(client, "/Account/Login", "/Account/Login", new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password
            });

        private static async Task<string> TextAsync(HttpResponseMessage response) =>
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        [Fact]
        public async Task TC_L1_01_ValidCredentials_OpenTheLabDashboard()
        {
            var (email, _) = await CreateTechnicianAsync();
            var client = _factory.CreateClientAs(role: null);

            var login = await LoginAsync(client, email, Password);

            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.Equal("/Lab/Dashboard", login.Headers.Location?.OriginalString);

            var dashboard = await client.GetAsync("/Lab/Dashboard");
            Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
            var html = await TextAsync(dashboard);
            Assert.Contains("Lab Dashboard", html);
            Assert.Contains("Welcome, Lina Lab", html);
        }

        [Fact]
        public async Task TC_L1_02_WrongPassword_ShowsInvalidEmailOrPassword_AndKeepsTheForm()
        {
            var (email, _) = await CreateTechnicianAsync();
            var client = _factory.CreateClientAs(role: null);

            var response = await LoginAsync(client, email, "Wrong@123");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await TextAsync(response);
            Assert.Contains("Invalid email or password.", html);
            Assert.Contains("name=\"Password\"", html);
        }

        [Fact]
        public async Task TC_L1_02_UnknownEmail_ShowsTheSameMessage()
        {
            var response = await LoginAsync(_factory.CreateClientAs(role: null), "nobody@test.local", Password);

            Assert.Contains("Invalid email or password.", await TextAsync(response));
        }

        [Fact]
        public async Task TC_L1_03_FiveFailures_LockTheAccount_EvenForTheRightPassword()
        {
            var (email, _) = await CreateTechnicianAsync();
            var client = _factory.CreateClientAs(role: null);

            for (var i = 0; i < 5; i++)
            {
                await LoginAsync(client, email, "Wrong@123");
            }
            var response = await LoginAsync(client, email, Password);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("temporarily locked", await TextAsync(response));
        }

        [Fact]
        public async Task TC_L1_04_Logout_EndsTheSession_AndTheDashboardRedirectsToLogin()
        {
            var (email, _) = await CreateTechnicianAsync();
            var client = _factory.CreateClientAs(role: null);
            await LoginAsync(client, email, Password);

            var logout = await FormHelper.PostFormAsync(client, "/Lab/Dashboard", "/Account/Logout", new Dictionary<string, string>());
            Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

            var dashboard = await client.GetAsync("/Lab/Dashboard");
            Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
            Assert.StartsWith("/Account/Login", dashboard.Headers.Location?.PathAndQuery ?? dashboard.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task Exception_SuspendedTechnician_CannotSignIn()
        {
            var (email, id) = await CreateTechnicianAsync();
            using (var scope = _factory.Services.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<ILabTechnicianService>();
                Assert.True((await service.SetActiveAsync(id, isActive: false)).Success);
            }

            var response = await LoginAsync(_factory.CreateClientAs(role: null), email, Password);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(AccountStatusMessages.Suspended, await TextAsync(response));
        }

        [Fact]
        public async Task OtherRoles_CannotOpenTheLabDashboard()
        {
            foreach (var role in new[] { "Admin", "Doctor", "Patient", "Pharmacist" })
            {
                var response = await _factory.CreateClientAs(role).GetAsync("/Lab/Dashboard");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
    }
}
