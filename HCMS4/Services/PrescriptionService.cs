using HCMS4.Data;
using HCMS4.Models;
using HCMS4.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Services
{
    public class PrescriptionService : IPrescriptionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PrescriptionService> _logger;

        public PrescriptionService(ApplicationDbContext context, ILogger<PrescriptionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Prescription> GetByIdAsync(int id)
        {
            return await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Patient)
                    .ThenInclude(pat => pat.User)
                .Include(p => p.Doctor)
                    .ThenInclude(doc => doc.User)
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Drug)
                .Include(p => p.Appointment)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Prescription> GetByIdWithDetailsAsync(int id)
        {
            // GetByIdAsync already includes all necessary navigation properties
            return await GetByIdAsync(id);
        }

        public async Task<List<Prescription>> GetPendingAsync()
        {
            return await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Patient)
                    .ThenInclude(pat => pat.User)
                .Include(p => p.Doctor)
                    .ThenInclude(doc => doc.User)
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Drug)
                .Where(p => p.Status == PrescriptionStatus.Pending)
                .OrderBy(p => p.PrescriptionDate)
                .ToListAsync();
        }

        public async Task<List<Prescription>> GetByPatientIdAsync(int patientId)
        {
            return await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Doctor)
                    .ThenInclude(d => d.User)
                .Include(p => p.Doctor)
                    .ThenInclude(d => d.Specialization)
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Drug)
                .Include(p => p.Appointment)
                .Where(p => p.PatientId == patientId)
                .OrderByDescending(p => p.PrescriptionDate)
                .ToListAsync();
        }

        public async Task<List<Prescription>> GetByDoctorIdAsync(int doctorId)
        {
            return await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Patient)
                    .ThenInclude(pat => pat.User)
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Drug)
                .Where(p => p.DoctorId == doctorId)
                .OrderByDescending(p => p.PrescriptionDate)
                .ToListAsync();
        }

        public async Task<ServiceResult> CreateAsync(Prescription prescription, List<PrescriptionItem> items)
        {
            // Batch load all drugs upfront to avoid N+1 queries
            var drugIds = items.Select(i => i.DrugId).Where(id => id.HasValue).Select(id => id.Value).Distinct().ToList();
            var drugs = await _context.Drugs
                .Where(d => drugIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id);

            var errors = new List<string>();
            var now = DateTime.UtcNow;

            foreach (var item in items)
            {
                if (!item.DrugId.HasValue)
                {
                    errors.Add($"Item '{item.DrugName}' has no drug selected.");
                    continue;
                }

                if (!drugs.TryGetValue(item.DrugId.Value, out var drug))
                {
                    errors.Add($"Drug ID {item.DrugId.Value} not found.");
                    continue;
                }

                if (drug.Quantity < item.Quantity)
                {
                    errors.Add($"Insufficient stock for '{drug.Name}'. Available: {drug.Quantity}, Requested: {item.Quantity}");
                }

                if (drug.ExpiryDate <= now)
                {
                    errors.Add($"Drug '{drug.Name}' has expired on {drug.ExpiryDate:yyyy-MM-dd}");
                }
            }

            if (errors.Any())
            {
                return ServiceResult.Fail("Validation failed.", errors);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Calculate medication total using pre-loaded drugs
                prescription.MedicationTotal = items.Sum(i =>
                    i.DrugId.HasValue && drugs.TryGetValue(i.DrugId.Value, out var d)
                        ? i.Quantity * d.Price
                        : 0);
                prescription.Status = PrescriptionStatus.Pending;
                prescription.CreatedAt = now;
                prescription.UpdatedAt = now;

                _context.Prescriptions.Add(prescription);
                await _context.SaveChangesAsync();

                foreach (var item in items)
                {
                    item.PrescriptionId = prescription.Id;
                    _context.PrescriptionItems.Add(item);

                    // Use pre-loaded drug from dictionary
                    if (item.DrugId.HasValue && drugs.TryGetValue(item.DrugId.Value, out var drug))
                    {
                        drug.Quantity -= item.Quantity;
                        drug.UpdatedAt = now;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Prescription {PrescriptionId} created with {ItemCount} items",
                    prescription.Id, items.Count);

                return ServiceResult.Ok("Prescription created successfully and added to pending queue.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating prescription");
                return ServiceResult.Fail("An error occurred while creating the prescription.");
            }
        }

        public async Task<ServiceResult> MarkAsCompletedAsync(int id)
        {
            try
            {
                var prescription = await _context.Prescriptions
                    .Include(p => p.Patient)
                        .ThenInclude(pat => pat.User)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (prescription == null)
                {
                    return ServiceResult.Fail("Prescription not found.");
                }

                if (prescription.Status != PrescriptionStatus.Pending)
                {
                    return ServiceResult.Fail("Only pending prescriptions can be marked as completed.");
                }

                prescription.Status = PrescriptionStatus.Completed;
                prescription.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Prescription {PrescriptionId} marked as completed for patient {PatientName}",
                    id, prescription.Patient?.User?.FullName);

                return ServiceResult.Ok($"Prescription for {prescription.Patient?.User?.FullName} marked as completed!");
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict updating prescription {PrescriptionId}", id);
                return ServiceResult.Fail("The prescription was modified by another user. Please refresh and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking prescription {PrescriptionId} as completed", id);
                return ServiceResult.Fail("An error occurred while updating the prescription.");
            }
        }

        public async Task<ServiceResult> CancelAsync(int id)
        {
            try
            {
                var prescription = await _context.Prescriptions
                    .Include(p => p.PrescriptionItems)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (prescription == null)
                {
                    return ServiceResult.Fail("Prescription not found.");
                }

                if (prescription.Status == PrescriptionStatus.Canceled)
                {
                    return ServiceResult.Fail("Prescription is already canceled.");
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                // Batch load all drugs for stock restoration
                var drugIds = prescription.PrescriptionItems.Select(i => i.DrugId).Where(id => id.HasValue).Select(id => id.Value).Distinct().ToList();
                var drugs = await _context.Drugs
                    .Where(d => drugIds.Contains(d.Id))
                    .ToDictionaryAsync(d => d.Id);

                // Restore stock
                foreach (var item in prescription.PrescriptionItems)
                {
                    if (item.DrugId.HasValue && drugs.TryGetValue(item.DrugId.Value, out var drug))
                    {
                        drug.Quantity += item.Quantity;
                        drug.UpdatedAt = DateTime.UtcNow;
                    }
                }

                prescription.Status = PrescriptionStatus.Canceled;
                prescription.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Prescription {PrescriptionId} canceled and stock restored", id);
                return ServiceResult.Ok("Prescription cancelled and stock restored.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling prescription {PrescriptionId}", id);
                return ServiceResult.Fail("An error occurred while cancelling the prescription.");
            }
        }

        public async Task<bool> HasDrugStockAsync(int drugId, int quantity)
        {
            var drug = await _context.Drugs.AsNoTracking().FirstOrDefaultAsync(d => d.Id == drugId);
            return drug != null && drug.Quantity >= quantity && drug.ExpiryDate > DateTime.UtcNow;
        }
    }
}
