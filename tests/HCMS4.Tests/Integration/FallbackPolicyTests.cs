using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HCMS4.Tests.Integration
{
    // Endpoints must require a signed-in user by default; only marked pages stay public.
    public class FallbackPolicyTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public FallbackPolicyTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public void FallbackPolicy_RequiresAuthenticatedUser()
        {
            var options = _factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

            Assert.NotNull(options.FallbackPolicy);
            Assert.Contains(options.FallbackPolicy!.Requirements,
                r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Home/About")]
        [InlineData("/Account/Login")]
        [InlineData("/Account/Register")]
        public async Task PublicPages_Anonymous_ReturnOk(string url)
        {
            var client = _factory.CreateClientAs(role: null);

            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData("/Account/Profile")]
        [InlineData("/Account/EditProfile")]
        [InlineData("/Patient/Dashboard")]
        public async Task ProtectedPages_Anonymous_RedirectToLoginWithReturnUrl(string url)
        {
            var client = _factory.CreateClientAs(role: null);

            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location?.OriginalString ?? string.Empty;
            Assert.Contains("/Account/Login", location);
            Assert.Contains("ReturnUrl", location);
        }
    }
}
