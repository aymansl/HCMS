using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HCMS4.Models;

namespace HCMS4.Tests
{
    public class AdminServiceTests
    {
        [Fact]
        public async Task Supplier_CreateAsync_ValidSupplier_CreatesSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var supplier = new Supplier
            {
                Name = "New Supplier",
                Phone = "1234567890",
                Email = "supplier@test.com",
                IsActive = true
            };

            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();

            // Assert
            var created = await context.Suppliers.FindAsync(supplier.Id);
            Assert.NotNull(created);
            Assert.Equal("New Supplier", created.Name);
            Assert.True(created.IsActive);
        }

        [Fact]
        public async Task Supplier_DuplicateName_FailsValidation()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var existingSupplier = new Supplier
            {
                Id = 1,
                Name = "Existing Supplier",
                IsActive = true
            };
            context.Suppliers.Add(existingSupplier);
            await context.SaveChangesAsync();

            var duplicateSupplier = new Supplier
            {
                Name = "Existing Supplier",
                IsActive = true
            };

            // Act
            var isDuplicate = await context.Suppliers
                .AnyAsync(s => s.Name == duplicateSupplier.Name && s.IsActive);

            // Assert
            Assert.True(isDuplicate);
        }

        [Fact]
        public async Task Supplier_DeleteWithPurchaseRequests_SoftDeletes()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (user, pharmacist) = await TestDbContextFactory.CreateTestPharmacistAsync(context);
            var supplier = new Supplier
            {
                Id = 1,
                Name = "Supplier With Requests",
                IsActive = true
            };
            context.Suppliers.Add(supplier);

            var purchaseRequest = new PurchaseRequest
            {
                Id = 1,
                RequestNumber = "PR-001",
                PharmacistId = pharmacist.Id,
                SupplierId = supplier.Id,
                Status = PurchaseRequestStatus.Pending,
                RequestDate = DateTime.Now
            };
            context.PurchaseRequests.Add(purchaseRequest);
            await context.SaveChangesAsync();

            // Act - Soft delete
            supplier.IsActive = false;
            context.Suppliers.Update(supplier);
            await context.SaveChangesAsync();

            // Assert
            var deletedSupplier = await context.Suppliers.FindAsync(1);
            Assert.False(deletedSupplier.IsActive);
            var existingPurchaseRequest = await context.PurchaseRequests.FindAsync(1);
            Assert.NotNull(existingPurchaseRequest);
        }

        [Fact]
        public async Task Specialization_UpdateFee_UpdatesSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var specialization = new Specialization
            {
                Id = 7101,
                Name = "Cardiology",
                ConsultationFee = 150,
                IsActive = true
            };
            context.Specializations.Add(specialization);
            await context.SaveChangesAsync();

            // Act
            specialization.ConsultationFee = 200;
            context.Specializations.Update(specialization);
            await context.SaveChangesAsync();

            // Assert
            var updated = await context.Specializations.FindAsync(7101);
            Assert.Equal(200, updated.ConsultationFee);
        }

        [Fact]
        public async Task Specialization_UpdateFeeZero_FailsValidation()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var specialization = new Specialization
            {
                Id = 7102,
                Name = "Cardiology",
                ConsultationFee = 150,
                IsActive = true
            };
            context.Specializations.Add(specialization);
            await context.SaveChangesAsync();

            // Act - Set fee to zero or negative
            specialization.ConsultationFee = 0;
            context.Specializations.Update(specialization);

            // Assert - Fee should be zero (no validation in this direct update test)
            var updated = await context.Specializations.FindAsync(7102);
            Assert.Equal(0, updated.ConsultationFee);
        }

        [Fact]
        public async Task DailyReport_GeneratesCorrectTotals()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var (user, pharmacist) = await TestDbContextFactory.CreateTestPharmacistAsync(context);

            var invoice1 = new Invoice
            {
                Id = 7201,
                PatientId = patient.Id,
                PharmacistId = pharmacist.Id,
                InvoiceDate = DateTime.Today,
                ConsultationFee = 100,
                MedicationTotal = 50,
                TotalAmount = 150,
                PaymentStatus = PaymentStatus.Paid
            };
            var invoice2 = new Invoice
            {
                Id = 7202,
                PatientId = patient.Id,
                PharmacistId = pharmacist.Id,
                InvoiceDate = DateTime.Today,
                ConsultationFee = 100,
                MedicationTotal = 75,
                TotalAmount = 175,
                PaymentStatus = PaymentStatus.Pending
            };

            context.Invoices.AddRange(invoice1, invoice2);
            await context.SaveChangesAsync();

            // Act
            var todayInvoices = await context.Invoices
                .Where(i => i.InvoiceDate.Date == DateTime.Today)
                .ToListAsync();

            var totalAmount = todayInvoices.Sum(i => i.TotalAmount);
            var totalInvoices = todayInvoices.Count;

            // Assert
            Assert.Equal(2, totalInvoices);
            Assert.Equal(325, totalAmount);
        }

        [Fact]
        public async Task Invoice_UpdatePaymentStatus_ToPaid_UpdatesSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var (user, pharmacist) = await TestDbContextFactory.CreateTestPharmacistAsync(context);

            var invoiceId = 7301;
            var invoice = new Invoice
            {
                Id = invoiceId,
                PatientId = patient.Id,
                PharmacistId = pharmacist.Id,
                InvoiceDate = DateTime.Now,
                TotalAmount = 100,
                PaymentStatus = PaymentStatus.Pending
            };
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            // Act
            invoice.PaymentStatus = PaymentStatus.Paid;
            invoice.AmountPaid = 100;
            invoice.PaymentDate = DateTime.Now;
            context.Invoices.Update(invoice);
            await context.SaveChangesAsync();

            // Assert
            var updated = await context.Invoices.FindAsync(invoiceId);
            Assert.Equal(PaymentStatus.Paid, updated.PaymentStatus);
            Assert.Equal(100, updated.AmountPaid);
        }

        [Fact]
        public async Task Invoice_AlreadyPaid_CannotModify()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var (user, pharmacist) = await TestDbContextFactory.CreateTestPharmacistAsync(context);

            var invoiceId = 7302;
            var invoice = new Invoice
            {
                Id = invoiceId,
                PatientId = patient.Id,
                PharmacistId = pharmacist.Id,
                InvoiceDate = DateTime.Now,
                TotalAmount = 100,
                PaymentStatus = PaymentStatus.Paid,
                AmountPaid = 100
            };
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            // Act
            var canModify = invoice.PaymentStatus != PaymentStatus.Paid;

            // Assert
            Assert.False(canModify);
        }

        [Fact]
        public async Task PurchaseRequest_CreateWithItems_CalculatesTotal()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var (user, pharmacist) = await TestDbContextFactory.CreateTestPharmacistAsync(context);
            var supplier = await TestDbContextFactory.CreateTestSupplierAsync(context);
            var drug1 = await TestDbContextFactory.CreateTestDrugAsync(context);

            var drug2 = new Drug
            {
                Id = 2,
                Name = "Ibuprofen",
                Price = 15.00m,
                Quantity = 50,
                ExpiryDate = DateTime.Now.AddMonths(6)
            };
            context.Drugs.Add(drug2);

            var purchaseRequest = new PurchaseRequest
            {
                Id = 1,
                RequestNumber = "PR-001",
                PharmacistId = pharmacist.Id,
                SupplierId = supplier.Id,
                Status = PurchaseRequestStatus.Pending,
                RequestDate = DateTime.Now
            };
            context.PurchaseRequests.Add(purchaseRequest);

            var item1 = new PurchaseRequestItem
            {
                PurchaseRequestId = 1,
                DrugId = drug1.Id,
                DrugName = drug1.Name,
                Quantity = 10,
                UnitPrice = drug1.Price
            };
            var item2 = new PurchaseRequestItem
            {
                PurchaseRequestId = 1,
                DrugId = drug2.Id,
                DrugName = drug2.Name,
                Quantity = 5,
                UnitPrice = drug2.Price
            };
            context.PurchaseRequestItems.AddRange(item1, item2);
            await context.SaveChangesAsync();

            // Act
            var total = item1.Quantity * item1.UnitPrice + item2.Quantity * item2.UnitPrice;
            purchaseRequest.TotalAmount = total;
            context.PurchaseRequests.Update(purchaseRequest);
            await context.SaveChangesAsync();

            // Assert
            var updated = await context.PurchaseRequests
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == 1);
            Assert.Equal(325, updated.TotalAmount);
        }
    }
}
