using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    // Phase 5 splits the big controllers into module controllers. Every old URL must keep working:
    // the set of (controller, action, HTTP verbs) routes must stay exactly as recorded in RouteInventory.txt.
    public class RouteInventoryTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public RouteInventoryTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private List<string> CurrentRoutes()
        {
            var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
            return endpoints
                .Select(e => (Descriptor: e.Metadata.GetMetadata<ControllerActionDescriptor>(),
                              Methods: e.Metadata.GetMetadata<HttpMethodMetadata>()))
                .Where(x => x.Descriptor != null)
                .Select(x =>
                {
                    var verbs = x.Methods?.HttpMethods.Count > 0 ? string.Join(",", x.Methods.HttpMethods.OrderBy(m => m)) : "ANY";
                    return $"{verbs} /{x.Descriptor!.ControllerName}/{x.Descriptor.ActionName}";
                })
                .Distinct()
                .OrderBy(r => r, StringComparer.Ordinal)
                .ToList();
        }

        [Fact]
        public void Routes_MatchTheRecordedInventory()
        {
            var expectedPath = Path.Combine(AppContext.BaseDirectory, "Integration", "RouteInventory.txt");
            var actual = CurrentRoutes();

            if (Environment.GetEnvironmentVariable("HCMS_WRITE_ROUTE_INVENTORY") == "1")
            {
                File.WriteAllLines(Environment.GetEnvironmentVariable("HCMS_ROUTE_INVENTORY_OUT")!, actual);
                return;
            }

            var expected = File.ReadAllLines(expectedPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            Assert.Equal(expected, actual);
        }
    }
}
