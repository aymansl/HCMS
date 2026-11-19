using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Pharmacy.Controllers
{
    // Part of the former AdminController; [ControllerName] keeps the /Admin/... URLs.
    [Authorize(Roles = "Admin")]
    [ControllerName("Admin")]
    public class AdminPharmacyController : HcmsControllerBase
    {
        private readonly IPrescriptionService _prescriptionService;
        private readonly ISupplierService _supplierService;
        private readonly IPurchaseRequestService _purchaseRequestService;
        private readonly ILogger<AdminPharmacyController> _logger;

        public AdminPharmacyController(
            IPrescriptionService prescriptionService,
            ISupplierService supplierService,
            IPurchaseRequestService purchaseRequestService,
            ILogger<AdminPharmacyController> logger)
        {
            _prescriptionService = prescriptionService;
            _supplierService = supplierService;
            _purchaseRequestService = purchaseRequestService;
            _logger = logger;
        }

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

        public async Task<IActionResult> Suppliers()
        {
            try
            {
                return View(await _supplierService.GetActiveAsync());
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
                    var result = await _supplierService.CreateAsync(supplier);
                    if (result.Success)
                    {
                        _logger.LogInformation("Supplier {SupplierName} created by admin {User}",
                            supplier.Name, User.Identity?.Name);
                        TempData["SuccessMessage"] = result.Message;
                        return RedirectToAction(nameof(Suppliers));
                    }

                    ModelState.AddModelError("", result.Message);
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

            var supplier = await _supplierService.GetByIdAsync(id.Value);
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
                    if (await _supplierService.GetByIdAsync(id) == null)
                        return NotFound();

                    var result = await _supplierService.UpdateAsync(supplier);
                    if (result.Success)
                    {
                        _logger.LogInformation("Supplier {SupplierName} updated by admin {User}",
                            supplier.Name, User.Identity?.Name);
                        TempData["SuccessMessage"] = result.Message;
                        return RedirectToAction(nameof(Suppliers));
                    }

                    ModelState.AddModelError("", result.Message);
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
                var result = await _supplierService.RemoveAsync(id);
                if (result.Success)
                {
                    _logger.LogInformation("Supplier {SupplierId} removed by admin {User}: {Message}",
                        id, User.Identity?.Name, result.Message);
                }

                TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting supplier");
                TempData["ErrorMessage"] = "An error occurred while deleting the supplier.";
            }

            return RedirectToAction(nameof(Suppliers));
        }

        public async Task<IActionResult> PurchaseRequests(string status = "all")
        {
            try
            {
                var purchaseRequests = await _purchaseRequestService.GetAllAsync(status);

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
                _logger.LogError(ex, "Error loading admin purchase requests");
                TempData["ErrorMessage"] = "An error occurred while loading purchase requests.";
                return View(new AdminPurchaseRequestListViewModel());
            }
        }

        public async Task<IActionResult> PurchaseRequestDetails(int id)
        {
            try
            {
                var purchaseRequest = await _purchaseRequestService.GetByIdAsync(id);
                if (purchaseRequest == null)
                {
                    TempData["ErrorMessage"] = "Purchase request not found.";
                    return RedirectToAction("PurchaseRequests");
                }

                return View(new AdminPurchaseRequestDetailViewModel
                {
                    PurchaseRequest = purchaseRequest,
                    Items = purchaseRequest.Items.ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading purchase request details {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the purchase request.";
                return RedirectToAction("PurchaseRequests");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePurchaseRequest(int id)
        {
            try
            {
                var currentUser = await GetCurrentUserAsync();
                var result = await _purchaseRequestService.ApproveAsync(id, currentUser?.UserName);
                if (!result.Success)
                {
                    TempData["ErrorMessage"] = result.Message;
                    return RedirectToAction("PurchaseRequests");
                }

                _logger.LogInformation("Purchase request {RequestId} approved by admin {AdminUsername}. Inventory updated.",
                    id, currentUser?.UserName);
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("PurchaseRequestDetails", new { id });
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
                    return RedirectToAction("PurchaseRequestDetails", new { id });
                }

                var result = await _purchaseRequestService.RejectAsync(id, rejectionReason);
                if (!result.Success)
                {
                    TempData["ErrorMessage"] = result.Message;
                    return RedirectToAction("PurchaseRequests");
                }

                _logger.LogInformation("Purchase request {RequestId} rejected by admin {AdminUsername}. Reason: {Reason}",
                    id, User.Identity?.Name, rejectionReason);
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("PurchaseRequestDetails", new { id });
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
