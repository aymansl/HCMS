namespace HCMS4.ViewModels
{
    public class UserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public bool IsDoctor { get; set; }
        public bool IsPatient { get; set; }
        public bool IsAdmin { get; set; }
    }
}
