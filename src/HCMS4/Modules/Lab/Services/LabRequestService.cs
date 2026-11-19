using System.Globalization;
using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Modules.Records.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Lab.Services
{
    /// <summary>Lab test requests between doctors and the lab (UC-L03).</summary>
    public interface ILabRequestService
    {
        /// <summary>An empty form for the patient, or null when the patient does not exist.</summary>
        Task<LabRequestFormViewModel?> NewFormAsync(int patientId);

        /// <summary>The doctor's returned request as a form, or null when it is not theirs or not returned.</summary>
        Task<LabRequestFormViewModel?> EditFormAsync(int doctorId, int requestId);

        /// <summary>Fills the display fields of a posted form again (patient name, active tests).</summary>
        Task FillFormAsync(LabRequestFormViewModel model);

        /// <summary>Sends a new request to the lab (status Requested) and notifies the lab.</summary>
        Task<ServiceResult<LabRequest>> CreateAsync(int doctorId, LabRequestFormViewModel model);

        /// <summary>Resends a returned request with corrected tests or notes (ReturnedToDoctor → Requested).</summary>
        Task<ServiceResult<LabRequest>> ResubmitAsync(int doctorId, LabRequestFormViewModel model);

        Task<List<LabRequest>> GetForDoctorAsync(int doctorId, int? patientId = null);

        /// <summary>A request with its patient, doctor and tests, for the lab.</summary>
        Task<LabRequest?> GetDetailsAsync(int id);

        /// <summary>
        /// The lab's incoming requests (Requested, Received, InProgress) by arrival time, oldest first,
        /// matching the filter (UC-L02).
        /// </summary>
        Task<List<LabRequest>> GetIncomingAsync(LabRequestFilter filter);

        /// <summary>The newest requests waiting for the lab to confirm receipt.</summary>
        Task<List<LabRequest>> GetNewForLabAsync(int count = 5);
        Task<int> CountNewAsync();

        /// <summary>The technician confirms receipt (Requested → Received) and the time is recorded.</summary>
        Task<ServiceResult> ReceiveAsync(int id, string userId);

        /// <summary>The technician returns an incomplete request to the doctor with a note.</summary>
        Task<ServiceResult> ReturnToDoctorAsync(int id, string userId, string note);
    }

    public class LabRequestService : ILabRequestService
    {
        public const string PatientNotFoundError = "The lab request was rejected: the patient record was not found.";
        public const string NoAccessError = "You do not have access to this patient's record.";
        public const string InactiveTestError = "One or more selected tests are not available.";

        private readonly ApplicationDbContext _context;
        private readonly IMedicalRecordService _records;
        private readonly INotificationService _notifications;
        private readonly IClinicClock _clock;

        public LabRequestService(ApplicationDbContext context, IMedicalRecordService records,
            INotificationService notifications, IClinicClock clock)
        {
            _context = context;
            _records = records;
            _notifications = notifications;
            _clock = clock;
        }

        private static string LabLink(int id) => $"/Lab/RequestDetails/{id.ToString(CultureInfo.InvariantCulture)}";
        private static string DoctorLink(int id) => $"/Doctor/EditLabRequest/{id.ToString(CultureInfo.InvariantCulture)}";

        private Task<string?> PatientNameAsync(int patientId) =>
            _context.Patients.AsNoTracking().Where(p => p.Id == patientId)
                .Select(p => p.User.FirstName + " " + p.User.LastName)
                .FirstOrDefaultAsync();

        private Task<List<LabTest>> ActiveTestsAsync() =>
            _context.LabTests.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();

        public async Task<LabRequestFormViewModel?> NewFormAsync(int patientId)
        {
            var name = await PatientNameAsync(patientId);
            if (name == null)
                return null;

            return new LabRequestFormViewModel { PatientId = patientId, PatientName = name, AvailableTests = await ActiveTestsAsync() };
        }

        public async Task<LabRequestFormViewModel?> EditFormAsync(int doctorId, int requestId)
        {
            var request = await _context.LabRequests.AsNoTracking().Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.DoctorId == doctorId);
            if (request == null || request.Status != LabRequestStatus.ReturnedToDoctor)
                return null;

            var model = new LabRequestFormViewModel
            {
                Id = request.Id,
                PatientId = request.PatientId,
                ClinicalNotes = request.ClinicalNotes,
                SelectedTestIds = request.Items.Select(i => i.LabTestId).ToList()
            };
            await FillFormAsync(model);
            return model;
        }

        public async Task FillFormAsync(LabRequestFormViewModel model)
        {
            model.PatientName = await PatientNameAsync(model.PatientId) ?? string.Empty;
            model.AvailableTests = await ActiveTestsAsync();
            if (model.Id != 0)
            {
                model.ReturnNote = await _context.LabRequests.AsNoTracking()
                    .Where(r => r.Id == model.Id).Select(r => r.ReturnNote).FirstOrDefaultAsync();
            }
        }

        /// <summary>The selected active tests, or an error message.</summary>
        private async Task<(List<LabTest> Tests, string? Error)> SelectedTestsAsync(LabRequestFormViewModel model)
        {
            var ids = model.SelectedTestIds.Distinct().ToList();
            if (ids.Count == 0)
                return (new List<LabTest>(), LabRequestFormViewModel.NoTestsError);

            var tests = await _context.LabTests.Where(t => ids.Contains(t.Id) && t.IsActive).ToListAsync();
            return tests.Count == ids.Count ? (tests, null) : (tests, InactiveTestError);
        }

        private static string? CleanNotes(string? notes) => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        public async Task<ServiceResult<LabRequest>> CreateAsync(int doctorId, LabRequestFormViewModel model)
        {
            var doctorUserId = await _context.Doctors.Where(d => d.Id == doctorId).Select(d => d.UserId).FirstOrDefaultAsync();
            if (doctorUserId == null)
                return ServiceResult<LabRequest>.Fail("Doctor not found.");

            // Exception flow: the patient record does not exist — reject and alert the doctor.
            var patientName = await PatientNameAsync(model.PatientId);
            if (patientName == null)
            {
                await _notifications.CreateForUserAsync(doctorUserId, "Lab request rejected", PatientNotFoundError,
                    NotificationType.LabRequest);
                return ServiceResult<LabRequest>.Fail(PatientNotFoundError);
            }

            if (!await _records.DoctorHasAccessAsync(doctorId, model.PatientId))
                return ServiceResult<LabRequest>.Fail(NoAccessError);

            var (tests, error) = await SelectedTestsAsync(model);
            if (error != null)
                return ServiceResult<LabRequest>.Fail(error);

            var now = _clock.Now;
            var request = new LabRequest
            {
                PatientId = model.PatientId,
                DoctorId = doctorId,
                ClinicalNotes = CleanNotes(model.ClinicalNotes),
                Status = LabRequestStatus.Requested,
                RequestedAt = now,
                StatusChangedAt = now,
                StatusChangedBy = doctorUserId,
                Items = tests.Select(t => new LabRequestItem { LabTestId = t.Id, Price = t.Price }).ToList()
            };
            _context.LabRequests.Add(request);
            await _context.SaveChangesAsync();

            await _notifications.CreateForRoleAsync(Roles.LabTechnician, "New lab request",
                $"New lab request #{request.Id} for {patientName}: {string.Join(", ", tests.Select(t => t.Name).Order())}.",
                NotificationType.LabRequest, LabLink(request.Id), nameof(LabRequest), request.Id);

            return ServiceResult<LabRequest>.Ok(request, $"Lab request #{request.Id} was sent to the lab.");
        }

        public async Task<ServiceResult<LabRequest>> ResubmitAsync(int doctorId, LabRequestFormViewModel model)
        {
            var request = await _context.LabRequests.Include(r => r.Items).Include(r => r.Doctor)
                .FirstOrDefaultAsync(r => r.Id == model.Id && r.DoctorId == doctorId);
            if (request == null)
                return ServiceResult<LabRequest>.Fail("Lab request not found.");

            var (tests, error) = await SelectedTestsAsync(model);
            if (error != null)
                return ServiceResult<LabRequest>.Fail(error);

            var transition = LabRequestWorkflow.Machine.Transition(request, LabRequestStatus.Requested, request.Doctor.UserId, _clock);
            if (!transition.Success)
                return ServiceResult<LabRequest>.Fail("Only a request returned by the lab can be edited and resent.");

            request.ClinicalNotes = CleanNotes(model.ClinicalNotes);
            request.RequestedAt = transition.Data.ChangedAt;

            var selected = tests.ToDictionary(t => t.Id);
            _context.LabRequestItems.RemoveRange(request.Items.Where(i => !selected.ContainsKey(i.LabTestId)).ToList());
            foreach (var test in tests.Where(t => request.Items.All(i => i.LabTestId != t.Id)))
            {
                request.Items.Add(new LabRequestItem { LabTestId = test.Id, Price = test.Price });
            }

            await _context.SaveChangesAsync();

            var patientName = await PatientNameAsync(request.PatientId);
            await _notifications.CreateForRoleAsync(Roles.LabTechnician, "Lab request resent",
                $"Lab request #{request.Id} for {patientName} was corrected and resent by the doctor.",
                NotificationType.LabRequest, LabLink(request.Id), nameof(LabRequest), request.Id);

            return ServiceResult<LabRequest>.Ok(request, $"Lab request #{request.Id} was resent to the lab.");
        }

        private IQueryable<LabRequest> WithDetails() => _context.LabRequests.AsNoTracking()
            .Include(r => r.Patient).ThenInclude(p => p.User)
            .Include(r => r.Doctor).ThenInclude(d => d.User)
            .Include(r => r.Items).ThenInclude(i => i.LabTest);

        public Task<List<LabRequest>> GetForDoctorAsync(int doctorId, int? patientId = null) =>
            WithDetails()
                .Where(r => r.DoctorId == doctorId && (patientId == null || r.PatientId == patientId))
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

        public Task<LabRequest?> GetDetailsAsync(int id) => WithDetails().FirstOrDefaultAsync(r => r.Id == id);

        public async Task<List<LabRequest>> GetIncomingAsync(LabRequestFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);
            var query = WithDetails().Where(r => LabRequestFilter.IncomingStatuses.Contains(r.Status));

            // Keep the status a variable: on SQL Server, EF folds a constant status combined with the
            // IN list above into "WHERE 0 = 1" (checked with ToQueryString, EF Core 10).
            if (filter.Status is { } status && LabRequestFilter.IncomingStatuses.Contains(status))
                query = query.Where(r => r.Status == status);

            if (filter.Date is { } date)
            {
                var from = date.ToDateTime(TimeOnly.MinValue);
                var to = from.AddDays(1);
                query = query.Where(r => r.RequestedAt >= from && r.RequestedAt < to);
            }

            if (!string.IsNullOrWhiteSpace(filter.PatientName))
            {
                // Case-insensitive, whatever the database collation; EF translates ToUpper() to SQL UPPER().
                var name = filter.PatientName.Trim().ToUpperInvariant();
#pragma warning disable CA1304, CA1311, CA1862
                query = query.Where(r => (r.Patient.User.FirstName + " " + r.Patient.User.LastName).ToUpper().Contains(name));
#pragma warning restore CA1304, CA1311, CA1862
            }

            return await query.OrderBy(r => r.RequestedAt).ThenBy(r => r.Id).ToListAsync();
        }

        public Task<List<LabRequest>> GetNewForLabAsync(int count = 5) =>
            WithDetails()
                .Where(r => r.Status == LabRequestStatus.Requested)
                .OrderBy(r => r.RequestedAt)
                .Take(count)
                .ToListAsync();

        public Task<int> CountNewAsync() => _context.LabRequests.CountAsync(r => r.Status == LabRequestStatus.Requested);

        public async Task<ServiceResult> ReceiveAsync(int id, string userId)
        {
            var request = await _context.LabRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null)
                return ServiceResult.Fail("Lab request not found.");

            var transition = LabRequestWorkflow.Machine.Transition(request, LabRequestStatus.Received, userId, _clock);
            if (!transition.Success)
                return ServiceResult.Fail($"Lab request #{id} cannot be received: its status is {request.Status}.");

            request.ReceivedAt = transition.Data.ChangedAt;
            request.ReceivedBy = userId;
            await _context.SaveChangesAsync();
            return ServiceResult.Ok($"Lab request #{id} was received.");
        }

        public async Task<ServiceResult> ReturnToDoctorAsync(int id, string userId, string note)
        {
            if (string.IsNullOrWhiteSpace(note))
                return ServiceResult.Fail("Write a note for the doctor.");

            var request = await _context.LabRequests.Include(r => r.Doctor).FirstOrDefaultAsync(r => r.Id == id);
            if (request == null)
                return ServiceResult.Fail("Lab request not found.");

            var transition = LabRequestWorkflow.Machine.Transition(request, LabRequestStatus.ReturnedToDoctor, userId, _clock);
            if (!transition.Success)
                return ServiceResult.Fail($"Lab request #{id} cannot be returned: its status is {request.Status}.");

            request.ReturnedAt = transition.Data.ChangedAt;
            request.ReturnNote = note.Trim();
            await _context.SaveChangesAsync();

            await _notifications.CreateForUserAsync(request.Doctor.UserId, "Lab request returned",
                $"The lab returned request #{id}: {request.ReturnNote}",
                NotificationType.LabRequest, DoctorLink(id), nameof(LabRequest), id);

            return ServiceResult.Ok($"Lab request #{id} was returned to the doctor.");
        }
    }

    /// <summary>The incoming requests filter (UC-L02 alternative flow). Empty fields do not filter.</summary>
    public class LabRequestFilter
    {
        /// <summary>The statuses the lab still has to work on.</summary>
        public static readonly IReadOnlyList<LabRequestStatus> IncomingStatuses =
            new[] { LabRequestStatus.Requested, LabRequestStatus.Received, LabRequestStatus.InProgress };

        /// <summary>One of <see cref="IncomingStatuses"/>; any other status is ignored.</summary>
        public LabRequestStatus? Status { get; set; }

        /// <summary>The day the request arrived (Damascus time).</summary>
        public DateOnly? Date { get; set; }

        public string? PatientName { get; set; }

        public bool IsEmpty => Status == null && Date == null && string.IsNullOrWhiteSpace(PatientName);
    }

    /// <summary>A catalogue test is in use when any lab request includes it (UC-L06 alternative flow).</summary>
    public class LabRequestTestUsage : ILabTestUsage
    {
        private readonly ApplicationDbContext _context;

        public LabRequestTestUsage(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<bool> IsUsedAsync(int labTestId) => _context.LabRequestItems.AnyAsync(i => i.LabTestId == labTestId);
    }
}
