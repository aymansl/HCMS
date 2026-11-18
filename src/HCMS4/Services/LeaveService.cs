using HCMS4.Shared.Common;
using HCMS4.Data;
using HCMS4.Models;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Services
{
    public interface ILeaveService
    {
        Task<LeaveRequest> CreateLeaveRequestAsync(LeaveRequest request);
        Task<List<LeaveRequest>> GetPendingRequestsForDoctorAsync(int doctorId);

        /// <summary>All of the doctor's requests (pending, approved and rejected), newest first.</summary>
        Task<List<LeaveRequest>> GetRequestsForDoctorAsync(int doctorId);
        Task<List<DoctorLeave>> GetDoctorLeavesAsync(int doctorId);
        Task<ServiceResult> ApproveLeaveRequestAsync(int requestId, string notes);
        Task<ServiceResult> RejectLeaveRequestAsync(int requestId, string reason);
        Task<ServiceResult> RegisterDoctorLeaveAsync(DoctorLeave leave);
        Task<bool> IsDoctorOnLeaveAsync(int doctorId, DateTime date);
        Task NotifyManagerAsync(int doctorId, string message);
    }

    public class LeaveService : ILeaveService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LeaveService> _logger;
        private readonly INotificationService _notificationService;

        public LeaveService(ApplicationDbContext context, ILogger<LeaveService> logger, INotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task<LeaveRequest> CreateLeaveRequestAsync(LeaveRequest request)
        {
            request.CreatedAt = ClinicTime.Now;
            request.Status = LeaveStatus.Pending;

            _context.LeaveRequests.Add(request);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Leave request created for doctor {DoctorId}", request.DoctorId);

            await NotifyManagerAsync(request.DoctorId,
                $"New leave request from {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}.");

            return request;
        }

        public async Task<List<LeaveRequest>> GetPendingRequestsForDoctorAsync(int doctorId)
        {
            return await _context.LeaveRequests
                .Where(lr => lr.DoctorId == doctorId && lr.Status == LeaveStatus.Pending)
                .OrderByDescending(lr => lr.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<LeaveRequest>> GetRequestsForDoctorAsync(int doctorId)
        {
            return await _context.LeaveRequests
                .AsNoTracking()
                .Where(lr => lr.DoctorId == doctorId)
                .OrderByDescending(lr => lr.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<DoctorLeave>> GetDoctorLeavesAsync(int doctorId)
        {
            return await _context.DoctorLeaves
                .Where(dl => dl.DoctorId == doctorId)
                .OrderByDescending(dl => dl.StartDate)
                .ToListAsync();
        }

        public async Task<ServiceResult> ApproveLeaveRequestAsync(int requestId, string notes)
        {
            try
            {
                var request = await _context.LeaveRequests
                    .Include(lr => lr.Doctor)
                        .ThenInclude(d => d.User)
                    .FirstOrDefaultAsync(lr => lr.Id == requestId);

                if (request == null)
                {
                    return ServiceResult.Fail("Leave request not found.");
                }

                if (request.Status != LeaveStatus.Pending)
                {
                    return ServiceResult.Fail("This request has already been processed.");
                }

                var doctorLeave = new DoctorLeave
                {
                    DoctorId = request.DoctorId,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    LeaveType = request.LeaveType,
                    Notes = notes,
                    Status = LeaveStatus.Approved,
                    CreatedAt = ClinicTime.Now
                };

                _context.DoctorLeaves.Add(doctorLeave);
                request.Status = LeaveStatus.Approved;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Leave request {RequestId} approved for doctor {DoctorId}",
                    requestId, request.DoctorId);

                return ServiceResult.Ok($"Leave request approved. Doctor's leave registered from {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving leave request {RequestId}", requestId);
                return ServiceResult.Fail("Failed to approve request, please try again later.");
            }
        }

        public async Task<ServiceResult> RejectLeaveRequestAsync(int requestId, string reason)
        {
            try
            {
                var request = await _context.LeaveRequests
                    .Include(lr => lr.Doctor)
                    .FirstOrDefaultAsync(lr => lr.Id == requestId);

                if (request == null)
                {
                    return ServiceResult.Fail("Leave request not found.");
                }

                if (request.Status != LeaveStatus.Pending)
                {
                    return ServiceResult.Fail("This request has already been processed.");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return ServiceResult.Fail("Please provide a rejection reason.");
                }

                request.Status = LeaveStatus.Rejected;
                request.RejectionReason = reason.Trim(); // previously discarded (issue #17)

                await _context.SaveChangesAsync();

                // Tell the doctor why; the reason is also listed on the doctor's profile.
                if (request.Doctor != null)
                {
                    await _notificationService.CreateForUserAsync(
                        request.Doctor.UserId,
                        "Leave request rejected",
                        $"Your leave request from {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd} was rejected: {request.RejectionReason}",
                        NotificationType.General,
                        "/Doctor/Profile",
                        nameof(LeaveRequest),
                        request.Id);
                }

                _logger.LogInformation("Leave request {RequestId} rejected", requestId);

                return ServiceResult.Ok("Leave request rejected.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting leave request {RequestId}", requestId);
                return ServiceResult.Fail("Failed to reject request, please try again later.");
            }
        }

        public async Task<ServiceResult> RegisterDoctorLeaveAsync(DoctorLeave leave)
        {
            try
            {
                if (leave.EndDate < leave.StartDate)
                {
                    return ServiceResult.Fail("End date must be after start date.");
                }

                var hasConflict = await _context.DoctorLeaves
                    .AnyAsync(dl => dl.DoctorId == leave.DoctorId &&
                                   dl.Status == LeaveStatus.Approved &&
                                   ((dl.StartDate <= leave.StartDate && dl.EndDate >= leave.StartDate) ||
                                    (dl.StartDate <= leave.EndDate && dl.EndDate >= leave.EndDate) ||
                                    (dl.StartDate >= leave.StartDate && dl.EndDate <= leave.EndDate)));

                if (hasConflict)
                {
                    return ServiceResult.Fail("This leave period conflicts with an existing leave.");
                }

                leave.Status = LeaveStatus.Approved;
                leave.CreatedAt = ClinicTime.Now;

                _context.DoctorLeaves.Add(leave);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Doctor leave registered for doctor {DoctorId} from {Start} to {End}",
                    leave.DoctorId, leave.StartDate, leave.EndDate);

                return ServiceResult.Ok("Doctor's leave registered successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering doctor leave");
                return ServiceResult.Fail("Failed to submit request, please try again later.");
            }
        }

        public async Task<bool> IsDoctorOnLeaveAsync(int doctorId, DateTime date)
        {
            return await _context.DoctorLeaves
                .AnyAsync(dl => dl.DoctorId == doctorId &&
                                dl.Status == LeaveStatus.Approved &&
                                dl.StartDate <= date &&
                                dl.EndDate >= date);
        }

        public async Task NotifyManagerAsync(int doctorId, string message)
        {
            // Managers are the users in the Admin role; they review leaves on the doctor's leave page.
            await _notificationService.CreateForRoleAsync(
                "Admin",
                "Doctor leave request",
                message,
                NotificationType.General,
                $"/Admin/ViewDoctorLeaves/{doctorId}",
                nameof(Doctor),
                doctorId);

            _logger.LogInformation("Managers notified about doctor {DoctorId}: {Message}", doctorId, message);
        }
    }
}
