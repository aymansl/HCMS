using HCMS4.Data;
using HCMS4.Models;
using HCMS4.Models.Common;
using HCMS4.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HCMS4.Controllers
{
    [Authorize(Roles = "Patient")]
    public class PatientController : BaseController
    {
        private readonly IAppointmentService _appointmentService;
        private readonly ILeaveService _leaveService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;
        private readonly IWebHostEnvironment _environment;

        public PatientController(
            ApplicationDbContext context,
            ILogger<PatientController> logger,
            IAppointmentService appointmentService,
            ILeaveService leaveService,
            INotificationService notificationService,
            IActivityLogService activityLogService,
            IWebHostEnvironment environment)
            : base(context, logger)
        {
            _appointmentService = appointmentService;
            _leaveService = leaveService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
            _environment = environment;
        }

        public async Task<IActionResult> Dashboard()
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var upcomingAppointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Specialization)
                .Where(a => a.PatientId == patientId.Value &&
                            a.Status == AppointmentStatus.Scheduled &&
                            a.AppointmentDateTime >= DateTime.UtcNow)
                .OrderBy(a => a.AppointmentDateTime)
                .Take(5)
                .ToListAsync();

            var activeSurveyCount = await _context.SurveyAssignments
                .Include(sa => sa.Survey)
                .CountAsync(sa => sa.PatientId == patientId.Value &&
                                  sa.Status == SurveyAssignmentStatus.Pending &&
                                  sa.Survey.Status == SurveyStatus.Active &&
                                  sa.Survey.StartDate <= DateTime.UtcNow.Date &&
                                  (!sa.Survey.EndDate.HasValue || sa.Survey.EndDate.Value >= DateTime.UtcNow.Date));

            var openComplaintCount = await _context.Complaints
                .CountAsync(c => c.PatientId == patientId.Value &&
                                 c.Status != ComplaintStatus.Resolved &&
                                 c.Status != ComplaintStatus.Closed);

            var completedVisitsCount = await _context.Appointments
                .CountAsync(a => a.PatientId == patientId.Value && a.Status == AppointmentStatus.Completed);

            List<UserNotification> notifications;
            try
            {
                notifications = await _context.UserNotifications
                    .Where(n => n.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier))
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .ToListAsync();
            }
            catch
            {
                notifications = new List<UserNotification>();
            }

            var articles = await _context.MedicalArticles
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Where(a => a.Status == ArticleStatus.Published)
                .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
                .Take(6)
                .ToListAsync();

            var viewModel = new PatientDashboardViewModel
            {
                UpcomingAppointmentsCount = upcomingAppointments.Count,
                CompletedVisitsCount = completedVisitsCount,
                ActiveSurveyCount = activeSurveyCount,
                OpenComplaintCount = openComplaintCount,
                RecentNotifications = notifications,
                PublishedArticles = articles,
                UpcomingAppointments = upcomingAppointments.Select(a => new AppointmentHistoryViewModel
                {
                    Id = a.Id,
                    AppointmentDateTime = a.AppointmentDateTime,
                    DoctorName = a.Doctor?.User?.FullName ?? "Unknown Doctor",
                    Specialization = a.Doctor?.Specialization?.Name ?? "General",
                    Status = a.Status.ToString(),
                    ConsultationFee = a.ConsultationFee,
                    CreatedAt = a.CreatedAt
                }).ToList()
            };

            return View(viewModel);
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
                    return RedirectToAction(nameof(Dashboard));
                }

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
                    AppointmentDateTime = DateTime.Now.AddDays(1).Date.AddHours(9),
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading book appointment page for patient");
                TempData["ErrorMessage"] = "An error occurred while loading the appointment booking page.";
                return RedirectToAction(nameof(Dashboard));
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
                        return RedirectToAction(nameof(Dashboard));
                    }

                    var patient = await _context.Patients
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == patientId.Value);

                    if (patient == null || patient.Id != model.PatientId)
                    {
                        TempData["ErrorMessage"] = "You can only book appointments for yourself.";
                        return RedirectToAction(nameof(Dashboard));
                    }

                    if (model.AppointmentDateTime <= DateTime.UtcNow)
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
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
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
            var doctor = await _context.Doctors
                .Include(d => d.Specialization)
                .FirstOrDefaultAsync(d => d.Id == doctorId);
            var fee = doctor?.Specialization?.ConsultationFee ?? 0;
            return Ok(fee);
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

        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> InvoiceHistory()
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var invoices = await _context.Invoices
                    .Where(i => i.PatientId == patientId.Value)
                    .Include(i => i.Appointment)
                        .ThenInclude(a => a != null ? a.Doctor : null)
                            .ThenInclude(d => d != null ? d.User : null)
                    .Include(i => i.Prescription)
                        .ThenInclude(p => p != null ? p.Doctor : null)
                            .ThenInclude(d => d != null ? d.User : null)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                var viewModel = invoices.Select(i => new InvoiceViewModel
                {
                    Id = i.Id,
                    InvoiceDate = i.InvoiceDate,
                    ConsultationFee = i.ConsultationFee,
                    MedicationTotal = i.MedicationTotal,
                    TotalAmount = i.TotalAmount,
                    PaymentStatus = i.PaymentStatus.ToString(),
                    AmountPaid = i.AmountPaid,
                    PaymentDate = i.PaymentDate,
                    Notes = i.Notes,
                    CreatedAt = i.UpdatedAt,
                    DoctorName = i.Appointment?.Doctor?.User?.FullName ??
                                i.Prescription?.Doctor?.User?.FullName ?? "N/A",
                    AppointmentDate = i.Appointment?.AppointmentDateTime
                }).ToList();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice history for user");
                TempData["ErrorMessage"] = "An error occurred while loading your invoice history.";
                return RedirectToAction("Dashboard", "Patient");
            }
        }

        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> InvoiceDetails(int id)
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var invoice = await _context.Invoices
                    .Include(i => i.Patient)
                        .ThenInclude(p => p.User)
                    .Include(i => i.Appointment)
                        .ThenInclude(a => a != null ? a.Doctor : null)
                            .ThenInclude(d => d != null ? d.User : null)
                    .Include(i => i.Prescription)
                        .ThenInclude(p => p != null ? p.PrescriptionItems : null)
                            .ThenInclude(pi => pi != null ? pi.Drug : null)
                    .FirstOrDefaultAsync(i => i.Id == id && i.PatientId == patientId.Value);

                if (invoice == null)
                {
                    TempData["ErrorMessage"] = "Invoice not found or you don't have permission to view it.";
                    return RedirectToAction(nameof(InvoiceHistory));
                }

                var viewModel = new InvoiceViewModel
                {
                    Id = invoice.Id,
                    InvoiceDate = invoice.InvoiceDate,
                    ConsultationFee = invoice.ConsultationFee,
                    MedicationTotal = invoice.MedicationTotal,
                    TotalAmount = invoice.TotalAmount,
                    PaymentStatus = invoice.PaymentStatus.ToString(),
                    AmountPaid = invoice.AmountPaid,
                    PaymentDate = invoice.PaymentDate,
                    Notes = invoice.Notes,
                    CreatedAt = invoice.UpdatedAt,
                    DoctorName = invoice.Appointment?.Doctor?.User?.FullName ??
                                invoice.Prescription?.Doctor?.User?.FullName ?? "N/A",
                    AppointmentDate = invoice.Appointment?.AppointmentDateTime
                };

                ViewBag.Invoice = invoice;
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice details {InvoiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading invoice details.";
                return RedirectToAction(nameof(InvoiceHistory));
            }
        }

        [HttpPost]
        [Authorize(Roles = "Patient")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int invoiceId, decimal amount, string paymentMethod, string? notes)
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var invoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.Id == invoiceId && i.PatientId == patientId.Value);

                if (invoice == null)
                {
                    TempData["ErrorMessage"] = "Invoice not found.";
                    return RedirectToAction(nameof(InvoiceHistory));
                }

                if (amount <= 0 || amount > invoice.TotalAmount)
                {
                    TempData["ErrorMessage"] = "Invalid payment amount.";
                    return RedirectToAction(nameof(InvoiceHistory));
                }

                invoice.AmountPaid = (invoice.AmountPaid ?? 0) + amount;
                invoice.PaymentDate = DateTime.Now;
                invoice.UpdatedAt = DateTime.Now;

                if (invoice.AmountPaid >= invoice.TotalAmount)
                    invoice.PaymentStatus = PaymentStatus.Paid;
                else if (invoice.AmountPaid > 0 && invoice.AmountPaid < invoice.TotalAmount)
                    invoice.PaymentStatus = PaymentStatus.Pending;

                if (!string.IsNullOrEmpty(notes))
                {
                    var paymentNote = $"Payment of ${amount:F2} via {paymentMethod} on {DateTime.Now:yyyy-MM-dd}. {notes}";
                    invoice.Notes = string.IsNullOrEmpty(invoice.Notes)
                        ? paymentNote
                        : $"{invoice.Notes}\n{paymentNote}";
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Payment of ${amount:F2} processed successfully for Invoice #{invoice.Id}.";
                return RedirectToAction(nameof(InvoiceHistory));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment for invoice {InvoiceId}", invoiceId);
                TempData["ErrorMessage"] = "An error occurred while processing your payment.";
                return RedirectToAction(nameof(InvoiceHistory));
            }
        }

        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> Prescriptions()
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var prescriptions = await _context.Prescriptions
                    .Where(p => p.PatientId == patientId.Value)
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.Specialization)
                    .Include(p => p.PrescriptionItems)
                        .ThenInclude(pi => pi.Drug)
                    .Include(p => p.Appointment)
                    .OrderByDescending(p => p.PrescriptionDate)
                    .ToListAsync();

                var reissueRequests = await _context.PrescriptionReissueRequests
                    .Where(r => r.PatientId == patientId.Value)
                    .GroupBy(r => r.PrescriptionId)
                    .Select(g => g.OrderByDescending(r => r.RequestDate).First())
                    .ToDictionaryAsync(r => r.PrescriptionId);

                var prescriptionViewModels = prescriptions.Select(prescription =>
                {
                    var drugNames = prescription.PrescriptionItems
                        .Select(pi => pi.Drug?.Name ?? pi.DrugName)
                        .Distinct()
                        .ToList();

                    var dosages = prescription.PrescriptionItems
                        .Select(pi => pi.Dosage)
                        .Distinct()
                        .ToList();

                    var durations = prescription.PrescriptionItems
                        .Select(pi => pi.Duration)
                        .Distinct()
                        .ToList();

                    var expiresAt = prescription.PrescriptionDate.AddDays(BusinessRules.PrescriptionReissueValidityDays);
                    var latestRequest = reissueRequests.TryGetValue(prescription.Id, out var request)
                        ? request
                        : null;

                    return new PatientPrescriptionsViewModel
                    {
                        Id = prescription.Id,
                        PrescriptionDate = prescription.PrescriptionDate,
                        DoctorName = prescription.Doctor?.User?.FullName ?? "UNKNOWN",
                        Specialization = prescription.Doctor?.Specialization?.Name ?? "UNDEFINED",
                        DrugName = drugNames.Any() ? string.Join(", ", drugNames.Take(2)) +
                            (drugNames.Count > 2 ? $" and{drugNames.Count - 2} more" : "") : "NO DRUGS",
                        Dosage = dosages.Any() ? string.Join("، ", dosages) : "-",
                        Duration = durations.Any() ? string.Join("، ", durations) : "-",
                        Status = prescription.Status.ToString(),
                        TotalCost = prescription.TotalCost,
                        Notes = prescription.Notes,
                        DispensedDate = prescription.DispensedDate,
                        DispensedBy = prescription.DispensedBy,
                        PrescriptionExpiryDate = expiresAt,
                        CanRequestReissue = IsPrescriptionEligibleForReissue(prescription) &&
                                            latestRequest?.Status != ReissueRequestStatus.Pending,
                        HasPendingReissueRequest = latestRequest?.Status == ReissueRequestStatus.Pending,
                        LatestReissueStatus = latestRequest?.Status.ToString()
                    };
                }).ToList();

                var listViewModel = new PatientPrescriptionsListViewModel
                {
                    Prescriptions = prescriptionViewModels
                };

                return View(listViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prescriptions for patient");
                TempData["ErrorMessage"] = "Error loading prescriptions.";
                return RedirectToAction("Dashboard", "Patient");
            }
        }

        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> PrescriptionDetails(int id)
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var prescription = await _context.Prescriptions
                    .Include(p => p.Patient)
                        .ThenInclude(pt => pt.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.Specialization)
                    .Include(p => p.PrescriptionItems)
                        .ThenInclude(pi => pi.Drug)
                    .Include(p => p.Appointment)
                    .FirstOrDefaultAsync(p => p.Id == id && p.PatientId == patientId.Value);

                if (prescription == null)
                {
                    TempData["ErrorMessage"] = "No prescription found.";
                    return RedirectToAction(nameof(Prescriptions));
                }

                var latestReissueRequest = await _context.PrescriptionReissueRequests
                    .Where(r => r.PatientId == patientId.Value && r.PrescriptionId == prescription.Id)
                    .OrderByDescending(r => r.RequestDate)
                    .FirstOrDefaultAsync();

                var pharmacistNotes = await _context.PharmacistPrescriptionNotes
                    .Include(n => n.Pharmacist)
                        .ThenInclude(p => p.User)
                    .Where(n => n.PrescriptionId == prescription.Id)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                var expiresAt = prescription.PrescriptionDate.AddDays(BusinessRules.PrescriptionReissueValidityDays);

                var detailedViewModel = new PatientPrescriptionDetailViewModel
                {
                    Id = prescription.Id,
                    PrescriptionDate = prescription.PrescriptionDate,
                    PatientName = prescription.Patient?.User?.FullName ?? "UNKNOWN",
                    DoctorName = prescription.Doctor?.User?.FullName ?? "UNKNOWN",
                    Specialization = prescription.Doctor?.Specialization?.Name ?? "UNDEFINED",
                    Status = prescription.Status.ToString(),
                    Notes = prescription.Notes,
                    TotalCost = prescription.TotalCost,
                    DispensedDate = prescription.DispensedDate,
                    DispensedBy = prescription.DispensedBy,
                    CreatedAt = prescription.CreatedAt,
                    AppointmentDate = prescription.Appointment?.AppointmentDateTime,
                    PrescriptionItems = prescription.PrescriptionItems.Select(pi => new PrescriptionItemDetailViewModel
                    {
                        DrugName = pi.Drug?.Name ?? pi.DrugName,
                        Dosage = pi.Dosage,
                        Duration = pi.Duration,
                        Frequency = pi.Frequency,
                        Quantity = pi.Quantity,
                        Instructions = pi.Instructions,
                        Price = pi.Drug?.Price ?? 0,
                        Subtotal = pi.Quantity * (pi.Drug?.Price ?? 0)
                    }).ToList(),
                    PharmacistNotes = pharmacistNotes.Select(note => new PrescriptionNoteDisplayViewModel
                    {
                        PharmacistName = note.Pharmacist?.User?.FullName ?? "Pharmacist",
                        NoteText = note.NoteText,
                        NotifyDoctor = note.NotifyDoctor,
                        CreatedAt = note.CreatedAt
                    }).ToList(),
                    PrescriptionExpiryDate = expiresAt,
                    CanRequestReissue = IsPrescriptionEligibleForReissue(prescription) &&
                                        latestReissueRequest?.Status != ReissueRequestStatus.Pending,
                    HasPendingReissueRequest = latestReissueRequest?.Status == ReissueRequestStatus.Pending,
                    LatestReissueStatus = latestReissueRequest?.Status.ToString()
                };

                return View(detailedViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prescription {PrescriptionId}", id);
                TempData["ErrorMessage"] = "Error loading prescription.";
                return RedirectToAction(nameof(Prescriptions));
            }
        }

        [HttpGet]
        public async Task<IActionResult> RateVisit(int appointmentId)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId.Value);

            if (appointment == null || appointment.Status != AppointmentStatus.Completed)
            {
                TempData["ErrorMessage"] = "Only completed visits can be rated.";
                return RedirectToAction(nameof(ViewAppointmentHistory));
            }

            var existingRating = await _context.VisitRatings
                .AnyAsync(vr => vr.PatientId == patientId.Value && vr.AppointmentId == appointmentId);

            if (existingRating)
            {
                TempData["ErrorMessage"] = "You have already rated this visit.";
                return RedirectToAction(nameof(ViewAppointmentHistory));
            }

            return View(new VisitRatingFormViewModel
            {
                AppointmentId = appointment.Id,
                DoctorId = appointment.DoctorId,
                DoctorName = appointment.Doctor?.User?.FullName ?? "Doctor",
                AppointmentDateTime = appointment.AppointmentDateTime
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateVisit(VisitRatingFormViewModel model)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == model.AppointmentId && a.PatientId == patientId.Value);

            if (appointment == null || appointment.Status != AppointmentStatus.Completed)
            {
                TempData["ErrorMessage"] = "Only completed visits can be rated.";
                return RedirectToAction(nameof(ViewAppointmentHistory));
            }

            if (await _context.VisitRatings.AnyAsync(vr => vr.PatientId == patientId.Value && vr.AppointmentId == model.AppointmentId))
            {
                TempData["ErrorMessage"] = "You have already rated this visit.";
                return RedirectToAction(nameof(ViewAppointmentHistory));
            }

            if (!ModelState.IsValid)
            {
                model.DoctorName = appointment.Doctor?.FullName ?? model.DoctorName;
                model.DoctorId = appointment.DoctorId;
                model.AppointmentDateTime = appointment.AppointmentDateTime;
                return View(model);
            }

            try
            {
                var rating = new VisitRating
                {
                    AppointmentId = appointment.Id,
                    PatientId = patientId.Value,
                    DoctorId = appointment.DoctorId,
                    Rating = model.Rating,
                    Comment = model.Comment,
                    DoctorCooperative = model.DoctorCooperative,
                    WaitingTimeReasonable = model.WaitingTimeReasonable,
                    CreatedAt = DateTime.UtcNow
                };

                _context.VisitRatings.Add(rating);
                await _context.SaveChangesAsync();

                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == appointment.DoctorId);
                if (doctor != null)
                {
                    doctor.RatingCount = await _context.VisitRatings.CountAsync(vr => vr.DoctorId == doctor.Id);
                    doctor.AverageRating = Math.Round((decimal)await _context.VisitRatings
                        .Where(vr => vr.DoctorId == doctor.Id)
                        .AverageAsync(vr => vr.Rating), 2);
                    await _context.SaveChangesAsync();
                }

                var currentUser = await GetCurrentUserAsync();
                await _activityLogService.LogAsync(
                    "VisitRated",
                    nameof(VisitRating),
                    $"Patient rated appointment #{appointment.Id} with {model.Rating} star(s).",
                    rating.Id.ToString(),
                    currentUser?.Id,
                    currentUser?.UserName);

                TempData["SuccessMessage"] = "Thank you for your rating, your feedback has been submitted successfully.";
                return RedirectToAction(nameof(ViewAppointmentHistory));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving visit rating for appointment {AppointmentId}", model.AppointmentId);
                ModelState.AddModelError(string.Empty, "Failed to save rating, please try again later.");
                model.DoctorName = appointment.Doctor?.FullName ?? model.DoctorName;
                model.DoctorId = appointment.DoctorId;
                model.AppointmentDateTime = appointment.AppointmentDateTime;
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPrescriptionReissue(int prescriptionId)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var prescription = await _context.Prescriptions
                .Include(p => p.Doctor)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId && p.PatientId == patientId.Value);

            if (prescription == null)
            {
                TempData["ErrorMessage"] = "Prescription not found.";
                return RedirectToAction(nameof(Prescriptions));
            }

            if (!IsPrescriptionEligibleForReissue(prescription))
            {
                TempData["ErrorMessage"] = "This prescription has expired and cannot be re-issued.";
                return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
            }

            var hasPendingRequest = await _context.PrescriptionReissueRequests
                .AnyAsync(r => r.PrescriptionId == prescriptionId &&
                               r.PatientId == patientId.Value &&
                               r.Status == ReissueRequestStatus.Pending);

            if (hasPendingRequest)
            {
                TempData["ErrorMessage"] = "A re-issuance request is already pending for this prescription.";
                return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
            }

            var request = new PrescriptionReissueRequest
            {
                PatientId = patientId.Value,
                PrescriptionId = prescription.Id,
                DoctorId = prescription.DoctorId,
                RequestDate = DateTime.UtcNow,
                Status = ReissueRequestStatus.Pending
            };

            _context.PrescriptionReissueRequests.Add(request);
            await _context.SaveChangesAsync();

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == prescription.DoctorId);
            if (doctor != null)
            {
                await _notificationService.CreateForUserAsync(
                    doctor.UserId,
                    "Prescription re-issuance request",
                    $"A patient requested re-issuance for prescription #{prescription.Id}.",
                    NotificationType.PrescriptionReissue,
                    "/Doctor/ReissueRequests",
                    nameof(PrescriptionReissueRequest),
                    request.Id);
            }

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "PrescriptionReissueRequested",
                nameof(PrescriptionReissueRequest),
                $"Patient requested re-issuance for prescription #{prescription.Id}.",
                request.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Re-issuance request has been sent to the doctor.";
            return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
        }

        [HttpGet]
        public async Task<IActionResult> Complaints()
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var complaints = await _context.Complaints
                .Where(c => c.PatientId == patientId.Value)
                .OrderByDescending(c => c.SubmissionDate)
                .ToListAsync();

            return View(complaints);
        }

        [HttpGet]
        public IActionResult CreateComplaint()
        {
            return View(new PatientComplaintFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateComplaint(PatientComplaintFormViewModel model)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string? attachmentPath = null;
            if (model.Attachment != null)
            {
                var extension = Path.GetExtension(model.Attachment.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx" };

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(nameof(model.Attachment), "File type not supported.");
                    return View(model);
                }

                if (model.Attachment.Length > BusinessRules.ComplaintAttachmentMaxBytes)
                {
                    ModelState.AddModelError(nameof(model.Attachment), "File size exceeds the allowed limit.");
                    return View(model);
                }

                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "complaints");
                Directory.CreateDirectory(uploadsPath);

                var storedFileName = $"{Guid.NewGuid():N}{extension}";
                var fullPath = Path.Combine(uploadsPath, storedFileName);
                await using var stream = System.IO.File.Create(fullPath);
                await model.Attachment.CopyToAsync(stream);
                attachmentPath = $"/uploads/complaints/{storedFileName}";
            }

            var complaint = new Complaint
            {
                PatientId = patientId.Value,
                Title = model.Title.Trim(),
                Type = model.Type,
                Description = model.Description.Trim(),
                AssociatedVisitDate = model.AssociatedVisitDate,
                AttachmentPath = attachmentPath,
                TrackingNumber = $"CMP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                SubmissionDate = DateTime.UtcNow,
                Status = ComplaintStatus.Submitted
            };

            _context.Complaints.Add(complaint);
            await _context.SaveChangesAsync();

            await _notificationService.CreateForRoleAsync(
                "Admin",
                "New patient complaint",
                $"A new complaint ({complaint.TrackingNumber}) requires review.",
                NotificationType.Complaint,
                "/Admin/Complaints",
                nameof(Complaint),
                complaint.Id);

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "ComplaintSubmitted",
                nameof(Complaint),
                $"Patient submitted complaint {complaint.TrackingNumber}.",
                complaint.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = $"Complaint submitted successfully. Tracking number: {complaint.TrackingNumber}";
            return RedirectToAction(nameof(Complaints));
        }

        [HttpGet]
        public async Task<IActionResult> Surveys()
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var assignments = await _context.SurveyAssignments
                .Include(sa => sa.Survey)
                .Where(sa => sa.PatientId == patientId.Value)
                .OrderByDescending(sa => sa.AssignedAt)
                .ToListAsync();

            return View(assignments);
        }

        [HttpGet]
        public async Task<IActionResult> TakeSurvey(int assignmentId)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await BuildSurveyResponseViewModelAsync(assignmentId, patientId.Value);
            if (viewModel == null)
            {
                TempData["ErrorMessage"] = "Survey not found or is no longer available.";
                return RedirectToAction(nameof(Surveys));
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TakeSurvey(SurveyResponseViewModel model)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var assignment = await _context.SurveyAssignments
                .Include(sa => sa.Survey)
                .FirstOrDefaultAsync(sa => sa.Id == model.SurveyAssignmentId &&
                                           sa.PatientId == patientId.Value);

            if (assignment == null || assignment.Status == SurveyAssignmentStatus.Completed)
            {
                TempData["ErrorMessage"] = "Survey not found or has already been completed.";
                return RedirectToAction(nameof(Surveys));
            }

            foreach (var question in model.Questions)
            {
                if (question.IsRequired)
                {
                    var hasAnswer = question.QuestionType switch
                    {
                        SurveyQuestionType.MultipleChoice => !string.IsNullOrWhiteSpace(question.AnswerText),
                        SurveyQuestionType.Rating => question.NumericValue.HasValue,
                        SurveyQuestionType.YesNo => question.BooleanValue.HasValue,
                        _ => !string.IsNullOrWhiteSpace(question.AnswerText)
                    };

                    if (!hasAnswer)
                    {
                        ModelState.AddModelError(string.Empty, $"Please answer question before proceeding: {question.QuestionText}");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                var refreshedModel = await BuildSurveyResponseViewModelAsync(model.SurveyAssignmentId, patientId.Value, model);
                return View(refreshedModel ?? model);
            }

            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM SurveyAnswers WHERE SurveyAssignmentId = {assignment.Id}");

            foreach (var question in model.Questions)
            {
                _context.SurveyAnswers.Add(new SurveyAnswer
                {
                    SurveyAssignmentId = assignment.Id,
                    SurveyQuestionId = question.SurveyQuestionId,
                    AnswerText = question.AnswerText,
                    NumericValue = question.NumericValue,
                    BooleanValue = question.BooleanValue
                });
            }

            assignment.Status = SurveyAssignmentStatus.Completed;
            assignment.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "SurveyCompleted",
                nameof(Survey),
                $"Patient completed survey #{assignment.SurveyId}.",
                assignment.SurveyId.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Thank you for participating! Your answers have been recorded successfully.";
            return RedirectToAction(nameof(Dashboard));
        }

        private bool IsPrescriptionEligibleForReissue(Prescription prescription)
        {
            return prescription.Status == PrescriptionStatus.Completed &&
                   prescription.PrescriptionDate.AddDays(BusinessRules.PrescriptionReissueValidityDays) >= DateTime.UtcNow;
        }

        private async Task<SurveyResponseViewModel?> BuildSurveyResponseViewModelAsync(int assignmentId, int patientId,
            SurveyResponseViewModel? submittedModel = null)
        {
            var assignment = await _context.SurveyAssignments
                .Include(sa => sa.Survey)
                .FirstOrDefaultAsync(sa => sa.Id == assignmentId &&
                                           sa.PatientId == patientId &&
                                           sa.Status == SurveyAssignmentStatus.Pending);

            if (assignment == null ||
                assignment.Survey.Status != SurveyStatus.Active ||
                assignment.Survey.StartDate > DateTime.UtcNow.Date ||
                (assignment.Survey.EndDate.HasValue && assignment.Survey.EndDate.Value < DateTime.UtcNow.Date))
            {
                return null;
            }

            var questions = await _context.SurveyQuestions
                .Include(q => q.Options)
                .Where(q => q.SurveyId == assignment.SurveyId)
                .OrderBy(q => q.DisplayOrder)
                .ToListAsync();

            return new SurveyResponseViewModel
            {
                SurveyId = assignment.SurveyId,
                SurveyAssignmentId = assignment.Id,
                Title = assignment.Survey.Title,
                Description = assignment.Survey.Description,
                Questions = questions.Select(question =>
                {
                    var submittedQuestion = submittedModel?.Questions
                        .FirstOrDefault(q => q.SurveyQuestionId == question.Id);

                    return new SurveyResponseQuestionViewModel
                    {
                        SurveyQuestionId = question.Id,
                        QuestionText = question.QuestionText,
                        QuestionType = question.QuestionType,
                        IsRequired = question.IsRequired,
                        Options = question.Options.OrderBy(o => o.DisplayOrder).ToList(),
                        AnswerText = submittedQuestion?.AnswerText,
                        NumericValue = submittedQuestion?.NumericValue,
                        BooleanValue = submittedQuestion?.BooleanValue
                    };
                }).ToList()
            };
        }
    }
}
