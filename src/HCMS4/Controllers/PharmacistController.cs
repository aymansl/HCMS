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

        public async Task<IActionResult> Drugs(string searchTerm = null, string expiryFilter = null)
        {
            try
            {
                var drugs = await _drugService.GetAllAsync(searchTerm, expiryFilter);

                var today = ClinicTime.Now;
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
                var query = _context.Prescriptions
                    .Include(p => p.Patient)
                        .ThenInclude(pat => pat.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(doc => doc.User)
                    .Include(p => p.PrescriptionItems)
                        .ThenInclude(pi => pi.Drug)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(status) && status != "all")
                {
                    if (Enum.TryParse<PrescriptionStatus>(status, true, out var statusEnum))
                    {
                        query = query.Where(p => p.Status == statusEnum);
                    }
                }

                var prescriptions = await query
                    .OrderByDescending(p => p.PrescriptionDate)
                    .ToListAsync();

                ViewBag.CurrentStatus = status;
                return View(prescriptions);
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
            var currentUser = await _userManager.GetUserAsync(User);
            var result = await _pharmacistService.MarkPrescriptionCompletedAsync(id, currentUser.Id);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(PrescriptionDetails), new { id });
            }

            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(PrescriptionDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> ProcessPrescription(int id)
        {
            try
            {
                _logger.LogInformation("ProcessPrescription called with ID: {Id}", id);

                var prescription = await _context.Prescriptions
                    .Include(p => p.Patient)
                        .ThenInclude(pat => pat.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(doc => doc.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(doc => doc.Specialization)
                    .Include(p => p.PrescriptionItems)
                        .ThenInclude(pi => pi.Drug)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (prescription == null)
                {
                    _logger.LogWarning("Prescription with ID {Id} not found in database", id);

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
                var user = await _userManager.GetUserAsync(User);
                await _activityLogService.LogAsync(
                    "PrescriptionHeld",
                    nameof(Prescription),
                    $"Prescription #{id} was put on hold.",
                    id.ToString(),
                    user?.Id,
                    user?.UserName);
            }

            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DispensePrescription(int id, List<int> selectedItemIds, string dispensingNotes)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var prescription = await _context.Prescriptions
                    .Include(p => p.PrescriptionItems)
                        .ThenInclude(pi => pi.Drug)
                    .Include(p => p.Patient)
                        .ThenInclude(pat => pat.User)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (prescription == null)
                {
                    return Json(new { success = false, message = "Prescription not found." });
                }

                if (prescription.Status != PrescriptionStatus.Pending)
                {
                    return Json(new { success = false, message = "Only pending prescriptions can be dispensed." });
                }

                if (selectedItemIds == null || !selectedItemIds.Any())
                {
                    return Json(new { success = false, message = "Please select at least one medication to dispense." });
                }


              

               

                var selectedItems = prescription.PrescriptionItems
                    .Where(pi => selectedItemIds.Contains(pi.Id))
                    .ToList();

                var insufficientStock = new List<string>();
                var itemsToDispense = new List<PrescriptionItem>();

                foreach (var item in selectedItems)
                {
                    if (item.Drug == null)
                    {
                        insufficientStock.Add($"Item #{item.Id}: Drug not found");
                        continue;
                    }

                    if (item.Drug.Quantity < item.Quantity)
                    {
                        insufficientStock.Add($"{item.Drug.Name}: Required {item.Quantity}, Available {item.Drug.Quantity}");
                    }
                    else
                    {
                        itemsToDispense.Add(item);
                    }
                }

                if (insufficientStock.Any())
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = "Cannot dispense due to insufficient stock:\n" + string.Join("\n", insufficientStock)
                    });
                }

                decimal totalAmount = 0;

                foreach (var item in itemsToDispense)
                {
                    if (item.Drug != null)
                    {
                        item.Drug.Quantity -= item.Quantity;
                        item.Drug.UpdatedAt = ClinicTime.Now;
                        totalAmount += item.Drug.Price * item.Quantity;

                        _logger.LogInformation("Dispensed {Quantity} units of {DrugName} for prescription {PrescriptionId}. New stock: {NewStock}",
                            item.Quantity, item.Drug.Name, prescription.Id, item.Drug.Quantity);
                    }
                }

                decimal taxAmount = totalAmount * 0.05m;
                decimal grandTotal = totalAmount + taxAmount;

                prescription.MedicationTotal = totalAmount;
                prescription.DispensedDate = ClinicTime.Now;
                prescription.DispensedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
                prescription.UpdatedAt = ClinicTime.Now;
                prescription.DispensingNotes = dispensingNotes;

                var allItemsDispensed = prescription.PrescriptionItems.All(pi => selectedItemIds.Contains(pi.Id));

                if (allItemsDispensed)
                {
                    prescription.Status = PrescriptionStatus.Completed;
                    _logger.LogInformation("Prescription {PrescriptionId} fully dispensed and marked as completed", prescription.Id);
                }
                else
                {
                    prescription.Status = PrescriptionStatus.Pending;
                    var dispensedNames = string.Join(", ", itemsToDispense.Select(i => i.Drug.Name));
                    var remainingNames = string.Join(", ", prescription.PrescriptionItems
                        .Where(pi => !selectedItemIds.Contains(pi.Id))
                        .Select(i => i.Drug.Name));

                    prescription.Notes = $"Partially dispensed on {ClinicTime.Now:yyyy-MM-dd HH:mm}. Dispensed: {dispensedNames}. Remaining: {remainingNames}";

                    _logger.LogInformation("Prescription {PrescriptionId} partially dispensed. Items dispensed: {DispensedCount}, Remaining: {RemainingCount}",
                        prescription.Id, itemsToDispense.Count, prescription.PrescriptionItems.Count - itemsToDispense.Count);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var currentUser = await _userManager.GetUserAsync(User);
                var pharmacist = await _context.Pharmacists.FirstOrDefaultAsync(p => p.UserId == currentUser.Id);

                if (pharmacist == null)
                {
                    return Json(new { success = false, message = "Pharmacist profile not found." });
                }
                var invoice = new Invoice
                {
                    PatientId = prescription.PatientId,
                    PrescriptionId = prescription.Id,
                    AppointmentId = prescription.AppointmentId,
                    InvoiceDate = ClinicTime.Now,
                    MedicationTotal = totalAmount,
                    TotalAmount = grandTotal,
                    PaymentStatus = PaymentStatus.Pending,
                    Notes = $"Generated from prescription #{prescription.Id} on {ClinicTime.Now:yyyy-MM-dd HH:mm}",
                    UpdatedAt = ClinicTime.Now,
                    PharmacistId = pharmacist?.Id,
                    DispensedAt = ClinicTime.Now,

                };



                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Invoice #{InvoiceId} created for prescription {PrescriptionId}. Total amount: {GrandTotal:C2}",
                    invoice.Id, prescription.Id, grandTotal);

                var message = allItemsDispensed
                    ? $"Prescription fully dispensed. Invoice #{invoice.Id} created. Total: {grandTotal:C2}"
                    : $"Partially dispensed ({itemsToDispense.Count} items). Invoice #{invoice.Id} created for dispensed items. Total: {grandTotal:C2}";

                return Json(new
                {
                    success = true,
                    message = message,
                    invoiceId = invoice.Id,
                    totalAmount = grandTotal,
                    isPartial = !allItemsDispensed
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error dispensing prescription {PrescriptionId}", id);
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }



        [HttpGet]
        public async Task<IActionResult> PrescriptionDetails(int id)
        {
            try
            {
                var prescription = await _context.Prescriptions
                    .Include(p => p.Patient)
                        .ThenInclude(pat => pat.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(doc => doc.User)
                    .Include(p => p.PrescriptionItems)
                        .ThenInclude(pi => pi.Drug)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (prescription == null)
                {
                    TempData["ErrorMessage"] = "Prescription not found.";
                    return RedirectToAction(nameof(Prescriptions));
                }

                var invoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.PrescriptionId == id);

                var pharmacistNotes = await _context.PharmacistPrescriptionNotes
                    .Include(n => n.Pharmacist)
                        .ThenInclude(p => p.User)
                    .Where(n => n.PrescriptionId == id)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                var reviewRequests = await _context.DoctorReviewRequests
                    .Include(r => r.Pharmacist)
                        .ThenInclude(p => p.User)
                    .Where(r => r.PrescriptionId == id)
                    .OrderByDescending(r => r.RequestDate)
                    .ToListAsync();

                ViewBag.Invoice = invoice;
                ViewBag.PharmacistNotes = pharmacistNotes;
                ViewBag.DoctorReviewRequests = reviewRequests;

                return View(prescription);
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
            var prescription = await _context.Prescriptions
                .Include(p => p.Patient)
                    .ThenInclude(pat => pat.User)
                .Include(p => p.Doctor)
                    .ThenInclude(doc => doc.User)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);

            if (prescription == null)
            {
                TempData["ErrorMessage"] = "Prescription not found.";
                return RedirectToAction(nameof(Prescriptions));
            }

            return View(new PrescriptionReviewRequestFormViewModel
            {
                PrescriptionId = prescription.Id,
                PatientName = prescription.Patient?.User?.FullName ?? "Patient",
                DoctorName = prescription.Doctor?.User?.FullName ?? "Doctor"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestDoctorReview(PrescriptionReviewRequestFormViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var pharmacist = await _context.Pharmacists.FirstOrDefaultAsync(p => p.UserId == currentUser.Id);
            var prescription = await _context.Prescriptions
                .Include(p => p.Doctor)
                .FirstOrDefaultAsync(p => p.Id == model.PrescriptionId);

            if (pharmacist == null || prescription == null)
            {
                TempData["ErrorMessage"] = "Prescription or pharmacist profile not found.";
                return RedirectToAction(nameof(Prescriptions));
            }

            if (!ModelState.IsValid)
            {
                model.PatientName = (await _context.Prescriptions
                    .Include(p => p.Patient)
                        .ThenInclude(pt => pt.User)
                    .Where(p => p.Id == model.PrescriptionId)
                    .Select(p => p.Patient.User.FullName)
                    .FirstOrDefaultAsync()) ?? "Patient";
                model.DoctorName = (await _context.Doctors
                    .Include(d => d.User)
                    .Where(d => d.Id == prescription.DoctorId)
                    .Select(d => d.User.FullName)
                    .FirstOrDefaultAsync()) ?? "Doctor";
                return View(model);
            }

            var request = new DoctorReviewRequest
            {
                PharmacistId = pharmacist.Id,
                PrescriptionId = prescription.Id,
                DoctorId = prescription.DoctorId,
                ReasonForReview = model.ReasonForReview,
                AdditionalComments = model.AdditionalComments,
                SuggestedAlternative = model.SuggestedAlternative,
                RequestDate = ClinicTime.Now,
                Status = ReviewRequestStatus.Pending
            };

            _context.DoctorReviewRequests.Add(request);
            await _context.SaveChangesAsync();

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == prescription.DoctorId);
            if (doctor != null)
            {
                await _notificationService.CreateForUserAsync(
                    doctor.UserId,
                    "Doctor review requested",
                    $"A pharmacist requested review for prescription #{prescription.Id}.",
                    NotificationType.DoctorReview,
                    "/Doctor/DoctorReviewRequests",
                    nameof(DoctorReviewRequest),
                    request.Id);
            }

            await _activityLogService.LogAsync(
                "DoctorReviewRequested",
                nameof(DoctorReviewRequest),
                $"Pharmacist requested doctor review for prescription #{prescription.Id}.",
                request.Id.ToString(),
                currentUser.Id,
                currentUser.UserName);

            TempData["SuccessMessage"] = "Review request has been sent to the doctor.";
            return RedirectToAction(nameof(PrescriptionDetails), new { id = prescription.Id });
        }

        [HttpGet]
        public async Task<IActionResult> AddPrescriptionNote(int prescriptionId)
        {
            var prescription = await _context.Prescriptions
                .Include(p => p.Patient)
                    .ThenInclude(pat => pat.User)
                .Include(p => p.Doctor)
                    .ThenInclude(doc => doc.User)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);

            if (prescription == null)
            {
                TempData["ErrorMessage"] = "Prescription not found.";
                return RedirectToAction(nameof(Prescriptions));
            }

            return View(new PrescriptionNoteFormViewModel
            {
                PrescriptionId = prescription.Id,
                PatientName = prescription.Patient?.User?.FullName ?? "Patient",
                DoctorName = prescription.Doctor?.User?.FullName ?? "Doctor"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPrescriptionNote(PrescriptionNoteFormViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var pharmacist = await _context.Pharmacists.FirstOrDefaultAsync(p => p.UserId == currentUser.Id);
            var prescription = await _context.Prescriptions
                .Include(p => p.Doctor)
                .FirstOrDefaultAsync(p => p.Id == model.PrescriptionId);

            if (pharmacist == null || prescription == null)
            {
                TempData["ErrorMessage"] = "Prescription or pharmacist profile not found.";
                return RedirectToAction(nameof(Prescriptions));
            }

            if (!ModelState.IsValid)
            {
                model.PatientName = (await _context.Prescriptions
                    .Include(p => p.Patient)
                        .ThenInclude(pt => pt.User)
                    .Where(p => p.Id == model.PrescriptionId)
                    .Select(p => p.Patient.User.FullName)
                    .FirstOrDefaultAsync()) ?? "Patient";
                model.DoctorName = (await _context.Doctors
                    .Include(d => d.User)
                    .Where(d => d.Id == prescription.DoctorId)
                    .Select(d => d.User.FullName)
                    .FirstOrDefaultAsync()) ?? "Doctor";
                return View(model);
            }

            var note = new PharmacistPrescriptionNote
            {
                PrescriptionId = prescription.Id,
                PharmacistId = pharmacist.Id,
                NoteText = model.NoteText.Trim(),
                NotifyDoctor = model.NotifyDoctor,
                CreatedAt = ClinicTime.Now
            };

            _context.PharmacistPrescriptionNotes.Add(note);
            await _context.SaveChangesAsync();

            if (model.NotifyDoctor)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == prescription.DoctorId);
                if (doctor != null)
                {
                    await _notificationService.CreateForUserAsync(
                        doctor.UserId,
                        "Prescription note added",
                        $"A pharmacist added a note to prescription #{prescription.Id}.",
                        NotificationType.PrescriptionNote,
                        "/Doctor/PatientRecord",
                        nameof(PharmacistPrescriptionNote),
                        note.Id);
                }
            }

            await _activityLogService.LogAsync(
                "PrescriptionNoteAdded",
                nameof(PharmacistPrescriptionNote),
                $"Pharmacist added a prescription note to prescription #{prescription.Id}.",
                note.Id.ToString(),
                currentUser.Id,
                currentUser.UserName);

            TempData["SuccessMessage"] = "Note added successfully.";
            return RedirectToAction(nameof(PrescriptionDetails), new { id = prescription.Id });
        }

        // GET: Pharmacist/PurchaseRequests
        public async Task<IActionResult> PurchaseRequests(string status = "all")
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var pharmacist = await _context.Pharmacists
                    .FirstOrDefaultAsync(p => p.UserId == currentUser.Id);

                if (pharmacist == null)
                {
                    TempData["ErrorMessage"] = "Pharmacist profile not found.";
                    return RedirectToAction("Dashboard");
                }

                var query = _context.PurchaseRequests
                    .Include(pr => pr.Pharmacist)
                        .ThenInclude(p => p.User)
                    .Include(pr => pr.Supplier)
                    .Include(pr => pr.Items)
                    .Where(pr => pr.PharmacistId == pharmacist.Id)
                    .AsQueryable();

                // Apply status filter
                if (status != "all" && Enum.TryParse<PurchaseRequestStatus>(status, true, out var statusEnum))
                {
                    query = query.Where(pr => pr.Status == statusEnum);
                }

                var purchaseRequests = await query
                    .OrderByDescending(pr => pr.RequestDate)
                    .ToListAsync();

                var viewModel = new PurchaseRequestListViewModel
                {
                    PurchaseRequests = purchaseRequests,
                    TotalCount = purchaseRequests.Count,
                    PendingCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Pending),
                    ApprovedCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Approved),
                    ReceivedCount = purchaseRequests.Count(pr => pr.Status == PurchaseRequestStatus.Received),
                    CurrentFilter = status
                };

                return View(viewModel);
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
                var suppliers = await _context.Suppliers
                    .Where(s => s.IsActive)
                    .Select(s => new SupplierSelectDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        ContactPerson = s.ContactPerson,
                        Phone = s.Phone
                    })
                    .ToListAsync();

                if (!suppliers.Any())
                {
                    TempData["WarningMessage"] = "No suppliers registered. Please contact the administrator to add suppliers.";
                }

                var drugs = await _context.Drugs
                    .Where(d => d.ExpiryDate > ClinicTime.Now)
                    .Select(d => new DrugSelectDto
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Price = d.Price,
                        CurrentStock = d.Quantity,
                        ExpiryStatus = d.ExpiryStatus
                    })
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                var viewModel = new CreatePurchaseRequestViewModel
                {
                    AvailableSuppliers = suppliers,
                    AvailableDrugs = drugs,
                    Items = new List<PurchaseRequestItemDto>()
                };

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
                var currentUser = await _userManager.GetUserAsync(User);
                var pharmacist = await _context.Pharmacists
                    .FirstOrDefaultAsync(p => p.UserId == currentUser.Id);

                if (pharmacist == null)
                {
                    TempData["ErrorMessage"] = "Pharmacist profile not found.";
                    return RedirectToAction("PurchaseRequests");
                }

                if (!model.Items.Any())
                {
                    ModelState.AddModelError("", "Please add at least one item to the purchase request.");
                }

                if (ModelState.IsValid)
                {
                    var requestNumber = $"PO-{ClinicTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";

                    var purchaseRequest = new PurchaseRequest
                    {
                        RequestNumber = requestNumber,
                        PharmacistId = pharmacist.Id,
                        SupplierId = model.SupplierId,
                        RequestDate = ClinicTime.Now,
                        Status = PurchaseRequestStatus.Pending,
                        Notes = model.Notes,
                        TotalAmount = model.Items.Sum(i => i.Quantity * i.UnitPrice),
                        CreatedAt = ClinicTime.Now,
                        UpdatedAt = ClinicTime.Now
                    };

                    _context.PurchaseRequests.Add(purchaseRequest);
                    await _context.SaveChangesAsync();

                    foreach (var item in model.Items)
                    {
                        var purchaseItem = new PurchaseRequestItem
                        {
                            PurchaseRequestId = purchaseRequest.Id,
                            DrugId = item.DrugId,
                            DrugName = item.DrugName,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            Notes = item.Notes
                        };
                        _context.PurchaseRequestItems.Add(purchaseItem);
                    }

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Purchase request {RequestNumber} created by pharmacist {PharmacistName}",
                        requestNumber, pharmacist.User?.FullName);

                    TempData["SuccessMessage"] = $"Purchase request #{requestNumber} created successfully and sent for review!";

                    return RedirectToAction("PurchaseRequestDetails", new { id = purchaseRequest.Id });
                }

                model.AvailableSuppliers = await _context.Suppliers
                    .Where(s => s.IsActive)
                    .Select(s => new SupplierSelectDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        ContactPerson = s.ContactPerson,
                        Phone = s.Phone
                    })
                    .ToListAsync();

                model.AvailableDrugs = await _context.Drugs
                    .Where(d => d.ExpiryDate > ClinicTime.Now)
                    .Select(d => new DrugSelectDto
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Price = d.Price,
                        CurrentStock = d.Quantity,
                        ExpiryStatus = d.ExpiryStatus
                    })
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating purchase request");
                TempData["ErrorMessage"] = $"An error occurred while creating the purchase request: {ex.Message}";
                return RedirectToAction("PurchaseRequests");
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
                        .ThenInclude(i => i.Drug)
                    .FirstOrDefaultAsync(pr => pr.Id == id);

                if (purchaseRequest == null)
                {
                    TempData["ErrorMessage"] = "Purchase request not found.";
                    return RedirectToAction("PurchaseRequests");
                }

                var currentUser = await _userManager.GetUserAsync(User);
                var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
                var isPharmacist = await _userManager.IsInRoleAsync(currentUser, "Pharmacist");

                var viewModel = new PurchaseRequestDetailViewModel
                {
                    PurchaseRequest = purchaseRequest,
                    Items = purchaseRequest.Items.ToList(),
                    CanApprove = isAdmin && purchaseRequest.Status == PurchaseRequestStatus.Pending,
                    CanReceive = isAdmin && purchaseRequest.Status == PurchaseRequestStatus.Ordered
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading purchase request details");
                TempData["ErrorMessage"] = "An error occurred while loading the purchase request.";
                return RedirectToAction("PurchaseRequests");
            }
        }

    }
}
