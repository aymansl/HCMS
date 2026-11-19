using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Notifications.Services;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Common;
using HCMS4.Data;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HCMS4.Controllers
{
    [Authorize(Roles = "Pharmacist")]
    public class PharmacistController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PharmacistController> _logger;
        private readonly IDrugService _drugService;
        private readonly IPharmacistService _pharmacistService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public PharmacistController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILogger<PharmacistController> logger,
            IDrugService drugService,
            IPharmacistService pharmacistService,
            INotificationService notificationService,
            IActivityLogService activityLogService)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _drugService = drugService;
            _pharmacistService = pharmacistService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
        }

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var pharmacist = await _context.Pharmacists
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == currentUser.Id);

                var drugs = await _context.Drugs.AsNoTracking().ToListAsync();
                var today = ClinicTime.Now;

                var viewModel = new PharmacistDashboardViewModel
                {
                    TotalDrugs = drugs.Count,
                    ExpiringSoonCount = drugs.Count(d => d.ExpiryDate <= today.AddDays(BusinessRules.DrugExpiryWarningDays) && d.ExpiryDate > today),
                    ExpiredCount = drugs.Count(d => d.ExpiryDate <= today),
                    LowStockCount = drugs.Count(d => d.Quantity < BusinessRules.LowStockThreshold),

                    PendingPrescriptions = await _context.Prescriptions
                        .AsNoTracking()
                        .CountAsync(p => p.Status == PrescriptionStatus.Pending),
                    CompletedPrescriptions = await _context.Prescriptions
                        .AsNoTracking()
                        .CountAsync(p => p.Status == PrescriptionStatus.Completed),

                    ExpiringDrugs = await _drugService.GetExpiringDrugsAsync(),
                    LowStockDrugs = await _drugService.GetLowStockDrugsAsync(),

                    RecentPrescriptions = await _context.Prescriptions
                        .AsNoTracking()
                        .Include(p => p.Patient)
                            .ThenInclude(pat => pat.User)
                        .Include(p => p.Doctor)
                            .ThenInclude(doc => doc.User)
                        .OrderByDescending(p => p.PrescriptionDate)
                        .Take(5)
                        .ToListAsync(),

                    Alerts = new List<string>()
                };

                if (viewModel.ExpiredCount > 0)
                    viewModel.Alerts.Add($"⚠️ {viewModel.ExpiredCount} drug(s) have expired and need to be removed.");

                if (viewModel.ExpiringSoonCount > 0)
                    viewModel.Alerts.Add($"⚠️ {viewModel.ExpiringSoonCount} drug(s) will expire within {BusinessRules.DrugExpiryWarningDays} days.");

                if (viewModel.LowStockCount > 0)
                    viewModel.Alerts.Add($"⚠️ {viewModel.LowStockCount} drug(s) are running low on stock.");

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pharmacist dashboard");
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return View(new PharmacistDashboardViewModel());
            }
        }



    }
}
