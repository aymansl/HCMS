# Quick Testing Guide - New Features

## 🔐 Admin Login Credentials
- **Email:** admin@gmail.com
- **Password:** SecureP@ssw0rd123

---

## 📍 URLs for Testing

### Supplier Management

| Action | URL | Method |
|--------|-----|--------|
| List Suppliers | `/Admin/Suppliers` | GET |
| Create Supplier | `/Admin/CreateSupplier` | GET |
| Create Supplier (Submit) | `/Admin/CreateSupplier` | POST |
| Edit Supplier | `/Admin/EditSupplier/{id}` | GET |
| Edit Supplier (Submit) | `/Admin/EditSupplier/{id}` | POST |
| Delete Supplier | `/Admin/DeleteSupplier` | POST |

### Price List Management

| Action | URL | Method |
|--------|-----|--------|
| View Price List | `/Admin/PriceList` | GET |
| Update Fee | `/Admin/UpdateSpecializationFee` | POST |

---

## ✅ Test Scenarios

### Scenario 1: Add New Supplier (UC-A-04 / T-25-1)
1. Navigate to `/Admin/Suppliers`
2. Click "Add New Supplier" button
3. Fill in the form:
   - Supplier Name: "PharmaCorp Inc."
   - Contact Person: "John Smith"
   - Phone: "123-456-7890"
   - Email: "contact@pharmacorp.com"
   - Address: "123 Main St, City"
4. Click "Save Supplier"
5. **Expected:** Success message, redirect to suppliers list

### Scenario 2: Duplicate Supplier Name (UC-A-04 / T-25-2)
1. Navigate to `/Admin/CreateSupplier`
2. Enter a supplier name that already exists
3. Fill other fields
4. Click "Save Supplier"
5. **Expected:** Error message "Supplier name already exists. Please use a different name." -- NO ERROR MESSAGE IS SHOWING


### Scenario 3: Delete Supplier with Confirmation (UC-A-05 / T-26-1)
1. Navigate to `/Admin/Suppliers`
2. Click delete icon on a supplier
3. Confirm deletion in modal dialog
4. **Expected:** Success message, supplier removed or deactivated

### Scenario 4: Cancel Supplier Deletion (UC-A-05 / T-26-2)
1. Navigate to `/Admin/Suppliers`
2. Click delete icon on a supplier
3. Click "Cancel" in modal dialog
4. **Expected:** Modal closes, supplier remains in list

### Scenario 5: View Supplier List (UC-A-06 / T-27-1)
1. Navigate to `/Admin/Suppliers`
2. **Expected:** Table displays all suppliers with columns:
   - ID, Name, Contact Person, Phone, Email, Address, Actions
3. Statistics cards show: Total, Active, With Email counts

### Scenario 6: Empty Supplier List (UC-A-06 / T-27-2)
1. Delete all suppliers (or use fresh database)
2. Navigate to `/Admin/Suppliers`
3. **Expected:** Message "No Suppliers Found" with "Add First Supplier" button

### Scenario 7: Update Consultation Fee (UC-A-07 / T-28-1)
1. Navigate to `/Admin/PriceList`
2. Click edit icon on any specialization
3. Enter a new valid fee (e.g., 200.00)
4. Click "Update Fee"
5. **Expected:** Success message, updated fee displayed

### Scenario 8: Enter Negative Price (UC-A-07 / T-28-2)
1. Navigate to `/Admin/PriceList`
2. Click edit icon on any specialization
3. Enter a negative or zero value (e.g., -50 or 0)
4. Click "Update Fee"
5. **Expected:** Error message "Please enter a valid price greater than zero"

---

## 🎯 Quick Access from Dashboard

After logging in as admin, the Dashboard now includes quick link cards:

1. **Manage Suppliers** (Blue card) → Direct link to `/Admin/Suppliers`
2. **Price List** (Green card) → Direct link to `/Admin/PriceList`
3. **Drug Inventory** (Cyan card) → Direct link to `/Admin/Drugs`
4. **Daily Report** (Yellow card) → Direct link to `/Admin/DailyPharmacyReport`

---

## 📋 Verification Checklist

- [ ] Can create new supplier with all fields
- [ ] Duplicate supplier name validation works
- [ ] Can edit existing supplier
- [ ] Delete confirmation modal appears
- [ ] Can cancel deletion
- [ ] Can confirm deletion
- [ ] Suppliers with purchase requests are deactivated (not deleted)
- [ ] Supplier list displays all active suppliers
- [ ] Empty state message shows when no suppliers
- [ ] Statistics cards show correct counts
- [ ] Price list displays all specializations
- [ ] Can update consultation fee
- [ ] Negative/zero price validation works
- [ ] Success/error messages display correctly
- [ ] Navigation cards appear on dashboard
- [ ] All actions are logged in application logs

---

**All features are ready for testing! 🚀**
