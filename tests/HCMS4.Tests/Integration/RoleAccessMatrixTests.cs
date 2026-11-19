using System.Net;
using System.Reflection;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    /// <summary>
    /// S2: each role reaches only its own endpoints. Every GET route is requested as each of the seven roles;
    /// the response must be 403 exactly when the role does not hold the controller's permission.
    /// </summary>
    public class RoleAccessMatrixTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public RoleAccessMatrixTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        public static IEnumerable<object[]> AllRoles() => Roles.All.Select(r => new object[] { r });

        private List<(string Url, string Policy)> ProtectedGetRoutes()
        {
            var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
            return endpoints
                .Select(e => (Descriptor: e.Metadata.GetMetadata<ControllerActionDescriptor>(),
                              Methods: e.Metadata.GetMetadata<HttpMethodMetadata>()))
                .Where(x => x.Descriptor != null)
                .Where(x => x.Methods == null || x.Methods.HttpMethods.Count == 0 || x.Methods.HttpMethods.Contains("GET"))
                .Where(x => x.Descriptor!.ControllerTypeInfo.GetCustomAttribute<AllowAnonymousAttribute>() == null
                         && x.Descriptor.MethodInfo.GetCustomAttribute<AllowAnonymousAttribute>() == null)
                .Select(x => (Url: $"/{x.Descriptor!.ControllerName}/{x.Descriptor.ActionName}",
                              Policy: x.Descriptor.ControllerTypeInfo.GetCustomAttribute<AuthorizeAttribute>()?.Policy))
                .Where(x => x.Policy != null)
                .Select(x => (x.Url, x.Policy!))
                .Distinct()
                .ToList();
        }

        [Theory]
        [MemberData(nameof(AllRoles))]
        public async Task Role_IsForbidden_ExactlyWhereItLacksThePermission(string role)
        {
            var routes = ProtectedGetRoutes();
            Assert.True(routes.Count > 50, $"Expected the protected GET routes to be discovered, found {routes.Count}.");
            var client = _factory.CreateClientAs(role, $"matrix-{role}");
            var failures = new List<string>();

            foreach (var (url, policy) in routes)
            {
                var allowed = RolePermissions.Has(role, policy);
                HttpStatusCode? status;
                try
                {
                    status = (await client.GetAsync(url)).StatusCode;
                }
                catch (Exception)
                {
                    status = null; // the action ran and failed without its parameters: authorization was passed
                }

                var forbidden = status == HttpStatusCode.Forbidden;
                if (allowed == forbidden)
                {
                    failures.Add($"{url} ({policy}): expected {(allowed ? "access" : "403")}, got {(status?.ToString() ?? "exception")}");
                }
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public async Task NewRoles_ReachNoExistingPage()
        {
            foreach (var role in new[] { Roles.LabTechnician, Roles.Radiologist, Roles.EmergencySupervisor })
            {
                var client = _factory.CreateClientAs(role, $"matrix-{role}");
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Admin/Dashboard")).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Pharmacist/Drugs")).StatusCode);
            }
        }
    }
}
