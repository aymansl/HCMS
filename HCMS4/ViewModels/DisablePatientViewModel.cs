namespace HCMS4.ViewModels
{
    public class DisablePatientViewModel
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string? DisableReason { get; set; }
    }
}
