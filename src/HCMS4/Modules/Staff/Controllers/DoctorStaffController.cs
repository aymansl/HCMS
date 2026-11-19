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

namespace HCMS4.Modules.Staff.Controllers
{
    // Part of the former DoctorController; [ControllerName] keeps the /Doctor/... URLs.
    [Authorize(Roles = "Doctor")]
    [ControllerName("Doctor")]
    public class DoctorStaffController : BaseController
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IPrescriptionService _prescriptionService;
        private readonly ILeaveService _leaveService;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public DoctorStaffController(
            ApplicationDbContext context,
            ILogger<DoctorStaffController> logger,
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

        public async Task<IActionResult> Profile()
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                var doctor = await _context.Doctors
                    .Include(d => d.User)
                    .Include(d => d.Specialization)
                    .FirstOrDefaultAsync(d => d.Id == doctorId.Value);

                if (doctor == null)
                {
                    TempData["ErrorMessage"] = "Doctor profile not found.";
                    return RedirectToAction("Dashboard");
                }

                var leaveRequests = await _leaveService.GetRequestsForDoctorAsync(doctorId.Value);
                var approvedLeaves = await _leaveService.GetDoctorLeavesAsync(doctorId.Value);

                var viewModel = new DoctorLeaveViewModel
                {
                    DoctorId = doctor.Id,
                    DoctorName = doctor.FullName,
                    LeaveRequests = leaveRequests.Select(lr => new LeaveRequestViewModel
                    {
                        Id = lr.Id,
                        StartDate = lr.StartDate,
                        EndDate = lr.EndDate,
                        LeaveType = lr.LeaveType,
                        Reason = lr.Reason,
                        RejectionReason = lr.RejectionReason,
                        Status = lr.Status,
                        CreatedAt = lr.CreatedAt
                    }).ToList(),
                    ApprovedLeaves = approvedLeaves.Select(dl => new DoctorLeaveItem
                    {
                        Id = dl.Id,
                        StartDate = dl.StartDate,
                        EndDate = dl.EndDate,
                        LeaveType = dl.LeaveType.ToString(),
                        Notes = dl.Notes,
                        Status = dl.Status
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading doctor profile");
                TempData["ErrorMessage"] = "Error loading profile. Please try again.";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpGet]
        public IActionResult RequestLeave()
        {
            var viewModel = new LeaveRequestViewModel
            {
                StartDate = ClinicTime.Today.AddDays(1),
                EndDate = ClinicTime.Today.AddDays(2)
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestLeave(LeaveRequestViewModel model)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                if (model.StartDate < ClinicTime.Today)
                {
                    ModelState.AddModelError("StartDate", "Cannot request leave for a past date.");
                    return View(model);
                }

                if (model.EndDate < model.StartDate)
                {
                    ModelState.AddModelError("EndDate", "End date must be after or equal to start date.");
                    return View(model);
                }

                var request = new LeaveRequest
                {
                    DoctorId = doctorId.Value,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    LeaveType = model.LeaveType,
                    Reason = model.Reason,
                    CreatedAt = ClinicTime.Now,
                    Status = LeaveStatus.Pending
                };

                await _leaveService.CreateLeaveRequestAsync(request);

                TempData["SuccessMessage"] = "Leave request submitted successfully.";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting leave request");
                TempData["ErrorMessage"] = "Failed to submit request, please try again later.";
                return View(model);
            }
        }
    }
}
