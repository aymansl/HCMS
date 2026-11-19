using HCMS4.Data;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace HCMS4.Tests
{
    // Supplier, purchase-request, dispensing and prescription-request services.
    public class PharmacyWorkflowServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();

        // 2026-11-18 09:00 UTC = 12:00 Damascus.
        private readonly ClinicClock _clock = new(
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero)), ClinicTime.TimeZone);

        private SupplierService Suppliers => new(_db, _clock);
        private PurchaseRequestService PurchaseRequests => new(_db, Suppliers, _clock);
        private DispensingService Dispensing => new(_db, _clock, NullLogger<DispensingService>.Instance);
        private PrescriptionRequestService Requests => new(_db, _clock);

        public void Dispose() => _db.Dispose();

        private async Task<(Prescription prescription, PrescriptionItem first, PrescriptionItem second, Drug drug,
            string pharmacistUserId, string doctorUserId, int patientId, int doctorId)> SeedPrescriptionAsync(
            PrescriptionStatus status = PrescriptionStatus.Pending, DateTime? date = null)
        {
            var (doctorUser, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            var (pharmacistUser, _) = await TestDbContextFactory.CreateTestPharmacistAsync(_db);
            var drug = await TestDbContextFactory.CreateTestDrugAsync(_db); // price 25, stock 100
            var first = new PrescriptionItem { DrugId = drug.Id, DrugName = drug.Name, Quantity = 4 };
            var second = new PrescriptionItem { DrugId = drug.Id, DrugName = drug.Name, Quantity = 2 };
            var prescription = new Prescription
            {
                PatientId = patient.Id, DoctorId = doctor.Id, Status = status,
                PrescriptionDate = date ?? new DateTime(2026, 11, 10),
                PrescriptionItems = { first, second }
            };
            _db.Prescriptions.Add(prescription);
            await _db.SaveChangesAsync();
            return (prescription, first, second, drug, pharmacistUser.Id, doctorUser.Id, patient.Id, doctor.Id);
        }

        // ---- Suppliers ----

        [Fact]
        public async Task Supplier_DuplicateActiveName_IsRejected()
        {
            var existing = await TestDbContextFactory.CreateTestSupplierAsync(_db);

            var result = await Suppliers.CreateAsync(new Supplier { Name = existing.Name, Phone = "1" });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Supplier_Update_ChangesFields_AndUnknownFails()
        {
            var supplier = await TestDbContextFactory.CreateTestSupplierAsync(_db);

            var result = await Suppliers.UpdateAsync(new Supplier { Id = supplier.Id, Name = "Renamed", Phone = "999" });

            Assert.True(result.Success);
            Assert.Equal("Renamed", (await Suppliers.GetByIdAsync(supplier.Id))!.Name);
            Assert.False((await Suppliers.UpdateAsync(new Supplier { Id = 999999, Name = "X" })).Success);
        }

        [Fact]
        public async Task Supplier_Remove_DeactivatesWhenUsed_DeletesOtherwise()
        {
            var used = await TestDbContextFactory.CreateTestSupplierAsync(_db);
            var unused = await TestDbContextFactory.CreateTestSupplierAsync(_db);
            var (_, pharmacist) = await TestDbContextFactory.CreateTestPharmacistAsync(_db);
            _db.PurchaseRequests.Add(new PurchaseRequest { RequestNumber = "PO-1", PharmacistId = pharmacist.Id, SupplierId = used.Id });
            await _db.SaveChangesAsync();

            Assert.True((await Suppliers.RemoveAsync(used.Id)).Success);
            Assert.True((await Suppliers.RemoveAsync(unused.Id)).Success);

            Assert.False((await _db.Suppliers.AsNoTracking().SingleAsync(s => s.Id == used.Id)).IsActive);
            Assert.False(await _db.Suppliers.AnyAsync(s => s.Id == unused.Id));
            Assert.DoesNotContain(await Suppliers.GetActiveAsync(), s => s.Id == used.Id);
        }

        // ---- Purchase requests ----

        [Fact]
        public async Task PurchaseRequest_CreateWithoutItems_Fails()
        {
            var result = await PurchaseRequests.CreateAsync(1, new CreatePurchaseRequestViewModel());

            Assert.False(result.Success);
        }

        [Fact]
        public async Task PurchaseRequest_CreateThenApprove_AddsStock_AndSecondApprovalFails()
        {
            var (_, pharmacist) = await TestDbContextFactory.CreateTestPharmacistAsync(_db);
            var drug = await TestDbContextFactory.CreateTestDrugAsync(_db);
            var created = await PurchaseRequests.CreateAsync(pharmacist.Id, new CreatePurchaseRequestViewModel
            {
                Items = { new PurchaseRequestItemDto { DrugId = drug.Id, DrugName = drug.Name, Quantity = 10, UnitPrice = 2 } }
            });
            Assert.True(created.Success);
            Assert.Equal(20, created.Data.TotalAmount);
            Assert.StartsWith("PO-20261118-", created.Data.RequestNumber);

            var approved = await PurchaseRequests.ApproveAsync(created.Data.Id, "admin");

            Assert.True(approved.Success);
            Assert.Equal(110, (await _db.Drugs.AsNoTracking().SingleAsync(d => d.Id == drug.Id)).Quantity);
            Assert.Equal(PurchaseRequestStatus.Approved, (await PurchaseRequests.GetByIdAsync(created.Data.Id))!.Status);
            Assert.False((await PurchaseRequests.ApproveAsync(created.Data.Id, "admin")).Success);
        }

        [Fact]
        public async Task PurchaseRequest_Reject_NeedsReason_AndFiltersByStatusAndPharmacist()
        {
            var (_, pharmacist) = await TestDbContextFactory.CreateTestPharmacistAsync(_db);
            var (_, other) = await TestDbContextFactory.CreateTestPharmacistAsync(_db);
            var request = new PurchaseRequest { RequestNumber = "PO-A", PharmacistId = pharmacist.Id };
            _db.PurchaseRequests.AddRange(request, new PurchaseRequest { RequestNumber = "PO-B", PharmacistId = other.Id });
            await _db.SaveChangesAsync();

            Assert.False((await PurchaseRequests.RejectAsync(request.Id, " ")).Success);
            Assert.True((await PurchaseRequests.RejectAsync(request.Id, "too expensive")).Success);

            Assert.Single(await PurchaseRequests.GetAllAsync("Rejected"));
            Assert.Equal(2, (await PurchaseRequests.GetAllAsync("all")).Count);
            Assert.Equal("PO-B", Assert.Single(await PurchaseRequests.GetAllAsync("all", other.Id)).RequestNumber);
        }

        // ---- Dispensing ----

        [Fact]
        public async Task Dispense_CompletesPrescription_TakesAllStock_AndCreatesInvoiceWithTax()
        {
            var seed = await SeedPrescriptionAsync();

            var result = await Dispensing.DispenseAsync(seed.prescription.Id, "ok", seed.pharmacistUserId);

            Assert.True(result.Success);
            Assert.Equal(157.5m, result.Data.TotalAmount); // 6 x 25 = 150 + 5% tax
            Assert.Equal(94, (await _db.Drugs.AsNoTracking().SingleAsync(d => d.Id == seed.drug.Id)).Quantity);
            var prescription = await _db.Prescriptions.AsNoTracking().SingleAsync(p => p.Id == seed.prescription.Id);
            Assert.Equal(PrescriptionStatus.Completed, prescription.Status);
            Assert.Equal(new DateTime(2026, 11, 18, 12, 0, 0), prescription.DispensedDate);
            var invoice = await _db.Invoices.AsNoTracking().SingleAsync(i => i.Id == result.Data.InvoiceId);
            Assert.Equal(PaymentStatus.Pending, invoice.PaymentStatus);
            Assert.Equal(150m, invoice.MedicationTotal);
        }

        [Fact]
        public async Task Dispense_Twice_IsRejected_SoStockAndInvoiceAreTakenOnce()
        {
            var seed = await SeedPrescriptionAsync();

            Assert.True((await Dispensing.DispenseAsync(seed.prescription.Id, null, seed.pharmacistUserId)).Success);
            Assert.False((await Dispensing.DispenseAsync(seed.prescription.Id, null, seed.pharmacistUserId)).Success);

            Assert.Single(await _db.Invoices.ToListAsync());
            Assert.Equal(94, (await _db.Drugs.AsNoTracking().SingleAsync(d => d.Id == seed.drug.Id)).Quantity);
        }

        [Fact]
        public async Task Dispense_OneItemShort_DispensesNothing()
        {
            // Stock 3 covers the second item (2) but not both (4 + 2): no partial dispensing.
            var seed = await SeedPrescriptionAsync();
            var drug = await _db.Drugs.SingleAsync(d => d.Id == seed.drug.Id);
            drug.Quantity = 3;
            await _db.SaveChangesAsync();

            var result = await Dispensing.DispenseAsync(seed.prescription.Id, null, seed.pharmacistUserId);

            Assert.False(result.Success);
            Assert.Contains("insufficient stock", result.Message);
            Assert.False(await _db.Invoices.AnyAsync());
            Assert.Equal(3, (await _db.Drugs.AsNoTracking().SingleAsync(d => d.Id == seed.drug.Id)).Quantity);
            Assert.Equal(PrescriptionStatus.Pending, (await _db.Prescriptions.AsNoTracking().SingleAsync(p => p.Id == seed.prescription.Id)).Status);
        }

        [Fact]
        public async Task Dispense_UnknownPharmacist_ChangesNothing()
        {
            var seed = await SeedPrescriptionAsync();

            Assert.False((await Dispensing.DispenseAsync(seed.prescription.Id, null, "no-such-user")).Success);

            Assert.False(await _db.Invoices.AnyAsync());
            Assert.Equal(100, (await _db.Drugs.AsNoTracking().SingleAsync(d => d.Id == seed.drug.Id)).Quantity);
        }

        [Fact]
        public async Task Dispense_CompletedPrescription_IsRejected()
        {
            var seed = await SeedPrescriptionAsync(PrescriptionStatus.Completed);

            Assert.False((await Dispensing.DispenseAsync(seed.prescription.Id, null, seed.pharmacistUserId)).Success);
        }

        [Fact]
        public async Task ReviewRequestAndNote_ReturnDoctorToNotify_AndShowInDetails()
        {
            var seed = await SeedPrescriptionAsync();

            var review = await Dispensing.RequestDoctorReviewAsync(seed.pharmacistUserId, new PrescriptionReviewRequestFormViewModel
            {
                PrescriptionId = seed.prescription.Id, ReasonForReview = default
            });
            var silentNote = await Dispensing.AddNoteAsync(seed.pharmacistUserId, new PrescriptionNoteFormViewModel
            {
                PrescriptionId = seed.prescription.Id, NoteText = "  check dose  ", NotifyDoctor = false
            });

            Assert.Equal(seed.doctorUserId, review.Data.NotifyUserId);
            Assert.Null(silentNote.Data.NotifyUserId);
            var details = await Dispensing.GetDetailsAsync(seed.prescription.Id);
            Assert.Single(details!.ReviewRequests);
            Assert.Equal("check dose", Assert.Single(details.PharmacistNotes).NoteText);
            Assert.False((await Dispensing.AddNoteAsync("no-such-user", new PrescriptionNoteFormViewModel { PrescriptionId = seed.prescription.Id, NoteText = "x" })).Success);
        }

        [Fact]
        public async Task Queue_FiltersByStatus_AndPartiesNameBothSides()
        {
            var seed = await SeedPrescriptionAsync();

            Assert.Single(await Dispensing.GetQueueAsync("pending"));
            Assert.Empty(await Dispensing.GetQueueAsync("Completed"));
            Assert.Single(await Dispensing.GetQueueAsync("all"));
            var parties = await Dispensing.GetPartiesAsync(seed.prescription.Id);
            Assert.NotNull(parties);
            Assert.Null(await Dispensing.GetPartiesAsync(999999));
        }

        // ---- Prescription requests ----

        [Fact]
        public async Task Reissue_OnlyForRecentCompletedPrescriptions_OnePendingAtATime()
        {
            var recent = await SeedPrescriptionAsync(PrescriptionStatus.Completed, new DateTime(2026, 11, 1));
            var pending = await SeedPrescriptionAsync(PrescriptionStatus.Pending);
            var old = await SeedPrescriptionAsync(PrescriptionStatus.Completed, new DateTime(2026, 9, 1));

            var first = await Requests.RequestReissueAsync(recent.patientId, recent.prescription.Id);
            Assert.True(first.Success);
            Assert.Equal(recent.doctorUserId, first.Data.NotifyUserId);
            Assert.False((await Requests.RequestReissueAsync(recent.patientId, recent.prescription.Id)).Success);
            Assert.False((await Requests.RequestReissueAsync(pending.patientId, pending.prescription.Id)).Success);
            Assert.False((await Requests.RequestReissueAsync(old.patientId, old.prescription.Id)).Success);
            Assert.False((await Requests.RequestReissueAsync(old.patientId, recent.prescription.Id)).Success); // not their prescription

            var list = await Requests.GetPatientPrescriptionsAsync(recent.patientId);
            Assert.True(Assert.Single(list).HasPendingReissueRequest);
            var detail = await Requests.GetPatientPrescriptionDetailAsync(recent.patientId, recent.prescription.Id);
            Assert.False(detail!.CanRequestReissue);
            Assert.Null(await Requests.GetPatientPrescriptionDetailAsync(old.patientId, recent.prescription.Id));
        }

        [Fact]
        public async Task Doctor_ReviewsReissueAndAnswersReviewRequest_OnlyForOwnRequests()
        {
            var seed = await SeedPrescriptionAsync(PrescriptionStatus.Completed, new DateTime(2026, 11, 1));
            var reissue = await Requests.RequestReissueAsync(seed.patientId, seed.prescription.Id);
            var review = await Dispensing.RequestDoctorReviewAsync(seed.pharmacistUserId,
                new PrescriptionReviewRequestFormViewModel { PrescriptionId = seed.prescription.Id });

            Assert.False((await Requests.ReviewReissueRequestAsync(seed.doctorId + 1000, reissue.Data.Id, true, null)).Success);
            var approved = await Requests.ReviewReissueRequestAsync(seed.doctorId, reissue.Data.Id, true, "fine");
            Assert.True(approved.Success);
            Assert.Equal(seed.prescription.Id, approved.Data.PrescriptionId);
            Assert.Equal(ReissueRequestStatus.Approved, Assert.Single(await Requests.GetReissueRequestsForDoctorAsync(seed.doctorId)).Status);

            Assert.False((await Requests.RespondToReviewRequestAsync(seed.doctorId, review.Data.Id, " ")).Success);
            var answered = await Requests.RespondToReviewRequestAsync(seed.doctorId, review.Data.Id, " keep it ");
            Assert.True(answered.Success);
            Assert.Equal(seed.pharmacistUserId, answered.Data.NotifyUserId);
            var stored = Assert.Single(await Requests.GetReviewRequestsForDoctorAsync(seed.doctorId));
            Assert.Equal("keep it", stored.DoctorResponse);
            Assert.Equal(ReviewRequestStatus.Responded, stored.Status);
        }
    }
}
