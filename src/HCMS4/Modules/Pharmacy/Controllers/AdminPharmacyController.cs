using HCMS4.Controllers;
using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Models;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using HCMS4.Shared.Reporting.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static HCMS4.ViewModels.GenerateInvoiceViewModel;
using System.Security.Claims;

namespace HCMS4.Modules.Pharmacy.Controllers
{
    // Part of the former AdminController; [ControllerName] keeps the /Admin/... URLs.
    [Authorize(Roles = "Admin")]
    [ControllerName("Admin")]
    public class AdminPharmacyController : BaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IDailyReportService _reportService;
        private readonly IPatientService _patientService;
        private readonly IAppointmentService _appointmentService;
        private readonly IPrescriptionService _prescriptionService;
        private readonly ApplicationDbContext _adminContext;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly ILeaveService _leaveService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public AdminPharmacyController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AdminPharmacyController> logger,
            IDailyReportService reportService,
            IPatientService patientService,
            IAppointmentService appointmentService,
            IPrescriptionService prescriptionService,
            INoShowRiskService noShowRiskService,
            ILeaveService leaveService,
            INotificationService notificationService,
            IActivityLogService activityLogService)
            : base(context, logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _reportService = reportService;
            _patientService = patientService;
            _appointmentService = appointmentService;
            _prescriptionService = prescriptionService;
            _adminContext = context;
            _noShowRiskService = noShowRiskService;
            _leaveService = leaveService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
        }

        private bool DrugExists(int id) => _context.Drugs.Any(e => e.Id == id);

        public async Task<IActionResult> PendingPrescriptions()
        {
            try
            {
                var pendingPrescriptions = await _prescriptionService.GetPendingAsync();
                return View(pendingPrescriptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending prescriptions");
                TempData["ErrorMessage"] = "An error occurred while loading pending prescriptions.";
                return View(new List<Prescription>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPrescriptionCompleted(int id)
        {
            var result = await _prescriptionService.MarkAsCompletedAsync(id);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(PendingPrescriptions));
        }

        public async Task<IActionResult> Suppliers()
        {
            try
            {
                var suppliers = await _context.Suppliers
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                return View(suppliers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading suppliers");
                TempData["ErrorMessage"] = "An error occurred while loading suppliers.";
                return View(new List<Supplier>());
            }
        }

        public IActionResult CreateSupplier()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSupplier(Supplier supplier)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingSupplier = await _context.Suppliers
                        .AnyAsync(s => s.Name == supplier.Name && s.IsActive);

                    if (existingSupplier)
                    {
                        ModelState.AddModelError("", "Supplier name already exists. Please use a different name.");
                        return View(supplier);
                    }

                    supplier.CreatedAt = ClinicTime.Now;
                    supplier.IsActive = true;

                    _context.Suppliers.Add(supplier);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Supplier {SupplierName} created by admin {User}",
                        supplier.Name, User.Identity?.Name);
                    TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' added successfully!";
                    return RedirectToAction(nameof(Suppliers));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating supplier");
                    ModelState.AddModelError("", "An error occurred while creating the supplier.");
                }
            }

            return View(supplier);
        }

        public async Task<IActionResult> EditSupplier(int? id)
        {
            if (id == null)
                return NotFound();

            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
                return NotFound();

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSupplier(int id, Supplier supplier)
        {
            if (id != supplier.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingSupplier = await _context.Suppliers
                        .AnyAsync(s => s.Name == supplier.Name && s.IsActive && s.Id != supplier.Id);

                    if (existingSupplier)
                    {
                        ModelState.AddModelError("", "Supplier name already exists. Please use a different name.");
                        return View(supplier);
                    }

                    var existing = await _context.Suppliers.FindAsync(id);
                    if (existing == null)
                        return NotFound();

                    existing.Name = supplier.Name;
                    existing.ContactPerson = supplier.ContactPerson;
                    existing.Phone = supplier.Phone;
                    existing.Email = supplier.Email;
                    existing.Address = supplier.Address;

                    _context.Suppliers.Update(existing);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Supplier {SupplierName} updated by admin {User}",
                        supplier.Name, User.Identity?.Name);
                    TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' updated successfully!";
                    return RedirectToAction(nameof(Suppliers));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(supplier.Id))
                        return NotFound();
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating supplier");
                    ModelState.AddModelError("", "An error occurred while updating the supplier.");
                }
            }

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            try
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.PurchaseRequests)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (supplier == null)
                {
                    TempData["ErrorMessage"] = "Supplier not found.";
                    return RedirectToAction(nameof(Suppliers));
                }

                if (supplier.PurchaseRequests?.Any() == true)
                {
                    supplier.IsActive = false;
                    _context.Suppliers.Update(supplier);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Supplier {SupplierName} deactivated by admin {User}",
                        supplier.Name, User.Identity?.Name);
                    TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' has been deactivated (has associated purchase requests).";
                }
                else
                {
                    _context.Suppliers.Remove(supplier);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Supplier {SupplierName} deleted by admin {User}",
                        supplier.Name, User.Identity?.Name);
                    TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' deleted successfully!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting supplier");
                TempData["ErrorMessage"] = "An error occurred while deleting the supplier.";
            }

            return RedirectToAction(nameof(Suppliers));
        }

        private bool SupplierExists(int id) => _context.Suppliers.Any(e => e.Id == id);

        public async Task<IActionResult> PurchaseRequests(string status = "all")
        {
            try
            {
                IQueryable<PurchaseRequest> query;

                if (status != "all" && Enum.TryParse<PurchaseRequestStatus>(status, true, out var statusEnum))
                {
                    query = _context.PurchaseRequests
                        .Include(pr => pr.Pharmacist)
                            .ThenInclude(p => p.User)
                        .Include(pr => pr.Supplier)
                        .Include(pr => pr.Items)
                        .Where(pr => pr.Status == statusEnum)
                        .OrderByDescending(pr => pr.RequestDate);
                }
                else
                {
                    query = _context.PurchaseRequests
                        .Include(pr => pr.Pharmacist)
                            .ThenInclude(p => p.User)
                        .Include(pr => pr.Supplier)
                        .Include(pr => pr.Items)
                        .OrderByDescending(pr => pr.RequestDate);
                }

                var purchaseRequests = await query.ToListAsync();

                var viewModel = new AdminPurchaseRequestListViewModel
                {
                    PurchaseRequests = purchaseRequests,
                    PendingCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Pending),
                    ApprovedCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Approved),
                    RejectedCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Rejected),
                    OrderedCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Ordered),
                    ReceivedCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Received),
                    SelectedStatus = status
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin purchase requests: {Message}", ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading purchase requests: " + ex.Message;
                return View(new AdminPurchaseRequestListViewModel());
            }
        }

        public async Task<IActionResult> PurchaseRequestDetails(int id)
        {
            try
            {
                var purchaseRequest = await _context.PurchaseRequests
                    .Include(pr => pr.Pharmacist)
                        .ThenInclude(p => p.User)
                    .Include(pr => pr.Supplier)
                    .Include(pr => pr.Items)
                    .FirstOrDefaultAsync(pr => pr.Id == id);

                if (purchaseRequest == null)
                {
                    TempData["ErrorMessage"] = "Purchase request not found.";
                    return RedirectToAction("PurchaseRequests");
                }

                var viewModel = new AdminPurchaseRequestDetailViewModel
                {
                    PurchaseRequest = purchaseRequest,
                    Items = purchaseRequest.Items.ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading purchase request details {Id}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading the purchase request: " + ex.Message;
                return RedirectToAction("PurchaseRequests");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePurchaseRequest(int id)
        {
            try
            {
                var purchaseRequest = await _context.PurchaseRequests
                    .Include(pr => pr.Items)
                    .FirstOrDefaultAsync(pr => pr.Id == id);

                if (purchaseRequest == null)
                {
                    TempData["ErrorMessage"] = "Purchase request not found.";
                    return RedirectToAction("PurchaseRequests");
                }

                if (purchaseRequest.Status != PurchaseRequestStatus.Pending)
                {
                    TempData["ErrorMessage"] = "This request has already been processed.";
                    return RedirectToAction("PurchaseRequests");
                }

                foreach (var item in purchaseRequest.Items)
                {
                    var drug = await _context.Drugs.FindAsync(item.DrugId);
                    if (drug != null)
                    {
                        drug.Quantity += item.Quantity;
                    }
                }

                var currentUser = await _userManager.GetUserAsync(User);
                purchaseRequest.Status = PurchaseRequestStatus.Approved;
                purchaseRequest.ApprovedDate = ClinicTime.Now;
                purchaseRequest.ApprovedBy = currentUser?.UserName;
                purchaseRequest.UpdatedAt = ClinicTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Purchase request {RequestId} approved by admin {AdminUsername}. Inventory updated.",
                    id, currentUser?.UserName);

                TempData["SuccessMessage"] = "Purchase request approved successfully. Drug quantities have been updated.";
                return RedirectToAction("PurchaseRequestDetails", new { id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving purchase request {Id}", id);
                TempData["ErrorMessage"] = "Failed to approve request, please try again later.";
                return RedirectToAction("PurchaseRequests");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPurchaseRequest(int id, string rejectionReason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    TempData["ErrorMessage"] = "Please enter a rejection reason.";
                    return RedirectToAction("PurchaseRequestDetails", new { id = id });
                }

                var purchaseRequest = await _context.PurchaseRequests.FindAsync(id);
                if (purchaseRequest == null)
                {
                    TempData["ErrorMessage"] = "Purchase request not found.";
                    return RedirectToAction("PurchaseRequests");
                }

                if (purchaseRequest.Status != PurchaseRequestStatus.Pending)
                {
                    TempData["ErrorMessage"] = "This request has already been processed.";
                    return RedirectToAction("PurchaseRequests");
                }

                var currentUser = await _userManager.GetUserAsync(User);
                purchaseRequest.Status = PurchaseRequestStatus.Rejected;
                purchaseRequest.RejectionReason = rejectionReason;
                purchaseRequest.UpdatedAt = ClinicTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Purchase request {RequestId} rejected by admin {AdminUsername}. Reason: {Reason}",
                    id, currentUser?.UserName, rejectionReason);

                TempData["SuccessMessage"] = "Purchase request rejected.";
                return RedirectToAction("PurchaseRequestDetails", new { id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting purchase request {Id}", id);
                TempData["ErrorMessage"] = "Failed to reject request, please try again later.";
                return RedirectToAction("PurchaseRequests");
            }
        }
    }
}
