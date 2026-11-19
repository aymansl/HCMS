using HCMS4.Data;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Pharmacy.Services
{
    public record DispenseOutcome(int InvoiceId, decimal TotalAmount);

    public record PrescriptionDetails(
        Prescription Prescription,
        Invoice? Invoice,
        List<PharmacistPrescriptionNote> PharmacistNotes,
        List<DoctorReviewRequest> ReviewRequests);

    public record PrescriptionParties(int PrescriptionId, string PatientName, string DoctorName);

    /// <summary>A record that was created, and the user (if any) who should be notified about it.</summary>
    public record RecordCreated(int Id, string? NotifyUserId);

    /// <summary>The pharmacist's prescription queue: dispensing, notes and doctor review requests.</summary>
    public interface IDispensingService
    {
        /// <summary>Newest first; <paramref name="status"/> "all" or a <see cref="PrescriptionStatus"/> name.</summary>
        Task<List<Prescription>> GetQueueAsync(string status);
        Task<Prescription?> GetForProcessingAsync(int id);
        Task<PrescriptionDetails?> GetDetailsAsync(int id);
        Task<PrescriptionParties?> GetPartiesAsync(int prescriptionId);

        /// <summary>
        /// Dispenses every item of a pending prescription: takes them out of stock, completes the prescription
        /// and creates a pending invoice (medication + tax). There is no partial dispensing: when any item is
        /// short, nothing is dispensed. This is the only place where prescribed drugs leave the stock.
        /// </summary>
        Task<ServiceResult<DispenseOutcome>> DispenseAsync(int prescriptionId, string? dispensingNotes, string pharmacistUserId);

        Task<ServiceResult<RecordCreated>> RequestDoctorReviewAsync(string pharmacistUserId, PrescriptionReviewRequestFormViewModel model);
        /// <summary>The returned doctor user id is set only when the note asks to notify the doctor.</summary>
        Task<ServiceResult<RecordCreated>> AddNoteAsync(string pharmacistUserId, PrescriptionNoteFormViewModel model);
    }

    public class DispensingService : IDispensingService
    {
        public const decimal TaxRate = 0.05m;

        private readonly ApplicationDbContext _context;
        private readonly IClinicClock _clock;
        private readonly ILogger<DispensingService> _logger;

        public DispensingService(ApplicationDbContext context, IClinicClock clock, ILogger<DispensingService> logger)
        {
            _context = context;
            _clock = clock;
            _logger = logger;
        }

        public async Task<List<Prescription>> GetQueueAsync(string status)
        {
            var query = _context.Prescriptions.AsNoTracking()
                .Include(p => p.Patient)
                    .ThenInclude(pat => pat.User)
                .Include(p => p.Doctor)
                    .ThenInclude(doc => doc.User)
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Drug)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status != "all" &&
                Enum.TryParse<PrescriptionStatus>(status, true, out var statusEnum))
            {
                query = query.Where(p => p.Status == statusEnum);
            }

            return await query.OrderByDescending(p => p.PrescriptionDate).ToListAsync();
        }

        public Task<Prescription?> GetForProcessingAsync(int id) =>
            _context.Prescriptions.AsNoTracking()
                .Include(p => p.Patient)
                    .ThenInclude(pat => pat.User)
                .Include(p => p.Doctor)
                    .ThenInclude(doc => doc.User)
                .Include(p => p.Doctor)
                    .ThenInclude(doc => doc.Specialization)
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Drug)
                .FirstOrDefaultAsync(p => p.Id == id);

        public async Task<PrescriptionDetails?> GetDetailsAsync(int id)
        {
            var prescription = await _context.Prescriptions.AsNoTracking()
                .Include(p => p.Patient)
                    .ThenInclude(pat => pat.User)
                .Include(p => p.Doctor)
                    .ThenInclude(doc => doc.User)
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Drug)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prescription == null)
                return null;

            var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.PrescriptionId == id);

            var notes = await _context.PharmacistPrescriptionNotes.AsNoTracking()
                .Include(n => n.Pharmacist)
                    .ThenInclude(p => p.User)
                .Where(n => n.PrescriptionId == id)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            var reviewRequests = await _context.DoctorReviewRequests.AsNoTracking()
                .Include(r => r.Pharmacist)
                    .ThenInclude(p => p.User)
                .Where(r => r.PrescriptionId == id)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return new PrescriptionDetails(prescription, invoice, notes, reviewRequests);
        }

        public Task<PrescriptionParties?> GetPartiesAsync(int prescriptionId) =>
            _context.Prescriptions.AsNoTracking()
                .Where(p => p.Id == prescriptionId)
                .Select(p => new PrescriptionParties(
                    p.Id,
                    p.Patient != null && p.Patient.User != null ? p.Patient.User.FullName : "Patient",
                    p.Doctor != null && p.Doctor.User != null ? p.Doctor.User.FullName : "Doctor"))
                .FirstOrDefaultAsync();

        public async Task<ServiceResult<DispenseOutcome>> DispenseAsync(int prescriptionId, string? dispensingNotes, string pharmacistUserId)
        {
            var pharmacist = await _context.Pharmacists.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == pharmacistUserId);
            if (pharmacist == null)
                return ServiceResult<DispenseOutcome>.Fail("Pharmacist profile not found.");

            var prescription = await _context.Prescriptions
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Drug)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);

            if (prescription == null)
                return ServiceResult<DispenseOutcome>.Fail("Prescription not found.");

            if (prescription.Status != PrescriptionStatus.Pending)
                return ServiceResult<DispenseOutcome>.Fail("Only pending prescriptions can be dispensed.");

            var items = prescription.PrescriptionItems.ToList();
            if (!items.Any())
                return ServiceResult<DispenseOutcome>.Fail("This prescription has no medications to dispense.");

            var insufficientStock = new List<string>();
            foreach (var item in items)
            {
                if (item.Drug == null)
                    insufficientStock.Add($"Item #{item.Id}: Drug not found");
                else if (item.Drug.Quantity < item.Quantity)
                    insufficientStock.Add($"{item.Drug.Name}: Required {item.Quantity}, Available {item.Drug.Quantity}");
            }

            if (insufficientStock.Any())
            {
                return ServiceResult<DispenseOutcome>.Fail(
                    "Cannot dispense due to insufficient stock:\n" + string.Join("\n", insufficientStock));
            }

            var now = _clock.Now;
            await using var transaction = await _context.Database.BeginTransactionAsync();

            decimal totalAmount = 0;
            foreach (var item in items)
            {
                item.Drug.Quantity -= item.Quantity;
                item.Drug.UpdatedAt = now;
                totalAmount += item.Drug.Price * item.Quantity;
            }

            var grandTotal = totalAmount + totalAmount * TaxRate;

            prescription.Status = PrescriptionStatus.Completed;
            prescription.MedicationTotal = totalAmount;
            prescription.DispensedDate = now;
            prescription.DispensedBy = pharmacistUserId;
            prescription.UpdatedAt = now;
            prescription.DispensingNotes = dispensingNotes;

            var invoice = new Invoice
            {
                PatientId = prescription.PatientId,
                PrescriptionId = prescription.Id,
                AppointmentId = prescription.AppointmentId,
                InvoiceDate = now,
                MedicationTotal = totalAmount,
                TotalAmount = grandTotal,
                PaymentStatus = PaymentStatus.Pending,
                Notes = $"Generated from prescription #{prescription.Id} on {now:yyyy-MM-dd HH:mm}",
                UpdatedAt = now,
                PharmacistId = pharmacist.Id,
                DispensedAt = now
            };
            _context.Invoices.Add(invoice);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Prescription {PrescriptionId} dispensed ({Items} items); invoice {InvoiceId} total {Total}",
                prescription.Id, items.Count, invoice.Id, grandTotal);

            return ServiceResult<DispenseOutcome>.Ok(new DispenseOutcome(invoice.Id, grandTotal),
                $"Prescription dispensed. Invoice #{invoice.Id} created. Total: {grandTotal:C2}");
        }

        public async Task<ServiceResult<RecordCreated>> RequestDoctorReviewAsync(string pharmacistUserId,
            PrescriptionReviewRequestFormViewModel model)
        {
            var (pharmacist, prescription) = await LoadPharmacistAndPrescriptionAsync(pharmacistUserId, model.PrescriptionId);
            if (pharmacist == null || prescription == null)
                return ServiceResult<RecordCreated>.Fail("Prescription or pharmacist profile not found.");

            var request = new DoctorReviewRequest
            {
                PharmacistId = pharmacist.Id,
                PrescriptionId = prescription.Id,
                DoctorId = prescription.DoctorId,
                ReasonForReview = model.ReasonForReview,
                AdditionalComments = model.AdditionalComments,
                SuggestedAlternative = model.SuggestedAlternative,
                RequestDate = _clock.Now,
                Status = ReviewRequestStatus.Pending
            };
            _context.DoctorReviewRequests.Add(request);
            await _context.SaveChangesAsync();

            return ServiceResult<RecordCreated>.Ok(new RecordCreated(request.Id, prescription.Doctor?.UserId),
                "Review request has been sent to the doctor.");
        }

        public async Task<ServiceResult<RecordCreated>> AddNoteAsync(string pharmacistUserId, PrescriptionNoteFormViewModel model)
        {
            var (pharmacist, prescription) = await LoadPharmacistAndPrescriptionAsync(pharmacistUserId, model.PrescriptionId);
            if (pharmacist == null || prescription == null)
                return ServiceResult<RecordCreated>.Fail("Prescription or pharmacist profile not found.");

            var note = new PharmacistPrescriptionNote
            {
                PrescriptionId = prescription.Id,
                PharmacistId = pharmacist.Id,
                NoteText = model.NoteText.Trim(),
                NotifyDoctor = model.NotifyDoctor,
                CreatedAt = _clock.Now
            };
            _context.PharmacistPrescriptionNotes.Add(note);
            await _context.SaveChangesAsync();

            return ServiceResult<RecordCreated>.Ok(
                new RecordCreated(note.Id, model.NotifyDoctor ? prescription.Doctor?.UserId : null),
                "Note added successfully.");
        }

        private async Task<(Staff.Models.Pharmacist? pharmacist, Prescription? prescription)> LoadPharmacistAndPrescriptionAsync(
            string pharmacistUserId, int prescriptionId)
        {
            var pharmacist = await _context.Pharmacists.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == pharmacistUserId);
            var prescription = await _context.Prescriptions.AsNoTracking()
                .Include(p => p.Doctor)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);
            return (pharmacist, prescription);
        }
    }
}
