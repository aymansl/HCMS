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

        public async Task<ServiceResult> MarkPrescriptionCompletedAsync(int prescriptionId, string pharmacistUserId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var prescription = await _context.Prescriptions
                    .Include(p => p.PrescriptionItems)
                        .ThenInclude(pi => pi.Drug)
                    .Include(p => p.Patient)
                        .ThenInclude(pat => pat.User)
                    .FirstOrDefaultAsync(p => p.Id == prescriptionId);

                if (prescription == null)
                {
                    return ServiceResult.Fail("Prescription not found.");
                }

                if (prescription.Status != PrescriptionStatus.Pending)
                {
                    return ServiceResult.Fail("Only pending prescriptions can be marked as completed.");
                }

                // Batch load all drugs for this prescription
                var drugIds = prescription.PrescriptionItems
                    .Where(i => i.DrugId > 0)
                    .Select(i => i.DrugId)
                    .Distinct()
                    .ToList();
                var drugs = await _context.Drugs
                    .Where(d => drugIds.Contains(d.Id))
                    .ToDictionaryAsync(d => d.Id);

                // Check inventory for all items
                var insufficientStock = new List<string>();
                foreach (var item in prescription.PrescriptionItems)
                {
                    if (item.DrugId <= 0 || !drugs.TryGetValue(item.DrugId, out var drug))
                    {
                        insufficientStock.Add($"Item #{item.Id}: Drug not found");
                        continue;
                    }

                    if (drug.Quantity < item.Quantity)
                    {
                        insufficientStock.Add($"{drug.Name}: Required {item.Quantity}, Available {drug.Quantity}");
                    }
                }

                if (insufficientStock.Any())
                {
                    return ServiceResult.Fail(
                        "Cannot complete prescription due to insufficient stock:\n" +
                        string.Join("\n", insufficientStock));
                }

                // Deduct quantities from inventory
                foreach (var item in prescription.PrescriptionItems)
                {
                    if (item.DrugId > 0 && drugs.TryGetValue(item.DrugId, out var drug))
                    {
                        drug.Quantity -= item.Quantity;
                        drug.UpdatedAt = ClinicTime.Now;
                    }
                }

                // Update prescription status
                var pharmacist = await _context.Pharmacists
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.UserId == pharmacistUserId);

                prescription.Status = PrescriptionStatus.Completed;
                prescription.DispensedDate = ClinicTime.Now;
                prescription.DispensedBy = pharmacist?.User?.FullName ?? "Unknown";
                prescription.UpdatedAt = ClinicTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Prescription {PrescriptionId} marked as completed by pharmacist {UserId}. Inventory updated.",
                    prescriptionId, pharmacistUserId);

                return ServiceResult.Ok($"Prescription for {prescription.Patient?.User?.FullName} marked as completed! Inventory has been updated.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error marking prescription {PrescriptionId} as completed", prescriptionId);
                return ServiceResult.Fail($"An error occurred: {ex.Message}");
            }
        }
    }
}
