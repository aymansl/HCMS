using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Lab.Services
{
    public enum LabTestDeleteOutcome { Deleted, Deactivated }

    /// <summary>The lab test catalogue (UC-L06).</summary>
    public interface ILabTestService
    {
        /// <summary>Tests by name; inactive ones too unless <paramref name="activeOnly"/>.</summary>
        Task<List<LabTest>> GetAllAsync(bool activeOnly = false);
        Task<LabTest?> GetByIdAsync(int id);
        Task<int> CountActiveAsync();

        /// <summary>Adds (Id 0) or updates a test. Rejects an invalid range, a negative price or a duplicate name.</summary>
        Task<ServiceResult<LabTest>> SaveAsync(LabTestFormViewModel model);

        /// <summary>Deletes the test, or deactivates it when past requests use it.</summary>
        Task<ServiceResult<LabTestDeleteOutcome>> DeleteAsync(int id);
    }

    public class LabTestService : ILabTestService
    {
        public const string DuplicateNameError = "A test with this name already exists.";

        private readonly ApplicationDbContext _context;
        private readonly ILabTestUsage _usage;
        private readonly IClinicClock _clock;

        public LabTestService(ApplicationDbContext context, ILabTestUsage usage, IClinicClock clock)
        {
            _context = context;
            _usage = usage;
            _clock = clock;
        }

        public Task<List<LabTest>> GetAllAsync(bool activeOnly = false) =>
            _context.LabTests.AsNoTracking()
                .Where(t => !activeOnly || t.IsActive)
                .OrderBy(t => t.Name)
                .ToListAsync();

        public Task<LabTest?> GetByIdAsync(int id) =>
            _context.LabTests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);

        public Task<int> CountActiveAsync() => _context.LabTests.CountAsync(t => t.IsActive);

        public async Task<ServiceResult<LabTest>> SaveAsync(LabTestFormViewModel model)
        {
            var name = model.Name.Trim();
            var unit = model.Unit.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(unit))
                return ServiceResult<LabTest>.Fail("Test name and unit are required.");

            if (model.Price is null or < 0)
                return ServiceResult<LabTest>.Fail("The price cannot be negative.");

            if (model.ReferenceMin.HasValue && model.ReferenceMax.HasValue && model.ReferenceMin > model.ReferenceMax)
                return ServiceResult<LabTest>.Fail(LabTestFormViewModel.RangeError);

            // Case-insensitive, whatever the database collation. EF translates ToUpper() to SQL UPPER(),
            // so the culture overloads the analyzers ask for cannot be used here.
            var upper = name.ToUpperInvariant();
#pragma warning disable CA1304, CA1311, CA1862
            var duplicate = await _context.LabTests.AnyAsync(t => t.Id != model.Id && t.Name.ToUpper() == upper);
#pragma warning restore CA1304, CA1311, CA1862
            if (duplicate)
                return ServiceResult<LabTest>.Fail(DuplicateNameError);

            var now = _clock.Now;
            LabTest test;
            if (model.Id == 0)
            {
                test = new LabTest { CreatedAt = now, IsActive = true };
                _context.LabTests.Add(test);
            }
            else
            {
                var existing = await _context.LabTests.FirstOrDefaultAsync(t => t.Id == model.Id);
                if (existing == null)
                    return ServiceResult<LabTest>.Fail("Test not found.");
                test = existing;
                test.IsActive = model.IsActive;
            }

            test.Name = name;
            test.Unit = unit;
            test.ReferenceMin = model.ReferenceMin;
            test.ReferenceMax = model.ReferenceMax;
            test.Price = model.Price.Value;
            test.UpdatedAt = now;

            await _context.SaveChangesAsync();
            return ServiceResult<LabTest>.Ok(test, model.Id == 0
                ? $"Test '{test.Name}' was added to the catalogue."
                : $"Test '{test.Name}' was updated.");
        }

        public async Task<ServiceResult<LabTestDeleteOutcome>> DeleteAsync(int id)
        {
            var test = await _context.LabTests.FirstOrDefaultAsync(t => t.Id == id);
            if (test == null)
                return ServiceResult<LabTestDeleteOutcome>.Fail("Test not found.");

            if (await _usage.IsUsedAsync(id))
            {
                if (!test.IsActive)
                    return ServiceResult<LabTestDeleteOutcome>.Fail($"Test '{test.Name}' is used by past requests and is already inactive.");

                test.IsActive = false;
                test.UpdatedAt = _clock.Now;
                await _context.SaveChangesAsync();
                return ServiceResult<LabTestDeleteOutcome>.Ok(LabTestDeleteOutcome.Deactivated,
                    $"Test '{test.Name}' is used by past requests, so it was deactivated instead of deleted.");
            }

            _context.LabTests.Remove(test);
            await _context.SaveChangesAsync();
            return ServiceResult<LabTestDeleteOutcome>.Ok(LabTestDeleteOutcome.Deleted, $"Test '{test.Name}' was deleted.");
        }
    }

    /// <summary>
    /// Whether past lab requests use a test. Until lab requests exist (L3) no test is in use;
    /// L3 replaces this with a check on the request items.
    /// </summary>
    public interface ILabTestUsage
    {
        Task<bool> IsUsedAsync(int labTestId);
    }

    public class NoLabTestUsage : ILabTestUsage
    {
        public Task<bool> IsUsedAsync(int labTestId) => Task.FromResult(false);
    }
}
