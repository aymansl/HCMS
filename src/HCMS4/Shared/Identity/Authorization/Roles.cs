namespace HCMS4.Shared.Identity.Authorization
{
    /// <summary>The seven roles of the system (docs/REQUIREMENTS.md §1).</summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Doctor = "Doctor";
        public const string Patient = "Patient";
        public const string Pharmacist = "Pharmacist";
        public const string LabTechnician = "LabTechnician";
        public const string Radiologist = "Radiologist";
        public const string EmergencySupervisor = "EmergencySupervisor";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Admin, Doctor, Patient, Pharmacist, LabTechnician, Radiologist, EmergencySupervisor
        };
    }
}
