using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Identity.Models;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    // Regression tests for issue #9: AccessDeniedPath pointed to a missing action (404).
    public class AccessDeniedTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public AccessDeniedTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task AccessDeniedPage_ShowsArabicMessage()
        {
            var client = _factory.CreateClientAs("Doctor");

            var response = await client.GetAsync("/Account/AccessDenied");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("غير مصرح بالوصول", html);
        }

        [Fact]
        public async Task AdminPage_LoggedInDoctor_IsRedirectedToAccessDenied()
        {
            const string password = "Doctor@123";
            var email = $"doctor{Guid.NewGuid():N}@test.local";
            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Real", LastName = "Doctor" };
                Assert.True((await userManager.CreateAsync(user, password)).Succeeded);
                await userManager.AddToRoleAsync(user, "Doctor");
            }

            var client = _factory.CreateClientAs(role: null);
            await FormHelper.PostFormAsync(client, "/Account/Login", "/Account/Login", new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password
            });

            var response = await client.GetAsync("/Admin/Dashboard");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery ?? response.Headers.Location?.OriginalString);
        }
    }
}
