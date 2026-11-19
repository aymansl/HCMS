using HCMS4.Data;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Pharmacy.Services
{
    /// <summary>A request the doctor answered, and the user to notify about it.</summary>
    public record RequestAnswered(int PrescriptionId, string NotifyUserId);

    /// <summary>
    /// The patient's prescriptions and re-issuance requests, and the doctor's side of
    /// re-issuance requests and pharmacist review requests.
    /// </summary>
    public interface IPrescriptionRequestService
    {
        Task<List<PatientPrescriptionsViewModel>> GetPatientPrescriptionsAsync(int patientId);
        Task<PatientPrescriptionDetailViewModel?> GetPatientPrescriptionDetailAsync(int patientId, int prescriptionId);
        /// <summary>Only completed prescriptions within the validity window, and one pending request at a time.</summary>
        Task<ServiceResult<RecordCreated>> RequestReissueAsync(int patientId, int prescriptionId);

        Task<List<PrescriptionReissueRequest>> GetReissueRequestsForDoctorAsync(int doctorId);
        /// <summary>The result names the patient's user to notify.</summary>
        Task<ServiceResult<RequestAnswered>> ReviewReissueRequestAsync(int doctorId, int requestId, bool approve, string? doctorResponse);

        Task<List<DoctorReviewRequest>> GetReviewRequestsForDoctorAsync(int doctorId);
        /// <summary>The result names the pharmacist's user to notify.</summary>
        Task<ServiceResult<RequestAnswered>> RespondToReviewRequestAsync(int doctorId, int requestId, string doctorResponse);

        bool IsEligibleForReissue(Prescription prescription);
    }

    public class PrescriptionRequestService : IPrescriptionRequestService
    {
        private readonly ApplicationDbContext _context;
        private readonly IClinicClock _clock;

        public PrescriptionRequestService(ApplicationDbContext context, IClinicClock clock)
        {
            _context = context;
            _clock = clock;
        }

        public bool IsEligibleForReissue(Prescription prescription) =>
            prescription.Status == PrescriptionStatus.Completed &&
            prescription.PrescriptionDate.AddDays(BusinessRules.PrescriptionReissueValidityDays) >= _clock.Now;

        public async Task<List<PatientPrescriptionsViewModel>> GetPatientPrescriptionsAsync(int patientId)
        {
            var prescriptions = await _context.Prescriptions.AsNoTracking()
                .Where(p => p.PatientId == patientId)
                .Include(p => p.Doctor)
                    .ThenInclude(d => d.User)
                .Include(p => p.Doctor)
                    .ThenInclude(d => d.Specialization)
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Drug)
                .Include(p => p.Appointment)
                .OrderByDescending(p => p.PrescriptionDate)
                .ToListAsync();

            var latestRequests = (await _context.PrescriptionReissueRequests.AsNoTracking()
                    .Where(r => r.PatientId == patientId)
                    .ToListAsync())
                .GroupBy(r => r.PrescriptionId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.RequestDate).First());

            return prescriptions.Select(prescription =>
            {
                var drugNames = prescription.PrescriptionItems.Select(pi => pi.Drug?.Name ?? pi.DrugName).Distinct().ToList();
                var dosages = prescription.PrescriptionItems.Select(pi => pi.Dosage).Distinct().ToList();
                var durations = prescription.PrescriptionItems.Select(pi => pi.Duration).Distinct().ToList();
                var latestRequest = latestRequests.GetValueOrDefault(prescription.Id);

                return new PatientPrescriptionsViewModel
                {
                    Id = prescription.Id,
                    PrescriptionDate = prescription.PrescriptionDate,
                    DoctorName = prescription.Doctor?.User?.FullName ?? "UNKNOWN",
                    Specialization = prescription.Doctor?.Specialization?.Name ?? "UNDEFINED",
                    DrugName = drugNames.Count > 0 ? string.Join(", ", drugNames.Take(2)) +
                        (drugNames.Count > 2 ? $" and{drugNames.Count - 2} more" : "") : "NO DRUGS",
                    Dosage = dosages.Count > 0 ? string.Join(", ", dosages) : "-",
                    Duration = durations.Count > 0 ? string.Join(", ", durations) : "-",
                    Status = prescription.Status.ToString(),
                    TotalCost = prescription.TotalCost,
                    Notes = prescription.Notes,
                    DispensedDate = prescription.DispensedDate,
                    DispensedBy = prescription.DispensedBy,
                    PrescriptionExpiryDate = prescription.PrescriptionDate.AddDays(BusinessRules.PrescriptionReissueValidityDays),
                    CanRequestReissue = IsEligibleForReissue(prescription) && latestRequest?.Status != ReissueRequestStatus.Pending,
                    HasPendingReissueRequest = latestRequest?.Status == ReissueRequestStatus.Pending,
                    LatestReissueStatus = latestRequest?.Status.ToString()
                };
            }).ToList();
        }

        public async Task<PatientPrescriptionDetailViewModel?> GetPatientPrescriptionDetailAsync(int patientId, int prescriptionId)
        {
            var prescription = await _context.Prescriptions.AsNoTracking()
                .Include(p => p.Patient)
                    .ThenInclude(pt => pt.User)
                .Include(p => p.Doctor)
                    .ThenInclude(d => d.User)
                .Include(p => p.Doctor)
                    .ThenInclude(d => d.Specialization)
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Drug)
                .Include(p => p.Appointment)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId && p.PatientId == patientId);

            if (prescription == null)
                return null;

            var latestRequest = await _context.PrescriptionReissueRequests.AsNoTracking()
                .Where(r => r.PatientId == patientId && r.PrescriptionId == prescription.Id)
                .OrderByDescending(r => r.RequestDate)
                .FirstOrDefaultAsync();

            var pharmacistNotes = await _context.PharmacistPrescriptionNotes.AsNoTracking()
                .Include(n => n.Pharmacist)
                    .ThenInclude(p => p.User)
                .Where(n => n.PrescriptionId == prescription.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return new PatientPrescriptionDetailViewModel
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
                PrescriptionExpiryDate = prescription.PrescriptionDate.AddDays(BusinessRules.PrescriptionReissueValidityDays),
                CanRequestReissue = IsEligibleForReissue(prescription) && latestRequest?.Status != ReissueRequestStatus.Pending,
                HasPendingReissueRequest = latestRequest?.Status == ReissueRequestStatus.Pending,
                LatestReissueStatus = latestRequest?.Status.ToString()
            };
        }

        public async Task<ServiceResult<RecordCreated>> RequestReissueAsync(int patientId, int prescriptionId)
        {
            var prescription = await _context.Prescriptions.AsNoTracking()
                .Include(p => p.Doctor)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId && p.PatientId == patientId);

            if (prescription == null)
                return ServiceResult<RecordCreated>.Fail("Prescription not found.");

            if (!IsEligibleForReissue(prescription))
                return ServiceResult<RecordCreated>.Fail("This prescription has expired and cannot be re-issued.");

            var hasPendingRequest = await _context.PrescriptionReissueRequests.AnyAsync(r =>
                r.PrescriptionId == prescriptionId && r.PatientId == patientId && r.Status == ReissueRequestStatus.Pending);
            if (hasPendingRequest)
                return ServiceResult<RecordCreated>.Fail("A re-issuance request is already pending for this prescription.");

            var request = new PrescriptionReissueRequest
            {
                PatientId = patientId,
                PrescriptionId = prescription.Id,
                DoctorId = prescription.DoctorId,
                RequestDate = _clock.Now,
                Status = ReissueRequestStatus.Pending
            };
            _context.PrescriptionReissueRequests.Add(request);
            await _context.SaveChangesAsync();

            return ServiceResult<RecordCreated>.Ok(new RecordCreated(request.Id, prescription.Doctor?.UserId),
                "Re-issuance request has been sent to the doctor.");
        }

        public Task<List<PrescriptionReissueRequest>> GetReissueRequestsForDoctorAsync(int doctorId) =>
            _context.PrescriptionReissueRequests.AsNoTracking()
                .Include(r => r.Patient)
                    .ThenInclude(p => p.User)
                .Include(r => r.Prescription)
                .Where(r => r.DoctorId == doctorId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

        public async Task<ServiceResult<RequestAnswered>> ReviewReissueRequestAsync(int doctorId, int requestId, bool approve, string? doctorResponse)
        {
            var request = await _context.PrescriptionReissueRequests
                .Include(r => r.Patient)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.DoctorId == doctorId);

            if (request == null)
                return ServiceResult<RequestAnswered>.Fail("Request not found.");

            request.Status = approve ? ReissueRequestStatus.Approved : ReissueRequestStatus.Rejected;
            request.DoctorResponse = doctorResponse;
            request.ReviewedAt = _clock.Now;
            await _context.SaveChangesAsync();

            return ServiceResult<RequestAnswered>.Ok(new RequestAnswered(request.PrescriptionId, request.Patient.UserId), $"Request {(approve ? "approved" : "rejected")} successfully.");
        }

        public Task<List<DoctorReviewRequest>> GetReviewRequestsForDoctorAsync(int doctorId) =>
            _context.DoctorReviewRequests.AsNoTracking()
                .Include(r => r.Pharmacist)
                    .ThenInclude(p => p.User)
                .Include(r => r.Prescription)
                    .ThenInclude(p => p.Patient)
                        .ThenInclude(p => p.User)
                .Where(r => r.DoctorId == doctorId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

        public async Task<ServiceResult<RequestAnswered>> RespondToReviewRequestAsync(int doctorId, int requestId, string doctorResponse)
        {
            if (string.IsNullOrWhiteSpace(doctorResponse))
                return ServiceResult<RequestAnswered>.Fail("Please provide a response.");

            var request = await _context.DoctorReviewRequests
                .Include(r => r.Pharmacist)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.DoctorId == doctorId);

            if (request == null)
                return ServiceResult<RequestAnswered>.Fail("Review request not found.");

            request.DoctorResponse = doctorResponse.Trim();
            request.Status = ReviewRequestStatus.Responded;
            request.ReviewedAt = _clock.Now;
            await _context.SaveChangesAsync();

            return ServiceResult<RequestAnswered>.Ok(new RequestAnswered(request.PrescriptionId, request.Pharmacist.UserId), "Response sent successfully.");
        }
    }
}
