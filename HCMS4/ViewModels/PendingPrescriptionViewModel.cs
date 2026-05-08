namespace HCMS4.ViewModels
{
    public class PendingPrescriptionViewModel
    {
        public int Id { get; set; }
        public string PatientName { get; set; }
        public string DoctorName { get; set; }
        public DateTime PrescriptionDate { get; set; }
        public List<PrescriptionItemViewModel> Items { get; set; } = new();
        public string Notes { get; set; }
        public int DaysPending => (DateTime.Now - PrescriptionDate).Days;
    }
}
