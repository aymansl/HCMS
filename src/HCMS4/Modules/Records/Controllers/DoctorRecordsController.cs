using HCMS4.Controllers;
using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
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

namespace HCMS4.Modules.Records.Controllers
{
    // Part of the former DoctorController; [ControllerName] keeps the /Doctor/... URLs.
    [Authorize(Roles = "Doctor")]
    [ControllerName("Doctor")]
    public class DoctorRecordsController : BaseController
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IPrescriptionService _prescriptionService;
        private readonly ILeaveService _leaveService;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public DoctorRecordsController(
            ApplicationDbContext context,
            ILogger<DoctorRecordsController> logger,
            IAppointmentService appointmentService,
            IPrescriptionService prescriptionService,
            ILeaveService leaveService,
            INoShowRiskService noShowRiskService,
            INotificationService notificationService,
            IActivityLogService activityLogService)
            : base(context, logger)
        {
            _appointmentService = appointmentService;
            _prescriptionService = prescriptionService;
            _leaveService = leaveService;
            _noShowRiskService = noShowRiskService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
        }

        [HttpGet]
        public async Task<IActionResult> PatientRecord(int patientId)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                var hasAccess = await _context.Appointments
                    .AnyAsync(a => a.DoctorId == doctorId.Value && a.PatientId == patientId);

                if (!hasAccess)
                {
                    TempData["ErrorMessage"] = "You do not have access to this patient's record.";
                    return RedirectToAction("ViewAppointments");
                }

                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == patientId);

                if (patient == null)
                {
                    TempData["ErrorMessage"] = "Patient not found.";
                    return RedirectToAction("ViewAppointments");
                }

                var viewModel = new PatientMedicalRecordViewModel
                {
                    PatientId = patient.Id,
                    FullName = patient.User != null ? patient.User.FullName : "Unknown",
                    Email = patient.User != null ? patient.User.Email : "No email",
                    PhoneNumber = patient.User?.PhoneNumber,
                    DateOfBirth = patient.DateOfBirth,
                    Address = patient.Address,
                    EmergencyContact = patient.EmergencyContact,
                    ChronicConditions = patient.ChronicConditions
                };

                var appointments = await _context.Appointments
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.User)
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.Specialization)
                    .Include(a => a.Patient)
                    .Where(a => a.PatientId == patientId)
                    .OrderByDescending(a => a.AppointmentDateTime)
                    .ToListAsync();

                viewModel.AppointmentHistory = appointments.Select(a => new AppointmentHistoryViewModel
                {
                    Id = a.Id,
                    AppointmentDateTime = a.AppointmentDateTime,
                    DoctorName = a.Doctor?.User != null ? a.Doctor.User.FullName : "Unknown Doctor",
                    Specialization = a.Doctor?.Specialization?.Name ?? "General",
                    Status = a.Status.ToString(),
                    ConsultationFee = a.ConsultationFee,
                    CreatedAt = a.CreatedAt
                }).ToList();

                var prescriptions = await _context.Prescriptions
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.Specialization)
                    .Include(p => p.PrescriptionItems)
                        .ThenInclude(pi => pi.Drug)
                    .Where(p => p.PatientId == patientId)
                    .OrderByDescending(p => p.PrescriptionDate)
                    .ToListAsync();

                var prescriptionNotes = await _context.PharmacistPrescriptionNotes
                    .Include(n => n.Pharmacist)
                        .ThenInclude(p => p.User)
                    .Where(n => prescriptions.Select(p => p.Id).Contains(n.PrescriptionId))
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                viewModel.Prescriptions = prescriptions.Select(p => new PrescriptionViewModel
                {
                    Id = p.Id,
                    PrescriptionDate = p.PrescriptionDate,
                    DoctorName = p.Doctor?.User != null ? p.Doctor.User.FullName : "Unknown Doctor",
                    Specialization = p.Doctor?.Specialization?.Name ?? "General",
                    Status = p.Status.ToString(),
                    Notes = p.Notes,
                    TotalCost = p.TotalCost,
                    Items = p.PrescriptionItems.Select(pi => new PrescriptionItemViewModel
                    {
                        DrugName = pi.DrugName,
                        Dosage = pi.Dosage,
                        Frequency = pi.Frequency,
                        Duration = int.TryParse(pi.Duration, out int dur) ? dur : 0,
                        Instructions = pi.Instructions
                    }).ToList(),
                    PharmacistNotes = prescriptionNotes
                        .Where(note => note.PrescriptionId == p.Id)
                        .Select(note => new PrescriptionNoteDisplayViewModel
                        {
                            PharmacistName = note.Pharmacist?.User?.FullName ?? "Pharmacist",
                            NoteText = note.NoteText,
                            NotifyDoctor = note.NotifyDoctor,
                            CreatedAt = note.CreatedAt
                        }).ToList()
                }).ToList();

                var clinicalNotes = await _context.ClinicalNotes
                    .Include(cn => cn.Doctor)
                        .ThenInclude(d => d.User)
                    .Where(cn => cn.PatientId == patientId)
                    .OrderByDescending(cn => cn.Date)
                    .ToListAsync();

                viewModel.ClinicalNotes = clinicalNotes.Select(cn => new ClinicalNoteViewModel
                {
                    Id = cn.Id,
                    Date = cn.Date,
                    NoteType = cn.NoteType,
                    Content = cn.Content,
                    Diagnosis = cn.Diagnosis,
                    DoctorName = cn.Doctor?.User != null ? cn.Doctor.User.FullName : "Unknown Doctor"
                }).ToList();

                var invoices = await _context.Invoices
                    .Include(i => i.Appointment)
                    .Where(i => i.PatientId == patientId)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                viewModel.Invoices = invoices.Select(i => new InvoiceViewModel
                {
                    Id = i.Id,
                    InvoiceDate = i.InvoiceDate,
                    TotalAmount = i.TotalAmount,
                    PaymentStatus = i.PaymentStatus.ToString(),
                    PaymentDate = i.PaymentDate
                }).ToList();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading patient record for patient ID: {PatientId}", patientId);
                TempData["ErrorMessage"] = "Unable to load patient record. Please try again.";
                return RedirectToAction("ViewAppointments");
            }
        }

        [HttpGet]
        public async Task<IActionResult> CreateClinicalNote(int patientId)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                ViewBag.PatientId = patientId;
                var viewModel = new CreateClinicalNoteViewModel
                {
                    PatientId = patientId
                };
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create clinical note form");
                TempData["ErrorMessage"] = "Error loading form. Please try again.";
                return RedirectToAction("PatientRecord", new { patientId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClinicalNote(CreateClinicalNoteViewModel viewModel)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                if (ModelState.IsValid)
                {
                    var clinicalNote = new ClinicalNote
                    {
                        PatientId = viewModel.PatientId,
                        DoctorId = doctorId.Value,
                        NoteType = viewModel.NoteType,
                        Content = viewModel.Content,
                        Diagnosis = viewModel.Diagnosis,
                        Date = ClinicTime.Now
                    };

                    _context.ClinicalNotes.Add(clinicalNote);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Clinical note added successfully.";
                    return RedirectToAction("PatientRecord", new { patientId = viewModel.PatientId });
                }

                ViewBag.PatientId = viewModel.PatientId;
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating clinical note");
                TempData["ErrorMessage"] = "Error creating clinical note. Please try again.";
                return RedirectToAction("PatientRecord", new { patientId = viewModel.PatientId });
            }
        }
    }
}
