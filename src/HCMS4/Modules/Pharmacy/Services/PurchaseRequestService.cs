using HCMS4.Data;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Pharmacy.Services
{
    /// <summary>Purchase requests: created by pharmacists, approved or rejected by the admin.</summary>
    public interface IPurchaseRequestService
    {
        /// <summary>Newest first; <paramref name="status"/> "all" or a <see cref="PurchaseRequestStatus"/> name.</summary>
        Task<List<PurchaseRequest>> GetAllAsync(string status, int? pharmacistId = null);
        Task<PurchaseRequest?> GetByIdAsync(int id);
        /// <summary>Fills the supplier and drug drop-downs of the create form.</summary>
        Task FillFormOptionsAsync(CreatePurchaseRequestViewModel model);
        Task<ServiceResult<PurchaseRequest>> CreateAsync(int pharmacistId, CreatePurchaseRequestViewModel model);
        /// <summary>Approves a pending request and adds its quantities to the drug stock.</summary>
        Task<ServiceResult> ApproveAsync(int id, string? approvedBy);
        Task<ServiceResult> RejectAsync(int id, string rejectionReason);
    }

    public class PurchaseRequestService : IPurchaseRequestService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISupplierService _supplierService;
        private readonly IClinicClock _clock;

        public PurchaseRequestService(ApplicationDbContext context, ISupplierService supplierService, IClinicClock clock)
        {
            _context = context;
            _supplierService = supplierService;
            _clock = clock;
        }

        public async Task<List<PurchaseRequest>> GetAllAsync(string status, int? pharmacistId = null)
        {
            var query = _context.PurchaseRequests.AsNoTracking()
                .Include(pr => pr.Pharmacist)
                    .ThenInclude(p => p.User)
                .Include(pr => pr.Supplier)
                .Include(pr => pr.Items)
                .AsQueryable();

            if (pharmacistId.HasValue)
                query = query.Where(pr => pr.PharmacistId == pharmacistId.Value);

            if (status != "all" && Enum.TryParse<PurchaseRequestStatus>(status, true, out var statusEnum))
                query = query.Where(pr => pr.Status == statusEnum);

            return await query.OrderByDescending(pr => pr.RequestDate).ToListAsync();
        }

        public Task<PurchaseRequest?> GetByIdAsync(int id) =>
            _context.PurchaseRequests.AsNoTracking()
                .Include(pr => pr.Pharmacist)
                    .ThenInclude(p => p.User)
                .Include(pr => pr.Supplier)
                .Include(pr => pr.Items)
                    .ThenInclude(i => i.Drug)
                .FirstOrDefaultAsync(pr => pr.Id == id);

        public async Task FillFormOptionsAsync(CreatePurchaseRequestViewModel model)
        {
            model.AvailableSuppliers = await _supplierService.GetSelectOptionsAsync();

            var now = _clock.Now;
            var drugs = await _context.Drugs.AsNoTracking()
                .Where(d => d.ExpiryDate > now)
                .OrderBy(d => d.Name)
                .ToListAsync();
            model.AvailableDrugs = drugs.Select(d => new DrugSelectDto
            {
                Id = d.Id,
                Name = d.Name,
                Price = d.Price,
                CurrentStock = d.Quantity,
                ExpiryStatus = d.ExpiryStatus
            }).ToList();
        }

        public async Task<ServiceResult<PurchaseRequest>> CreateAsync(int pharmacistId, CreatePurchaseRequestViewModel model)
        {
            if (!model.Items.Any())
            {
                return ServiceResult<PurchaseRequest>.Fail("Please add at least one item to the purchase request.");
            }

            var now = _clock.Now;
            var purchaseRequest = new PurchaseRequest
            {
                RequestNumber = $"PO-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                PharmacistId = pharmacistId,
                SupplierId = model.SupplierId,
                RequestDate = now,
                Status = PurchaseRequestStatus.Pending,
                Notes = model.Notes,
                TotalAmount = model.Items.Sum(i => i.Quantity * i.UnitPrice),
                CreatedAt = now,
                UpdatedAt = now,
                Items = model.Items.Select(item => new PurchaseRequestItem
                {
                    DrugId = item.DrugId,
                    DrugName = item.DrugName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Notes = item.Notes
                }).ToList()
            };

            _context.PurchaseRequests.Add(purchaseRequest);
            await _context.SaveChangesAsync();
            return ServiceResult<PurchaseRequest>.Ok(purchaseRequest,
                $"Purchase request #{purchaseRequest.RequestNumber} created successfully and sent for review!");
        }

        public async Task<ServiceResult> ApproveAsync(int id, string? approvedBy)
        {
            var purchaseRequest = await _context.PurchaseRequests
                .Include(pr => pr.Items)
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (purchaseRequest == null)
                return ServiceResult.Fail("Purchase request not found.");

            if (purchaseRequest.Status != PurchaseRequestStatus.Pending)
                return ServiceResult.Fail("This request has already been processed.");

            foreach (var item in purchaseRequest.Items)
            {
                var drug = await _context.Drugs.FindAsync(item.DrugId);
                if (drug != null)
                {
                    drug.Quantity += item.Quantity;
                }
            }

            purchaseRequest.Status = PurchaseRequestStatus.Approved;
            purchaseRequest.ApprovedDate = _clock.Now;
            purchaseRequest.ApprovedBy = approvedBy;
            purchaseRequest.UpdatedAt = _clock.Now;
            await _context.SaveChangesAsync();
            return ServiceResult.Ok("Purchase request approved successfully. Drug quantities have been updated.");
        }

        public async Task<ServiceResult> RejectAsync(int id, string rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
                return ServiceResult.Fail("Please enter a rejection reason.");

            var purchaseRequest = await _context.PurchaseRequests.FindAsync(id);
            if (purchaseRequest == null)
                return ServiceResult.Fail("Purchase request not found.");

            if (purchaseRequest.Status != PurchaseRequestStatus.Pending)
                return ServiceResult.Fail("This request has already been processed.");

            purchaseRequest.Status = PurchaseRequestStatus.Rejected;
            purchaseRequest.RejectionReason = rejectionReason;
            purchaseRequest.UpdatedAt = _clock.Now;
            await _context.SaveChangesAsync();
            return ServiceResult.Ok("Purchase request rejected.");
        }
    }
}
