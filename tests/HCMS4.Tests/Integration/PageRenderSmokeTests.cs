using System.Globalization;
using System.Net;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;

namespace HCMS4.Tests.Integration
{
    /// <summary>
    /// Opens every main page of each role against one seeded data set. Guards the controller-to-service
    /// refactoring: each page must render (200) without showing an error message.
    /// </summary>
    public class PageRenderSmokeTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private static readonly SemaphoreSlim SeedLock = new(1, 1);
        private static Dictionary<string, string>? _ids;

        private readonly HcmsWebApplicationFactory _factory;

        public PageRenderSmokeTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        public static IEnumerable<object[]> Pages() => new[]
        {
            // Admin
            new object[] { "Admin", "/Admin/Dashboard" },
            new object[] { "Admin", "/Admin/Patients" },
            new object[] { "Admin", "/Admin/PatientDetails/{patientId}" },
            new object[] { "Admin", "/Admin/EditPatient/{patientId}" },
            new object[] { "Admin", "/Admin/DisablePatient/{patientId}" },
            new object[] { "Admin", "/Admin/ManageAppointments" },
            new object[] { "Admin", "/Admin/BookAppointment" },
            new object[] { "Admin", "/Admin/RescheduleAppointment/{scheduledId}" },
            new object[] { "Admin", "/Admin/DoctorSchedule/{doctorId}" },
            new object[] { "Admin", "/Admin/PendingPrescriptions" },
            new object[] { "Admin", "/Admin/Suppliers" },
            new object[] { "Admin", "/Admin/EditSupplier/{supplierId}" },
            new object[] { "Admin", "/Admin/PurchaseRequests" },
            new object[] { "Admin", "/Admin/PurchaseRequestDetails/{purchaseRequestId}" },
            new object[] { "Admin", "/Admin/ManageInvoices" },
            new object[] { "Admin", "/Admin/ManageInvoices?Status=Pending&Search=a&From=2020-01-01&To=2099-01-01" },
            new object[] { "Admin", "/Admin/GenerateInvoice?patientId={patientId}" },
            new object[] { "Admin", "/Admin/ViewInvoice/{invoiceId}" },
            new object[] { "Admin", "/Admin/InvoiceDetails/{invoiceId}" },
            new object[] { "Admin", "/Admin/UpdatePaymentStatus/{invoiceId}" },
            new object[] { "Admin", "/Admin/DailyPharmacyReport" },
            new object[] { "Admin", "/Admin/Complaints" },
            new object[] { "Admin", "/Admin/ComplaintDetails/{complaintId}" },
            new object[] { "Admin", "/Admin/Surveys" },
            new object[] { "Admin", "/Admin/CreateSurvey" },
            new object[] { "Admin", "/Admin/LabTechnicians" },
            new object[] { "Admin", "/Admin/CreateLabTechnician" },
            // Lab technician
            new object[] { "LabTechnician", "/Lab/Dashboard" },
            new object[] { "LabTechnician", "/Lab/Tests" },
            new object[] { "LabTechnician", "/Lab/CreateTest" },
            new object[] { "LabTechnician", "/Lab/EditTest/{labTestId}" },
            new object[] { "LabTechnician", "/Lab/RequestDetails/{labRequestId}" },
            new object[] { "LabTechnician", "/Lab/Requests" },
            new object[] { "LabTechnician", "/Lab/Schedule" },
            new object[] { "LabTechnician", "/Lab/PatientHistory" },
            new object[] { "LabTechnician", "/Lab/IssuedResults" },
            new object[] { "LabTechnician", "/Lab/IssuedResults?PatientName=test&TestId={labTestId}&Date=2026-11-19" },
            new object[] { "LabTechnician", "/Lab/ResultReport/{issuedLabRequestId}" },
            new object[] { "LabTechnician", "/Lab/PatientHistory?term=test" },
            new object[] { "LabTechnician", "/Lab/PatientHistory?patientId={patientId}" },
            new object[] { "LabTechnician", "/Lab/PatientHistory?patientId={patientId}&testId={labTestId}" },
            new object[] { "LabTechnician", "/Lab/EnterResults/{labRequestId}" },
            new object[] { "LabTechnician", "/Lab/EnterResults/{receivedLabRequestId}" },
            new object[] { "LabTechnician", "/Lab/EnterResults/{issuedLabRequestId}" },
            new object[] { "LabTechnician", "/Lab/RequestDetails/{issuedLabRequestId}" },
            new object[] { "Doctor", "/Doctor/LabResult/{issuedLabRequestId}" },
            new object[] { "Doctor", "/Doctor/LabRequestDetails/{receivedLabRequestId}" },
            new object[] { "Doctor", "/Doctor/LabRequestDetails/{cancelledLabRequestId}" },
            new object[] { "LabTechnician", "/Lab/RequestDetails/{receivedLabRequestId}" },
            new object[] { "Patient", "/Patient/LabResult/{issuedLabRequestId}" },
            new object[] { "LabTechnician", "/Lab/Schedule?date=2026-11-21" },
            new object[] { "LabTechnician", "/Lab/Requests?Status=Requested&Date=2026-11-19&PatientName=test" },
            // Doctor
            new object[] { "Doctor", "/Doctor/Dashboard" },
            new object[] { "Doctor", "/Doctor/ViewAppointments" },
            new object[] { "Doctor", "/Doctor/RescheduleAppointment/{scheduledId}" },
            new object[] { "Doctor", "/Doctor/DailyVisitingPatientsReport?date={visitDate}" },
            new object[] { "Doctor", "/Doctor/PatientRecord?patientId={patientId}" },
            new object[] { "Doctor", "/Doctor/CreateClinicalNote?patientId={patientId}" },
            new object[] { "Doctor", "/Doctor/CreatePrescription?patientId={patientId}" },
            new object[] { "Doctor", "/Doctor/LabRequests" },
            new object[] { "Doctor", "/Doctor/LabRequests?patientId={patientId}" },
            new object[] { "Doctor", "/Doctor/CreateLabRequest?patientId={patientId}" },
            new object[] { "Doctor", "/Doctor/EditLabRequest/{returnedLabRequestId}" },
            new object[] { "Doctor", "/Doctor/CancelLabRequest/{labRequestId}" },
            new object[] { "LabTechnician", "/Lab/RequestDetails/{cancelledLabRequestId}" },
            new object[] { "Doctor", "/Doctor/ReissueRequests" },
            new object[] { "Doctor", "/Doctor/DoctorReviewRequests" },
            new object[] { "Doctor", "/Doctor/MedicalArticles" },
            new object[] { "Doctor", "/Doctor/CreateMedicalArticle" },
            new object[] { "Doctor", "/Doctor/Profile" },
            // Patient
            new object[] { "Patient", "/Patient/Dashboard" },
            new object[] { "Patient", "/Patient/BookAppointment" },
            new object[] { "Patient", "/Patient/ViewAppointmentHistory" },
            new object[] { "Patient", "/Patient/RateVisit?appointmentId={completedId}" },
            new object[] { "Patient", "/Patient/Prescriptions" },
            new object[] { "Patient", "/Patient/PrescriptionDetails/{completedPrescriptionId}" },
            new object[] { "Patient", "/Patient/InvoiceHistory" },
            new object[] { "Patient", "/Patient/InvoiceDetails/{invoiceId}" },
            new object[] { "Patient", "/Patient/Complaints" },
            new object[] { "Patient", "/Patient/CreateComplaint" },
            new object[] { "Patient", "/Patient/Surveys" },
            new object[] { "Patient", "/Patient/LabRequests" },
            new object[] { "Patient", "/Patient/BookLabAppointment?requestId={labRequestId}" },
            // Pharmacist
            new object[] { "Pharmacist", "/Pharmacist/Dashboard" },
            new object[] { "Pharmacist", "/Pharmacist/Drugs" },
            new object[] { "Pharmacist", "/Pharmacist/Prescriptions" },
            new object[] { "Pharmacist", "/Pharmacist/ProcessPrescription/{pendingPrescriptionId}" },
            new object[] { "Pharmacist", "/Pharmacist/PrescriptionDetails/{completedPrescriptionId}" },
            new object[] { "Pharmacist", "/Pharmacist/RequestDoctorReview?prescriptionId={pendingPrescriptionId}" },
            new object[] { "Pharmacist", "/Pharmacist/AddPrescriptionNote?prescriptionId={pendingPrescriptionId}" },
            new object[] { "Pharmacist", "/Pharmacist/PurchaseRequests" },
            new object[] { "Pharmacist", "/Pharmacist/CreatePurchaseRequest" },
            new object[] { "Pharmacist", "/Pharmacist/PurchaseRequestDetails/{purchaseRequestId}" },
            // Account pages for the two roles with extra profile data
            new object[] { "Doctor", "/Account/Profile" },
            new object[] { "Doctor", "/Account/EditProfile" },
            new object[] { "Patient", "/Account/Profile" },
            new object[] { "Patient", "/Account/EditProfile" },
        };

        [Theory]
        [MemberData(nameof(Pages))]
        public async Task Page_Renders_WithoutError(string role, string pathTemplate)
        {
            var ids = await SeedOnceAsync();
            var path = ids.Aggregate(pathTemplate, (current, pair) => current.Replace("{" + pair.Key + "}", pair.Value));
            var client = _factory.CreateClientAs(role, ids[role + "UserId"]);

            var response = await client.GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();

            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"{role} {path} returned {(int)response.StatusCode} {response.Headers.Location}");
            Assert.DoesNotContain("error occurred", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Error loading", body, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<Dictionary<string, string>> SeedOnceAsync()
        {
            await SeedLock.WaitAsync();
            try
            {
                if (_ids != null)
                    return _ids;

                var ids = new Dictionary<string, string> { ["AdminUserId"] = "smoke-admin", ["LabTechnicianUserId"] = "smoke-lab" };
                await _factory.WithDbAsync(async db =>
                {
                    var (doctorUser, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                    var (patientUser, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                    var (pharmacistUser, pharmacist) = await TestDbContextFactory.CreateTestPharmacistAsync(db);
                    var supplier = await TestDbContextFactory.CreateTestSupplierAsync(db);
                    var drug = await TestDbContextFactory.CreateTestDrugAsync(db);
                    var now = ClinicTime.Now;

                    var completed = new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = now.Date.AddDays(-1).AddHours(10), Status = AppointmentStatus.Completed, ConsultationFee = 50 };
                    var scheduled = new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = now.Date.AddDays(3).AddHours(10), Status = AppointmentStatus.Scheduled, ConsultationFee = 50 };
                    db.Appointments.AddRange(completed, scheduled);
                    await db.SaveChangesAsync();

                    var pending = new Prescription { DoctorId = doctor.Id, PatientId = patient.Id, Status = PrescriptionStatus.Pending, PrescriptionItems = { new PrescriptionItem { DrugId = drug.Id, DrugName = drug.Name, Quantity = 1, Dosage = "1", Duration = "5", Frequency = "daily" } } };
                    var done = new Prescription { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentId = completed.Id, Status = PrescriptionStatus.Completed, MedicationTotal = 25, PrescriptionItems = { new PrescriptionItem { DrugId = drug.Id, DrugName = drug.Name, Quantity = 1, Dosage = "1", Duration = "5", Frequency = "daily" } } };
                    db.Prescriptions.AddRange(pending, done);
                    await db.SaveChangesAsync();

                    var invoice = new Invoice { PatientId = patient.Id, AppointmentId = completed.Id, PrescriptionId = done.Id, InvoiceDate = now, ConsultationFee = 50, MedicationTotal = 25, TotalAmount = 75, PaymentStatus = PaymentStatus.Pending };
                    var complaint = new Complaint { PatientId = patient.Id, Title = "Wait", Description = "Long wait", TrackingNumber = "CMP-SMOKE", SubmissionDate = now };
                    var purchaseRequest = new PurchaseRequest { RequestNumber = "PO-SMOKE", PharmacistId = pharmacist.Id, SupplierId = supplier.Id, Items = { new PurchaseRequestItem { DrugId = drug.Id, DrugName = drug.Name, Quantity = 5, UnitPrice = 2 } } };
                    db.Invoices.Add(invoice);
                    db.Complaints.Add(complaint);
                    db.PurchaseRequests.Add(purchaseRequest);
                    await db.SaveChangesAsync();

                    var labTest = new LabTest { Name = "CBC-SMOKE", Unit = "10^9/L", ReferenceMin = 4, ReferenceMax = 11, Price = 5000 };
                    var labRequest = new LabRequest { PatientId = patient.Id, DoctorId = doctor.Id, Items = { new LabRequestItem { LabTest = labTest, Price = 5000 } } };
                    var returnedLabRequest = new LabRequest { PatientId = patient.Id, DoctorId = doctor.Id, Status = LabRequestStatus.ReturnedToDoctor, ReturnNote = "Missing info", Items = { new LabRequestItem { LabTest = labTest, Price = 5000 } } };
                    var cancelledLabRequest = new LabRequest { PatientId = patient.Id, DoctorId = doctor.Id, Status = LabRequestStatus.Cancelled, CancelledAt = now, CancelReason = "Not needed", Items = { new LabRequestItem { LabTest = labTest, Price = 5000 } } };
                    var receivedLabRequest = new LabRequest { PatientId = patient.Id, DoctorId = doctor.Id, Status = LabRequestStatus.Received, Items = { new LabRequestItem { LabTest = labTest, Price = 5000 } } };
                    var issuedLabRequest = new LabRequest
                    {
                        PatientId = patient.Id, DoctorId = doctor.Id, Status = LabRequestStatus.ResultIssued, ResultIssuedAt = now, HasCriticalResult = true,
                        Items = { new LabRequestItem { LabTest = labTest, Price = 5000, Result = new LabResult { NumericValue = 30, IsAbnormal = true, IsCritical = true } } }
                    };
                    db.LabRequests.AddRange(labRequest, returnedLabRequest, cancelledLabRequest, receivedLabRequest, issuedLabRequest);
                    await db.SaveChangesAsync();

                    ids["DoctorUserId"] = doctorUser.Id;
                    ids["PatientUserId"] = patientUser.Id;
                    ids["PharmacistUserId"] = pharmacistUser.Id;
                    ids["doctorId"] = doctor.Id.ToString(CultureInfo.InvariantCulture);
                    ids["patientId"] = patient.Id.ToString(CultureInfo.InvariantCulture);
                    ids["supplierId"] = supplier.Id.ToString(CultureInfo.InvariantCulture);
                    ids["completedId"] = completed.Id.ToString(CultureInfo.InvariantCulture);
                    ids["scheduledId"] = scheduled.Id.ToString(CultureInfo.InvariantCulture);
                    ids["visitDate"] = completed.AppointmentDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    ids["pendingPrescriptionId"] = pending.Id.ToString(CultureInfo.InvariantCulture);
                    ids["completedPrescriptionId"] = done.Id.ToString(CultureInfo.InvariantCulture);
                    ids["invoiceId"] = invoice.Id.ToString(CultureInfo.InvariantCulture);
                    ids["complaintId"] = complaint.Id.ToString(CultureInfo.InvariantCulture);
                    ids["purchaseRequestId"] = purchaseRequest.Id.ToString(CultureInfo.InvariantCulture);
                    ids["labTestId"] = labTest.Id.ToString(CultureInfo.InvariantCulture);
                    ids["labRequestId"] = labRequest.Id.ToString(CultureInfo.InvariantCulture);
                    ids["returnedLabRequestId"] = returnedLabRequest.Id.ToString(CultureInfo.InvariantCulture);
                    ids["cancelledLabRequestId"] = cancelledLabRequest.Id.ToString(CultureInfo.InvariantCulture);
                    ids["receivedLabRequestId"] = receivedLabRequest.Id.ToString(CultureInfo.InvariantCulture);
                    ids["issuedLabRequestId"] = issuedLabRequest.Id.ToString(CultureInfo.InvariantCulture);
                });

                _ids = ids;
                return ids;
            }
            finally
            {
                SeedLock.Release();
            }
        }
    }
}
