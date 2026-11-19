using HCMS4.Modules.Staff.Models;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Staff.Controllers
{
    // The doctor's own profile and leave requests; [ControllerName] keeps the /Doctor/... URLs.
    [Authorize(Roles = "Doctor")]
    [ControllerName("Doctor")]
    public class DoctorStaffController : HcmsControllerBase
    {
        private readonly IStaffService _staffService;
        private readonly ILeaveService _leaveService;
        private readonly IClinicClock _clock;
        private readonly ILogger<DoctorStaffController> _logger;

        public DoctorStaffController(IStaffService staffService, ILeaveService leaveService, IClinicClock clock,
            ILogger<DoctorStaffController> logger)
        {
            _staffService = staffService;
            _leaveService = leaveService;
            _clock = clock;
            _logger = logger;
        }

        public async Task<IActionResult> Profile()
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                var doctor = await _staffService.GetDoctorProfileAsync(doctorId.Value);
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
            return View(new LeaveRequestViewModel
            {
                StartDate = _clock.Today.AddDays(1),
                EndDate = _clock.Today.AddDays(2)
            });
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

                if (model.StartDate < _clock.Today)
                {
                    ModelState.AddModelError("StartDate", "Cannot request leave for a past date.");
                    return View(model);
                }

                if (model.EndDate < model.StartDate)
                {
                    ModelState.AddModelError("EndDate", "End date must be after or equal to start date.");
                    return View(model);
                }

                await _leaveService.CreateLeaveRequestAsync(new LeaveRequest
                {
                    DoctorId = doctorId.Value,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    LeaveType = model.LeaveType,
                    Reason = model.Reason,
                    CreatedAt = _clock.Now,
                    Status = LeaveStatus.Pending
                });

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
