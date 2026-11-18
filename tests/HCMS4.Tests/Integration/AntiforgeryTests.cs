using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HCMS4.Tests.Integration
{
    // Every unsafe request must carry a valid antiforgery token.
    public class AntiforgeryTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public AntiforgeryTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public void GlobalAntiforgeryFilter_IsRegistered()
        {
            var mvcOptions = _factory.Services.GetRequiredService<IOptions<MvcOptions>>().Value;

            Assert.Contains(mvcOptions.Filters, f => f is AutoValidateAntiforgeryTokenAttribute);
        }

        [Theory]
        [InlineData("Pharmacist", "/Pharmacist/UpdateStock/1")]
        [InlineData("Patient", "/Patient/ProcessPayment")]
        [InlineData("Admin", "/Admin/EnablePatient/1")]
        public async Task Post_WithoutToken_IsRejected(string role, string url)
        {
            var client = _factory.CreateClientAs(role);

            var response = await client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
