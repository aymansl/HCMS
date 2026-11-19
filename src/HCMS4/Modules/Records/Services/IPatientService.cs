using HCMS4.Modules.Records.Models;
using HCMS4.Shared.Common;
namespace HCMS4.Modules.Records.Services
{
    public interface IPatientService
    {
        Task<Patient> GetByIdAsync(int id);
        Task<Patient> GetByIdWithDetailsAsync(int id);
        Task<PaginatedResult<Patient>> GetAllAsync(PaginationParams pagination);
        /// <summary>All patients with their user, for selection lists.</summary>
        Task<List<Patient>> GetAllWithUserAsync();
        Task<ServiceResult> UpdateAsync(int id, Patient patient);
        Task<ServiceResult> DeleteAsync(int id);
        Task<bool> HasRelatedRecordsAsync(int id);
        Task<int> GetCountAsync();
        Task<ServiceResult> DisablePatientAsync(int id, string reason);
        Task<ServiceResult> EnablePatientAsync(int id);
    }
}
