using HCMS4.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HCMS4.Tests.Integration
{
    /// <summary>
    /// Hosts the real web app in memory with an InMemory database and a header-based test login.
    /// </summary>
    public class HcmsWebApplicationFactory : WebApplicationFactory<Program>
    {
        public const string AdminEmail = "admin@test.local";
        public const string AdminPassword = "Test@12345";

        private const string SelectorScheme = "TestOrIdentity";
        private readonly string _databaseName = $"hcms-tests-{Guid.NewGuid()}";

        /// <summary>Temporary folder used instead of App_Data for complaint attachments.</summary>
        public string UploadsPath { get; } = Path.Combine(Path.GetTempPath(), $"hcms-tests-uploads-{Guid.NewGuid():N}");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "InMemory",
                    ["AdminSettings:Email"] = AdminEmail,
                    ["AdminSettings:UserName"] = "testadmin",
                    ["AdminSettings:Password"] = AdminPassword,
                    ["AiService:BaseUrl"] = "http://127.0.0.1:1",
                    ["AiService:ApiKey"] = "test-key",
                    ["Uploads:ComplaintsPath"] = UploadsPath
                });
            });

            builder.ConfigureServices(services =>
            {
                // Replace SQL Server with an InMemory database shared by this factory instance.
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options
                    .UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

                // The nightly report job is not needed in tests.
                services.RemoveAll<IHostedService>();

                // Requests with the X-Test-Role header use the test scheme; others use the real Identity cookie.
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { })
                    .AddPolicyScheme(SelectorScheme, SelectorScheme, options =>
                    {
                        options.ForwardDefaultSelector = context =>
                            context.Request.Headers.ContainsKey(TestAuthHandler.RoleHeader)
                                ? TestAuthHandler.SchemeName
                                : IdentityConstants.ApplicationScheme;
                    });

                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = SelectorScheme;
                    options.DefaultAuthenticateScheme = SelectorScheme;
                    options.DefaultChallengeScheme = SelectorScheme;
                    options.DefaultForbidScheme = SelectorScheme;
                    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultSignOutScheme = IdentityConstants.ApplicationScheme;
                });
            });
        }

        /// <summary>Creates a client that does not follow redirects and is logged in with the given role.</summary>
        public HttpClient CreateClientAs(string? role, string? userId = null)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            if (role != null)
            {
                client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
            }
            if (userId != null)
            {
                client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            }
            return client;
        }

        /// <summary>Runs an action against the app's database inside a service scope.</summary>
        public async Task WithDbAsync(Func<ApplicationDbContext, Task> action)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await action(db);
        }
    }
}
