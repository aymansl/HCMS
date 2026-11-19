using HCMS4.Data;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Pharmacy.Services
{
    public interface ISupplierService
    {
        Task<List<Supplier>> GetActiveAsync();
        Task<List<SupplierSelectDto>> GetSelectOptionsAsync();
        Task<Supplier?> GetByIdAsync(int id);
        Task<ServiceResult> CreateAsync(Supplier supplier);
        Task<ServiceResult> UpdateAsync(Supplier supplier);
        /// <summary>Deletes the supplier, or only deactivates it when purchase requests refer to it.</summary>
        Task<ServiceResult> RemoveAsync(int id);
    }

    public class SupplierService : ISupplierService
    {
        private readonly ApplicationDbContext _context;
        private readonly IClinicClock _clock;

        public SupplierService(ApplicationDbContext context, IClinicClock clock)
        {
            _context = context;
            _clock = clock;
        }

        public Task<List<Supplier>> GetActiveAsync() =>
            _context.Suppliers.AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

        public Task<List<SupplierSelectDto>> GetSelectOptionsAsync() =>
            _context.Suppliers.AsNoTracking()
                .Where(s => s.IsActive)
                .Select(s => new SupplierSelectDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    ContactPerson = s.ContactPerson,
                    Phone = s.Phone
                })
                .ToListAsync();

        public Task<Supplier?> GetByIdAsync(int id) =>
            _context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);

        public async Task<ServiceResult> CreateAsync(Supplier supplier)
        {
            if (await NameTakenAsync(supplier.Name, excludeId: null))
            {
                return ServiceResult.Fail("Supplier name already exists. Please use a different name.");
            }

            supplier.CreatedAt = _clock.Now;
            supplier.IsActive = true;
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();
            return ServiceResult.Ok($"Supplier '{supplier.Name}' added successfully!");
        }

        public async Task<ServiceResult> UpdateAsync(Supplier supplier)
        {
            if (await NameTakenAsync(supplier.Name, supplier.Id))
            {
                return ServiceResult.Fail("Supplier name already exists. Please use a different name.");
            }

            var existing = await _context.Suppliers.FindAsync(supplier.Id);
            if (existing == null)
            {
                return ServiceResult.Fail("Supplier not found.");
            }

            existing.Name = supplier.Name;
            existing.ContactPerson = supplier.ContactPerson;
            existing.Phone = supplier.Phone;
            existing.Email = supplier.Email;
            existing.Address = supplier.Address;
            await _context.SaveChangesAsync();
            return ServiceResult.Ok($"Supplier '{supplier.Name}' updated successfully!");
        }

        public async Task<ServiceResult> RemoveAsync(int id)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.PurchaseRequests)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null)
            {
                return ServiceResult.Fail("Supplier not found.");
            }

            if (supplier.PurchaseRequests?.Count > 0)
            {
                supplier.IsActive = false;
                await _context.SaveChangesAsync();
                return ServiceResult.Ok($"Supplier '{supplier.Name}' has been deactivated (has associated purchase requests).");
            }

            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();
            return ServiceResult.Ok($"Supplier '{supplier.Name}' deleted successfully!");
        }

        private Task<bool> NameTakenAsync(string name, int? excludeId) =>
            _context.Suppliers.AnyAsync(s => s.Name == name && s.IsActive && (excludeId == null || s.Id != excludeId));
    }
}
