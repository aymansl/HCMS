using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;
using static HCMS4.ViewModels.GenerateInvoiceViewModel;

namespace HCMS4.Modules.Billing.Services
{
    public enum PaymentUpdateOutcome { Updated, NotFound, AlreadyPaid, Invalid }

    public record PaymentUpdateResult(PaymentUpdateOutcome Outcome, string Message);

    /// <summary>The raw values of the admin's "update payment status" form.</summary>
    public record PaymentUpdateRequest(string? PaymentStatus, string? AmountPaid, string? PaymentDate, string? Notes);

    /// <summary>Invoices: the admin's invoice generation and payment updates, and the patient's invoice views.</summary>
    public interface IInvoiceService
    {
        /// <summary>The form with the last 30 days of completed visits and prescriptions; null when the patient is unknown.</summary>
        Task<GenerateInvoiceViewModel?> GetGenerateFormAsync(int patientId);
        /// <summary>Reloads the patient name and drop-downs after a failed submit.</summary>
        Task FillGenerateFormOptionsAsync(GenerateInvoiceViewModel model);
        Task<ServiceResult<Invoice>> CreateAsync(GenerateInvoiceViewModel model);

        /// <summary>The admin's invoice list: filtered, newest first, one page, with totals of everything that matches.</summary>
        Task<AdminInvoiceListViewModel> GetListAsync(InvoiceListFilter filter);

        /// <summary>The invoice with patient, doctors and (when it has a prescription) prescription items.</summary>
        Task<Invoice?> GetAsync(int id);
        Task<PaymentUpdateResult> UpdatePaymentStatusAsync(int id, PaymentUpdateRequest request);

        Task<List<Invoice>> GetForPatientAsync(int patientId);
        Task<Invoice?> GetForPatientAsync(int patientId, int invoiceId);
    }

    public class InvoiceService : IInvoiceService
    {
        public const string PatientNotFoundMessage = "Patient not found.";

        private const int RecentDays = 30;
        private const int ReloadListSize = 10;

        private readonly ApplicationDbContext _context;
        private readonly IClinicClock _clock;

        public InvoiceService(ApplicationDbContext context, IClinicClock clock)
        {
            _context = context;
            _clock = clock;
        }

        public async Task<GenerateInvoiceViewModel?> GetGenerateFormAsync(int patientId)
        {
            var patientName = await GetPatientNameAsync(patientId);
            if (patientName == null)
                return null;

            var since = _clock.Now.AddDays(-RecentDays);
            var appointments = await CompletedAppointments(patientId)
                .Where(a => a.AppointmentDateTime >= since)
                .ToListAsync();
            var prescriptions = await CompletedPrescriptions(patientId)
                .Where(p => p.PrescriptionDate >= since)
                .ToListAsync();

            var consultationFee = appointments.Sum(a => a.ConsultationFee);
            var medicationTotal = prescriptions.Sum(p => p.MedicationTotal ?? 0);

            return new GenerateInvoiceViewModel
            {
                PatientId = patientId,
                PatientName = patientName,
                ConsultationFee = consultationFee,
                MedicationTotal = medicationTotal,
                TotalAmount = consultationFee + medicationTotal,
                RecentAppointments = ToAppointmentOptions(appointments),
                RecentPrescriptions = ToPrescriptionOptions(prescriptions),
                ExistingInvoices = await _context.Invoices.AsNoTracking()
                    .Where(i => i.PatientId == patientId)
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(5)
                    .ToListAsync()
            };
        }

        public async Task FillGenerateFormOptionsAsync(GenerateInvoiceViewModel model)
        {
            var patientName = await GetPatientNameAsync(model.PatientId);
            if (patientName != null)
                model.PatientName = patientName;

            model.RecentAppointments = ToAppointmentOptions(
                await CompletedAppointments(model.PatientId).Take(ReloadListSize).ToListAsync());
            model.RecentPrescriptions = ToPrescriptionOptions(
                await CompletedPrescriptions(model.PatientId).Take(ReloadListSize).ToListAsync());
        }

        public async Task<ServiceResult<Invoice>> CreateAsync(GenerateInvoiceViewModel model)
        {
            var calculatedTotal = model.ConsultationFee + model.MedicationTotal;
            if (Math.Abs(model.TotalAmount - calculatedTotal) > 0.01m)
            {
                return ServiceResult<Invoice>.Fail(
                    $"Total amount (${model.TotalAmount:F2}) must equal Consultation Fee (${model.ConsultationFee:F2}) + Medication Total (${model.MedicationTotal:F2}) = ${calculatedTotal:F2}");
            }

            var patientName = await GetPatientNameAsync(model.PatientId);
            if (patientName == null)
                return ServiceResult<Invoice>.Fail(PatientNotFoundMessage);

            var errors = new List<string>();
            if (model.AppointmentId.HasValue &&
                !await _context.Appointments.AnyAsync(a => a.Id == model.AppointmentId.Value && a.PatientId == model.PatientId))
            {
                errors.Add("Appointment not found or doesn't belong to this patient.");
            }

            if (model.PrescriptionId.HasValue &&
                !await _context.Prescriptions.AnyAsync(p => p.Id == model.PrescriptionId.Value && p.PatientId == model.PatientId))
            {
                errors.Add("Prescription not found or doesn't belong to this patient.");
            }

            if (errors.Any())
                return ServiceResult<Invoice>.Fail(errors[0], errors);

            var invoice = new Invoice
            {
                PatientId = model.PatientId,
                AppointmentId = model.AppointmentId,
                PrescriptionId = model.PrescriptionId,
                InvoiceDate = _clock.Now,
                ConsultationFee = model.ConsultationFee,
                MedicationTotal = model.MedicationTotal,
                TotalAmount = model.TotalAmount,
                PaymentStatus = PaymentStatus.Pending,
                Notes = model.Notes,
                UpdatedAt = _clock.Now
            };
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            return ServiceResult<Invoice>.Ok(invoice, $"Invoice #{invoice.Id} created successfully for {patientName}!");
        }

        public async Task<AdminInvoiceListViewModel> GetListAsync(InvoiceListFilter filter)
        {
            var query = _context.Invoices.AsNoTracking().AsQueryable();

            if (filter.Status != "all" && Enum.TryParse<PaymentStatus>(filter.Status, true, out var status))
                query = query.Where(i => i.PaymentStatus == status);

            if (filter.From.HasValue)
            {
                var from = filter.From.Value.Date;
                query = query.Where(i => i.InvoiceDate >= from);
            }

            if (filter.To.HasValue)
            {
                var toExclusive = filter.To.Value.Date.AddDays(1);
                query = query.Where(i => i.InvoiceDate < toExclusive);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                var isNumber = int.TryParse(term, out var invoiceId);
                query = query.Where(i =>
                    (isNumber && i.Id == invoiceId) ||
                    (i.Patient.User.FirstName + " " + i.Patient.User.LastName).Contains(term) ||
                    (i.Patient.User.Email != null && i.Patient.User.Email.Contains(term)));
            }

            var totals = await query
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    Pending = g.Count(i => i.PaymentStatus == PaymentStatus.Pending),
                    Paid = g.Count(i => i.PaymentStatus == PaymentStatus.Paid),
                    Total = g.Sum(i => i.TotalAmount),
                    PaidAmount = g.Sum(i => i.PaymentStatus == PaymentStatus.Paid ? (i.AmountPaid ?? i.TotalAmount) : 0)
                })
                .FirstOrDefaultAsync();

            var pageSize = Math.Clamp(filter.PageSize, 1, 100);
            var page = Math.Max(filter.Page, 1);
            var items = await query
                .Include(i => i.Patient)
                    .ThenInclude(p => p.User)
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new AdminInvoiceListViewModel
            {
                Filter = filter,
                Invoices = new PaginatedResult<Invoice>
                {
                    Items = items,
                    TotalCount = totals?.Count ?? 0,
                    PageNumber = page,
                    PageSize = pageSize
                },
                PendingCount = totals?.Pending ?? 0,
                PaidCount = totals?.Paid ?? 0,
                TotalAmount = totals?.Total ?? 0,
                PaidAmount = totals?.PaidAmount ?? 0
            };
        }

        public Task<Invoice?> GetAsync(int id) =>
            InvoicesWithDetails().FirstOrDefaultAsync(i => i.Id == id);

        public async Task<PaymentUpdateResult> UpdatePaymentStatusAsync(int id, PaymentUpdateRequest request)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null)
                return new(PaymentUpdateOutcome.NotFound, "Invoice not found.");

            if (invoice.PaymentStatus == PaymentStatus.Paid)
                return new(PaymentUpdateOutcome.AlreadyPaid, "This invoice is already marked as Paid and cannot be modified.");

            if (string.IsNullOrEmpty(request.PaymentStatus))
                return new(PaymentUpdateOutcome.Invalid, "Payment status is required.");

            if (!Enum.TryParse<PaymentStatus>(request.PaymentStatus, out var newStatus))
                return new(PaymentUpdateOutcome.Invalid, "Invalid payment status.");

            var now = _clock.Now;
            if (newStatus == PaymentStatus.Paid)
            {
                if (string.IsNullOrEmpty(request.AmountPaid))
                    return new(PaymentUpdateOutcome.Invalid, "Amount paid is required for paid status.");

                if (!decimal.TryParse(request.AmountPaid, out var amountPaid))
                    return new(PaymentUpdateOutcome.Invalid, "Invalid amount format.");

                if (amountPaid != invoice.TotalAmount)
                    return new(PaymentUpdateOutcome.Invalid, $"Amount paid (${amountPaid:F2}) must equal the total amount (${invoice.TotalAmount:F2}).");

                DateTime paymentDate = now;
                if (!string.IsNullOrEmpty(request.PaymentDate) && !DateTime.TryParse(request.PaymentDate, out paymentDate))
                    return new(PaymentUpdateOutcome.Invalid, "Invalid payment date format.");

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

            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                var prefix = $"[{now:yyyy-MM-dd HH:mm}] Payment Update: ";
                invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                    ? $"{prefix}{request.Notes}"
                    : $"{invoice.Notes}\n{prefix}{request.Notes}";
            }

            invoice.UpdatedAt = now;
            await _context.SaveChangesAsync();
            return new(PaymentUpdateOutcome.Updated, $"Payment status updated successfully to '{invoice.PaymentStatus}'.");
        }

        public Task<List<Invoice>> GetForPatientAsync(int patientId) =>
            InvoicesWithDetails()
                .Where(i => i.PatientId == patientId)
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

        public Task<Invoice?> GetForPatientAsync(int patientId, int invoiceId) =>
            InvoicesWithDetails().FirstOrDefaultAsync(i => i.Id == invoiceId && i.PatientId == patientId);

        private IQueryable<Invoice> InvoicesWithDetails() =>
            _context.Invoices.AsNoTracking()
                .AsSplitQuery()
                .Include(i => i.Patient)
                    .ThenInclude(p => p.User)
                .Include(i => i.Appointment)
                    .ThenInclude(a => a!.Doctor)
                        .ThenInclude(d => d.User)
                .Include(i => i.Prescription)
                    .ThenInclude(p => p!.Doctor)
                        .ThenInclude(d => d.User)
                .Include(i => i.Prescription)
                    .ThenInclude(p => p!.PrescriptionItems)
                        .ThenInclude(pi => pi.Drug);

        private async Task<string?> GetPatientNameAsync(int patientId)
        {
            var patient = await _context.Patients.AsNoTracking()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == patientId);
            return patient == null ? null : patient.User?.FullName ?? "Unknown";
        }

        private IQueryable<Appointment> CompletedAppointments(int patientId) =>
            _context.Appointments.AsNoTracking()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Where(a => a.PatientId == patientId && a.Status == AppointmentStatus.Completed)
                .OrderByDescending(a => a.AppointmentDateTime);

        private IQueryable<Prescription> CompletedPrescriptions(int patientId) =>
            _context.Prescriptions.AsNoTracking()
                .Include(p => p.Doctor)
                    .ThenInclude(d => d.User)
                .Where(p => p.PatientId == patientId && p.Status == PrescriptionStatus.Completed)
                .OrderByDescending(p => p.PrescriptionDate);

        private static List<AppointmentSelectDto> ToAppointmentOptions(IEnumerable<Appointment> appointments) =>
            appointments.Select(a =>
            {
                var doctorName = a.Doctor?.User?.FullName ?? "Unknown";
                return new AppointmentSelectDto
                {
                    Id = a.Id,
                    AppointmentDate = a.AppointmentDateTime,
                    DoctorName = doctorName,
                    ConsultationFee = a.ConsultationFee,
                    DisplayText = $"{a.AppointmentDateTime:yyyy-MM-dd} - {doctorName} - {a.ConsultationFee:F2}"
                };
            }).ToList();

        private static List<PrescriptionSelectDto> ToPrescriptionOptions(IEnumerable<Prescription> prescriptions) =>
            prescriptions.Select(p =>
            {
                var doctorName = p.Doctor?.User?.FullName ?? "Unknown";
                return new PrescriptionSelectDto
                {
                    Id = p.Id,
                    PrescriptionDate = p.PrescriptionDate,
                    DoctorName = doctorName,
                    MedicationTotal = p.MedicationTotal ?? 0,
                    DisplayText = $"{p.PrescriptionDate:yyyy-MM-dd} - {doctorName} - ${(p.MedicationTotal ?? 0):F2}"
                };
            }).ToList();
    }
}
