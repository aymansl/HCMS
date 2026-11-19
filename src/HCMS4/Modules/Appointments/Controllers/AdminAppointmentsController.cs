using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HCMS4.Shared.Identity.Authorization;

namespace HCMS4.Modules.Appointments.Controllers
{
    // Part of the former AdminController; [ControllerName] keeps the /Admin/... URLs.
    [Authorize(Policy = Permissions.Appointments.Manage)]
    [ControllerName("Admin")]
    public class AdminAppointmentsController : HcmsControllerBase
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IPatientService _patientService;
        private readonly IStaffService _staffService;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;
        private readonly IClinicClock _clock;
        private readonly ILogger<AdminAppointmentsController> _logger;

        public AdminAppointmentsController(
            IAppointmentService appointmentService,
            IPatientService patientService,
            IStaffService staffService,
            INoShowRiskService noShowRiskService,
            INotificationService notificationService,
            IActivityLogService activityLogService,
            IClinicClock clock,
            ILogger<AdminAppointmentsController> logger)
        {
            _appointmentService = appointmentService;
            _patientService = patientService;
            _staffService = staffService;
            _noShowRiskService = noShowRiskService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
            _clock = clock;
            _logger = logger;
        }

        public async Task<IActionResult> ManageAppointments(string status = "all", string riskLevel = "all")
        {
            await _appointmentService.UpdatePastScheduledToCompletedAsync();

            var todayAppointments = await _appointmentService.GetTodayAppointmentsAsync();
            var upcomingAppointments = await _appointmentService.GetUpcomingAppointmentsAsync();
            var canceledAppointments = await _appointmentService.GetCanceledAppointmentsAsync();

            List<Appointment> filteredAppointments = new();
            if (status != "all" && Enum.TryParse<AppointmentStatus>(status, out var statusEnum))
            {
                var result = await _appointmentService.GetAllAsync(
                    new PaginationParams { PageNumber = 1, PageSize = 100 }, status);
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
                    var risk = await _noShowRiskService.CalculateRiskScoresAsync(allAppointments);
                    riskScores = risk.Scores;
                    analyticsAvailable = risk.IsServiceAvailable;
                    isUsingAI = risk.IsUsingAI;
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

            viewModel.AvailableDoctors = await _staffService.GetBookableDoctorsAsync(_clock.Today);

            return View(viewModel);
        }

        public async Task<IActionResult> BookAppointment(int? patientId = null)
        {
            var viewModel = new BookAppointmentViewModel
            {
                AvailableDoctors = BookingSelectLists.ToDoctorOptions(await _staffService.GetBookableDoctorsAsync(_clock.Today)),
                AvailablePatients = BookingSelectLists.ToPatientOptions(await _patientService.GetAllWithUserAsync()),
                AppointmentDateTime = _clock.Now.AddDays(1).Date.AddHours(9),
            };

            if (patientId.HasValue)
                viewModel.PatientId = patientId.Value;

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment(BookAppointmentViewModel model)
        {
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
                        ConsultationFee = await _appointmentService.GetConsultationFeeAsync(model.DoctorId),
                        CreatedAt = _clock.Now,
                        UpdatedAt = _clock.Now
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

            model.AvailableDoctors = BookingSelectLists.ToDoctorOptions(await _staffService.GetBookableDoctorsAsync(_clock.Today));
            model.AvailablePatients = BookingSelectLists.ToPatientOptions(await _patientService.GetAllWithUserAsync());

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetDoctorConsultationFee(int doctorId)
        {
            return Ok(await _appointmentService.GetConsultationFeeAsync(doctorId));
        }

        public async Task<IActionResult> RescheduleAppointment(int? id)
        {
            if (id == null)
                return NotFound();

            var appointment = await _appointmentService.GetByIdAsync(id.Value);

            if (appointment == null)
                return NotFound();

            if (appointment.Status != AppointmentStatus.Scheduled)
            {
                TempData["ErrorMessage"] = "Only scheduled appointments can be rescheduled.";
                return RedirectToAction(nameof(ManageAppointments));
            }

            var viewModel = new RescheduleAppointmentViewModel
            {
                AppointmentId = appointment.Id,
                PatientName = appointment.Patient?.User?.FullName ?? string.Empty,
                DoctorName = appointment.Doctor?.User?.FullName ?? string.Empty,
                CurrentAppointmentDateTime = appointment.AppointmentDateTime,
                NewAppointmentDateTime = appointment.AppointmentDateTime.AddDays(1)
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleAppointment(RescheduleAppointmentViewModel model)
        {
            var appointment = await _appointmentService.GetByIdAsync(model.AppointmentId);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction(nameof(ManageAppointments));
            }

            var previousDateTime = appointment.AppointmentDateTime;
            if (ModelState.IsValid)
            {
                var result = await _appointmentService.RescheduleAsync(
                    model.AppointmentId, model.NewAppointmentDateTime);

                if (result.Success)
                {
                    if (appointment.Patient != null)
                    {
                        await _notificationService.CreateForUserAsync(
                            appointment.Patient.UserId,
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

            var appointment = await _appointmentService.GetByIdAsync(id);

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
            var appointment = await _appointmentService.GetByIdAsync(id);

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

        public async Task<IActionResult> DoctorSchedule(int id, string status = "all")
        {
            var doctor = await _staffService.GetDoctorProfileAsync(id);

            if (doctor == null)
                return NotFound();
            var allAppointments = await _appointmentService.GetByDoctorIdAsync(id);
            var today = _clock.Today;

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
    }
}
