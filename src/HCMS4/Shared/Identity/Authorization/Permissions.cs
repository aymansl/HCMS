namespace HCMS4.Shared.Identity.Authorization
{
    /// <summary>
    /// Permission names, used as policy names: <c>[Authorize(Policy = Permissions.Pharmacy.Dispense)]</c>.
    /// Which roles hold each permission is defined in one place: <see cref="RolePermissions"/>.
    /// </summary>
    public static class Permissions
    {
        public static class Portal
        {
            public const string AdminDashboard = "Portal.AdminDashboard";
            public const string DoctorDashboard = "Portal.DoctorDashboard";
            public const string PatientDashboard = "Portal.PatientDashboard";
            public const string PharmacistDashboard = "Portal.PharmacistDashboard";
            public const string LabDashboard = "Portal.LabDashboard";
        }

        public static class Staff
        {
            /// <summary>Doctors, pharmacists, specializations and leave approval.</summary>
            public const string Manage = "Staff.Manage";
            /// <summary>A doctor's own profile and leave requests.</summary>
            public const string OwnProfile = "Staff.OwnProfile";
        }

        public static class Records
        {
            public const string ManagePatients = "Records.ManagePatients";
            /// <summary>Medical records and clinical notes of the doctor's own patients.</summary>
            public const string ViewMedicalRecords = "Records.ViewMedicalRecords";
        }

        public static class Appointments
        {
            public const string Manage = "Appointments.Manage";
            public const string ViewOwnSchedule = "Appointments.ViewOwnSchedule";
            public const string Book = "Appointments.Book";
        }

        public static class Pharmacy
        {
            /// <summary>Suppliers, purchase-request approval and the pending prescription list.</summary>
            public const string Oversee = "Pharmacy.Oversee";
            public const string Prescribe = "Pharmacy.Prescribe";
            public const string ViewOwnPrescriptions = "Pharmacy.ViewOwnPrescriptions";
            /// <summary>Drug inventory, dispensing and purchase requests.</summary>
            public const string Dispense = "Pharmacy.Dispense";
        }

        public static class Billing
        {
            public const string ManageInvoices = "Billing.ManageInvoices";
            public const string ViewOwnInvoices = "Billing.ViewOwnInvoices";
        }

        public static class Feedback
        {
            /// <summary>Complaints review and surveys.</summary>
            public const string Manage = "Feedback.Manage";
            public const string PublishArticles = "Feedback.PublishArticles";
            /// <summary>A patient's ratings, complaints and survey answers.</summary>
            public const string Submit = "Feedback.Submit";
            public const string ViewComplaintAttachments = "Feedback.ViewComplaintAttachments";
        }

        // Reserved for the next increments; no endpoint uses them yet.

        public static class Lab
        {
            public const string ReceiveRequest = "Lab.ReceiveRequest";
            public const string ManageTests = "Lab.ManageTests";
            public const string EnterResults = "Lab.EnterResults";
        }

        public static class Radiology
        {
            public const string ScheduleSession = "Radiology.ScheduleSession";
            public const string WriteReport = "Radiology.WriteReport";
        }

        public static class Emergency
        {
            public const string ManageCases = "Emergency.ManageCases";
        }
    }
}
