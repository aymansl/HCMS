using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;
namespace HCMS4.Modules.Pharmacy.Services
{
    public interface IDrugService
    {
        Task<Drug?> GetByIdAsync(int id);
        Task<List<Drug>> GetAllAsync(string? searchTerm = null, string? expiryFilter = null);
        Task<ServiceResult> CreateAsync(Drug drug);
        Task<ServiceResult> UpdateAsync(Drug drug);
        Task<ServiceResult> DeleteAsync(int id);
        Task<ServiceResult> UpdateStockAsync(int id, int newQuantity);
        Task<List<Drug>> GetExpiringDrugsAsync(int daysThreshold = BusinessRules.DrugExpiryWarningDays);
        Task<List<Drug>> GetLowStockDrugsAsync(int threshold = BusinessRules.LowStockThreshold);
        Task<decimal> GetTotalInventoryValueAsync();
        /// <summary>Drugs a doctor can prescribe: in stock and not expired, by name.</summary>
        Task<List<HCMS4.ViewModels.DrugSelectionViewModel>> GetPrescribableDrugsAsync();
    }
}
