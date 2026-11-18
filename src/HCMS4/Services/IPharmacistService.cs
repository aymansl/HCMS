using HCMS4.Models;
using HCMS4.Models.Common;

namespace HCMS4.Services
{
    public interface IPharmacistService
    {
        Task<Pharmacist> GetByIdAsync(int id);
        Task<Pharmacist?> GetByUserIdAsync(string userId);
        Task<ServiceResult> UpdateProfileAsync(int id, string qualifications, string? contactInfo, string? shift);
        Task<ServiceResult> MarkPrescriptionCompletedAsync(int prescriptionId, string pharmacistUserId);
        /// <summary>Keeps a pending prescription in the queue for later processing.</summary>
        Task<ServiceResult> HoldPrescriptionAsync(int prescriptionId);
    }
}
