using HCMS4.Controllers;
using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Models;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using HCMS4.Shared.Reporting.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static HCMS4.ViewModels.GenerateInvoiceViewModel;
using System.Security.Claims;

namespace HCMS4.Modules.Staff.Controllers
{
    // Part of the former AdminController; [ControllerName] keeps the /Admin/... URLs.
    [Authorize(Roles = "Admin")]
    [ControllerName("Admin")]
    public class AdminStaffController : BaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IDailyReportService _reportService;
        private readonly IPatientService _patientService;
        private readonly IAppointmentService _appointmentService;
        private readonly IPrescriptionService _prescriptionService;
        private readonly ApplicationDbContext _adminContext;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly ILeaveService _leaveService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public AdminStaffController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AdminStaffController> logger,
            IDailyReportService reportService,
            IPatientService patientService,
            IAppointmentService appointmentService,
            IPrescriptionService prescriptionService,
            INoShowRiskService noShowRiskService,
            ILeaveService leaveService,
            INotificationService notificationService,
            IActivityLogService activityLogService)
            : base(context, logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _reportService = reportService;
            _patientService = patientService;
            _appointmentService = appointmentService;
            _prescriptionService = prescriptionService;
            _adminContext = context;
            _noShowRiskService = noShowRiskService;
            _leaveService = leaveService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
        }

        public async Task<IActionResult> CreateDoctor()
        {
            ViewBag.Specializations = await _context.Specializations
                .Where(s => s.IsActive)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"{s.Name} - {s.ConsultationFee:C2}"
                })
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDoctor(DoctorRegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var user = new ApplicationUser
                    {
                        UserName = model.Email,
                        Email = model.Email,
                        FirstName = model.FirstName,
                        LastName = model.LastName
                    };

                    var userResult = await _userManager.CreateAsync(user, model.Password);

                    if (userResult.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Doctor");

                        var doctor = new Doctor
                        {
                            UserId = user.Id,
                            User = user,
                            SpecializationId = model.SpecializationId.Value,
                            Qualifications = model.Qualifications,
                            ContactInfo = model.ContactInfo
                        };

                        _context.Doctors.Add(doctor);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Doctor account created successfully: {Email}", model.Email);
                        TempData["SuccessMessage"] = $"Doctor account for {model.FirstName} {model.LastName} created successfully!";
                        return RedirectToAction("Doctors", "Admin");
                    }

                    foreach (var error in userResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating doctor account");
                    ModelState.AddModelError(string.Empty, "An error occurred while creating the doctor account.");
                }
            }

            ViewBag.Specializations = await _context.Specializations
                .Where(s => s.IsActive)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"{s.Name} - {s.ConsultationFee:C2}"
                })
                .ToListAsync();

            return View(model);
        }

        public async Task<IActionResult> Doctors()
        {
            var doctors = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialization)
                .ToListAsync();

            return View(doctors);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "User ID is required.";
                return RedirectToAction("Doctors");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Doctors");
            }

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (user.Id == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction("Doctors");
            }

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                var doctor = await _context.Doctors
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.UserId == id);
                string doctorName = doctor?.FullName ?? "Unknown";

                if (doctor != null)
                {
                    var doctorAppointments = await _context.Appointments
                        .Where(a => a.DoctorId == doctor.Id)
                        .ToListAsync();
                    _context.Appointments.RemoveRange(doctorAppointments);

                    var doctorPrescriptions = await _context.Prescriptions
                        .Where(p => p.DoctorId == doctor.Id)
                        .ToListAsync();
                    _context.Prescriptions.RemoveRange(doctorPrescriptions);

                    _context.Doctors.Remove(doctor);
                }

                var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == id);
                if (patient != null)
                {
                    var patientAppointments = await _context.Appointments
                        .Where(a => a.PatientId == patient.Id)
                        .ToListAsync();
                    _context.Appointments.RemoveRange(patientAppointments);

                    var patientPrescriptions = await _context.Prescriptions
                        .Where(p => p.PatientId == patient.Id)
                        .ToListAsync();
                    _context.Prescriptions.RemoveRange(patientPrescriptions);

                    var patientInvoices = await _context.Invoices
                        .Where(i => i.PatientId == patient.Id)
                        .ToListAsync();
                    _context.Invoices.RemoveRange(patientInvoices);

                    _context.Patients.Remove(patient);
                }

                await _context.SaveChangesAsync();
                var result = await _userManager.DeleteAsync(user);

                if (result.Succeeded)
                {
                    await transaction.CommitAsync();
                    TempData["SuccessMessage"] = doctor != null
                        ? $"Doctor '{doctorName}' deleted successfully."
                        : "User deleted successfully.";
                }
                else
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Error deleting user account.";
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error deleting user {UserId}", id);
                TempData["ErrorMessage"] = "Cannot delete user. There might be related records that need to be deleted first.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                TempData["ErrorMessage"] = $"Error deleting user: {ex.Message}";
            }

            return RedirectToAction("Doctors");
        }

        public IActionResult CreatePharmacist()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePharmacist(PharmacistRegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var user = new ApplicationUser
                    {
                        UserName = model.Email,
                        Email = model.Email,
                        FirstName = model.FirstName,
                        LastName = model.LastName
                    };

                    var userResult = await _userManager.CreateAsync(user, model.Password);

                    if (userResult.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Pharmacist");

                        var pharmacist = new Pharmacist
                        {
                            UserId = user.Id,
                            Qualifications = model.Qualifications,
                            ContactInfo = model.ContactInfo,
                            Shift = model.Shift,
                            CreatedAt = ClinicTime.Now,
                            IsActive = true
                        };

                        _context.Pharmacists.Add(pharmacist);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Pharmacist account created successfully: {Email}", model.Email);
                        TempData["SuccessMessage"] = $"Pharmacist account for {model.FirstName} {model.LastName} created successfully!";
                        return RedirectToAction("Pharmacists", "Admin");
                    }

                    foreach (var error in userResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating pharmacist account");
                    ModelState.AddModelError(string.Empty, "An error occurred while creating the pharmacist account.");
                }
            }

            return View(model);
        }

        public async Task<IActionResult> Pharmacists()
        {
            var pharmacists = await _context.Pharmacists
                .Include(p => p.User)
                .OrderBy(p => p.User != null ? p.User.FirstName : "")
                .ToListAsync();

            return View(pharmacists);
        }

        public async Task<IActionResult> EditPharmacist(int? id)
        {
            if (id == null)
                return NotFound();

            var pharmacist = await _context.Pharmacists
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pharmacist == null)
                return NotFound();

            var viewModel = new PharmacistEditViewModel
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

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPharmacist(int id, PharmacistEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var pharmacist = await _context.Pharmacists
                        .Include(p => p.User)
                        .FirstOrDefaultAsync(p => p.Id == id);

                    if (pharmacist == null)
                        return NotFound();

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
                    _context.Pharmacists.Update(pharmacist);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Pharmacist {model.FirstName} {model.LastName} updated successfully!";
                    return RedirectToAction(nameof(Pharmacists));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating pharmacist");
                    ModelState.AddModelError("", "An error occurred while updating the pharmacist.");
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePharmacist(int id)
        {
            try
            {
                var pharmacist = await _context.Pharmacists
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (pharmacist == null)
                {
                    TempData["ErrorMessage"] = "Pharmacist not found.";
                    return RedirectToAction(nameof(Pharmacists));
                }

                var currentUserEmail = User.Identity?.Name;
                if (pharmacist.User?.Email == currentUserEmail)
                {
                    TempData["ErrorMessage"] = "You cannot delete your own account.";
                    return RedirectToAction(nameof(Pharmacists));
                }

                var userId = pharmacist.UserId;
                _context.Pharmacists.Remove(pharmacist);
                await _context.SaveChangesAsync();

                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                    await _userManager.DeleteAsync(user);

                TempData["SuccessMessage"] = $"Pharmacist {pharmacist.User?.FullName} deleted successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting pharmacist");
                TempData["ErrorMessage"] = "An error occurred while deleting the pharmacist.";
            }

            return RedirectToAction(nameof(Pharmacists));
        }

        public async Task<IActionResult> PriceList()
        {
            try
            {
                var specializations = await _context.Specializations
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                return View(specializations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading price list");
                TempData["ErrorMessage"] = "An error occurred while loading the price list.";
                return View(new List<Specialization>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSpecializationFee(int id, decimal consultationFee)
        {
            try
            {
                if (consultationFee <= 0)
                {
                    TempData["ErrorMessage"] = "Please enter a valid price greater than zero.";
                    return RedirectToAction(nameof(PriceList));
                }

                var specialization = await _context.Specializations.FindAsync(id);
                if (specialization == null)
                    return NotFound();

                var oldFee = specialization.ConsultationFee;
                specialization.ConsultationFee = consultationFee;

                _context.Specializations.Update(specialization);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Specialization {SpecName} fee updated from {OldFee} to {NewFee} by admin {User}",
                    specialization.Name, oldFee, consultationFee, User.Identity?.Name);
                TempData["SuccessMessage"] = $"Consultation fee for '{specialization.Name}' updated successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating specialization fee");
                TempData["ErrorMessage"] = "An error occurred while updating the consultation fee.";
            }

            return RedirectToAction(nameof(PriceList));
        }

        [HttpGet]
        public IActionResult RegisterDoctorLeave(int id)
        {
            var doctor = _context.Doctors
                .Include(d => d.User)
                .FirstOrDefault(d => d.Id == id);

            if (doctor == null)
            {
                TempData["ErrorMessage"] = "Doctor not found.";
                return RedirectToAction("Doctors");
            }

            var viewModel = new RegisterDoctorLeaveViewModel
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.FullName
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterDoctorLeave(RegisterDoctorLeaveViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (model.EndDate < model.StartDate)
                {
                    ModelState.AddModelError("EndDate", "End date must be after or equal to start date.");
                    return View(model);
                }

                var existingAppointments = await _context.Appointments
                    .Where(a => a.DoctorId == model.DoctorId &&
                               a.AppointmentDateTime.Date >= model.StartDate &&
                               a.AppointmentDateTime.Date <= model.EndDate &&
                               a.Status == AppointmentStatus.Scheduled)
                    .CountAsync();

                if (existingAppointments > 0)
                {
                    ViewBag.WarningMessage = $"Doctor has {existingAppointments} future appointment(s) during this period.";
                    ViewBag.ShowWarning = true;
                }

                var leave = new DoctorLeave
                {
                    DoctorId = model.DoctorId,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    LeaveType = model.LeaveType,
                    Notes = model.Notes,
                    Status = LeaveStatus.Approved,
                    CreatedAt = ClinicTime.Now,
                    CreatedBy = User.Identity?.Name
                };

                var result = await _leaveService.RegisterDoctorLeaveAsync(leave);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = "Doctor's leave registered successfully.";
                    return RedirectToAction("Doctors");
                }

                TempData["ErrorMessage"] = result.Message;
            }

            return View(model);
        }

        public async Task<IActionResult> ViewDoctorLeaves(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.DoctorLeaves)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doctor == null)
            {
                TempData["ErrorMessage"] = "Doctor not found.";
                return RedirectToAction("Doctors");
            }

            var pendingRequests = await _context.LeaveRequests
                .Where(lr => lr.DoctorId == id)
                .OrderByDescending(lr => lr.CreatedAt)
                .ToListAsync();

            var viewModel = new DoctorLeaveViewModel
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
                LeaveRequests = pendingRequests.Select(lr => new LeaveRequestViewModel
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

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveLeaveRequest(int requestId, string notes)
        {
            var result = await _leaveService.ApproveLeaveRequestAsync(requestId, notes);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectLeaveRequest(int requestId, string reason)
        {
            var result = await _leaveService.RejectLeaveRequestAsync(requestId, reason);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction("Dashboard");
        }
    }
}
