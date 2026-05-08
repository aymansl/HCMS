using HCMS4.Data;
using HCMS4.Models;
using HCMS4.Models.Common;
using HCMS4.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static HCMS4.ViewModels.GenerateInvoiceViewModel;

namespace HCMS4.Controllers
{
    [Authorize(Roles = "Admin,Doctor")]
    public class AdminController : BaseController
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

        public AdminController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AdminController> logger,
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

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.DoctorCount = await _context.Doctors.CountAsync();
            ViewBag.PatientCount = await _context.Patients.CountAsync();
            ViewBag.AppointmentCount = await _context.Appointments.CountAsync();
            ViewBag.PendingComplaintsCount = await _context.Complaints.CountAsync(c => c.Status == ComplaintStatus.Submitted || c.Status == ComplaintStatus.UnderReview);
            ViewBag.ActiveSurveysCount = await _context.Surveys.CountAsync(s => s.Status == SurveyStatus.Active);
            var todayReport = await _reportService.GetDailyReportAsync(DateTime.Today);
            ViewBag.TodayReport = todayReport;

            return View();
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

        public async Task<IActionResult> Patients(int page = 1, int pageSize = 20)
        {
            try
            {
                var pagination = new Models.Common.PaginationParams
                {
                    PageNumber = page,
                    PageSize = pageSize
                };

                var result = await _patientService.GetAllAsync(pagination);

                var totalScheduledAppointments = await _context.Appointments
                    .Where(a => a.Status == AppointmentStatus.Scheduled)
                    .CountAsync();

                var totalPrescriptions = result.Items.Sum(p => p.Prescriptions?.Count ?? 0);
                var pendingInvoicesCount = result.Items
                    .SelectMany(p => p.Invoices ?? new List<Invoice>())
                    .Count(i => i.PaymentStatus == PaymentStatus.Pending);

                var viewModel = new PatientStatsViewModel
                {
                    Patients = result.Items,
                    TotalScheduledAppointments = totalScheduledAppointments,
                    TotalPrescriptions = totalPrescriptions,
                    PendingInvoicesCount = pendingInvoicesCount
                };

                ViewBag.TotalCount = result.TotalCount;
                ViewBag.TotalPages = result.TotalPages;
                ViewBag.CurrentPage = page;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading patients");
                return View(new PatientStatsViewModel());
            }
        }

        public async Task<IActionResult> EditPatient(int? id)
        {
            if (id == null)
                return NotFound();

            var patient = await _patientService.GetByIdAsync(id.Value);
            if (patient == null)
                return NotFound();

            return View(patient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPatient(int id, Patient patient)
        {
            if (id != patient.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                var result = await _patientService.UpdateAsync(id, patient);
                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Patients));
                }

                TempData["ErrorMessage"] = result.Message;
            }

            return View(patient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePatient(int id)
        {
            var result = await _patientService.DeleteAsync(id);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Patients));
        }

        public async Task<IActionResult> PatientDetails(int? id)
        {
            if (id == null)
                return NotFound();

            var patient = await _patientService.GetByIdWithDetailsAsync(id.Value);
            if (patient == null)
                return NotFound();

            return View(patient);
        }

        private bool PatientExists(int id) => _context.Patients.Any(e => e.Id == id);

        public async Task<IActionResult> ManageAppointments(string status = "all", string riskLevel = "all")
        {
            await _appointmentService.UpdatePastScheduledToCompletedAsync();

            var today = DateTime.Today;

            var todayAppointments = await _appointmentService.GetTodayAppointmentsAsync();
            var upcomingAppointments = await _appointmentService.GetUpcomingAppointmentsAsync();
            var canceledAppointments = await _appointmentService.GetCanceledAppointmentsAsync();

            List<Appointment> filteredAppointments = new();
            if (status != "all" && Enum.TryParse<AppointmentStatus>(status, out var statusEnum))
            {
                var result = await _appointmentService.GetAllAsync(
                    new Models.Common.PaginationParams { PageNumber = 1, PageSize = 100 }, status);
                filteredAppointments = result.Items;
            }

            if (User.IsInRole("Doctor"))
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (doctorId.HasValue)
                {
                    todayAppointments = todayAppointments.Where(a => a.DoctorId == doctorId.Value).ToList();
                    upcomingAppointments = upcomingAppointments.Where(a => a.DoctorId == doctorId.Value).ToList();
                    canceledAppointments = canceledAppointments.Where(a => a.DoctorId == doctorId.Value).ToList();
                    filteredAppointments = filteredAppointments.Where(a => a.DoctorId == doctorId.Value).ToList();
                }
            }

            var allAppointments = todayAppointments
                .Concat(upcomingAppointments.Take(20))
                .Concat(filteredAppointments)
                .Select(a => a.Id)
                .Distinct()
                .ToList();

            var riskScores = new Dictionary<int, double>();
            var analyticsAvailable = true;
            var isUsingAI = false;

            try
            {
                if (allAppointments.Any())
                {
                    riskScores = await _noShowRiskService.CalculateRiskScoresAsync(allAppointments);
                    analyticsAvailable = _noShowRiskService.IsServiceAvailable;
                    isUsingAI = _noShowRiskService.IsUsingAI;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating no-show risk scores");
                analyticsAvailable = false;
            }

            var viewModel = new ManageAppointmentsViewModel
            {
                TodayAppointments = todayAppointments,
                UpcomingAppointments = upcomingAppointments.Take(20).ToList(),
                FilteredAppointments = filteredAppointments,
                CanceledAppointments = canceledAppointments,
                SelectedStatus = status,
                SelectedRiskLevel = riskLevel,
                AnalyticsServiceAvailable = analyticsAvailable,
                IsUsingAI = isUsingAI,
                AppointmentRiskScores = riskScores
            };

            viewModel.TodayAppointments = viewModel.TodayAppointments
                .Where(a => RiskLevelHelper.Matches(riskLevel, riskScores.GetValueOrDefault(a.Id)))
                .ToList();
            viewModel.UpcomingAppointments = viewModel.UpcomingAppointments
                .Where(a => RiskLevelHelper.Matches(riskLevel, riskScores.GetValueOrDefault(a.Id)))
                .ToList();
            viewModel.FilteredAppointments = viewModel.FilteredAppointments
                .Where(a => RiskLevelHelper.Matches(riskLevel, riskScores.GetValueOrDefault(a.Id)))
                .ToList();
            viewModel.CanceledAppointments = viewModel.CanceledAppointments
                .Where(a => RiskLevelHelper.Matches(riskLevel, riskScores.GetValueOrDefault(a.Id)))
                .ToList();

            var doctorsOnLeave = await _context.DoctorLeaves
                .Where(dl => dl.Status == LeaveStatus.Approved && dl.StartDate <= today && dl.EndDate >= today)
                .Select(dl => dl.DoctorId)
                .ToListAsync();

            viewModel.AvailableDoctors = await _context.Doctors
                .Include(d => d.User)
                .Where(d => d.IsAvailable && !doctorsOnLeave.Contains(d.Id))
                .ToListAsync();

            return View(viewModel);
        }

        public async Task<IActionResult> BookAppointment(int? patientId = null)
        {
            var today = DateTime.Today;
            var doctorsOnLeave = await _context.DoctorLeaves
                .Where(dl => dl.Status == LeaveStatus.Approved && dl.StartDate <= today && dl.EndDate >= today)
                .Select(dl => dl.DoctorId)
                .ToListAsync();

            var doctors = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialization)
                .Where(d => d.IsAvailable && !doctorsOnLeave.Contains(d.Id))
                .ToListAsync();

            var patients = await _context.Patients
                .Include(p => p.User)
                .ToListAsync();

            var viewModel = new BookAppointmentViewModel
            {
                AvailableDoctors = doctors.Select(d => new DoctorSelectDto
                {
                    Id = d.Id,
                    FullName = d.User != null ? d.User.FullName : "Unknown",
                    Specialization = d.Specialization != null ? d.Specialization.Name : "Not set",
                    ConsultationFee = d.Specialization != null ? d.Specialization.ConsultationFee : 0
                }).ToList(),
                AvailablePatients = patients.Select(p => new PatientSelectDto
                {
                    Id = p.Id,
                    FullName = p.User != null ? p.User.FullName : "Unknown",
                    Email = p.User != null ? p.User.Email : "N/A"
                }).ToList(),
                AppointmentDateTime = DateTime.UtcNow.AddDays(1).Date.AddHours(9),
            };

            if (patientId.HasValue)
                viewModel.PatientId = patientId.Value;

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment(BookAppointmentViewModel model)
        {
            var doctor = await _context.Doctors
                .Include(d => d.Specialization)
                .FirstOrDefaultAsync(d => d.Id == model.DoctorId);

            if (ModelState.IsValid)
            {
                try
                {
                    var appointment = new Appointment
                    {
                        PatientId = model.PatientId,
                        DoctorId = model.DoctorId,
                        AppointmentDateTime = model.AppointmentDateTime,
                        Status = AppointmentStatus.Scheduled,
                        ConsultationFee = doctor?.Specialization?.ConsultationFee ?? 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    if (!string.IsNullOrEmpty(model.Symptoms))
                    {
                        _logger.LogInformation(
                            "Appointment booked with symptoms: {Symptoms} for Patient {PatientId}",
                            model.Symptoms, model.PatientId);
                    }

                    var result = await _appointmentService.CreateAsync(appointment);
                    if (result.Success)
                    {
                        TempData["SuccessMessage"] = result.Message;
                        return RedirectToAction(nameof(ManageAppointments));
                    }

                    ModelState.AddModelError("", result.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error booking appointment");
                    ModelState.AddModelError("", "An error occurred while booking the appointment.");
                }
            }

            var doctorsForReload = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialization)
                .Where(d => d.IsAvailable)
                .ToListAsync();

            var patientsForReload = await _context.Patients
                .Include(p => p.User)
                .ToListAsync();

            model.AvailableDoctors = doctorsForReload.Select(d => new DoctorSelectDto
            {
                Id = d.Id,
                FullName = d.User != null ? d.User.FullName : "Unknown",
                Specialization = d.Specialization != null ? d.Specialization.Name : "Not set",
                ConsultationFee = d.Specialization != null ? d.Specialization.ConsultationFee : 0
            }).ToList();

            model.AvailablePatients = patientsForReload.Select(p => new PatientSelectDto
            {
                Id = p.Id,
                FullName = p.User != null ? p.User.FullName : "Unknown",
                Email = p.User != null ? p.User.Email : "N/A"
            }).ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetDoctorConsultationFee(int doctorId)
        {
            var doctor = await _context.Doctors
                .Include(d => d.Specialization)
                .FirstOrDefaultAsync(d => d.Id == doctorId);
            var fee = doctor?.Specialization?.ConsultationFee ?? 0;
            return Ok(fee);
        }

        public async Task<IActionResult> RescheduleAppointment(int? id)
        {
            if (id == null)
                return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
                return NotFound();

            if (User.IsInRole("Doctor"))
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue || appointment.DoctorId != doctorId.Value)
                {
                    TempData["ErrorMessage"] = "You can only reschedule your own appointments.";
                    return RedirectToAction(nameof(ManageAppointments));
                }
            }

            if (appointment.Status != AppointmentStatus.Scheduled)
            {
                TempData["ErrorMessage"] = "Only scheduled appointments can be rescheduled.";
                return RedirectToAction(nameof(ManageAppointments));
            }

            var viewModel = new RescheduleAppointmentViewModel
            {
                AppointmentId = appointment.Id,
                PatientName = appointment.Patient?.User?.FullName,
                DoctorName = appointment.Doctor?.User?.FullName,
                CurrentAppointmentDateTime = appointment.AppointmentDateTime,
                NewAppointmentDateTime = appointment.AppointmentDateTime.AddDays(1)
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleAppointment(RescheduleAppointmentViewModel model)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == model.AppointmentId);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction(nameof(ManageAppointments));
            }

            if (User.IsInRole("Doctor"))
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue || appointment.DoctorId != doctorId.Value)
                {
                    TempData["ErrorMessage"] = "You can only reschedule your own appointments.";
                    return RedirectToAction(nameof(ManageAppointments));
                }
            }

            var previousDateTime = appointment.AppointmentDateTime;
            if (ModelState.IsValid)
            {
                var result = await _appointmentService.RescheduleAsync(
                    model.AppointmentId, model.NewAppointmentDateTime);

                if (result.Success)
                {
                    var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == appointment.PatientId);
                    if (patient != null)
                    {
                        await _notificationService.CreateForUserAsync(
                            patient.UserId,
                            "Appointment rescheduled",
                            $"Your appointment was rescheduled from {previousDateTime:yyyy-MM-dd HH:mm} to {model.NewAppointmentDateTime:yyyy-MM-dd HH:mm}.",
                            NotificationType.AppointmentReminder,
                            "/Patient/ViewAppointmentHistory",
                            nameof(Appointment),
                            appointment.Id);
                    }

                    var currentUser = await GetCurrentUserAsync();
                    await _activityLogService.LogAsync(
                        "AppointmentRescheduled",
                        nameof(Appointment),
                        $"Appointment #{appointment.Id} was rescheduled from {previousDateTime:yyyy-MM-dd HH:mm} to {model.NewAppointmentDateTime:yyyy-MM-dd HH:mm}.",
                        appointment.Id.ToString(),
                        currentUser?.Id,
                        currentUser?.UserName);

                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(ManageAppointments));
                }

                ModelState.AddModelError("", result.Message);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelAppointment(int id, string cancellationReason)
        {
            _logger.LogDebug("CancelAppointment called - ID: {Id}, Reason: {Reason}", id, cancellationReason);

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction(nameof(ManageAppointments));
            }

            if (User.IsInRole("Doctor"))
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue || appointment.DoctorId != doctorId.Value)
                {
                    TempData["ErrorMessage"] = "You can only cancel your own appointments.";
                    return RedirectToAction(nameof(ManageAppointments));
                }
            }

            var result = await _appointmentService.CancelAsync(id, cancellationReason);

            if (result.Success && appointment.Patient != null)
            {
                await _notificationService.CreateForUserAsync(
                    appointment.Patient.UserId,
                    "Appointment canceled",
                    $"Your appointment on {appointment.AppointmentDateTime:yyyy-MM-dd HH:mm} was canceled. Reason: {cancellationReason}",
                    NotificationType.AppointmentReminder,
                    "/Patient/ViewAppointmentHistory",
                    nameof(Appointment),
                    appointment.Id);

                var currentUser = await GetCurrentUserAsync();
                await _activityLogService.LogAsync(
                    "AppointmentCanceled",
                    nameof(Appointment),
                    $"Appointment #{appointment.Id} was canceled. Reason: {cancellationReason}",
                    appointment.Id.ToString(),
                    currentUser?.Id,
                    currentUser?.UserName);
            }

            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(ManageAppointments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendReminder(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null || appointment.Status != AppointmentStatus.Scheduled)
            {
                TempData["ErrorMessage"] = "Appointment not found or cannot receive reminders.";
                return RedirectToAction(nameof(ManageAppointments));
            }

            if (User.IsInRole("Doctor"))
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue || appointment.DoctorId != doctorId.Value)
                {
                    TempData["ErrorMessage"] = "You can only send reminders for your own appointments.";
                    return RedirectToAction(nameof(ManageAppointments));
                }
            }

            if (appointment.Patient != null)
            {
                await _notificationService.CreateForUserAsync(
                    appointment.Patient.UserId,
                    "Appointment reminder",
                    $"Reminder: you have a high-risk appointment on {appointment.AppointmentDateTime:yyyy-MM-dd HH:mm}.",
                    NotificationType.AppointmentReminder,
                    "/Patient/ViewAppointmentHistory",
                    nameof(Appointment),
                    appointment.Id);
            }

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "AppointmentReminderSent",
                nameof(Appointment),
                $"A reminder was sent for appointment #{appointment.Id}.",
                appointment.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Reminder sent successfully.";
            return RedirectToAction(nameof(ManageAppointments));
        }
        
        private bool AppointmentExists(int id) => _context.Appointments.Any(e => e.Id == id);

        public async Task<IActionResult> DoctorSchedule(int id, string status = "all")
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doctor == null)
                return NotFound();
            var allAppointments = await _appointmentService.GetByDoctorIdAsync(id);
            var today = DateTime.Today;

            var todayAppointments = allAppointments
                .Where(a => a.AppointmentDateTime.Date == today)
                .OrderBy(a => a.AppointmentDateTime)
                .ToList();

            var upcomingAppointments = allAppointments
                .Where(a => a.AppointmentDateTime.Date > today &&
                           a.Status == AppointmentStatus.Scheduled)
                .OrderBy(a => a.AppointmentDateTime)
                .Take(20)
                .ToList();

            var completedAppointments = allAppointments
                .Where(a => a.Status == AppointmentStatus.Completed)
                .OrderByDescending(a => a.AppointmentDateTime)
                .Take(20)
                .ToList();

            var canceledAppointments = allAppointments
                .Where(a => a.Status == AppointmentStatus.Canceled)
                .OrderByDescending(a => a.AppointmentDateTime)
                .Take(20)
                .ToList();

            List<Appointment> filteredAppointments = new();
            if (status != "all" && Enum.TryParse<AppointmentStatus>(status, out var statusEnum))
            {
                filteredAppointments = allAppointments
                    .Where(a => a.Status == statusEnum)
                    .ToList();
            }

            var viewModel = new DoctorScheduleViewModel
            {
                DoctorId = id,
                DoctorName = doctor?.FullName ?? "Unknown",
                Specialization = doctor?.SpecializationName ?? "Not set",
                SelectedStatus = status,
                AllAppointments = allAppointments,
                TodayAppointments = todayAppointments,
                UpcomingAppointments = upcomingAppointments,
                CompletedAppointments = completedAppointments,
                CanceledAppointments = canceledAppointments,
                FilteredAppointments = filteredAppointments,
                TotalAppointments = allAppointments.Count,
                ScheduledCount = allAppointments.Count(a => a.Status == AppointmentStatus.Scheduled),
                CompletedCount = allAppointments.Count(a => a.Status == AppointmentStatus.Completed),
                CanceledCount = allAppointments.Count(a => a.Status == AppointmentStatus.Canceled)
            };

            return View(viewModel);
        }



        private bool DrugExists(int id) => _context.Drugs.Any(e => e.Id == id);

        public async Task<IActionResult> PendingPrescriptions()
        {
            try
            {
                var pendingPrescriptions = await _prescriptionService.GetPendingAsync();
                return View(pendingPrescriptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending prescriptions");
                TempData["ErrorMessage"] = "An error occurred while loading pending prescriptions.";
                return View(new List<Prescription>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPrescriptionCompleted(int id)
        {
            var result = await _prescriptionService.MarkAsCompletedAsync(id);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(PendingPrescriptions));
        }

        public async Task<IActionResult> GenerateInvoice(int patientId)
        {
            try
            {
                _logger.LogInformation("Loading GenerateInvoice for patient {PatientId}", patientId);

                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == patientId);

                if (patient == null)
                {
                    _logger.LogWarning("Patient not found with ID: {PatientId}", patientId);
                    TempData["ErrorMessage"] = "Patient not found.";
                    return RedirectToAction(nameof(Patients));
                }

                var recentAppointments = await _context.Appointments
                    .Where(a => a.PatientId == patientId &&
                               a.Status == AppointmentStatus.Completed &&
                               a.AppointmentDateTime >= DateTime.UtcNow.AddDays(-30))
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.User)
                    .OrderByDescending(a => a.AppointmentDateTime)
                    .ToListAsync();

                var recentPrescriptions = await _context.Prescriptions
                    .Where(p => p.PatientId == patientId &&
                               p.Status == PrescriptionStatus.Completed &&
                               p.PrescriptionDate >= DateTime.UtcNow.AddDays(-30))
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.User)
                    .OrderByDescending(p => p.PrescriptionDate)
                    .ToListAsync();

                var existingInvoices = await _context.Invoices
                    .Where(i => i.PatientId == patientId)
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(5)
                    .ToListAsync();

                var consultationFee = recentAppointments
                    .Sum(a => a.ConsultationFee);

                var medicationTotal = recentPrescriptions
                    .Sum(p => p.MedicationTotal ?? 0);

                var totalAmount = consultationFee + medicationTotal;

                var viewModel = new GenerateInvoiceViewModel
                {
                    PatientId = patient.Id,
                    PatientName = patient.User?.FullName ?? "Unknown",
                    ConsultationFee = consultationFee,
                    MedicationTotal = medicationTotal,
                    TotalAmount = totalAmount,
                    RecentAppointments = recentAppointments.Select(a => new GenerateInvoiceViewModel.AppointmentSelectDto
                    {
                        Id = a.Id,
                        AppointmentDate = a.AppointmentDateTime,
                        DoctorName = a.Doctor?.User?.FullName ?? "Unknown",
                        ConsultationFee = a.ConsultationFee,
                        DisplayText = $"{a.AppointmentDateTime:yyyy-MM-dd} - {a.Doctor?.User?.FullName ?? "Unknown"} - {a.ConsultationFee:F2}"
                    }).ToList(),
                    RecentPrescriptions = recentPrescriptions.Select(p => new PrescriptionSelectDto
                    {
                        Id = p.Id,
                        PrescriptionDate = p.PrescriptionDate,
                        DoctorName = p.Doctor?.User?.FullName ?? "Unknown",
                        MedicationTotal = p.MedicationTotal ?? 0,
                        DisplayText = $"{p.PrescriptionDate:yyyy-MM-dd} - {p.Doctor?.User?.FullName ?? "Unknown"} - ${(p.MedicationTotal ?? 0):F2}"
                    }).ToList(),
                    ExistingInvoices = existingInvoices
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice generation page for patient {PatientId}", patientId);
                TempData["ErrorMessage"] = $"An error occurred while loading invoice details: {ex.Message}";
                return RedirectToAction(nameof(Patients));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateInvoice(GenerateInvoiceViewModel model)
        {
            var calculatedTotal = model.ConsultationFee + model.MedicationTotal;
            if (Math.Abs(model.TotalAmount - calculatedTotal) > 0.01m)
            {
                ModelState.AddModelError("TotalAmount",
                    $"Total amount (${model.TotalAmount:F2}) must equal Consultation Fee (${model.ConsultationFee:F2}) + Medication Total (${model.MedicationTotal:F2}) = ${calculatedTotal:F2}");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var patient = await _context.Patients
                        .Include(p => p.User)
                        .FirstOrDefaultAsync(p => p.Id == model.PatientId);

                    if (patient == null)
                    {
                        TempData["ErrorMessage"] = "Patient not found.";
                        return RedirectToAction(nameof(Patients));
                    }

                    if (model.AppointmentId.HasValue)
                    {
                        var appointment = await _context.Appointments
                            .FirstOrDefaultAsync(a => a.Id == model.AppointmentId.Value && a.PatientId == model.PatientId);
                        if (appointment == null)
                            ModelState.AddModelError("AppointmentId", "Appointment not found or doesn't belong to this patient.");
                    }

                    if (model.PrescriptionId.HasValue)
                    {
                        var prescription = await _context.Prescriptions
                            .FirstOrDefaultAsync(p => p.Id == model.PrescriptionId.Value && p.PatientId == model.PatientId);
                        if (prescription == null)
                            ModelState.AddModelError("PrescriptionId", "Prescription not found or doesn't belong to this patient.");
                    }

                    if (!ModelState.IsValid)
                    {
                        await ReloadInvoiceDropdowns(model);
                        return View(model);
                    }

                    var invoice = new Invoice
                    {
                        PatientId = model.PatientId,
                        AppointmentId = model.AppointmentId,
                        PrescriptionId = model.PrescriptionId,
                        InvoiceDate = DateTime.UtcNow,
                        ConsultationFee = model.ConsultationFee,
                        MedicationTotal = model.MedicationTotal,
                        TotalAmount = model.TotalAmount,
                        PaymentStatus = PaymentStatus.Pending,
                        PaymentDate = null,
                        AmountPaid = null,
                        Notes = model.Notes,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Invoices.Add(invoice);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Invoice {InvoiceId} created for patient {PatientId}", invoice.Id, model.PatientId);
                    TempData["SuccessMessage"] = $"Invoice #{invoice.Id} created successfully for {patient.User?.FullName}!";
                    return RedirectToAction(nameof(Patients));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating invoice for patient {PatientId}", model.PatientId);
                    ModelState.AddModelError("", "An error occurred while creating the invoice.");
                    await ReloadInvoiceDropdowns(model);
                }
            }

            return View(model);
        }

        public async Task<IActionResult> ViewInvoice(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Patient)
                        .ThenInclude(p => p.User)
                    .Include(i => i.Appointment)
                        .ThenInclude(a => a.Doctor)
                            .ThenInclude(d => d.User)
                    .Include(i => i.Prescription)
                        .ThenInclude(p => p.Doctor)
                            .ThenInclude(d => d.User)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (invoice == null)
                    return NotFound();

                return View(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing invoice {InvoiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the invoice.";
                return RedirectToAction(nameof(Patients));
            }
        }

        public async Task<IActionResult> InvoiceDetails(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Patient)
                        .ThenInclude(p => p.User)
                    .Include(i => i.Appointment)
                        .ThenInclude(a => a.Doctor)
                            .ThenInclude(d => d.User)
                    .Include(i => i.Prescription)
                        .ThenInclude(p => p.Doctor)
                            .ThenInclude(d => d.User)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (invoice == null)
                    return NotFound();

                if (invoice.PrescriptionId.HasValue)
                {
                    invoice.Prescription = await _context.Prescriptions
                        .Include(p => p.PrescriptionItems)
                            .ThenInclude(pi => pi.Drug)
                        .FirstOrDefaultAsync(p => p.Id == invoice.PrescriptionId.Value);
                }

                return View(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice details {InvoiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading invoice details.";
                return RedirectToAction(nameof(ViewInvoice), new { id });
            }
        }

        private async Task ReloadInvoiceDropdowns(GenerateInvoiceViewModel model)
        {
            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == model.PatientId);

            if (patient != null)
                model.PatientName = patient.User != null ? patient.User.FullName : "Unknown";

            var appointments = await _context.Appointments
                .Where(a => a.PatientId == model.PatientId && a.Status == AppointmentStatus.Completed)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .OrderByDescending(a => a.AppointmentDateTime)
                .Take(10)
                .ToListAsync();

            model.RecentAppointments = appointments.Select(a => new GenerateInvoiceViewModel.AppointmentSelectDto
            {
                Id = a.Id,
                AppointmentDate = a.AppointmentDateTime,
                DoctorName = a.Doctor?.User != null ? a.Doctor.User.FullName : "Unknown",
                ConsultationFee = a.ConsultationFee,
                DisplayText = $"{a.AppointmentDateTime:yyyy-MM-dd} - {(a.Doctor?.User != null ? a.Doctor.User.FullName : "Unknown")} - {a.ConsultationFee:F2}"
            }).ToList();

            var prescriptions = await _context.Prescriptions
                .Where(p => p.PatientId == model.PatientId && p.Status == PrescriptionStatus.Completed)
                .Include(p => p.Doctor)
                    .ThenInclude(d => d.User)
                .OrderByDescending(p => p.PrescriptionDate)
                .Take(10)
                .ToListAsync();

            model.RecentPrescriptions = prescriptions.Select(p => new PrescriptionSelectDto
            {
                Id = p.Id,
                PrescriptionDate = p.PrescriptionDate,
                DoctorName = p.Doctor?.User != null ? p.Doctor.User.FullName : "Unknown",
                MedicationTotal = p.MedicationTotal ?? 0,
                DisplayText = $"{p.PrescriptionDate:yyyy-MM-dd} - {(p.Doctor?.User != null ? p.Doctor.User.FullName : "Unknown")} - ${(p.MedicationTotal ?? 0):F2}"
            }).ToList();
        }

        [HttpGet]
        public async Task<IActionResult> UpdatePaymentStatus(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Patient)
                        .ThenInclude(p => p.User)
                    .Include(i => i.Appointment)
                        .ThenInclude(a => a.Doctor)
                            .ThenInclude(d => d.User)
                    .Include(i => i.Prescription)
                        .ThenInclude(p => p.Doctor)
                            .ThenInclude(d => d.User)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (invoice == null)
                {
                    TempData["ErrorMessage"] = "Invoice not found.";
                    return RedirectToAction("ManageInvoices");
                }

                if (invoice.PaymentStatus == PaymentStatus.Paid)
                {
                    TempData["WarningMessage"] = "This invoice is already marked as Paid and cannot be modified.";
                    return RedirectToAction("InvoiceDetails", new { id = invoice.Id });
                }

                return View(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice for payment update.");
                TempData["ErrorMessage"] = "An error occurred while loading the invoice.";
                return RedirectToAction("ManageInvoices");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("UpdatePaymentStatus")]
        public async Task<IActionResult> UpdatePaymentStatusPost(int id)
        {
            try
            {
                var paymentStatusStr = Request.Form["PaymentStatus"].ToString();
                var amountPaidStr = Request.Form["AmountPaid"].ToString();
                var paymentDateStr = Request.Form["PaymentDate"].ToString();
                var notes = Request.Form["Notes"].ToString();

                var invoice = await _context.Invoices.FindAsync(id);
                if (invoice == null)
                {
                    TempData["ErrorMessage"] = "Invoice not found.";
                    return RedirectToAction("ManageInvoices");
                }

                if (invoice.PaymentStatus == PaymentStatus.Paid)
                {
                    TempData["WarningMessage"] = "This invoice is already marked as Paid and cannot be modified.";
                    return RedirectToAction("InvoiceDetails", new { id = invoice.Id });
                }

                if (string.IsNullOrEmpty(paymentStatusStr))
                {
                    TempData["ErrorMessage"] = "Payment status is required.";
                    return RedirectToAction("UpdatePaymentStatus", new { id });
                }

                if (!Enum.TryParse<PaymentStatus>(paymentStatusStr, out var newStatus))
                {
                    TempData["ErrorMessage"] = "Invalid payment status.";
                    return RedirectToAction("UpdatePaymentStatus", new { id });
                }

                if (newStatus == PaymentStatus.Paid)
                {
                    if (string.IsNullOrEmpty(amountPaidStr))
                    {
                        TempData["ErrorMessage"] = "Amount paid is required for paid status.";
                        return RedirectToAction("UpdatePaymentStatus", new { id });
                    }

                    if (!decimal.TryParse(amountPaidStr, out var amountPaid))
                    {
                        TempData["ErrorMessage"] = "Invalid amount format.";
                        return RedirectToAction("UpdatePaymentStatus", new { id });
                    }

                    if (amountPaid != invoice.TotalAmount)
                    {
                        TempData["ErrorMessage"] = $"Amount paid (${amountPaid:F2}) must equal the total amount (${invoice.TotalAmount:F2}).";
                        return RedirectToAction("UpdatePaymentStatus", new { id });
                    }

                    DateTime paymentDate;
                    if (string.IsNullOrEmpty(paymentDateStr))
                        paymentDate = DateTime.UtcNow;
                    else if (!DateTime.TryParse(paymentDateStr, out paymentDate))
                    {
                        TempData["ErrorMessage"] = "Invalid payment date format.";
                        return RedirectToAction("UpdatePaymentStatus", new { id });
                    }

                    invoice.PaymentStatus = PaymentStatus.Paid;
                    invoice.AmountPaid = amountPaid;
                    invoice.PaymentDate = paymentDate;
                }
                else if (newStatus == PaymentStatus.Pending)
                {
                    invoice.PaymentStatus = PaymentStatus.Pending;
                    invoice.AmountPaid = null;
                    invoice.PaymentDate = null;
                }

                if (!string.IsNullOrWhiteSpace(notes))
                {
                    var prefix = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] Payment Update: ";
                    invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                        ? $"{prefix}{notes}"
                        : $"{invoice.Notes}\n{prefix}{notes}";
                }

                invoice.UpdatedAt = DateTime.UtcNow;
                _context.Update(invoice);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Invoice {InvoiceId} payment status updated to {Status} by {User}",
                    invoice.Id, invoice.PaymentStatus, User.Identity?.Name ?? "Unknown");

                TempData["SuccessMessage"] = $"Payment status updated successfully to '{invoice.PaymentStatus}'.";
                return RedirectToAction("InvoiceDetails", new { id = invoice.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment status for invoice {InvoiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the payment status.";
                return RedirectToAction("ManageInvoices");
            }
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
                            CreatedAt = DateTime.UtcNow,
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

        public async Task<IActionResult> DailyPharmacyReport(DateTime? date = null)
        {
            try
            {
                var targetDate = date ?? DateTime.Today;
                var report = await _reportService.GetDailyReportAsync(targetDate);
                var availableDates = await _reportService.GetAvailableReportDatesAsync();

                ViewBag.AvailableDates = availableDates;
                ViewBag.SelectedDate = targetDate;

                return View(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading daily pharmacy report");
                TempData["ErrorMessage"] = "An error occurred while loading the daily report.";
                return View(new DailyPharmacyReportViewModel { HasInvoices = false, ErrorMessage = "Failed to load report." });
            }
        }

        public async Task<IActionResult> FilteredPharmacyReport(DailyReportFilterViewModel filters)
        {
            try
            {
                var report = await _reportService.GetFilteredReportAsync(
                    filters.StartDate,
                    filters.EndDate,
                    filters.PaymentStatus,
                    filters.SearchTerm);

                ViewBag.Filters = filters;
                return View("DailyPharmacyReport", report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading filtered pharmacy report");
                TempData["ErrorMessage"] = "An error occurred while loading the filtered report.";
                return RedirectToAction(nameof(DailyPharmacyReport));
            }
        }

        public async Task<IActionResult> ExportPharmacyReport(DateTime? date)
        {
            try
            {
                var targetDate = date ?? DateTime.Today;
                var report = await _reportService.GetDailyReportAsync(targetDate);

                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Invoice ID,Patient Name,Pharmacist,Total Amount,Time,Payment Status,Prescription ID");

                foreach (var invoice in report.Invoices)
                {
                    csv.AppendLine($"{invoice.InvoiceId},{invoice.PatientName},{invoice.PharmacistName},{invoice.TotalAmount:C2},{invoice.InvoiceTime:yyyy-MM-dd HH:mm},{invoice.PaymentStatus},{invoice.PrescriptionId}");
                }

                csv.AppendLine();
                csv.AppendLine($"Total Invoices,{report.TotalInvoices}");
                csv.AppendLine($"Total Amount,{report.TotalAmount:C2}");
                csv.AppendLine($"Report Date,{targetDate:yyyy-MM-dd}");

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                var result = new FileContentResult(bytes, "text/csv")
                {
                    FileDownloadName = $"Pharmacy_Report_{targetDate:yyyy-MM-dd}.csv"
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting pharmacy report");
                TempData["ErrorMessage"] = "An error occurred while exporting the report.";
                return RedirectToAction(nameof(DailyPharmacyReport));
            }
        }

        public async Task<IActionResult> Suppliers()
        {
            try
            {
                var suppliers = await _context.Suppliers
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                return View(suppliers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading suppliers");
                TempData["ErrorMessage"] = "An error occurred while loading suppliers.";
                return View(new List<Supplier>());
            }
        }

        public IActionResult CreateSupplier()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSupplier(Supplier supplier)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingSupplier = await _context.Suppliers
                        .AnyAsync(s => s.Name == supplier.Name && s.IsActive);

                    if (existingSupplier)
                    {
                        ModelState.AddModelError("", "Supplier name already exists. Please use a different name.");
                        return View(supplier);
                    }

                    supplier.CreatedAt = DateTime.UtcNow;
                    supplier.IsActive = true;

                    _context.Suppliers.Add(supplier);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Supplier {SupplierName} created by admin {User}",
                        supplier.Name, User.Identity?.Name);
                    TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' added successfully!";
                    return RedirectToAction(nameof(Suppliers));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating supplier");
                    ModelState.AddModelError("", "An error occurred while creating the supplier.");
                }
            }

            return View(supplier);
        }

        public async Task<IActionResult> EditSupplier(int? id)
        {
            if (id == null)
                return NotFound();

            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
                return NotFound();

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSupplier(int id, Supplier supplier)
        {
            if (id != supplier.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingSupplier = await _context.Suppliers
                        .AnyAsync(s => s.Name == supplier.Name && s.IsActive && s.Id != supplier.Id);

                    if (existingSupplier)
                    {
                        ModelState.AddModelError("", "Supplier name already exists. Please use a different name.");
                        return View(supplier);
                    }

                    var existing = await _context.Suppliers.FindAsync(id);
                    if (existing == null)
                        return NotFound();

                    existing.Name = supplier.Name;
                    existing.ContactPerson = supplier.ContactPerson;
                    existing.Phone = supplier.Phone;
                    existing.Email = supplier.Email;
                    existing.Address = supplier.Address;

                    _context.Suppliers.Update(existing);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Supplier {SupplierName} updated by admin {User}",
                        supplier.Name, User.Identity?.Name);
                    TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' updated successfully!";
                    return RedirectToAction(nameof(Suppliers));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(supplier.Id))
                        return NotFound();
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating supplier");
                    ModelState.AddModelError("", "An error occurred while updating the supplier.");
                }
            }

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            try
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.PurchaseRequests)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (supplier == null)
                {
                    TempData["ErrorMessage"] = "Supplier not found.";
                    return RedirectToAction(nameof(Suppliers));
                }

                if (supplier.PurchaseRequests?.Any() == true)
                {
                    supplier.IsActive = false;
                    _context.Suppliers.Update(supplier);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Supplier {SupplierName} deactivated by admin {User}",
                        supplier.Name, User.Identity?.Name);
                    TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' has been deactivated (has associated purchase requests).";
                }
                else
                {
                    _context.Suppliers.Remove(supplier);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Supplier {SupplierName} deleted by admin {User}",
                        supplier.Name, User.Identity?.Name);
                    TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' deleted successfully!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting supplier");
                TempData["ErrorMessage"] = "An error occurred while deleting the supplier.";
            }

            return RedirectToAction(nameof(Suppliers));
        }

        private bool SupplierExists(int id) => _context.Suppliers.Any(e => e.Id == id);

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

        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> DailyVisitingPatientsReport(DateTime? date = null)
        {
            try
            {
                var reportDate = date ?? DateTime.Today;

                if (reportDate > DateTime.Today)
                {
                    TempData["ErrorMessage"] = "Cannot display report for a future date.";
                    return View(new DailyVisitingPatientsReportViewModel
                    {
                        ReportDate = reportDate,
                        ErrorMessage = "Cannot display report for a future date."
                    });
                }

                var appointments = await _context.Appointments
                    .Include(a => a.Patient)
                        .ThenInclude(p => p.User)
                    .Include(a => a.Doctor)
                    .Where(a => a.Status == AppointmentStatus.Completed &&
                               a.AppointmentDateTime.Date == reportDate.Date)
                    .OrderBy(a => a.AppointmentDateTime)
                    .ToListAsync();

                var prescriptions = await _context.Prescriptions
                    .Where(p => p.AppointmentId.HasValue &&
                               appointments.Select(a => a.Id).Contains(p.AppointmentId.Value))
                    .Include(p => p.PrescriptionItems)
                    .ToListAsync();

                var viewModel = new DailyVisitingPatientsReportViewModel
                {
                    ReportDate = reportDate,
                    TotalPatients = appointments.Count,
                    Patients = appointments.Select(a =>
                    {
                        var prescription = prescriptions.FirstOrDefault(p => p.AppointmentId == a.Id);
                        var hasPrescription = prescription != null;
                        var diagnosis = GetDiagnosis(prescription);
                        return new VisitingPatientItem
                        {
                            AppointmentId = a.Id,
                            PatientName = a.Patient?.User?.FullName ?? "Unknown",
                            PhoneNumber = a.Patient?.User?.PhoneNumber,
                            AppointmentTime = a.AppointmentDateTime,
                            VisitType = DetermineVisitType(a.PatientId, a.Id),
                            Diagnosis = diagnosis,
                            HasPrescription = hasPrescription
                        };
                    }).ToList()
                };

                if (!viewModel.Patients.Any())
                {
                    viewModel.ErrorMessage = "No visiting patients on the selected date.";
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading daily visiting patients report");
                TempData["ErrorMessage"] = "Failed to load report data, please try again later.";
                return View(new DailyVisitingPatientsReportViewModel { ErrorMessage = "Failed to load report data, please try again later." });
            }
        }

        private string DetermineVisitType(int patientId, int currentAppointmentId)
        {
            var previousAppointments = _context.Appointments
                .Count(a => a.PatientId == patientId &&
                           a.Id != currentAppointmentId &&
                           a.Status == AppointmentStatus.Completed);

            return previousAppointments > 0 ? "Follow-up" : "Consultation";
        }

        private string? GetDiagnosis(Prescription? prescription)
        {
            if (prescription != null && prescription.PrescriptionItems.Any())
            {
                var drugName = prescription.PrescriptionItems.First().DrugName;
                return drugName?.Length > 20
                    ? drugName.Substring(0, 20) + "..."
                    : drugName;
            }

            return null;
        }

        [HttpGet]
        public IActionResult DisablePatient(int id)
        {
            var patient = _context.Patients
                .Include(p => p.User)
                .FirstOrDefault(p => p.Id == id);

            if (patient == null)
            {
                TempData["ErrorMessage"] = "Patient not found.";
                return RedirectToAction("Patients");
            }

            var viewModel = new DisablePatientViewModel
            {
                PatientId = patient.Id,
                PatientName = patient.User?.FullName ?? "Unknown"
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisablePatient(DisablePatientViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.DisableReason))
            {
                ModelState.AddModelError("DisableReason", "Please provide a reason for disabling the account.");
                return View(model);
            }

            var result = await _patientService.DisablePatientAsync(model.PatientId, model.DisableReason);

            if (result.Success)
            {
                TempData["SuccessMessage"] = "Patient account disabled successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Patients");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnablePatient(int id)
        {
            var result = await _patientService.EnablePatientAsync(id);

            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction("Patients");
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
                    CreatedAt = DateTime.UtcNow,
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

        public async Task<IActionResult> PurchaseRequests(string status = "all")
        {
            try
            {
                IQueryable<PurchaseRequest> query;

                if (status != "all" && Enum.TryParse<PurchaseRequestStatus>(status, true, out var statusEnum))
                {
                    query = _context.PurchaseRequests
                        .Include(pr => pr.Pharmacist)
                            .ThenInclude(p => p.User)
                        .Include(pr => pr.Supplier)
                        .Include(pr => pr.Items)
                        .Where(pr => pr.Status == statusEnum)
                        .OrderByDescending(pr => pr.RequestDate);
                }
                else
                {
                    query = _context.PurchaseRequests
                        .Include(pr => pr.Pharmacist)
                            .ThenInclude(p => p.User)
                        .Include(pr => pr.Supplier)
                        .Include(pr => pr.Items)
                        .OrderByDescending(pr => pr.RequestDate);
                }

                var purchaseRequests = await query.ToListAsync();

                var viewModel = new AdminPurchaseRequestListViewModel
                {
                    PurchaseRequests = purchaseRequests,
                    PendingCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Pending),
                    ApprovedCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Approved),
                    RejectedCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Rejected),
                    OrderedCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Ordered),
                    ReceivedCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Received),
                    SelectedStatus = status
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin purchase requests: {Message}", ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading purchase requests: " + ex.Message;
                return View(new AdminPurchaseRequestListViewModel());
            }
        }

        public async Task<IActionResult> PurchaseRequestDetails(int id)
        {
            try
            {
                var purchaseRequest = await _context.PurchaseRequests
                    .Include(pr => pr.Pharmacist)
                        .ThenInclude(p => p.User)
                    .Include(pr => pr.Supplier)
                    .Include(pr => pr.Items)
                    .FirstOrDefaultAsync(pr => pr.Id == id);

                if (purchaseRequest == null)
                {
                    TempData["ErrorMessage"] = "Purchase request not found.";
                    return RedirectToAction("PurchaseRequests");
                }

                var viewModel = new AdminPurchaseRequestDetailViewModel
                {
                    PurchaseRequest = purchaseRequest,
                    Items = purchaseRequest.Items.ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading purchase request details {Id}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading the purchase request: " + ex.Message;
                return RedirectToAction("PurchaseRequests");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePurchaseRequest(int id)
        {
            try
            {
                var purchaseRequest = await _context.PurchaseRequests
                    .Include(pr => pr.Items)
                    .FirstOrDefaultAsync(pr => pr.Id == id);

                if (purchaseRequest == null)
                {
                    TempData["ErrorMessage"] = "Purchase request not found.";
                    return RedirectToAction("PurchaseRequests");
                }

                if (purchaseRequest.Status != PurchaseRequestStatus.Pending)
                {
                    TempData["ErrorMessage"] = "This request has already been processed.";
                    return RedirectToAction("PurchaseRequests");
                }

                foreach (var item in purchaseRequest.Items)
                {
                    var drug = await _context.Drugs.FindAsync(item.DrugId);
                    if (drug != null)
                    {
                        drug.Quantity += item.Quantity;
                    }
                }

                var currentUser = await _userManager.GetUserAsync(User);
                purchaseRequest.Status = PurchaseRequestStatus.Approved;
                purchaseRequest.ApprovedDate = DateTime.UtcNow;
                purchaseRequest.ApprovedBy = currentUser?.UserName;
                purchaseRequest.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Purchase request {RequestId} approved by admin {AdminUsername}. Inventory updated.",
                    id, currentUser?.UserName);

                TempData["SuccessMessage"] = "Purchase request approved successfully. Drug quantities have been updated.";
                return RedirectToAction("PurchaseRequestDetails", new { id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving purchase request {Id}", id);
                TempData["ErrorMessage"] = "Failed to approve request, please try again later.";
                return RedirectToAction("PurchaseRequests");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPurchaseRequest(int id, string rejectionReason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    TempData["ErrorMessage"] = "Please enter a rejection reason.";
                    return RedirectToAction("PurchaseRequestDetails", new { id = id });
                }

                var purchaseRequest = await _context.PurchaseRequests.FindAsync(id);
                if (purchaseRequest == null)
                {
                    TempData["ErrorMessage"] = "Purchase request not found.";
                    return RedirectToAction("PurchaseRequests");
                }

                if (purchaseRequest.Status != PurchaseRequestStatus.Pending)
                {
                    TempData["ErrorMessage"] = "This request has already been processed.";
                    return RedirectToAction("PurchaseRequests");
                }

                var currentUser = await _userManager.GetUserAsync(User);
                purchaseRequest.Status = PurchaseRequestStatus.Rejected;
                purchaseRequest.RejectionReason = rejectionReason;
                purchaseRequest.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Purchase request {RequestId} rejected by admin {AdminUsername}. Reason: {Reason}",
                    id, currentUser?.UserName, rejectionReason);

                TempData["SuccessMessage"] = "Purchase request rejected.";
                return RedirectToAction("PurchaseRequestDetails", new { id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting purchase request {Id}", id);
                TempData["ErrorMessage"] = "Failed to reject request, please try again later.";
                return RedirectToAction("PurchaseRequests");
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Complaints()
        {
            var complaints = await _context.Complaints
                .Include(c => c.Patient)
                    .ThenInclude(p => p.User)
                .OrderByDescending(c => c.SubmissionDate)
                .ToListAsync();

            return View(complaints);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ComplaintDetails(int id)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Patient)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
            {
                TempData["ErrorMessage"] = "Complaint not found.";
                return RedirectToAction(nameof(Complaints));
            }

            if (complaint.Status == ComplaintStatus.Submitted)
            {
                complaint.Status = ComplaintStatus.UnderReview;
                await _context.SaveChangesAsync();
            }

            return View(complaint);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateComplaintStatus(int id, ComplaintStatus status, string? resolutionNotes)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Patient)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
            {
                TempData["ErrorMessage"] = "Complaint not found.";
                return RedirectToAction(nameof(Complaints));
            }

            complaint.Status = status;
            complaint.ResolutionNotes = resolutionNotes;
            complaint.ResolvedAt = status == ComplaintStatus.Resolved || status == ComplaintStatus.Closed
                ? DateTime.UtcNow
                : null;

            await _context.SaveChangesAsync();

            await _notificationService.CreateForUserAsync(
                complaint.Patient.UserId,
                "Complaint status updated",
                $"Your complaint {complaint.TrackingNumber} status is now {status}.",
                NotificationType.Complaint,
                "/Patient/Complaints",
                nameof(Complaint),
                complaint.Id);

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "ComplaintStatusUpdated",
                nameof(Complaint),
                $"Complaint {complaint.TrackingNumber} updated to {status}.",
                complaint.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Complaint updated successfully.";
            return RedirectToAction(nameof(ComplaintDetails), new { id });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Surveys()
        {
            var surveys = await _context.Surveys
                .Include(s => s.Questions)
                .Include(s => s.Assignments)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return View(surveys);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> CreateSurvey()
        {
            var viewModel = new SurveyEditorViewModel
            {
                AvailablePatients = await _context.Patients
                    .Include(p => p.User)
                    .Where(p => p.Status == PatientStatus.Active)
                    .Select(p => new PatientSelectDto
                    {
                        Id = p.Id,
                        FullName = p.User.FullName,
                        Email = p.User.Email ?? "N/A"
                    })
                    .ToListAsync()
            };

            return View(viewModel);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSurvey(SurveyEditorViewModel model)
        {
            if (model.EndDate.HasValue && model.EndDate.Value < model.StartDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "End date must be after start date.");
            }

            var validQuestions = model.Questions
                .Where(q => !string.IsNullOrWhiteSpace(q.QuestionText))
                .ToList();

            if (!validQuestions.Any())
            {
                ModelState.AddModelError(string.Empty, "You must add at least one question.");
            }

            if (model.TargetAudience == SurveyTargetAudience.SpecificPatients && !model.SpecificPatientIds.Any())
            {
                ModelState.AddModelError(string.Empty, "Please select at least one patient for a specific-patient survey.");
            }

            if (!ModelState.IsValid)
            {
                model.AvailablePatients = await _context.Patients
                    .Include(p => p.User)
                    .Where(p => p.Status == PatientStatus.Active)
                    .Select(p => new PatientSelectDto
                    {
                        Id = p.Id,
                        FullName = p.User.FullName,
                        Email = p.User.Email ?? "N/A"
                    })
                    .ToListAsync();
                return View(model);
            }

            var currentUser = await GetCurrentUserAsync();
            var survey = new Survey
            {
                Title = model.Title.Trim(),
                Description = model.Description?.Trim(),
                StartDate = model.StartDate.Date,
                EndDate = model.EndDate?.Date,
                TargetAudience = model.TargetAudience,
                TargetCriteria = model.TargetCriteria?.Trim(),
                Status = model.SendImmediately ? SurveyStatus.Active : SurveyStatus.Draft,
                CreatedByUserId = currentUser?.Id ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SentAt = model.SendImmediately ? DateTime.UtcNow : null
            };

            _context.Surveys.Add(survey);
            await _context.SaveChangesAsync();

            var questionOrder = 0;
            foreach (var question in validQuestions)
            {
                var surveyQuestion = new SurveyQuestion
                {
                    SurveyId = survey.Id,
                    QuestionText = question.QuestionText.Trim(),
                    QuestionType = question.QuestionType,
                    IsRequired = question.IsRequired,
                    DisplayOrder = questionOrder++
                };

                _context.SurveyQuestions.Add(surveyQuestion);
                await _context.SaveChangesAsync();

                if (question.QuestionType == SurveyQuestionType.MultipleChoice &&
                    !string.IsNullOrWhiteSpace(question.OptionsText))
                {
                    var options = question.OptionsText
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(o => o.Trim())
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .Distinct()
                        .ToList();

                    for (var i = 0; i < options.Count; i++)
                    {
                        _context.SurveyQuestionOptions.Add(new SurveyQuestionOption
                        {
                            SurveyQuestionId = surveyQuestion.Id,
                            OptionText = options[i],
                            DisplayOrder = i
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            if (model.SendImmediately)
            {
                var targetPatients = await GetSurveyTargetPatientsAsync(model);
                foreach (var patient in targetPatients)
                {
                    _context.SurveyAssignments.Add(new SurveyAssignment
                    {
                        SurveyId = survey.Id,
                        PatientId = patient.Id,
                        Status = SurveyAssignmentStatus.Pending,
                        AssignedAt = DateTime.UtcNow
                    });

                    await _notificationService.CreateForUserAsync(
                        patient.UserId,
                        "New survey available",
                        $"A new survey '{survey.Title}' is available in your account.",
                        NotificationType.Survey,
                        "/Patient/Surveys",
                        nameof(Survey),
                        survey.Id);
                }

                await _context.SaveChangesAsync();
            }

            await _activityLogService.LogAsync(
                model.SendImmediately ? "SurveyCreatedAndSent" : "SurveyDraftCreated",
                nameof(Survey),
                $"Survey '{survey.Title}' was {(model.SendImmediately ? "created and sent" : "saved as draft")}.",
                survey.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Survey created successfully.";
            return RedirectToAction(nameof(Surveys));
        }

        private async Task<List<Patient>> GetSurveyTargetPatientsAsync(SurveyEditorViewModel model)
        {
            IQueryable<Patient> query = _context.Patients
                .Include(p => p.User)
                .Where(p => p.Status == PatientStatus.Active);

            if (model.TargetAudience == SurveyTargetAudience.SpecificPatients)
            {
                query = query.Where(p => model.SpecificPatientIds.Contains(p.Id));
            }
            else if (model.TargetAudience == SurveyTargetAudience.SpecificCategory &&
                     !string.IsNullOrWhiteSpace(model.TargetCriteria))
            {
                var criteria = model.TargetCriteria.Trim().ToLower();
                query = query.Where(p =>
                    (p.ChronicConditions != null && p.ChronicConditions.ToLower().Contains(criteria)) ||
                    (p.Address != null && p.Address.ToLower().Contains(criteria)) ||
                    (p.User.Email != null && p.User.Email.ToLower().Contains(criteria)) ||
                    (p.User.FirstName + " " + p.User.LastName).ToLower().Contains(criteria));
            }

            return await query.ToListAsync();
        }
    }
}