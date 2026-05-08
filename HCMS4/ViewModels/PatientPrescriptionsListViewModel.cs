namespace HCMS4.ViewModels
{
    public class PatientPrescriptionsListViewModel
    {
        public List<PatientPrescriptionsViewModel> Prescriptions { get; set; } = new();
        public bool HasPrescriptions => Prescriptions.Any();
        public int TotalCount => Prescriptions.Count;

        // Summary statistics
        public int PendingCount => Prescriptions.Count(p => p.Status == "Pending");
        public int CompletedCount => Prescriptions.Count(p => p.Status == "Completed");
        public decimal TotalSpent => Prescriptions.Where(p => p.Status == "Completed").Sum(p => p.TotalCost);
    }
}
