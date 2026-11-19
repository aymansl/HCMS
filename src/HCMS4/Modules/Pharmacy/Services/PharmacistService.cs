using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Common;
using HCMS4.Data;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Pharmacy.Services
{
    public class PharmacistService : IPharmacistService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PharmacistService> _logger;

        public PharmacistService(ApplicationDbContext context, ILogger<PharmacistService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult> HoldPrescriptionAsync(int prescriptionId)
        {
            var prescription = await _context.Prescriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);

            if (prescription == null)
            {
                return ServiceResult.Fail("Prescription not found.");
            }

            if (prescription.Status != PrescriptionStatus.Pending)
            {
                return ServiceResult.Fail("Only pending prescriptions can be put on hold.");
            }

            // Holding leaves the prescription pending, so it stays in the pharmacist's queue.
            _logger.LogInformation("Prescription {PrescriptionId} put on hold", prescriptionId);
            return ServiceResult.Ok("Prescription kept in the pending queue.");
        }

        public async Task<Pharmacist> GetByIdAsync(int id)
        {
            return await _context.Pharmacists
                .AsNoTracking()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Pharmacist?> GetByUserIdAsync(string userId)
        {
            return await _context.Pharmacists
                .AsNoTracking()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);
        }

        public async Task<ServiceResult> UpdateProfileAsync(int id, string qualifications, string? contactInfo, string? shift)
        {
            try
            {
                var pharmacist = await _context.Pharmacists.FindAsync(id);
                if (pharmacist == null)
                {
                    return ServiceResult.Fail("Pharmacist not found.");
                }

                pharmacist.Qualifications = qualifications;
                pharmacist.ContactInfo = contactInfo;
                pharmacist.Shift = shift;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Pharmacist {PharmacistId} profile updated successfully", id);
                return ServiceResult.Ok("Profile updated successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pharmacist profile {PharmacistId}", id);
                return ServiceResult.Fail("An error occurred while updating the profile.");
            }
        }
    }
}
