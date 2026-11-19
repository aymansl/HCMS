using HCMS4.Controllers;
using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Billing.Services;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Feedback.Services;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HCMS4.Modules.Appointments.Controllers
{
    // Part of the former PatientController; [ControllerName] keeps the /Patient/... URLs.
    [Authorize(Roles = "Patient")]
    [ControllerName("Patient")]
    public class PatientAppointmentsController : BaseController
    {
        private readonly IAppointmentService _appointmentService;
        private readonly ILeaveService _leaveService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;
        private readonly IComplaintAttachmentService _attachmentService;
        private readonly IInvoicePaymentService _paymentService;

        public PatientAppointmentsController(
            ApplicationDbContext context,
            ILogger<PatientAppointmentsController> logger,
            IAppointmentService appointmentService,
            ILeaveService leaveService,
            INotificationService notificationService,
            IActivityLogService activityLogService,
            IComplaintAttachmentService attachmentService,
            IInvoicePaymentService paymentService)
            : base(context, logger)
        {
            _paymentService = paymentService;
            _appointmentService = appointmentService;
            _leaveService = leaveService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
            _attachmentService = attachmentService;
        }

        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> BookAppointment()
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == patientId.Value);

                if (patient == null)
                {
                    TempData["ErrorMessage"] = "Patient record not found.";
                    return RedirectToAction("Dashboard");
                }

                var today = ClinicTime.Today;
                var doctorsOnLeave = await _context.DoctorLeaves
                    .Where(dl => dl.Status == LeaveStatus.Approved && dl.StartDate <= today && dl.EndDate >= today)
                    .Select(dl => dl.DoctorId)
                    .ToListAsync();

                var doctors = await _context.Doctors
                    .Include(d => d.User)
                    .Include(d => d.Specialization)
                    .Where(d => d.IsAvailable && !doctorsOnLeave.Contains(d.Id))
                    .ToListAsync();

                var viewModel = new BookAppointmentViewModel
                {
                    PatientId = patient.Id,
                    AvailableDoctors = doctors.Select(d => new DoctorSelectDto
                    {
                        Id = d.Id,
                        FullName = d.User != null ? d.User.FullName : "Unknown",
                        Specialization = d.Specialization != null ? d.Specialization.Name : "Not set",
                        ConsultationFee = d.Specialization != null ? d.Specialization.ConsultationFee : 0
                    }).ToList(),
                    AvailablePatients = new List<PatientSelectDto>
                    {
                        new PatientSelectDto
                        {
                            Id = patient.Id,
                            FullName = patient.User != null ? patient.User.FullName : "Unknown",
                            Email = patient.User != null ? patient.User.Email : "N/A"
                        }
                    },
                    AppointmentDateTime = ClinicTime.Now.AddDays(1).Date.AddHours(9),
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading book appointment page for patient");
                TempData["ErrorMessage"] = "An error occurred while loading the appointment booking page.";
                return RedirectToAction("Dashboard");
            }
        }

        [Authorize(Roles = "Patient")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment(BookAppointmentViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var patientId = await GetCurrentPatientIdAsync();
                    if (!patientId.HasValue)
                    {
                        TempData["ErrorMessage"] = "User not found.";
                        return RedirectToAction("Dashboard");
                    }

                    var patient = await _context.Patients
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == patientId.Value);

                    if (patient == null || patient.Id != model.PatientId)
                    {
                        TempData["ErrorMessage"] = "You can only book appointments for yourself.";
                        return RedirectToAction("Dashboard");
                    }

                    if (model.AppointmentDateTime <= ClinicTime.Now)
                    {
                        ModelState.AddModelError("AppointmentDateTime", "Appointment must be in the future.");
                        return await ReloadViewModel(model, patient.Id);
                    }

                    var doctor = await _context.Doctors
                        .Include(d => d.Specialization)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(d => d.Id == model.DoctorId && d.IsAvailable);

                    if (doctor == null)
                    {
                        ModelState.AddModelError("DoctorId", "Selected doctor is not available.");
                        return await ReloadViewModel(model, patient.Id);
                    }

                    // Delegate conflict detection to the service (DRY - no duplication)
                    var appointment = new Appointment
                    {
                        PatientId = model.PatientId,
                        DoctorId = model.DoctorId,
                        AppointmentDateTime = model.AppointmentDateTime,
                        Status = AppointmentStatus.Scheduled,
                        ConsultationFee = doctor?.Specialization?.ConsultationFee ?? 0,
                        CreatedAt = ClinicTime.Now,
                        UpdatedAt = ClinicTime.Now
                    };

                    var result = await _appointmentService.CreateAsync(appointment);
                    if (result.Success)
                    {
                        if (!string.IsNullOrEmpty(model.Symptoms))
                        {
                            _logger.LogInformation(
                                "Patient {PatientId} booked appointment with symptoms: {Symptoms}",
                                model.PatientId, model.Symptoms);
                        }

                        TempData["SuccessMessage"] = result.Message;
                        return RedirectToAction(nameof(ViewAppointmentHistory));
                    }

                    ModelState.AddModelError("", result.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error booking appointment");
                    ModelState.AddModelError("", "An error occurred while booking the appointment.");
                }
            }

            var currentPatientId = await GetCurrentPatientIdAsync();
            if (currentPatientId.HasValue)
            {
                return await ReloadViewModel(model, currentPatientId.Value);
            }

            return View(model);
        }

        private async Task<IActionResult> ReloadViewModel(BookAppointmentViewModel model, int patientId)
        {
            var today = ClinicTime.Today;
            var doctorsOnLeave = await _context.DoctorLeaves
                .Where(dl => dl.Status == LeaveStatus.Approved && dl.StartDate <= today && dl.EndDate >= today)
                .Select(dl => dl.DoctorId)
                .ToListAsync();

            var doctors = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialization)
                .Where(d => d.IsAvailable && !doctorsOnLeave.Contains(d.Id))
                .ToListAsync();

            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == patientId);

            model.AvailableDoctors = doctors.Select(d => new DoctorSelectDto
            {
                Id = d.Id,
                FullName = d.User != null ? d.User.FullName : "Unknown",
                Specialization = d.Specialization != null ? d.Specialization.Name : "Not set",
                ConsultationFee = d.Specialization != null ? d.Specialization.ConsultationFee : 0
            }).ToList();

            if (patient != null)
            {
                model.AvailablePatients = new List<PatientSelectDto>
                {
                    new PatientSelectDto
                    {
                        Id = patient.Id,
                        FullName = patient.User != null ? patient.User.FullName : "Unknown",
                        Email = patient.User != null ? patient.User.Email : "N/A"
                    }
                };
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetDoctorConsultationFee(int doctorId)
        {
            return Ok(await _appointmentService.GetConsultationFeeAsync(doctorId));
        }

        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> ViewAppointmentHistory()
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var appointments = await _context.Appointments
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.User)
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.Specialization)
                    .Include(a => a.Patient)
                    .Where(a => a.PatientId == patientId.Value)
                    .OrderByDescending(a => a.AppointmentDateTime)
                    .ToListAsync();

                var visitRatings = await _context.VisitRatings
                    .Where(vr => vr.PatientId == patientId.Value)
                    .ToDictionaryAsync(vr => vr.AppointmentId);

                var viewModels = appointments.Select(a => new AppointmentHistoryViewModel
                {
                    Id = a.Id,
                    AppointmentDateTime = a.AppointmentDateTime,
                    DoctorName = a.Doctor?.User != null ? a.Doctor.User.FullName : "Unknown Doctor",
                    Specialization = a.Doctor?.Specialization?.Name ?? "General",
                    Status = a.Status.ToString(),
                    ConsultationFee = a.ConsultationFee,
                    CreatedAt = a.CreatedAt,
                    HasVisitRating = visitRatings.ContainsKey(a.Id),
                    VisitRatingId = visitRatings.TryGetValue(a.Id, out var rating) ? rating.Id : null,
                    CanRateVisit = a.Status == AppointmentStatus.Completed && !visitRatings.ContainsKey(a.Id)
                }).ToList();

                return View(viewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving appointment history for patient");
                ModelState.AddModelError("", "An error occurred while retrieving appointment history.");
                return View(new List<AppointmentHistoryViewModel>());
            }
        }
    }
}
