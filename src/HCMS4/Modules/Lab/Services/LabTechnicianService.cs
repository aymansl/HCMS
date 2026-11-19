using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Lab.Services
{
    /// <summary>Lab technician accounts (UC-L01): created and suspended by the admin.</summary>
    public interface ILabTechnicianService
    {
        Task<List<LabTechnician>> GetAllAsync();
        Task<LabTechnician?> GetByUserIdAsync(string userId);
        Task<ServiceResult<LabTechnician>> CreateAsync(CreateLabTechnicianViewModel model);
        /// <summary>Suspends or reactivates a technician. Suspending also ends the technician's open sessions.</summary>
        Task<ServiceResult> SetActiveAsync(int id, bool isActive);
    }

    public class LabTechnicianService : ILabTechnicianService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IClinicClock _clock;

        public LabTechnicianService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IClinicClock clock)
        {
            _context = context;
            _userManager = userManager;
            _clock = clock;
        }

        public Task<List<LabTechnician>> GetAllAsync() =>
            _context.LabTechnicians.AsNoTracking()
                .Include(t => t.User)
                .OrderBy(t => t.User.FirstName).ThenBy(t => t.User.LastName)
                .ToListAsync();

        public Task<LabTechnician?> GetByUserIdAsync(string userId) =>
            _context.LabTechnicians.AsNoTracking()
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);

        public async Task<ServiceResult<LabTechnician>> CreateAsync(CreateLabTechnicianViewModel model)
        {
            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                return ServiceResult<LabTechnician>.Fail("An account with this email already exists.");
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim()
            };

            var created = await _userManager.CreateAsync(user, model.Password);
            if (!created.Succeeded)
            {
                return ServiceResult<LabTechnician>.Fail("Could not create the lab technician account.",
                    created.Errors.Select(e => e.Description).ToList());
            }

            await _userManager.AddToRoleAsync(user, Roles.LabTechnician);

            var technician = new LabTechnician
            {
                UserId = user.Id,
                Qualifications = model.Qualifications?.Trim(),
                ContactInfo = model.ContactInfo?.Trim(),
                IsActive = true,
                CreatedAt = _clock.Now
            };
            _context.LabTechnicians.Add(technician);
            await _context.SaveChangesAsync();

            return ServiceResult<LabTechnician>.Ok(technician,
                $"Lab technician account for {user.FullName} created successfully.");
        }

        public async Task<ServiceResult> SetActiveAsync(int id, bool isActive)
        {
            var technician = await _context.LabTechnicians.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id);
            if (technician == null)
            {
                return ServiceResult.Fail("Lab technician not found.");
            }

            if (technician.IsActive == isActive)
            {
                return ServiceResult.Fail(isActive ? "The account is already active." : "The account is already suspended.");
            }

            technician.IsActive = isActive;
            technician.DeactivatedAt = isActive ? null : _clock.Now;
            await _context.SaveChangesAsync();

            if (!isActive)
            {
                // A new security stamp invalidates the technician's existing sign-in cookie.
                await _userManager.UpdateSecurityStampAsync(technician.User);
            }

            return ServiceResult.Ok(isActive
                ? $"{technician.FullName}'s account was reactivated."
                : $"{technician.FullName}'s account was suspended.");
        }
    }

    /// <summary>A suspended lab technician cannot sign in.</summary>
    public class LabTechnicianAccountStatusCheck : IAccountStatusCheck
    {
        private readonly ApplicationDbContext _context;

        public LabTechnicianAccountStatusCheck(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<bool> IsSuspendedAsync(string userId) =>
            _context.LabTechnicians.AsNoTracking().AnyAsync(t => t.UserId == userId && !t.IsActive);
    }
}
