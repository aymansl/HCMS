using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Identity.Authorization;
using HCMS4.Shared.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace HCMS4.Shared.Identity.Services
{
    public class AdminSeeder
    {
        public static async Task SeedAdminUser(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var roleManager = scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();

            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AdminSeeder>>();

            // All seven roles, including LabTechnician, Radiologist and EmergencySupervisor (S2).
            // Permissions are not stored: they come from RolePermissions.
            foreach (var roleName in Roles.All)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                    logger.LogInformation("Created role: {RoleName}", roleName);
                }
            }

            var adminEmail = config["AdminSettings:Email"];
            var adminUserName = config["AdminSettings:UserName"];
            var adminPassword = config["AdminSettings:Password"];

            // Fail fast: never fall back to a hard-coded admin account.
            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                const string message = "AdminSettings:Email and AdminSettings:Password must be configured " +
                    "(User Secrets in development, environment variables elsewhere). See README.md.";
                logger.LogCritical(message);
                throw new InvalidOperationException(message);
            }

            if (string.IsNullOrWhiteSpace(adminUserName))
            {
                adminUserName = adminEmail;
            }

            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminUserName,
                    Email = adminEmail,
                    FirstName = "System",
                    LastName = "Administrator",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    logger.LogInformation("Admin user created successfully with email {Email}", adminEmail);
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogCritical("Error creating admin user: {Errors}", errors);
                    throw new InvalidOperationException($"Could not create the admin user: {errors}");
                }
            }
            else
            {
                logger.LogInformation("Admin user already exists with email {Email}", adminEmail);
            }
        }
    }
}
