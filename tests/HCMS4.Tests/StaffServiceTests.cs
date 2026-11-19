using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Models;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests
{
    public class StaffServiceTests : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public StaffServiceTests()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var dbName = Guid.NewGuid().ToString();
            services.AddDbContext<ApplicationDbContext>(o => o
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton(ClinicTime.Default);
            services.AddScoped<IStaffService, StaffService>();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();

            var roles = Scope<RoleManager<IdentityRole>>();
            foreach (var role in new[] { "Doctor", "Pharmacist" })
            {
                roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
            }
        }

        private T Scope<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();
        private IStaffService Service => Scope<IStaffService>();
        private ApplicationDbContext Db => Scope<ApplicationDbContext>();

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
        }

        [Fact]
        public async Task CreateDoctorAsync_CreatesUserInDoctorRoleAndProfile()
        {
            var result = await Service.CreateDoctorAsync(new DoctorRegisterViewModel
            {
                Email = "dr@test.local", FirstName = "Sam", LastName = "Doe", Password = "Doctor@123", SpecializationId = 1
            });

            Assert.True(result.Success);
            var user = await Scope<UserManager<ApplicationUser>>().FindByEmailAsync("dr@test.local");
            Assert.True(await Scope<UserManager<ApplicationUser>>().IsInRoleAsync(user!, "Doctor"));
            Assert.True(await Db.Doctors.AnyAsync(d => d.UserId == user!.Id));
        }

        [Fact]
        public async Task CreateDoctorAsync_WeakPassword_ReturnsErrors()
        {
            var result = await Service.CreateDoctorAsync(new DoctorRegisterViewModel
            {
                Email = "weak@test.local", FirstName = "A", LastName = "B", Password = "weak", SpecializationId = 1
            });

            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public async Task CreateUpdateDeletePharmacist_Works()
        {
            var created = await Service.CreatePharmacistAsync(new PharmacistRegisterViewModel
            {
                Email = "ph@test.local", FirstName = "Pat", LastName = "Ph", Password = "Pharm@123", Qualifications = "BPharm"
            });
            Assert.True(created.Success);

            var pharmacist = (await Service.GetPharmacistsAsync()).Single();
            var edit = await Service.GetPharmacistForEditAsync(pharmacist.Id);
            edit!.Shift = "Night";
            Assert.True((await Service.UpdatePharmacistAsync(edit)).Success);
            Assert.Equal("Night", (await Service.GetPharmacistForEditAsync(pharmacist.Id))!.Shift);

            Assert.False((await Service.DeletePharmacistAsync(pharmacist.Id, "ph@test.local")).Success); // own account
            Assert.True((await Service.DeletePharmacistAsync(pharmacist.Id, "admin@test.local")).Success);
            Assert.Empty(await Service.GetPharmacistsAsync());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        public async Task UpdateSpecializationFeeAsync_NonPositive_IsRejected(decimal fee)
        {
            Db.Specializations.Add(new Specialization { Id = 900, Name = "Test", ConsultationFee = 50, IsActive = true });
            await Db.SaveChangesAsync();

            Assert.False((await Service.UpdateSpecializationFeeAsync(900, fee)).Success);
        }

        [Fact]
        public async Task UpdateSpecializationFeeAsync_Valid_Updates()
        {
            Db.Specializations.Add(new Specialization { Id = 901, Name = "Test", ConsultationFee = 50, IsActive = true });
            await Db.SaveChangesAsync();

            Assert.True((await Service.UpdateSpecializationFeeAsync(901, 75)).Success);
            Assert.Equal(75, (await Service.GetSpecializationsAsync()).Single(s => s.Id == 901).ConsultationFee);
        }

        [Fact]
        public async Task DeleteUserAsync_CannotDeleteSelf_AndDeletesDoctorWithAppointments()
        {
            var (user, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(Db);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(Db);
            Db.Appointments.Add(new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2027, 1, 1) });
            await Db.SaveChangesAsync();
            var userManager = Scope<UserManager<ApplicationUser>>();
            await userManager.UpdateSecurityStampAsync(user);

            Assert.False((await Service.DeleteUserAsync(user.Id, user.Id)).Success);

            var result = await Service.DeleteUserAsync(user.Id, "someone-else");

            Assert.True(result.Success);
            Assert.False(await Db.Doctors.AnyAsync(d => d.Id == doctor.Id));
            Assert.False(await Db.Appointments.AnyAsync(a => a.DoctorId == doctor.Id));
        }

        [Fact]
        public async Task CountScheduledAppointmentsAsync_CountsOnlyScheduledInRange()
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(Db);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(Db);
            Db.Appointments.AddRange(
                new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2027, 1, 2, 10, 0, 0), Status = AppointmentStatus.Scheduled },
                new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2027, 1, 3, 10, 0, 0), Status = AppointmentStatus.Canceled },
                new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2027, 2, 1, 10, 0, 0), Status = AppointmentStatus.Scheduled });
            await Db.SaveChangesAsync();

            Assert.Equal(1, await Service.CountScheduledAppointmentsAsync(doctor.Id, new DateTime(2027, 1, 1), new DateTime(2027, 1, 31)));
        }

        [Fact]
        public async Task GetDoctorLeavesAsync_IncludesRequestsAndApprovedLeaves_OrNullForUnknownDoctor()
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(Db);
            Db.DoctorLeaves.Add(new DoctorLeave { DoctorId = doctor.Id, StartDate = new DateTime(2027, 1, 1), EndDate = new DateTime(2027, 1, 2), Status = LeaveStatus.Approved });
            Db.LeaveRequests.Add(new LeaveRequest { DoctorId = doctor.Id, StartDate = new DateTime(2027, 3, 1), EndDate = new DateTime(2027, 3, 2), Status = LeaveStatus.Rejected, RejectionReason = "busy" });
            await Db.SaveChangesAsync();

            var view = await Service.GetDoctorLeavesAsync(doctor.Id);

            Assert.NotNull(view);
            Assert.Single(view!.ApprovedLeaves);
            Assert.Equal("busy", Assert.Single(view.LeaveRequests).RejectionReason);
            Assert.Null(await Service.GetDoctorLeavesAsync(999999));
        }

        [Fact]
        public async Task GetDoctorAndProfile_LoadUserAndSpecialization()
        {
            var (_, doctor, specialization) = await TestDbContextFactory.CreateTestDoctorAsync(Db);

            Assert.NotNull((await Service.GetDoctorAsync(doctor.Id))!.User);
            Assert.Equal(specialization.Id, (await Service.GetDoctorProfileAsync(doctor.Id))!.Specialization!.Id);
            Assert.Contains(await Service.GetDoctorsAsync(), d => d.Id == doctor.Id);
            Assert.NotNull(await Service.GetActiveSpecializationsAsync());
        }
    }
}
