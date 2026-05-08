using HCMS4.Data;
using HCMS4.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HCMS4.Controllers
{
    public abstract class BaseController : Controller
    {
        protected readonly ApplicationDbContext _context;
        protected readonly ILogger _logger;

        protected BaseController(ApplicationDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        protected async Task<int?> GetCurrentPatientIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return null;

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            return patient?.Id;
        }

        protected async Task<int?> GetCurrentDoctorIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return null;

            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(d => d.UserId == userId);

            return doctor?.Id;
        }

        protected async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                return null;

            return await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == userName || u.Email == userName);
        }

        //protected IActionResult HandleError(Exception ex, string action, string successMessage = null)
        //{
        //    _logger.LogError(ex, "Error in {Action}", action);
        //    TempData["ErrorMessage"] = "An error occurred. Please try again.";
        //    return RedirectToAction(action);
        //}

        //protected IActionResult HandleErrorWithView(Exception ex, string operation)
        //{
        //    _logger.LogError(ex, "Error during {Operation}", operation);
        //    TempData["ErrorMessage"] = "An error occurred. Please try again.";
        //    return View();
        //}

        //protected IActionResult HandleNotFound(string message = "The requested resource was not found.")
        //{
        //    TempData["ErrorMessage"] = message;
        //    return NotFound();
        //}

        //protected IActionResult HandleSuccess(string message)
        //{
        //    TempData["SuccessMessage"] = message;
        //    return RedirectToAction(nameof(Index));
        //}

        //protected void AddErrors(IdentityResult result)
        //{
        //    foreach (var error in result.Errors)
        //    {
        //        ModelState.AddModelError(string.Empty, error.Description);
        //    }
        //}

        //protected void AddError(string message)
        //{
        //    ModelState.AddModelError(string.Empty, message);
        //}
    }
}
