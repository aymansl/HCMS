using HCMS4.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace HCMS4.Services
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

            string[] roleNames = { "Admin", "Doctor", "Patient", "Pharmacist" };
            foreach (var roleName in roleNames)
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

            // If no User Secrets are configured, fall back to defaults (with a warning)
            if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
            {
                logger.LogWarning("Admin credentials not found in User Secrets. Using default admin credentials. " +
                    "Run: dotnet user-secrets set \"AdminSettings:Email\" \"admin@hospital.com\"");
                adminEmail = "admin@hospital.com";
                adminUserName = "admin";
                adminPassword = "Admin@123";
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
                    logger.LogError("Error creating admin user: {Errors}",
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogInformation("Admin user already exists with email {Email}", adminEmail);
            }
        }
    }
}
