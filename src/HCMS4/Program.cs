using HCMS4.Data;
using HCMS4.Modules.Appointments;
using HCMS4.Modules.Billing;
using HCMS4.Modules.Feedback;
using HCMS4.Modules.Pharmacy;
using HCMS4.Modules.Portal;
using HCMS4.Modules.Records;
using HCMS4.Modules.Staff;
using HCMS4.Shared.Audit;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity;
using HCMS4.Shared.Identity.Services;
using HCMS4.Shared.Notifications;
using HCMS4.Shared.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddControllersWithViews(options =>
{
    // Validate the antiforgery token on every unsafe request (POST, PUT, DELETE, ...).
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
})
.AddRazorOptions(options =>
{
    // Module controllers find their views in /Modules/{Module}/Views.
    options.ViewLocationExpanders.Add(new ModuleViewLocationExpander());
});


builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true; 
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict; 
});

builder.Services.AddHcmsIdentity();


builder.Services.AddAuthorization(options =>
{
    // Every endpoint requires a signed-in user unless it is marked [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("DoctorOnly", policy => policy.RequireRole("Doctor"));
    options.AddPolicy("PatientOnly", policy => policy.RequireRole("Patient"));
    options.AddPolicy("AdminOrDoctor", policy =>
        policy.RequireRole("Admin", "Doctor"));
});

// Shared services, then one registration call per module.
builder.Services
    .AddSharedCommon()
    .AddNotifications()
    .AddAudit()
    .AddReporting()
    .AddAppointmentsModule()
    .AddRecordsModule()
    .AddPharmacyModule()
    .AddBillingModule()
    .AddFeedbackModule()
    .AddStaffModule()
    .AddPortalModule();

var cultureInfo = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed admin on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (dbContext.Database.IsRelational())
    {
        dbContext.Database.Migrate();
    }
    else
    {
        // Non-relational providers (integration tests) cannot run migrations.
        dbContext.Database.EnsureCreated();
    }

    await AdminSeeder.SeedAdminUser(scope.ServiceProvider);
}

app.Run();

// Exposes the entry point to WebApplicationFactory in the integration tests.
public partial class Program { }
