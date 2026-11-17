namespace HCMS4.ViewModels
{
    public class PharmacistViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string? Qualifications { get; set; }
        public string? ContactInfo { get; set; }
        public string? Shift { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}