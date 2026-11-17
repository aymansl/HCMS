using HCMS4.Models;
using HCMS4.Models.Common;

namespace HCMS4.Services
{
    public interface IPrescriptionService
    {
        Task<Prescription> GetByIdAsync(int id);
        Task<Prescription> GetByIdWithDetailsAsync(int id);
        Task<List<Prescription>> GetPendingAsync();
        Task<List<Prescription>> GetByPatientIdAsync(int patientId);
        Task<List<Prescription>> GetByDoctorIdAsync(int doctorId);
        Task<ServiceResult> CreateAsync(Prescription prescription, List<PrescriptionItem> items);
        Task<ServiceResult> MarkAsCompletedAsync(int id);
        Task<ServiceResult> CancelAsync(int id);
        Task<bool> HasDrugStockAsync(int drugId, int quantity);
    }
}
