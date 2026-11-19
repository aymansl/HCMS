using HCMS4.Data;
using HCMS4.Modules.Records.Models;
using HCMS4.Shared.Identity.Services;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Records.Services
{
    /// <summary>A patient disabled by the admin cannot sign in.</summary>
    public class PatientAccountStatusCheck : IAccountStatusCheck
    {
        private readonly ApplicationDbContext _context;

        public PatientAccountStatusCheck(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<bool> IsSuspendedAsync(string userId) =>
            _context.Patients.AsNoTracking().AnyAsync(p => p.UserId == userId && p.Status == PatientStatus.Disabled);
    }
}
