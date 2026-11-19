namespace HCMS4.Shared.Identity.Services
{
    /// <summary>
    /// Lets a module refuse sign-in for its own accounts (a disabled patient, an inactive pharmacist,
    /// a suspended lab technician). The login page asks every registered check after the password is
    /// verified and before the session is created.
    /// </summary>
    public interface IAccountStatusCheck
    {
        /// <summary>True when the user has a profile in this module and that profile may not sign in.</summary>
        Task<bool> IsSuspendedAsync(string userId);
    }

    public static class AccountStatusMessages
    {
        public const string Suspended = "Your account is suspended. Contact the administrator.";
    }
}
