using System.Net;

namespace HCMS4.Tests.Integration
{
    public class SmokeTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public SmokeTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task HomePage_Anonymous_ReturnsOk()
        {
            var client = _factory.CreateClientAs(role: null);

            var response = await client.GetAsync("/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task LoginPage_Anonymous_ReturnsOk()
        {
            var client = _factory.CreateClientAs(role: null);

            var response = await client.GetAsync("/Account/Login");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
