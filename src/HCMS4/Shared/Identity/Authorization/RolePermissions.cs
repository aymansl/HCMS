namespace HCMS4.Shared.Identity.Authorization
{
    /// <summary>The one place that says which role holds which permission.</summary>
    public static class RolePermissions
    {
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByRole =
            new Dictionary<string, IReadOnlyList<string>>
            {
                [Roles.Admin] = new[]
                {
                    Permissions.Notifications.View,
                    Permissions.Portal.AdminDashboard,
                    Permissions.Staff.Manage,
                    Permissions.Records.ManagePatients,
                    Permissions.Appointments.Manage,
                    Permissions.Pharmacy.Oversee,
                    Permissions.Billing.ManageInvoices,
                    Permissions.Feedback.Manage,
                    Permissions.Feedback.ViewComplaintAttachments,
                },
                [Roles.Doctor] = new[]
                {
                    Permissions.Notifications.View,
                    Permissions.Portal.DoctorDashboard,
                    Permissions.Staff.OwnProfile,
                    Permissions.Records.ViewMedicalRecords,
                    Permissions.Appointments.ViewOwnSchedule,
                    Permissions.Pharmacy.Prescribe,
                    Permissions.Feedback.PublishArticles,
                    Permissions.Lab.CreateRequest,
                },
                [Roles.Patient] = new[]
                {
                    Permissions.Notifications.View,
                    Permissions.Portal.PatientDashboard,
                    Permissions.Lab.BookAppointment,
                    Permissions.Lab.ViewOwnResults,
                    Permissions.Appointments.Book,
                    Permissions.Pharmacy.ViewOwnPrescriptions,
                    Permissions.Billing.ViewOwnInvoices,
                    Permissions.Feedback.Submit,
                    Permissions.Feedback.ViewComplaintAttachments,
                },
                [Roles.Pharmacist] = new[]
                {
                    Permissions.Notifications.View,
                    Permissions.Portal.PharmacistDashboard,
                    Permissions.Pharmacy.Dispense,
                },
                [Roles.LabTechnician] = new[]
                {
                    Permissions.Notifications.View,
                    Permissions.Portal.LabDashboard,
                    Permissions.Lab.ReceiveRequest,
                    Permissions.Lab.ManageTests,
                    Permissions.Lab.EnterResults,
                },
                [Roles.Radiologist] = new[]
                {
                    Permissions.Notifications.View,
                    Permissions.Radiology.ScheduleSession,
                    Permissions.Radiology.WriteReport,
                },
                [Roles.EmergencySupervisor] = new[]
                {
                    Permissions.Notifications.View,
                    Permissions.Emergency.ManageCases,
                },
            };

        /// <summary>Every permission name defined in <see cref="Permissions"/>.</summary>
        public static IReadOnlyList<string> AllPermissions { get; } = typeof(Permissions)
            .GetNestedTypes()
            .SelectMany(t => t.GetFields())
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        public static IReadOnlyList<string> RolesWith(string permission) =>
            ByRole.Where(pair => pair.Value.Contains(permission)).Select(pair => pair.Key).ToList();

        public static bool Has(string role, string permission) =>
            ByRole.TryGetValue(role, out var permissions) && permissions.Contains(permission);
    }
}
