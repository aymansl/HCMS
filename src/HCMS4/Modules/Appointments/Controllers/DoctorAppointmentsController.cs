using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Appointments.Controllers
{
    // Part of the former DoctorController; [ControllerName] keeps the /Doctor/... URLs.
    [Authorize(Roles = "Doctor")]
    [ControllerName("Doctor")]
    public class DoctorAppointmentsController : HcmsControllerBase
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IDailyVisitReportService _visitReportService;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;
        private readonly IClinicClock _clock;
        private readonly ILogger<DoctorAppointmentsController> _logger;

        public DoctorAppointmentsController(
            IAppointmentService appointmentService,
            IDailyVisitReportService visitReportService,
            INoShowRiskService noShowRiskService,
            INotificationService notificationService,
            IActivityLogService activityLogService,
            IClinicClock clock,
            ILogger<DoctorAppointmentsController> logger)
        {
            _appointmentService = appointmentService;
            _visitReportService = visitReportService;
            _noShowRiskService = noShowRiskService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
            _clock = clock;
            _logger = logger;
        }

        public async Task<IActionResult> ViewAppointments(string viewMode = "today", DateTime? selectedDate = null,
                                                     DateTime? startDate = null, DateTime? endDate = null,
                                                     string riskLevel = "all")
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                var viewModel = new DoctorAppointmentsViewModel
                {
                    SelectedDate = selectedDate,
                    StartDate = startDate,
                    EndDate = endDate,
                    ViewMode = viewMode,
                    SelectedRiskLevel = riskLevel
                };

                var today = _clock.Today;
                var now = _clock.Now;

                if (viewMode == "today")
                {
                    viewModel.TodayAppointments = await _appointmentService.GetDoctorAppointmentsAsync(
                        doctorId.Value, today, today, scheduledOnly: true);
                    viewModel.UpcomingAppointments = await _appointmentService.GetDoctorAppointmentsAsync(
                        doctorId.Value, today.AddDays(1), DateTime.MaxValue, scheduledOnly: true,
                        take: BusinessRules.MaxUpcomingAppointments);
                }
                else if (viewMode == "date" && selectedDate.HasValue)
                {
                    viewModel.TodayAppointments = await _appointmentService.GetDoctorAppointmentsAsync(
                        doctorId.Value, selectedDate.Value, selectedDate.Value, scheduledOnly: true);
                }
                else if (viewMode == "range" && startDate.HasValue && endDate.HasValue)
                {
                    var appointments = await _appointmentService.GetDoctorAppointmentsAsync(
                        doctorId.Value, startDate.Value, endDate.Value, scheduledOnly: false);

                    foreach (var appointment in appointments)
                    {
                        if (appointment.AppointmentDateTime.Date == today &&
                            appointment.Status == AppointmentStatus.Scheduled)
                        {
                            viewModel.TodayAppointments.Add(appointment);
                        }
                        else if (appointment.AppointmentDateTime > now &&
                                 appointment.Status == AppointmentStatus.Scheduled)
                        {
                            viewModel.UpcomingAppointments.Add(appointment);
                        }
                        else
                        {
                            viewModel.PastAppointments.Add(appointment);
                        }
                    }
                }

                var appointmentIds = viewModel.TodayAppointments
                    .Concat(viewModel.UpcomingAppointments)
                    .Concat(viewModel.PastAppointments)
                    .Select(a => a.Id)
                    .Distinct()
                    .ToList();

                if (appointmentIds.Any())
                {
                    var risk = await _noShowRiskService.CalculateRiskScoresAsync(appointmentIds);
                    viewModel.AppointmentRiskScores = risk.Scores;
                    viewModel.AnalyticsServiceAvailable = risk.IsServiceAvailable;
                    viewModel.IsUsingAI = risk.IsUsingAI;

                    viewModel.TodayAppointments = viewModel.TodayAppointments
                        .Where(a => RiskLevelHelper.Matches(riskLevel, viewModel.AppointmentRiskScores.GetValueOrDefault(a.Id)))
                        .ToList();
                    viewModel.UpcomingAppointments = viewModel.UpcomingAppointments
                        .Where(a => RiskLevelHelper.Matches(riskLevel, viewModel.AppointmentRiskScores.GetValueOrDefault(a.Id)))
                        .ToList();
                    viewModel.PastAppointments = viewModel.PastAppointments
                        .Where(a => RiskLevelHelper.Matches(riskLevel, viewModel.AppointmentRiskScores.GetValueOrDefault(a.Id)))
                        .ToList();
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading doctor appointments");
                TempData["ErrorMessage"] = "Error loading appointments. Please try again.";
                return View(new DoctorAppointmentsViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendReminder(int id)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var appointment = await _appointmentService.GetByIdAsync(id);

            if (appointment == null || appointment.DoctorId != doctorId.Value || appointment.Status != AppointmentStatus.Scheduled)
            {
                TempData["ErrorMessage"] = "Appointment not found or cannot receive reminders.";
                return RedirectToAction(nameof(ViewAppointments));
            }

            if (appointment.Patient != null)
            {
                await _notificationService.CreateForUserAsync(
                    appointment.Patient.UserId,
                    "Appointment reminder",
                    $"Reminder: you have an appointment on {appointment.AppointmentDateTime:yyyy-MM-dd HH:mm}.",
                    NotificationType.AppointmentReminder,
                    "/Patient/ViewAppointmentHistory",
                    nameof(Appointment),
                    appointment.Id);
            }

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "AppointmentReminderSent",
                nameof(Appointment),
                $"Doctor sent a reminder for appointment #{appointment.Id}.",
                appointment.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Reminder sent to the patient.";
            return RedirectToAction(nameof(ViewAppointments));
        }

        public async Task<IActionResult> ViewReports()
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                return RedirectToAction(nameof(DailyVisitingPatientsReport));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reports");
                TempData["ErrorMessage"] = "Error loading reports. Please try again.";
                return RedirectToAction("Dashboard");
            }
        }

        public async Task<IActionResult> DailyVisitingPatientsReport(DateTime? date = null)
        {
            try
            {
                var reportDate = date ?? _clock.Today;

                if (reportDate > _clock.Today)
                {
                    TempData["ErrorMessage"] = "Cannot display report for a future date.";
                    return View(new DailyVisitingPatientsReportViewModel
                    {
                        ReportDate = reportDate,
                        ErrorMessage = "Cannot display report for a future date."
                    });
                }

                var viewModel = await _visitReportService.GetReportAsync(reportDate);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading daily visiting patients report");
                TempData["ErrorMessage"] = "Failed to load report data, please try again later.";
                return View(new DailyVisitingPatientsReportViewModel { ErrorMessage = "Failed to load report data, please try again later." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> RescheduleAppointment(int id)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            var appointment = await _appointmentService.GetByIdWithDetailsAsync(id);

            if (appointment == null || !doctorId.HasValue || appointment.DoctorId != doctorId.Value)
            {
                TempData["ErrorMessage"] = "You can only reschedule your own appointments.";
                return RedirectToAction(nameof(ViewAppointments));
            }

            if (appointment.Status != AppointmentStatus.Scheduled)
            {
                TempData["ErrorMessage"] = "Only scheduled appointments can be rescheduled.";
                return RedirectToAction(nameof(ViewAppointments));
            }

            var viewModel = new RescheduleAppointmentViewModel
            {
                AppointmentId = appointment.Id,
                PatientName = appointment.Patient?.User?.FullName ?? string.Empty,
                DoctorName = appointment.Doctor?.User?.FullName ?? string.Empty,
                CurrentAppointmentDateTime = appointment.AppointmentDateTime,
                NewAppointmentDateTime = appointment.AppointmentDateTime.AddDays(1)
            };

            return RescheduleView(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleAppointment(RescheduleAppointmentViewModel model)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            var appointment = await _appointmentService.GetByIdWithDetailsAsync(model.AppointmentId);

            if (appointment == null || !doctorId.HasValue || appointment.DoctorId != doctorId.Value)
            {
                TempData["ErrorMessage"] = "You can only reschedule your own appointments.";
                return RedirectToAction(nameof(ViewAppointments));
            }

            var previousDateTime = appointment.AppointmentDateTime;
            if (ModelState.IsValid)
            {
                var result = await _appointmentService.RescheduleAsync(model.AppointmentId, model.NewAppointmentDateTime);

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
                    return RedirectToAction(nameof(ViewAppointments));
                }

                ModelState.AddModelError("", result.Message);
            }

            return RescheduleView(model);
        }

        // The reschedule form is shared with the admin area; the back link points to the doctor's list.
        private ViewResult RescheduleView(RescheduleAppointmentViewModel model)
        {
            ViewData["BackAction"] = nameof(ViewAppointments);
            return View("~/Modules/Appointments/Views/Admin/RescheduleAppointment.cshtml", model);
        }
    }
}
