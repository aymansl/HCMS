using HCMS4.Data;
using HCMS4.Models;
using HCMS4.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Services
{
    public interface ILeaveService
    {
        Task<LeaveRequest> CreateLeaveRequestAsync(LeaveRequest request);
        Task<List<LeaveRequest>> GetPendingRequestsForDoctorAsync(int doctorId);
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

        public LeaveService(ApplicationDbContext context, ILogger<LeaveService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<LeaveRequest> CreateLeaveRequestAsync(LeaveRequest request)
        {
            request.CreatedAt = DateTime.UtcNow;
            request.Status = LeaveStatus.Pending;

            _context.LeaveRequests.Add(request);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Leave request created for doctor {DoctorId}", request.DoctorId);

            return request;
        }

        public async Task<List<LeaveRequest>> GetPendingRequestsForDoctorAsync(int doctorId)
        {
            return await _context.LeaveRequests
                .Where(lr => lr.DoctorId == doctorId && lr.Status == LeaveStatus.Pending)
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
                    CreatedAt = DateTime.UtcNow
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
                var request = await _context.LeaveRequests.FindAsync(requestId);

                if (request == null)
                {
                    return ServiceResult.Fail("Leave request not found.");
                }

                if (request.Status != LeaveStatus.Pending)
                {
                    return ServiceResult.Fail("This request has already been processed.");
                }

                request.Status = LeaveStatus.Rejected;

                await _context.SaveChangesAsync();

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
                leave.CreatedAt = DateTime.UtcNow;

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

        public Task NotifyManagerAsync(int doctorId, string message)
        {
            _logger.LogInformation("Notification for doctor {DoctorId}: {Message}", doctorId, message);
            return Task.CompletedTask;
        }
    }
}
