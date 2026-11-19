using HCMS4.Data;
using HCMS4.Modules.Records.Models;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Records.Services
{
    /// <summary>The doctor's view of a patient's medical record and clinical notes.</summary>
    public interface IMedicalRecordService
    {
        /// <summary>A doctor may open a record only for patients they have an appointment with.</summary>
        Task<bool> DoctorHasAccessAsync(int doctorId, int patientId);
        Task<PatientMedicalRecordViewModel?> GetMedicalRecordAsync(int patientId);
        /// <summary>Fails when the doctor has no appointment with the patient.</summary>
        Task<ServiceResult> AddClinicalNoteAsync(int doctorId, CreateClinicalNoteViewModel model);
    }

    public class MedicalRecordService : IMedicalRecordService
    {
        private readonly ApplicationDbContext _context;
        private readonly IClinicClock _clock;

        public MedicalRecordService(ApplicationDbContext context, IClinicClock clock)
        {
            _context = context;
            _clock = clock;
        }

        public Task<bool> DoctorHasAccessAsync(int doctorId, int patientId) =>
            _context.Appointments.AnyAsync(a => a.DoctorId == doctorId && a.PatientId == patientId);

        public async Task<PatientMedicalRecordViewModel?> GetMedicalRecordAsync(int patientId)
        {
            var patient = await _context.Patients.AsNoTracking()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == patientId);
            if (patient == null)
            {
                return null;
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

            var appointments = await _context.Appointments.AsNoTracking()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
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

            var prescriptions = await _context.Prescriptions.AsNoTracking()
                .Include(p => p.Doctor).ThenInclude(d => d.User)
                .Include(p => p.Doctor).ThenInclude(d => d.Specialization)
                .Include(p => p.PrescriptionItems).ThenInclude(pi => pi.Drug)
                .Where(p => p.PatientId == patientId)
                .OrderByDescending(p => p.PrescriptionDate)
                .ToListAsync();

            var prescriptionIds = prescriptions.Select(p => p.Id).ToList();
            var prescriptionNotes = await _context.PharmacistPrescriptionNotes.AsNoTracking()
                .Include(n => n.Pharmacist).ThenInclude(p => p.User)
                .Where(n => prescriptionIds.Contains(n.PrescriptionId))
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
                    Duration = int.TryParse(pi.Duration, out var duration) ? duration : 0,
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

            var clinicalNotes = await _context.ClinicalNotes.AsNoTracking()
                .Include(cn => cn.Doctor).ThenInclude(d => d.User)
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

            var invoices = await _context.Invoices.AsNoTracking()
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

            return viewModel;
        }

        public async Task<ServiceResult> AddClinicalNoteAsync(int doctorId, CreateClinicalNoteViewModel model)
        {
            // Same rule as viewing the record: a doctor writes notes only for their own patients.
            if (!await DoctorHasAccessAsync(doctorId, model.PatientId))
            {
                return ServiceResult.Fail("You do not have access to this patient's record.");
            }

            _context.ClinicalNotes.Add(new ClinicalNote
            {
                PatientId = model.PatientId,
                DoctorId = doctorId,
                NoteType = model.NoteType,
                Content = model.Content,
                Diagnosis = model.Diagnosis,
                Date = _clock.Now
            });
            await _context.SaveChangesAsync();
            return ServiceResult.Ok("Clinical note added successfully.");
        }
    }
}
