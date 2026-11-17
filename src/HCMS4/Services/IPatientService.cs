using HCMS4.Models;
using HCMS4.Models.Common;

namespace HCMS4.Services
{
    public interface IPatientService
    {
        Task<Patient> GetByIdAsync(int id);
        Task<Patient> GetByIdWithDetailsAsync(int id);
        Task<PaginatedResult<Patient>> GetAllAsync(PaginationParams pagination);
        Task<ServiceResult> UpdateAsync(int id, Patient patient);
        Task<ServiceResult> DeleteAsync(int id);
        Task<bool> HasRelatedRecordsAsync(int id);
        Task<int> GetCountAsync();
        Task<ServiceResult> DisablePatientAsync(int id, string reason);
        Task<ServiceResult> EnablePatientAsync(int id);
    }
}
