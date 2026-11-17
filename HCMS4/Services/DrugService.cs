using HCMS4.Data;
using HCMS4.Models;
using HCMS4.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Services
{
    public class DrugService : IDrugService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DrugService> _logger;

        public DrugService(ApplicationDbContext context, ILogger<DrugService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Drug> GetByIdAsync(int id)
        {
            return await _context.Drugs
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<List<Drug>> GetAllAsync(string? searchTerm = null, string? expiryFilter = null)
        {
            var query = _context.Drugs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(d =>
                    d.Name.Contains(searchTerm) ||
                    (d.Supplier != null && d.Supplier.Contains(searchTerm)) ||
                    (d.Description != null && d.Description.Contains(searchTerm)));
            }

            var today = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(expiryFilter))
            {
                switch (expiryFilter)
                {
                    case "expired":
                        query = query.Where(d => d.ExpiryDate <= today);
                        break;
                    case "expiring-soon":
                        query = query.Where(d => d.ExpiryDate > today && d.ExpiryDate <= today.AddDays(BusinessRules.DrugExpiryWarningDays));
                        break;
                    case "valid":
                        query = query.Where(d => d.ExpiryDate > today.AddDays(BusinessRules.DrugExpiryWarningDays));
                        break;
                }
            }

            return await query
                .OrderBy(d => d.ExpiryDate)
                .ToListAsync();
        }

        public async Task<ServiceResult> CreateAsync(Drug drug)
        {
            try
            {
                drug.CreatedAt = DateTime.UtcNow;
                drug.UpdatedAt = DateTime.UtcNow;

                _context.Drugs.Add(drug);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Drug {DrugName} created successfully", drug.Name);
                return ServiceResult.Ok($"Drug '{drug.Name}' added successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating drug {DrugName}", drug.Name);
                return ServiceResult.Fail("An error occurred while creating the drug.");
            }
        }

        public async Task<ServiceResult> UpdateAsync(Drug drug)
        {
            try
            {
                var existingDrug = await _context.Drugs.FindAsync(drug.Id);
                if (existingDrug == null)
                {
                    return ServiceResult.Fail("Drug not found.");
                }

                existingDrug.Name = drug.Name;
                existingDrug.Supplier = drug.Supplier;
                existingDrug.Price = drug.Price;
                existingDrug.Quantity = drug.Quantity;
                existingDrug.ExpiryDate = drug.ExpiryDate;
                existingDrug.Description = drug.Description;
                existingDrug.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Drug {DrugId} updated successfully", drug.Id);
                return ServiceResult.Ok($"Drug '{drug.Name}' updated successfully!");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating drug {DrugId}", drug.Id);
                return ServiceResult.Fail("The drug was modified by another user. Please refresh and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating drug {DrugId}", drug.Id);
                return ServiceResult.Fail("An error occurred while updating the drug.");
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            try
            {
                var drug = await _context.Drugs
                    .Include(d => d.PrescriptionItems)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (drug == null)
                {
                    return ServiceResult.Fail("Drug not found.");
                }

                if (drug.PrescriptionItems.Any())
                {
                    return ServiceResult.Fail($"Cannot delete '{drug.Name}' because it is associated with existing prescriptions.");
                }

                _context.Drugs.Remove(drug);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Drug {DrugId} deleted successfully", id);
                return ServiceResult.Ok($"Drug '{drug.Name}' deleted successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting drug {DrugId}", id);
                return ServiceResult.Fail("An error occurred while deleting the drug.");
            }
        }

        public async Task<ServiceResult> UpdateStockAsync(int id, int newQuantity)
        {
            try
            {
                var drug = await _context.Drugs.FindAsync(id);
                if (drug == null)
                {
                    return ServiceResult.Fail("Drug not found.");
                }

                var oldQuantity = drug.Quantity;
                drug.Quantity = newQuantity;
                drug.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Stock for drug {DrugId} updated from {OldQty} to {NewQty}",
                    id, oldQuantity, newQuantity);

                string message = $"Stock for {drug.Name} updated successfully.";
                if (newQuantity < BusinessRules.LowStockThreshold && newQuantity > 0)
                {
                    message = $"Warning: {drug.Name} is now low on stock (Quantity: {newQuantity}).";
                }
                else if (newQuantity == 0)
                {
                    message = $"Alert: {drug.Name} is out of stock!";
                }

                return ServiceResult.Ok(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock for drug {DrugId}", id);
                return ServiceResult.Fail("An error occurred while updating stock.");
            }
        }

        public async Task<List<Drug>> GetExpiringDrugsAsync(int daysThreshold = BusinessRules.DrugExpiryWarningDays)
        {
            var today = DateTime.UtcNow;
            return await _context.Drugs
                .AsNoTracking()
                .Where(d => d.ExpiryDate <= today.AddDays(daysThreshold) && d.ExpiryDate > today)
                .OrderBy(d => d.ExpiryDate)
                .ToListAsync();
        }

        public async Task<List<Drug>> GetLowStockDrugsAsync(int threshold = BusinessRules.LowStockThreshold)
        {
            return await _context.Drugs
                .AsNoTracking()
                .Where(d => d.Quantity < threshold)
                .OrderBy(d => d.Quantity)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalInventoryValueAsync()
        {
            return await _context.Drugs
                .AsNoTracking()
                .SumAsync(d => d.Price * d.Quantity);
        }
    }
}
