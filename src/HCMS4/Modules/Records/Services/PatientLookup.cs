using HCMS4.Data;
using HCMS4.Shared.Identity.Services;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Records.Services
{
    public class PatientLookup : IPatientLookup
    {
        private readonly ApplicationDbContext _context;

        public PatientLookup(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int?> GetPatientIdAsync(string userId)
        {
            return await _context.Patients
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();
        }
    }
}
