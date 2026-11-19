using System.Globalization;
using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Lab.Services
{
    /// <summary>Result entry, issue and alerts (UC-L10).</summary>
    public interface ILabResultService
    {
        /// <summary>The entry page of a request, or null when it does not exist.</summary>
        Task<LabResultEntryViewModel?> GetEntryFormAsync(int requestId);

        /// <summary>
        /// Saves the entered values (the first save moves Received → InProgress). With <paramref name="issue"/>,
        /// every test must have a value; the request becomes ResultIssued and the doctor and patient are alerted.
        /// </summary>
        Task<ServiceResult> SaveAsync(int requestId, string userId, IDictionary<int, string?> values, bool issue);

        /// <summary>An issued request of the doctor with its results, or null.</summary>
        Task<LabRequest?> GetIssuedForDoctorAsync(int doctorId, int requestId);

        /// <summary>An issued request of the patient with its results, or null.</summary>
        Task<LabRequest?> GetIssuedForPatientAsync(string patientUserId, int requestId);

        /// <summary>The doctor confirms they have seen a critical result (UC-L10 alternative flow).</summary>
        Task<ServiceResult> AcknowledgeCriticalAsync(int doctorId, int requestId);

        /// <summary>The doctor's critical results that are not acknowledged yet, newest first.</summary>
        Task<List<LabRequest>> GetUnacknowledgedCriticalAsync(string doctorUserId);
    }

    public class LabResultService : ILabResultService
    {
        public const string CancelledError = "This request was cancelled; results cannot be entered.";
        public const string AlreadyIssuedError = "The result of this request has already been issued.";
        public const string NotReceivedError = "Confirm receipt of the request before entering results.";
        public const string IncompleteError = "Enter a value for every test before issuing the result.";

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly IClinicClock _clock;

        public LabResultService(ApplicationDbContext context, INotificationService notifications, IClinicClock clock)
        {
            _context = context;
            _notifications = notifications;
            _clock = clock;
        }

        private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

        private IQueryable<LabRequest> WithResults(bool tracking = false)
        {
            var query = _context.LabRequests
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .Include(r => r.Items).ThenInclude(i => i.LabTest)
                .Include(r => r.Items).ThenInclude(i => i.Result);
            return tracking ? query : query.AsNoTracking();
        }

        public async Task<LabResultEntryViewModel?> GetEntryFormAsync(int requestId)
        {
            var request = await WithResults().FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null)
                return null;

            return new LabResultEntryViewModel
            {
                Request = request,
                Rows = request.Items.OrderBy(i => i.LabTest.Name).Select(i => new LabResultEntryRow
                {
                    ItemId = i.Id,
                    Test = i.LabTest,
                    IsQualitative = LabResultRules.IsQualitative(i.LabTest),
                    Value = i.Result?.DisplayValue,
                    IsAbnormal = i.Result?.IsAbnormal ?? false,
                    IsCritical = i.Result?.IsCritical ?? false
                }).ToList()
            };
        }

        public async Task<ServiceResult> SaveAsync(int requestId, string userId, IDictionary<int, string?> values, bool issue)
        {
            var request = await WithResults(tracking: true).FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null)
                return ServiceResult.Fail("Lab request not found.");

            // UC-L10 exception and TC-L4-03: no result for a cancelled request.
            switch (request.Status)
            {
                case LabRequestStatus.Cancelled:
                    return ServiceResult.Fail(CancelledError);
                case LabRequestStatus.ResultIssued:
                    return ServiceResult.Fail(AlreadyIssuedError);
                case LabRequestStatus.Received:
                case LabRequestStatus.InProgress:
                    break;
                default:
                    return ServiceResult.Fail(NotReceivedError);
            }

            // Parse every value first: nothing is saved when one is wrong.
            var parsed = new Dictionary<LabRequestItem, (decimal? Number, string? Text)>();
            foreach (var item in request.Items)
            {
                var raw = values.TryGetValue(item.Id, out var v) ? v?.Trim() : null;
                if (string.IsNullOrEmpty(raw))
                    continue;

                if (LabResultRules.IsQualitative(item.LabTest))
                {
                    if (raw.Length > 200)
                        return ServiceResult.Fail($"The result of {item.LabTest.Name} is too long (200 characters at most).");
                    parsed[item] = (null, raw);
                }
                else if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    parsed[item] = (number, null);
                }
                else
                {
                    return ServiceResult.Fail($"The result of {item.LabTest.Name} must be a number (for example 4.5).");
                }
            }

            if (issue && parsed.Count < request.Items.Count)
            {
                var missing = request.Items.Where(i => !parsed.ContainsKey(i)).Select(i => i.LabTest.Name).Order();
                return ServiceResult.Fail($"{IncompleteError} Missing: {string.Join(", ", missing)}.");
            }

            var now = _clock.Now;
            foreach (var item in request.Items)
            {
                if (!parsed.TryGetValue(item, out var value))
                {
                    if (item.Result != null)
                        _context.LabResults.Remove(item.Result); // the value was cleared
                    continue;
                }

                var result = item.Result;
                if (result == null)
                {
                    result = new LabResult { LabRequestItemId = item.Id };
                    item.Result = result;
                    _context.LabResults.Add(result);
                }

                if (result.NumericValue != value.Number || result.TextValue != value.Text)
                {
                    result.EnteredAt = now;
                    result.EnteredBy = userId;
                }

                result.NumericValue = value.Number;
                result.TextValue = value.Text;
                result.IsAbnormal = value.Number.HasValue && LabResultRules.IsAbnormal(item.LabTest, value.Number.Value);
                result.IsCritical = value.Number.HasValue && LabResultRules.IsCritical(item.LabTest, value.Number.Value);
            }

            // The first save starts the work on the request.
            if (request.Status == LabRequestStatus.Received)
                LabRequestWorkflow.Machine.Transition(request, LabRequestStatus.InProgress, userId, _clock);

            if (!issue)
            {
                await _context.SaveChangesAsync();
                return ServiceResult.Ok($"The results of lab request #{requestId} were saved.");
            }

            var transition = LabRequestWorkflow.Machine.Transition(request, LabRequestStatus.ResultIssued, userId, _clock);
            if (!transition.Success)
                return ServiceResult.Fail(transition.Message);

            request.ResultIssuedAt = transition.Data.ChangedAt;
            request.ResultIssuedBy = userId;
            request.HasCriticalResult = parsed.Keys.Any(i => i.Result!.IsCritical);
            await _context.SaveChangesAsync();

            await NotifyAsync(request);
            return ServiceResult.Ok(request.HasCriticalResult
                ? $"The result of lab request #{requestId} was issued. It has a CRITICAL value: the doctor was alerted urgently."
                : $"The result of lab request #{requestId} was issued and the doctor and patient were notified.");
        }

        private async Task NotifyAsync(LabRequest request)
        {
            var patientName = request.Patient.User.FullName;
            var tests = string.Join(", ", request.Items.Select(i => i.LabTest.Name).Order());

            if (request.HasCriticalResult)
            {
                var critical = request.Items.Where(i => i.Result!.IsCritical)
                    .Select(i => $"{i.LabTest.Name} {i.Result!.DisplayValue} {i.LabTest.Unit}").Order();
                await _notifications.CreateForUserAsync(request.Doctor.UserId, "CRITICAL lab result",
                    $"Lab request #{request.Id} for {patientName} has a critical value: {string.Join(", ", critical)}. Open it and acknowledge.",
                    NotificationType.LabRequest, $"/Doctor/LabResult/{Id(request.Id)}", nameof(LabRequest), request.Id);
            }
            else
            {
                await _notifications.CreateForUserAsync(request.Doctor.UserId, "Lab result issued",
                    $"The result of lab request #{request.Id} for {patientName} ({tests}) is ready.",
                    NotificationType.LabRequest, $"/Doctor/LabResult/{Id(request.Id)}", nameof(LabRequest), request.Id);
            }

            await _notifications.CreateForUserAsync(request.Patient.UserId, "Your lab result is ready",
                $"The result of your lab tests ({tests}) is ready.",
                NotificationType.LabRequest, $"/Patient/LabResult/{Id(request.Id)}", nameof(LabRequest), request.Id);
        }

        public Task<LabRequest?> GetIssuedForDoctorAsync(int doctorId, int requestId) =>
            WithResults().FirstOrDefaultAsync(r =>
                r.Id == requestId && r.DoctorId == doctorId && r.Status == LabRequestStatus.ResultIssued);

        public Task<LabRequest?> GetIssuedForPatientAsync(string patientUserId, int requestId) =>
            WithResults().FirstOrDefaultAsync(r =>
                r.Id == requestId && r.Patient.UserId == patientUserId && r.Status == LabRequestStatus.ResultIssued);

        public async Task<ServiceResult> AcknowledgeCriticalAsync(int doctorId, int requestId)
        {
            var request = await _context.LabRequests.Include(r => r.Doctor)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.DoctorId == doctorId);
            if (request == null || request.Status != LabRequestStatus.ResultIssued)
                return ServiceResult.Fail("Lab result not found.");

            if (!request.HasCriticalResult)
                return ServiceResult.Fail("This result has no critical value to acknowledge.");

            if (request.CriticalAcknowledgedAt.HasValue)
                return ServiceResult.Ok("The critical result was already acknowledged.");

            request.CriticalAcknowledgedAt = _clock.Now;
            request.CriticalAcknowledgedBy = request.Doctor.UserId;
            await _context.SaveChangesAsync();
            return ServiceResult.Ok($"You acknowledged the critical result of lab request #{requestId}.");
        }

        public Task<List<LabRequest>> GetUnacknowledgedCriticalAsync(string doctorUserId) =>
            _context.LabRequests.AsNoTracking()
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Where(r => r.Doctor.UserId == doctorUserId && r.Status == LabRequestStatus.ResultIssued
                            && r.HasCriticalResult && r.CriticalAcknowledgedAt == null)
                .OrderByDescending(r => r.ResultIssuedAt)
                .ToListAsync();
    }
}
