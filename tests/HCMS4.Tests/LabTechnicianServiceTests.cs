using HCMS4.Data;
using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using HCMS4.Shared.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace HCMS4.Tests
{
    public class LabTechnicianServiceTests : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        // 2026-11-19 09:00 UTC = 12:00 Damascus.
        private readonly ClinicClock _clock = new(
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero)), ClinicTime.TimeZone);

        public LabTechnicianServiceTests()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var dbName = Guid.NewGuid().ToString();
            services.AddDbContext<ApplicationDbContext>(o => o
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton<IClinicClock>(_clock);
            services.AddScoped<ILabTechnicianService, LabTechnicianService>();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();
            Get<RoleManager<IdentityRole>>().CreateAsync(new IdentityRole(Roles.LabTechnician)).GetAwaiter().GetResult();
        }

        private T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();
        private ILabTechnicianService Service => Get<ILabTechnicianService>();
        private ApplicationDbContext Db => Get<ApplicationDbContext>();

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
        }

        private static CreateLabTechnicianViewModel Form(string email, string password = "Lab@1234") => new()
        {
            FirstName = " Lina ", LastName = "Lab", Email = email, Password = password, ConfirmPassword = password,
            Qualifications = " B.Sc. ", ContactInfo = "0999"
        };

        [Fact]
        public async Task Create_MakesAnActiveTechnicianInTheLabTechnicianRole()
        {
            var result = await Service.CreateAsync(Form("lina@test.local"));

            Assert.True(result.Success);
            var user = await Get<UserManager<ApplicationUser>>().FindByEmailAsync("lina@test.local");
            Assert.True(await Get<UserManager<ApplicationUser>>().IsInRoleAsync(user!, Roles.LabTechnician));
            var technician = await Db.LabTechnicians.AsNoTracking().SingleAsync();
            Assert.True(technician.IsActive);
            Assert.Equal("B.Sc.", technician.Qualifications);
            Assert.Equal(new DateTime(2026, 11, 19, 12, 0, 0), technician.CreatedAt);
            Assert.Equal("Lina", user!.FirstName);
        }

        [Fact]
        public async Task Create_DuplicateEmail_IsRejected()
        {
            Assert.True((await Service.CreateAsync(Form("dup@test.local"))).Success);

            var second = await Service.CreateAsync(Form("dup@test.local"));

            Assert.False(second.Success);
            Assert.Equal(1, await Db.LabTechnicians.CountAsync());
        }

        [Fact]
        public async Task Create_WeakPassword_ReturnsTheIdentityErrors_AndNoProfile()
        {
            var result = await Service.CreateAsync(Form("weak@test.local", "weak"));

            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.False(await Db.LabTechnicians.AnyAsync());
        }

        [Fact]
        public async Task Suspend_ThenReactivate_RecordsTheTime_AndTheStatusCheckFollows()
        {
            var created = await Service.CreateAsync(Form("s@test.local"));
            var check = new LabTechnicianAccountStatusCheck(Db);
            var userId = created.Data.UserId;
            var stampBefore = (await Get<UserManager<ApplicationUser>>().FindByIdAsync(userId))!.SecurityStamp;

            Assert.True((await Service.SetActiveAsync(created.Data.Id, false)).Success);
            var suspended = await Db.LabTechnicians.AsNoTracking().SingleAsync();
            Assert.False(suspended.IsActive);
            Assert.Equal(new DateTime(2026, 11, 19, 12, 0, 0), suspended.DeactivatedAt);
            Assert.True(await check.IsSuspendedAsync(userId));
            // Suspending ends the technician's open sessions.
            Assert.NotEqual(stampBefore, (await Get<UserManager<ApplicationUser>>().FindByIdAsync(userId))!.SecurityStamp);

            Assert.True((await Service.SetActiveAsync(created.Data.Id, true)).Success);
            Assert.Null((await Db.LabTechnicians.AsNoTracking().SingleAsync()).DeactivatedAt);
            Assert.False(await check.IsSuspendedAsync(userId));
        }

        [Fact]
        public async Task SetActive_SameState_OrUnknownId_Fails()
        {
            var created = await Service.CreateAsync(Form("x@test.local"));

            Assert.False((await Service.SetActiveAsync(created.Data.Id, true)).Success);
            Assert.False((await Service.SetActiveAsync(999999, false)).Success);
        }

        [Fact]
        public async Task Queries_ListAndFindByUser()
        {
            var created = await Service.CreateAsync(Form("q@test.local"));

            Assert.Single(await Service.GetAllAsync());
            Assert.Equal("Lina Lab", (await Service.GetByUserIdAsync(created.Data.UserId))!.FullName);
            Assert.Null(await Service.GetByUserIdAsync("someone-else"));
            Assert.False(await new LabTechnicianAccountStatusCheck(Db).IsSuspendedAsync("someone-else"));
        }
    }
}
