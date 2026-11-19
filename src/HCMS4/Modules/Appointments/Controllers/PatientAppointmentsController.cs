using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Feedback.Services;
using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Appointments.Controllers
{
    // Part of the former PatientController; [ControllerName] keeps the /Patient/... URLs.
    [Authorize(Roles = "Patient")]
    [ControllerName("Patient")]
    public class PatientAppointmentsController : HcmsControllerBase
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IPatientService _patientService;
        private readonly IStaffService _staffService;
        private readonly IVisitRatingService _visitRatingService;
        private readonly IClinicClock _clock;
        private readonly ILogger<PatientAppointmentsController> _logger;

        public PatientAppointmentsController(
            IAppointmentService appointmentService,
            IPatientService patientService,
            IStaffService staffService,
            IVisitRatingService visitRatingService,
            IClinicClock clock,
            ILogger<PatientAppointmentsController> logger)
        {
            _appointmentService = appointmentService;
            _patientService = patientService;
            _staffService = staffService;
            _visitRatingService = visitRatingService;
            _clock = clock;
            _logger = logger;
        }

        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> BookAppointment()
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var patient = await _patientService.GetByIdAsync(patientId.Value);

                if (patient == null)
                {
                    TempData["ErrorMessage"] = "Patient record not found.";
                    return RedirectToAction("Dashboard");
                }

                var viewModel = new BookAppointmentViewModel
                {
                    PatientId = patient.Id,
                    AvailableDoctors = BookingSelectLists.ToDoctorOptions(await _staffService.GetBookableDoctorsAsync(_clock.Today)),
                    AvailablePatients = BookingSelectLists.ToPatientOptions(new[] { patient }),
                    AppointmentDateTime = _clock.Now.AddDays(1).Date.AddHours(9),
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

                    if (patientId.Value != model.PatientId)
                    {
                        TempData["ErrorMessage"] = "You can only book appointments for yourself.";
                        return RedirectToAction("Dashboard");
                    }

                    if (model.AppointmentDateTime <= _clock.Now)
                    {
                        ModelState.AddModelError("AppointmentDateTime", "Appointment must be in the future.");
                        return await ReloadViewModel(model, patientId.Value);
                    }

                    var doctor = await _staffService.GetDoctorProfileAsync(model.DoctorId);
                    if (doctor == null || !doctor.IsAvailable)
                    {
                        ModelState.AddModelError("DoctorId", "Selected doctor is not available.");
                        return await ReloadViewModel(model, patientId.Value);
                    }

                    // Delegate conflict detection to the service (DRY - no duplication)
                    var appointment = new Appointment
                    {
                        PatientId = model.PatientId,
                        DoctorId = model.DoctorId,
                        AppointmentDateTime = model.AppointmentDateTime,
                        Status = AppointmentStatus.Scheduled,
                        ConsultationFee = doctor?.Specialization?.ConsultationFee ?? 0,
                        CreatedAt = _clock.Now,
                        UpdatedAt = _clock.Now
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
            model.AvailableDoctors = BookingSelectLists.ToDoctorOptions(await _staffService.GetBookableDoctorsAsync(_clock.Today));

            var patient = await _patientService.GetByIdAsync(patientId);
            if (patient != null)
            {
                model.AvailablePatients = BookingSelectLists.ToPatientOptions(new[] { patient });
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

                var appointments = await _appointmentService.GetByPatientIdAsync(patientId.Value);
                var visitRatings = await _visitRatingService.GetRatingIdsByAppointmentAsync(patientId.Value);

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
                    VisitRatingId = visitRatings.TryGetValue(a.Id, out var ratingId) ? ratingId : null,
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
