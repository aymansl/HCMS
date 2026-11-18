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
    }
}
