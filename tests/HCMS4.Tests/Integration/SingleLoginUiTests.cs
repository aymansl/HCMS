using System.Net;

namespace HCMS4.Tests.Integration
{
    // Regression tests for issue #10: a second, scaffolded login UI existed under /Identity.
    public class SingleLoginUiTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public SingleLoginUiTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/Identity/Account/Login")]
        [InlineData("/Identity/Account/Register")]
        [InlineData("/Identity/Account/Manage")]
        public async Task ScaffoldedIdentityPages_AreNotServed(string url)
        {
            // Signed in, so the fallback policy does not turn the missing page into a login redirect.
            var client = _factory.CreateClientAs("Patient");

            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task MvcLoginPage_IsServed()
        {
            var client = _factory.CreateClientAs(role: null);

            var response = await client.GetAsync("/Account/Login");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
