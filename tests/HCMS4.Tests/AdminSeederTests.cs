using HCMS4.Data;
using HCMS4.Models;
using HCMS4.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests
{
    // Regression tests for issue #2: the seeder fell back to a hard-coded admin password.
    public class AdminSeederTests
    {
        private static ServiceProvider BuildServices(Dictionary<string, string?> settings, string databaseName)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName));
            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            return services.BuildServiceProvider();
        }

        [Theory]
        [InlineData(null, "Test@12345")]
        [InlineData("admin@test.local", null)]
        [InlineData("", "")]
        public async Task SeedAdminUser_MissingSettings_Throws(string? email, string? password)
        {
            using var provider = BuildServices(new Dictionary<string, string?>
            {
                ["AdminSettings:Email"] = email,
                ["AdminSettings:Password"] = password
            }, Guid.NewGuid().ToString());

            await Assert.ThrowsAsync<InvalidOperationException>(() => AdminSeeder.SeedAdminUser(provider));

            using var scope = provider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Null(await userManager.FindByEmailAsync("admin@hospital.com"));
        }

        [Fact]
        public async Task SeedAdminUser_WithSettings_CreatesAdminInAdminRole()
        {
            using var provider = BuildServices(new Dictionary<string, string?>
            {
                ["AdminSettings:Email"] = "admin@test.local",
                ["AdminSettings:UserName"] = "testadmin",
                ["AdminSettings:Password"] = "Test@12345"
            }, Guid.NewGuid().ToString());

            await AdminSeeder.SeedAdminUser(provider);

            using var scope = provider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = await userManager.FindByEmailAsync("admin@test.local");
            Assert.NotNull(admin);
            Assert.True(await userManager.IsInRoleAsync(admin!, "Admin"));
            Assert.True(await userManager.CheckPasswordAsync(admin!, "Test@12345"));
        }

        [Fact]
        public async Task SeedAdminUser_RunTwice_DoesNotDuplicateAdmin()
        {
            using var provider = BuildServices(new Dictionary<string, string?>
            {
                ["AdminSettings:Email"] = "admin@test.local",
                ["AdminSettings:Password"] = "Test@12345"
            }, Guid.NewGuid().ToString());

            await AdminSeeder.SeedAdminUser(provider);
            await AdminSeeder.SeedAdminUser(provider);

            using var scope = provider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Single(await userManager.GetUsersInRoleAsync("Admin"));
        }

        [Fact]
        public async Task SeedAdminUser_WeakPassword_Throws()
        {
            using var provider = BuildServices(new Dictionary<string, string?>
            {
                ["AdminSettings:Email"] = "admin@test.local",
                ["AdminSettings:Password"] = "weak"
            }, Guid.NewGuid().ToString());

            await Assert.ThrowsAsync<InvalidOperationException>(() => AdminSeeder.SeedAdminUser(provider));
        }
    }
}
