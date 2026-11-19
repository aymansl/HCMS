namespace HCMS4.Shared.Identity.Services
{
    // Implemented by the Records module: maps an Identity user to its patient profile.
    public interface IPatientLookup
    {
        Task<int?> GetPatientIdAsync(string userId);
    }

    // Implemented by the Staff module: maps an Identity user to its doctor profile.
    public interface IDoctorLookup
    {
        Task<int?> GetDoctorIdAsync(string userId);
    }
}
