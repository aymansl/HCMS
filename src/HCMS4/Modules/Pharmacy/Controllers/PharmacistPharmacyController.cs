using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Pharmacy.Controllers
{
    // Part of the former PharmacistController; [ControllerName] keeps the /Pharmacist/... URLs.
    [Authorize(Roles = "Pharmacist")]
    [ControllerName("Pharmacist")]
    public class PharmacistPharmacyController : HcmsControllerBase
    {
        private readonly IDrugService _drugService;
        private readonly IPharmacistService _pharmacistService;
        private readonly IDispensingService _dispensingService;
        private readonly IPurchaseRequestService _purchaseRequestService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;
        private readonly IClinicClock _clock;
        private readonly ILogger<PharmacistPharmacyController> _logger;

        public PharmacistPharmacyController(
            IDrugService drugService,
            IPharmacistService pharmacistService,
            IDispensingService dispensingService,
            IPurchaseRequestService purchaseRequestService,
            INotificationService notificationService,
            IActivityLogService activityLogService,
            IClinicClock clock,
            ILogger<PharmacistPharmacyController> logger)
        {
            _drugService = drugService;
            _pharmacistService = pharmacistService;
            _dispensingService = dispensingService;
            _purchaseRequestService = purchaseRequestService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
            _clock = clock;
            _logger = logger;
        }

        public async Task<IActionResult> Drugs(string searchTerm = null, string expiryFilter = null)
        {
            try
            {
                var drugs = await _drugService.GetAllAsync(searchTerm, expiryFilter);

                var today = _clock.Now;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.ExpiryFilter = expiryFilter;
                ViewBag.TotalValue = drugs.Sum(d => d.Price * d.Quantity);
                ViewBag.ExpiredCount = drugs.Count(d => d.ExpiryDate <= today);
                ViewBag.ExpiringSoonCount = drugs.Count(d => d.ExpiryDate > today && d.ExpiryDate <= today.AddDays(BusinessRules.DrugExpiryWarningDays));

                return View(drugs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading drugs list");
                TempData["ErrorMessage"] = "An error occurred while loading the drugs.";
                return View(new List<Drug>());
            }
        }

        // GET: Pharmacist/CreateDrug
        public IActionResult CreateDrug()
        {
            return View();
        }

        // POST: Pharmacist/CreateDrug
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDrug(Drug drug)
        {
            if (ModelState.IsValid)
            {
                var result = await _drugService.CreateAsync(drug);
                if (result.Success)
                {
                    _logger.LogInformation("Drug {DrugName} created by pharmacist {User}",
                        drug.Name, User.Identity?.Name);
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Drugs));
                }

                ModelState.AddModelError("", result.Message);
            }

            return View(drug);
        }

        public async Task<IActionResult> EditDrug(int? id)
        {
            if (id == null)
                return NotFound();

            var drug = await _drugService.GetByIdAsync(id.Value);
            if (drug == null)
                return NotFound();

            return View(drug);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDrug(int id, Drug drug)
        {
            if (id != drug.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                var result = await _drugService.UpdateAsync(drug);
                if (result.Success)
                {
                    _logger.LogInformation("Drug {DrugName} updated by pharmacist {User}",
                        drug.Name, User.Identity?.Name);
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Drugs));
                }

                ModelState.AddModelError("", result.Message);
            }

            return View(drug);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDrug(int id)
        {
            var result = await _drugService.DeleteAsync(id);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Drugs));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStock(int id, int newQuantity)
        {
            var result = await _drugService.UpdateStockAsync(id, newQuantity);
            if (result.Success)
            {
                var messagePrefix = "Warning";
                if (newQuantity == 0)
                    messagePrefix = "Alert";

                TempData[newQuantity < BusinessRules.LowStockThreshold ? "WarningMessage" : "SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Drugs));
        }

        public async Task<IActionResult> Prescriptions(string status = "pending")
        {
            try
            {
                ViewBag.CurrentStatus = status;
                return View(await _dispensingService.GetQueueAsync(status));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prescriptions");
                TempData["ErrorMessage"] = "An error occurred while loading prescriptions.";
                return View(new List<Prescription>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPrescriptionCompleted(int id)
        {
            var result = await _pharmacistService.MarkPrescriptionCompletedAsync(id, CurrentUserId!);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(PrescriptionDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> ProcessPrescription(int id)
        {
            try
            {
                var prescription = await _dispensingService.GetForProcessingAsync(id);
                if (prescription == null)
                {
                    _logger.LogWarning("Prescription with ID {Id} not found", id);
                    TempData["ErrorMessage"] = "Prescription not found.";
                    return RedirectToAction(nameof(Prescriptions));
                }

                if (prescription.Status != PrescriptionStatus.Pending)
                {
                    TempData["ErrorMessage"] = "Only pending prescriptions can be processed.";
                    return RedirectToAction(nameof(Prescriptions));
                }

                return View(prescription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prescription {PrescriptionId} for processing", id);
                TempData["ErrorMessage"] = "An error occurred while loading the prescription.";
                return RedirectToAction(nameof(Prescriptions));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HoldPrescription(int id)
        {
            var result = await _pharmacistService.HoldPrescriptionAsync(id);
            if (result.Success)
            {
                await LogActivityAsync("PrescriptionHeld", nameof(Prescription), $"Prescription #{id} was put on hold.", id);
            }

            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DispensePrescription(int id, List<int> selectedItemIds, string dispensingNotes)
        {
            try
            {
                var result = await _dispensingService.DispenseAsync(id, selectedItemIds ?? new List<int>(), dispensingNotes, CurrentUserId!);
                if (!result.Success)
                {
                    return Json(new { success = false, message = result.Message });
                }

                return Json(new
                {
                    success = true,
                    message = result.Message,
                    invoiceId = result.Data.InvoiceId,
                    totalAmount = result.Data.TotalAmount,
                    isPartial = result.Data.IsPartial
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispensing prescription {PrescriptionId}", id);
                return Json(new { success = false, message = "An error occurred while dispensing the prescription." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> PrescriptionDetails(int id)
        {
            try
            {
                var details = await _dispensingService.GetDetailsAsync(id);
                if (details == null)
                {
                    TempData["ErrorMessage"] = "Prescription not found.";
                    return RedirectToAction(nameof(Prescriptions));
                }

                ViewBag.Invoice = details.Invoice;
                ViewBag.PharmacistNotes = details.PharmacistNotes;
                ViewBag.DoctorReviewRequests = details.ReviewRequests;
                return View(details.Prescription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prescription details {PrescriptionId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading prescription details.";
                return RedirectToAction(nameof(Prescriptions));
            }
        }

        [HttpGet]
        public async Task<IActionResult> RequestDoctorReview(int prescriptionId)
        {
            var parties = await _dispensingService.GetPartiesAsync(prescriptionId);
            if (parties == null)
            {
                TempData["ErrorMessage"] = "Prescription not found.";
                return RedirectToAction(nameof(Prescriptions));
            }

            return View(new PrescriptionReviewRequestFormViewModel
            {
                PrescriptionId = parties.PrescriptionId,
                PatientName = parties.PatientName,
                DoctorName = parties.DoctorName
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestDoctorReview(PrescriptionReviewRequestFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var parties = await _dispensingService.GetPartiesAsync(model.PrescriptionId);
                if (parties == null)
                {
                    TempData["ErrorMessage"] = "Prescription or pharmacist profile not found.";
                    return RedirectToAction(nameof(Prescriptions));
                }

                model.PatientName = parties.PatientName;
                model.DoctorName = parties.DoctorName;
                return View(model);
            }

            var result = await _dispensingService.RequestDoctorReviewAsync(CurrentUserId!, model);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(Prescriptions));
            }

            if (result.Data.NotifyUserId != null)
            {
                await _notificationService.CreateForUserAsync(
                    result.Data.NotifyUserId,
                    "Doctor review requested",
                    $"A pharmacist requested review for prescription #{model.PrescriptionId}.",
                    NotificationType.DoctorReview,
                    "/Doctor/DoctorReviewRequests",
                    "DoctorReviewRequest",
                    result.Data.Id);
            }

            await LogActivityAsync("DoctorReviewRequested", "DoctorReviewRequest",
                $"Pharmacist requested doctor review for prescription #{model.PrescriptionId}.", result.Data.Id);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(PrescriptionDetails), new { id = model.PrescriptionId });
        }

        [HttpGet]
        public async Task<IActionResult> AddPrescriptionNote(int prescriptionId)
        {
            var parties = await _dispensingService.GetPartiesAsync(prescriptionId);
            if (parties == null)
            {
                TempData["ErrorMessage"] = "Prescription not found.";
                return RedirectToAction(nameof(Prescriptions));
            }

            return View(new PrescriptionNoteFormViewModel
            {
                PrescriptionId = parties.PrescriptionId,
                PatientName = parties.PatientName,
                DoctorName = parties.DoctorName
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPrescriptionNote(PrescriptionNoteFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var parties = await _dispensingService.GetPartiesAsync(model.PrescriptionId);
                if (parties == null)
                {
                    TempData["ErrorMessage"] = "Prescription or pharmacist profile not found.";
                    return RedirectToAction(nameof(Prescriptions));
                }

                model.PatientName = parties.PatientName;
                model.DoctorName = parties.DoctorName;
                return View(model);
            }

            var result = await _dispensingService.AddNoteAsync(CurrentUserId!, model);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(Prescriptions));
            }

            if (result.Data.NotifyUserId != null)
            {
                await _notificationService.CreateForUserAsync(
                    result.Data.NotifyUserId,
                    "Prescription note added",
                    $"A pharmacist added a note to prescription #{model.PrescriptionId}.",
                    NotificationType.PrescriptionNote,
                    "/Doctor/PatientRecord",
                    "PharmacistPrescriptionNote",
                    result.Data.Id);
            }

            await LogActivityAsync("PrescriptionNoteAdded", "PharmacistPrescriptionNote",
                $"Pharmacist added a prescription note to prescription #{model.PrescriptionId}.", result.Data.Id);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(PrescriptionDetails), new { id = model.PrescriptionId });
        }

        public async Task<IActionResult> PurchaseRequests(string status = "all")
        {
            try
            {
                var pharmacist = await _pharmacistService.GetByUserIdAsync(CurrentUserId!);
                if (pharmacist == null)
                {
                    TempData["ErrorMessage"] = "Pharmacist profile not found.";
                    return RedirectToAction("Dashboard");
                }

                var purchaseRequests = await _purchaseRequestService.GetAllAsync(status, pharmacist.Id);

                return View(new PurchaseRequestListViewModel
                {
                    PurchaseRequests = purchaseRequests,
                    TotalCount = purchaseRequests.Count,
                    PendingCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Pending),
                    ApprovedCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Approved),
                    ReceivedCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Received),
                    CurrentFilter = status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading purchase requests");
                TempData["ErrorMessage"] = "An error occurred while loading purchase requests.";
                return View(new PurchaseRequestListViewModel());
            }
        }

        public async Task<IActionResult> CreatePurchaseRequest()
        {
            try
            {
                var viewModel = new CreatePurchaseRequestViewModel { Items = new List<PurchaseRequestItemDto>() };
                await _purchaseRequestService.FillFormOptionsAsync(viewModel);

                if (!viewModel.AvailableSuppliers.Any())
                {
                    TempData["WarningMessage"] = "No suppliers registered. Please contact the administrator to add suppliers.";
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create purchase request form");
                TempData["ErrorMessage"] = "An error occurred while loading the form.";
                return RedirectToAction("PurchaseRequests");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePurchaseRequest(CreatePurchaseRequestViewModel model)
        {
            try
            {
                var pharmacist = await _pharmacistService.GetByUserIdAsync(CurrentUserId!);
                if (pharmacist == null)
                {
                    TempData["ErrorMessage"] = "Pharmacist profile not found.";
                    return RedirectToAction("PurchaseRequests");
                }

                if (ModelState.IsValid)
                {
                    var result = await _purchaseRequestService.CreateAsync(pharmacist.Id, model);
                    if (result.Success)
                    {
                        _logger.LogInformation("Purchase request {RequestNumber} created by pharmacist {PharmacistId}",
                            result.Data.RequestNumber, pharmacist.Id);
                        TempData["SuccessMessage"] = result.Message;
                        return RedirectToAction("PurchaseRequestDetails", new { id = result.Data.Id });
                    }

                    ModelState.AddModelError("", result.Message);
                }

                await _purchaseRequestService.FillFormOptionsAsync(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating purchase request");
                TempData["ErrorMessage"] = "An error occurred while creating the purchase request.";
                return RedirectToAction("PurchaseRequests");
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

                var isAdmin = User.IsInRole("Admin");
                return View(new PurchaseRequestDetailViewModel
                {
                    PurchaseRequest = purchaseRequest,
                    Items = purchaseRequest.Items.ToList(),
                    CanApprove = isAdmin && purchaseRequest.Status == PurchaseRequestStatus.Pending,
                    CanReceive = isAdmin && purchaseRequest.Status == PurchaseRequestStatus.Ordered
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading purchase request details");
                TempData["ErrorMessage"] = "An error occurred while loading the purchase request.";
                return RedirectToAction("PurchaseRequests");
            }
        }

        private async Task LogActivityAsync(string action, string entityName, string description, int entityId)
        {
            var user = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(action, entityName, description, entityId.ToString(), user?.Id, user?.UserName);
        }
    }
}
