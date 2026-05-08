using HCMS4.Data;
using HCMS4.Models;
using HCMS4.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Services
{
    public class PatientService : IPatientService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PatientService> _logger;

        public PatientService(ApplicationDbContext context, ILogger<PatientService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Patient> GetByIdAsync(int id)
        {
            return await _context.Patients
                .AsNoTracking()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Patient> GetByIdWithDetailsAsync(int id)
        {
            return await _context.Patients
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.Appointments)
                    .ThenInclude(a => a.Doctor)
                        .ThenInclude(d => d.User)
                .Include(p => p.Prescriptions)
                    .ThenInclude(pr => pr.Doctor)
                        .ThenInclude(d => d.User)
                .Include(p => p.Invoices)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<PaginatedResult<Patient>> GetAllAsync(PaginationParams pagination)
        {
            var query = _context.Patients
                .Include(p => p.User)
                .AsNoTracking();

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(p => p.Id)
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            return new PaginatedResult<Patient>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }

        public async Task<ServiceResult> UpdateAsync(int id, Patient patient)
        {
            try
            {
                var existingPatient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (existingPatient == null)
                {
                    return ServiceResult.Fail("Patient not found.");
                }

                existingPatient.DateOfBirth = patient.DateOfBirth;
                existingPatient.Address = patient.Address;
                existingPatient.EmergencyContact = patient.EmergencyContact;

                if (existingPatient.User != null && patient.User != null)
                {
                    existingPatient.User.FirstName = patient.User.FirstName;
                    existingPatient.User.LastName = patient.User.LastName;
                    existingPatient.User.Email = patient.User.Email;
                    existingPatient.User.PhoneNumber = patient.User.PhoneNumber;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Patient {PatientId} updated successfully", id);
                return ServiceResult.Ok("Patient updated successfully.");
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict updating patient {PatientId}", id);
                return ServiceResult.Fail("The patient was modified by another user. Please refresh and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating patient {PatientId}", id);
                return ServiceResult.Fail("An error occurred while updating the patient.");
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            try
            {
                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (patient == null)
                {
                    return ServiceResult.Fail("Patient not found.");
                }

                if (await HasRelatedRecordsAsync(id))
                {
                    return ServiceResult.Fail($"Cannot delete patient '{patient.User?.Email}' because they have related records (appointments, prescriptions, or invoices).");
                }

                _context.Patients.Remove(patient);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Patient {PatientId} deleted successfully", id);
                return ServiceResult.Ok($"Patient '{patient.User?.Email}' deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting patient {PatientId}", id);
                return ServiceResult.Fail($"Error deleting patient: {ex.Message}");
            }
        }

        public async Task<bool> HasRelatedRecordsAsync(int id)
        {
            // Combined into a single query for performance
            return await _context.Appointments.AnyAsync(a => a.PatientId == id) ||
                   await _context.Prescriptions.AnyAsync(p => p.PatientId == id) ||
                   await _context.Invoices.AnyAsync(i => i.PatientId == id);
        }

        public async Task<int> GetCountAsync()
        {
            return await _context.Patients.AsNoTracking().CountAsync();
        }

        public async Task<ServiceResult> DisablePatientAsync(int id, string reason)
        {
            try
            {
                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (patient == null)
                {
                    return ServiceResult.Fail("Patient not found.");
                }

                if (patient.Status == PatientStatus.Disabled)
                {
                    return ServiceResult.Fail("Patient account is already disabled.");
                }

                patient.Status = PatientStatus.Disabled;
                patient.DisableReason = reason;
                patient.DisabledAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Patient {PatientId} disabled successfully by admin", id);
                return ServiceResult.Ok("Patient account disabled successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling patient {PatientId}", id);
                return ServiceResult.Fail("Failed to disable account, please try again later.");
            }
        }

        public async Task<ServiceResult> EnablePatientAsync(int id)
        {
            try
            {
                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (patient == null)
                {
                    return ServiceResult.Fail("Patient not found.");
                }

                if (patient.Status == PatientStatus.Active)
                {
                    return ServiceResult.Fail("Patient account is already active.");
                }

                patient.Status = PatientStatus.Active;
                patient.DisableReason = null;
                patient.DisabledAt = null;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Patient {PatientId} enabled successfully by admin", id);
                return ServiceResult.Ok("Patient account enabled successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling patient {PatientId}", id);
                return ServiceResult.Fail("Failed to enable account, please try again later.");
            }
        }
    }
}
