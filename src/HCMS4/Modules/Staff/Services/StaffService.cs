using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Models;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Staff.Services
{
    /// <summary>Doctors, pharmacists, specializations and leave views used by the admin and doctor pages.</summary>
    public interface IStaffService
    {
        Task<List<Specialization>> GetActiveSpecializationsAsync();
        Task<List<Specialization>> GetSpecializationsAsync();
        Task<ServiceResult> UpdateSpecializationFeeAsync(int id, decimal consultationFee);

        Task<List<Doctor>> GetDoctorsAsync();
        Task<Doctor?> GetDoctorAsync(int id);
        Task<ServiceResult> CreateDoctorAsync(DoctorRegisterViewModel model);
        /// <summary>Deletes a user and the doctor/patient data attached to it. Returns the doctor's name when it was a doctor.</summary>
        Task<ServiceResult<string?>> DeleteUserAsync(string userId, string? currentUserId);
        Task<int> CountScheduledAppointmentsAsync(int doctorId, DateTime from, DateTime to);
        Task<DoctorLeaveViewModel?> GetDoctorLeavesAsync(int doctorId);
        Task<Doctor?> GetDoctorProfileAsync(int doctorId);
        /// <summary>Doctors marked available and not on approved leave on <paramref name="date"/>, with user and specialization.</summary>
        Task<List<Doctor>> GetBookableDoctorsAsync(DateTime date);

        Task<List<Pharmacist>> GetPharmacistsAsync();
        Task<PharmacistEditViewModel?> GetPharmacistForEditAsync(int id);
        Task<ServiceResult> CreatePharmacistAsync(PharmacistRegisterViewModel model);
        Task<ServiceResult> UpdatePharmacistAsync(PharmacistEditViewModel model);
        Task<ServiceResult<string?>> DeletePharmacistAsync(int id, string? currentUserEmail);
    }

    public class StaffService : IStaffService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IClinicClock _clock;
        private readonly ILogger<StaffService> _logger;

        public StaffService(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            IClinicClock clock, ILogger<StaffService> logger)
        {
            _context = context;
            _userManager = userManager;
            _clock = clock;
            _logger = logger;
        }

        public Task<List<Specialization>> GetActiveSpecializationsAsync() =>
            _context.Specializations.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();

        public Task<List<Specialization>> GetSpecializationsAsync() =>
            _context.Specializations.AsNoTracking().OrderBy(s => s.Name).ToListAsync();

        public async Task<ServiceResult> UpdateSpecializationFeeAsync(int id, decimal consultationFee)
        {
            if (consultationFee <= 0)
            {
                return ServiceResult.Fail("Please enter a valid price greater than zero.");
            }

            var specialization = await _context.Specializations.FindAsync(id);
            if (specialization == null)
            {
                return ServiceResult.Fail("Specialization not found.");
            }

            var oldFee = specialization.ConsultationFee;
            specialization.ConsultationFee = consultationFee;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Specialization {SpecName} fee updated from {OldFee} to {NewFee}",
                specialization.Name, oldFee, consultationFee);
            return ServiceResult.Ok($"Consultation fee for '{specialization.Name}' updated successfully!");
        }

        public Task<List<Doctor>> GetDoctorsAsync() =>
            _context.Doctors.AsNoTracking()
                .Include(d => d.User)
                .Include(d => d.Specialization)
                .ToListAsync();

        public Task<Doctor?> GetDoctorAsync(int id) =>
            _context.Doctors.AsNoTracking().Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);

        public async Task<ServiceResult> CreateDoctorAsync(DoctorRegisterViewModel model)
        {
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName
            };

            var userResult = await _userManager.CreateAsync(user, model.Password);
            if (!userResult.Succeeded)
            {
                return ServiceResult.Fail("Could not create the doctor account.", userResult.Errors.Select(e => e.Description).ToList());
            }

            await _userManager.AddToRoleAsync(user, "Doctor");
            _context.Doctors.Add(new Doctor
            {
                UserId = user.Id,
                SpecializationId = model.SpecializationId,
                Qualifications = model.Qualifications,
                ContactInfo = model.ContactInfo
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Doctor account created successfully: {Email}", model.Email);
            return ServiceResult.Ok($"Doctor account for {model.FirstName} {model.LastName} created successfully!");
        }

        public async Task<ServiceResult<string?>> DeleteUserAsync(string userId, string? currentUserId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return ServiceResult<string?>.Fail("User ID is required.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<string?>.Fail("User not found.");
            }

            if (user.Id == currentUserId)
            {
                return ServiceResult<string?>.Fail("You cannot delete your own account.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var doctor = await _context.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.UserId == userId);
            var doctorName = doctor?.FullName;
            if (doctor != null)
            {
                _context.Appointments.RemoveRange(_context.Appointments.Where(a => a.DoctorId == doctor.Id));
                _context.Prescriptions.RemoveRange(_context.Prescriptions.Where(p => p.DoctorId == doctor.Id));
                _context.Doctors.Remove(doctor);
            }

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient != null)
            {
                _context.Appointments.RemoveRange(_context.Appointments.Where(a => a.PatientId == patient.Id));
                _context.Prescriptions.RemoveRange(_context.Prescriptions.Where(p => p.PatientId == patient.Id));
                _context.Invoices.RemoveRange(_context.Invoices.Where(i => i.PatientId == patient.Id));
                _context.Patients.Remove(patient);
            }

            await _context.SaveChangesAsync();
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                return ServiceResult<string?>.Fail("Error deleting user account.");
            }

            await transaction.CommitAsync();
            return ServiceResult<string?>.Ok(doctorName,
                doctor != null ? $"Doctor '{doctorName}' deleted successfully." : "User deleted successfully.");
        }

        public Task<int> CountScheduledAppointmentsAsync(int doctorId, DateTime from, DateTime to) =>
            _context.Appointments.CountAsync(a => a.DoctorId == doctorId &&
                                                  a.AppointmentDateTime.Date >= from &&
                                                  a.AppointmentDateTime.Date <= to &&
                                                  a.Status == AppointmentStatus.Scheduled);

        public async Task<DoctorLeaveViewModel?> GetDoctorLeavesAsync(int doctorId)
        {
            var doctor = await _context.Doctors.AsNoTracking()
                .Include(d => d.User)
                .Include(d => d.DoctorLeaves)
                .FirstOrDefaultAsync(d => d.Id == doctorId);
            if (doctor == null)
            {
                return null;
            }

            var requests = await _context.LeaveRequests.AsNoTracking()
                .Where(lr => lr.DoctorId == doctorId)
                .OrderByDescending(lr => lr.CreatedAt)
                .ToListAsync();

            return new DoctorLeaveViewModel
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.FullName,
                ApprovedLeaves = doctor.DoctorLeaves.Select(dl => new DoctorLeaveItem
                {
                    Id = dl.Id,
                    StartDate = dl.StartDate,
                    EndDate = dl.EndDate,
                    LeaveType = dl.LeaveType.ToString(),
                    Notes = dl.Notes,
                    Status = dl.Status
                }).ToList(),
                LeaveRequests = requests.Select(lr => new LeaveRequestViewModel
                {
                    Id = lr.Id,
                    StartDate = lr.StartDate,
                    EndDate = lr.EndDate,
                    LeaveType = lr.LeaveType,
                    Reason = lr.Reason,
                    RejectionReason = lr.RejectionReason,
                    Status = lr.Status,
                    CreatedAt = lr.CreatedAt
                }).ToList()
            };
        }

        public Task<Doctor?> GetDoctorProfileAsync(int doctorId) =>
            _context.Doctors.AsNoTracking()
                .Include(d => d.User)
                .Include(d => d.Specialization)
                .FirstOrDefaultAsync(d => d.Id == doctorId);

        public async Task<List<Doctor>> GetBookableDoctorsAsync(DateTime date)
        {
            var day = date.Date;
            var doctorsOnLeave = await _context.DoctorLeaves
                .Where(dl => dl.Status == LeaveStatus.Approved && dl.StartDate <= day && dl.EndDate >= day)
                .Select(dl => dl.DoctorId)
                .ToListAsync();

            return await _context.Doctors.AsNoTracking()
                .Include(d => d.User)
                .Include(d => d.Specialization)
                .Where(d => d.IsAvailable && !doctorsOnLeave.Contains(d.Id))
                .ToListAsync();
        }

        public Task<List<Pharmacist>> GetPharmacistsAsync() =>
            _context.Pharmacists.AsNoTracking()
                .Include(p => p.User)
                .OrderBy(p => p.User != null ? p.User.FirstName : "")
                .ToListAsync();

        public async Task<PharmacistEditViewModel?> GetPharmacistForEditAsync(int id)
        {
            var pharmacist = await _context.Pharmacists.AsNoTracking().Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
            return pharmacist == null ? null : new PharmacistEditViewModel
            {
                Id = pharmacist.Id,
                FirstName = pharmacist.User?.FirstName ?? "",
                LastName = pharmacist.User?.LastName ?? "",
                Email = pharmacist.User?.Email ?? "",
                PhoneNumber = pharmacist.User?.PhoneNumber,
                Qualifications = pharmacist.Qualifications,
                ContactInfo = pharmacist.ContactInfo,
                Shift = pharmacist.Shift,
                IsActive = pharmacist.IsActive
            };
        }

        public async Task<ServiceResult> CreatePharmacistAsync(PharmacistRegisterViewModel model)
        {
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName
            };

            var userResult = await _userManager.CreateAsync(user, model.Password);
            if (!userResult.Succeeded)
            {
                return ServiceResult.Fail("Could not create the pharmacist account.", userResult.Errors.Select(e => e.Description).ToList());
            }

            await _userManager.AddToRoleAsync(user, "Pharmacist");
            _context.Pharmacists.Add(new Pharmacist
            {
                UserId = user.Id,
                Qualifications = model.Qualifications,
                ContactInfo = model.ContactInfo,
                Shift = model.Shift,
                CreatedAt = _clock.Now,
                IsActive = true
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Pharmacist account created successfully: {Email}", model.Email);
            return ServiceResult.Ok($"Pharmacist account for {model.FirstName} {model.LastName} created successfully!");
        }

        public async Task<ServiceResult> UpdatePharmacistAsync(PharmacistEditViewModel model)
        {
            var pharmacist = await _context.Pharmacists.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == model.Id);
            if (pharmacist == null)
            {
                return ServiceResult.Fail("Pharmacist not found.");
            }

            pharmacist.User.FirstName = model.FirstName;
            pharmacist.User.LastName = model.LastName;
            pharmacist.User.Email = model.Email;
            pharmacist.User.UserName = model.Email;
            pharmacist.User.PhoneNumber = model.PhoneNumber;
            pharmacist.Qualifications = model.Qualifications;
            pharmacist.ContactInfo = model.ContactInfo;
            pharmacist.Shift = model.Shift;
            pharmacist.IsActive = model.IsActive;

            await _userManager.UpdateAsync(pharmacist.User);
            await _context.SaveChangesAsync();
            return ServiceResult.Ok($"Pharmacist {model.FirstName} {model.LastName} updated successfully!");
        }

        public async Task<ServiceResult<string?>> DeletePharmacistAsync(int id, string? currentUserEmail)
        {
            var pharmacist = await _context.Pharmacists.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
            if (pharmacist == null)
            {
                return ServiceResult<string?>.Fail("Pharmacist not found.");
            }

            if (pharmacist.User?.Email == currentUserEmail)
            {
                return ServiceResult<string?>.Fail("You cannot delete your own account.");
            }

            var name = pharmacist.User?.FullName;
            var userId = pharmacist.UserId;
            _context.Pharmacists.Remove(pharmacist);
            await _context.SaveChangesAsync();

            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
            }

            return ServiceResult<string?>.Ok(name, $"Pharmacist {name} deleted successfully!");
        }
    }
}
