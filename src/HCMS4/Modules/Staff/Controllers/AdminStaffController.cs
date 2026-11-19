using System.Globalization;
using HCMS4.Modules.Staff.Models;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using HCMS4.Shared.Identity.Authorization;

namespace HCMS4.Modules.Staff.Controllers
{
    // Admin pages for doctors, pharmacists, fees and leaves; [ControllerName] keeps the /Admin/... URLs.
    [Authorize(Policy = Permissions.Staff.Manage)]
    [ControllerName("Admin")]
    public class AdminStaffController : HcmsControllerBase
    {
        private readonly IStaffService _staffService;
        private readonly ILeaveService _leaveService;
        private readonly IClinicClock _clock;
        private readonly ILogger<AdminStaffController> _logger;

        public AdminStaffController(IStaffService staffService, ILeaveService leaveService, IClinicClock clock,
            ILogger<AdminStaffController> logger)
        {
            _staffService = staffService;
            _leaveService = leaveService;
            _clock = clock;
            _logger = logger;
        }

        private async Task LoadSpecializationsAsync()
        {
            ViewBag.Specializations = (await _staffService.GetActiveSpecializationsAsync())
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(CultureInfo.InvariantCulture),
                    Text = $"{s.Name} - {s.ConsultationFee:C2}"
                })
                .ToList();
        }

        public async Task<IActionResult> CreateDoctor()
        {
            await LoadSpecializationsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDoctor(DoctorRegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _staffService.CreateDoctorAsync(model);
                    if (result.Success)
                    {
                        TempData["SuccessMessage"] = result.Message;
                        return RedirectToAction("Doctors", "Admin");
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating doctor account");
                    ModelState.AddModelError(string.Empty, "An error occurred while creating the doctor account.");
                }
            }

            await LoadSpecializationsAsync();
            return View(model);
        }

        public async Task<IActionResult> Doctors()
        {
            return View(await _staffService.GetDoctorsAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var result = await _staffService.DeleteUserAsync(id, CurrentUserId);
                TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error deleting user {UserId}", id);
                TempData["ErrorMessage"] = "Cannot delete user. There might be related records that need to be deleted first.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                TempData["ErrorMessage"] = $"Error deleting user: {ex.Message}";
            }

            return RedirectToAction("Doctors");
        }

        public IActionResult CreatePharmacist()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePharmacist(PharmacistRegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _staffService.CreatePharmacistAsync(model);
                    if (result.Success)
                    {
                        TempData["SuccessMessage"] = result.Message;
                        return RedirectToAction("Pharmacists", "Admin");
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating pharmacist account");
                    ModelState.AddModelError(string.Empty, "An error occurred while creating the pharmacist account.");
                }
            }

            return View(model);
        }

        public async Task<IActionResult> Pharmacists()
        {
            return View(await _staffService.GetPharmacistsAsync());
        }

        public async Task<IActionResult> EditPharmacist(int? id)
        {
            if (id == null)
                return NotFound();

            var viewModel = await _staffService.GetPharmacistForEditAsync(id.Value);
            return viewModel == null ? NotFound() : View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPharmacist(int id, PharmacistEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _staffService.UpdatePharmacistAsync(model);
                    if (!result.Success)
                        return NotFound();

                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Pharmacists));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating pharmacist");
                    ModelState.AddModelError("", "An error occurred while updating the pharmacist.");
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePharmacist(int id)
        {
            try
            {
                var result = await _staffService.DeletePharmacistAsync(id, User.Identity?.Name);
                TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting pharmacist");
                TempData["ErrorMessage"] = "An error occurred while deleting the pharmacist.";
            }

            return RedirectToAction(nameof(Pharmacists));
        }

        public async Task<IActionResult> PriceList()
        {
            try
            {
                return View(await _staffService.GetSpecializationsAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading price list");
                TempData["ErrorMessage"] = "An error occurred while loading the price list.";
                return View(new List<Specialization>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSpecializationFee(int id, decimal consultationFee)
        {
            try
            {
                var result = await _staffService.UpdateSpecializationFeeAsync(id, consultationFee);
                TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating specialization fee");
                TempData["ErrorMessage"] = "An error occurred while updating the consultation fee.";
            }

            return RedirectToAction(nameof(PriceList));
        }

        [HttpGet]
        public async Task<IActionResult> RegisterDoctorLeave(int id)
        {
            var doctor = await _staffService.GetDoctorAsync(id);
            if (doctor == null)
            {
                TempData["ErrorMessage"] = "Doctor not found.";
                return RedirectToAction("Doctors");
            }

            return View(new RegisterDoctorLeaveViewModel { DoctorId = doctor.Id, DoctorName = doctor.FullName });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterDoctorLeave(RegisterDoctorLeaveViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (model.EndDate < model.StartDate)
                {
                    ModelState.AddModelError("EndDate", "End date must be after or equal to start date.");
                    return View(model);
                }

                var existingAppointments = await _staffService.CountScheduledAppointmentsAsync(model.DoctorId, model.StartDate, model.EndDate);
                if (existingAppointments > 0)
                {
                    ViewBag.WarningMessage = $"Doctor has {existingAppointments} future appointment(s) during this period.";
                    ViewBag.ShowWarning = true;
                }

                var result = await _leaveService.RegisterDoctorLeaveAsync(new DoctorLeave
                {
                    DoctorId = model.DoctorId,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    LeaveType = model.LeaveType,
                    Notes = model.Notes,
                    Status = LeaveStatus.Approved,
                    CreatedAt = _clock.Now,
                    CreatedBy = User.Identity?.Name
                });

                if (result.Success)
                {
                    TempData["SuccessMessage"] = "Doctor's leave registered successfully.";
                    return RedirectToAction("Doctors");
                }

                TempData["ErrorMessage"] = result.Message;
            }

            return View(model);
        }

        public async Task<IActionResult> ViewDoctorLeaves(int id)
        {
            var viewModel = await _staffService.GetDoctorLeavesAsync(id);
            if (viewModel == null)
            {
                TempData["ErrorMessage"] = "Doctor not found.";
                return RedirectToAction("Doctors");
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveLeaveRequest(int requestId, string notes)
        {
            var result = await _leaveService.ApproveLeaveRequestAsync(requestId, notes);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectLeaveRequest(int requestId, string reason)
        {
            var result = await _leaveService.RejectLeaveRequestAsync(requestId, reason);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction("Dashboard");
        }
    }
}
