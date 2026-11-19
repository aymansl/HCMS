using HCMS4.Data;
using HCMS4.Shared.Identity.Services;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Staff.Services
{
    /// <summary>A pharmacist marked inactive by the admin cannot sign in.</summary>
    public class PharmacistAccountStatusCheck : IAccountStatusCheck
    {
        private readonly ApplicationDbContext _context;

        public PharmacistAccountStatusCheck(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<bool> IsSuspendedAsync(string userId) =>
            _context.Pharmacists.AsNoTracking().AnyAsync(p => p.UserId == userId && !p.IsActive);
    }
}
