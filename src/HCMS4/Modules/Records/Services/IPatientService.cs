using HCMS4.Modules.Records.Models;
using HCMS4.Shared.Common;
namespace HCMS4.Modules.Records.Services
{
    public interface IPatientService
    {
        Task<Patient?> GetByIdAsync(int id);
        Task<Patient?> GetByIdWithDetailsAsync(int id);
        Task<PaginatedResult<Patient>> GetAllAsync(PaginationParams pagination);
        /// <summary>All patients with their user, for selection lists.</summary>
        Task<List<Patient>> GetAllWithUserAsync();
        Task<Patient?> GetByUserIdAsync(string userId);
        /// <summary>Creates the patient record of a newly registered user.</summary>
        Task<Patient> CreateForUserAsync(string userId, DateTime? dateOfBirth, string? chronicConditions);
        /// <summary>Updates the details a patient edits on their own profile; false when the user has no patient record.</summary>
        Task<bool> UpdateOwnDetailsAsync(string userId, DateTime? dateOfBirth, string? address, string? emergencyContact);
        Task<ServiceResult> UpdateAsync(int id, Patient patient);
        Task<ServiceResult> DeleteAsync(int id);
        Task<bool> HasRelatedRecordsAsync(int id);
        Task<int> GetCountAsync();
        Task<ServiceResult> DisablePatientAsync(int id, string reason);
        Task<ServiceResult> EnablePatientAsync(int id);
    }
}
