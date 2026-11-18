using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Common;
namespace HCMS4.Modules.Pharmacy.Services
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
