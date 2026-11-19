using HCMS4.Data;
using HCMS4.Shared.Identity.Services;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Staff.Services
{
    public class DoctorLookup : IDoctorLookup
    {
        private readonly ApplicationDbContext _context;

        public DoctorLookup(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int?> GetDoctorIdAsync(string userId)
        {
            return await _context.Doctors
                .AsNoTracking()
                .Where(d => d.UserId == userId)
                .Select(d => (int?)d.Id)
                .FirstOrDefaultAsync();
        }
    }
}
