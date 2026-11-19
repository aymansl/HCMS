using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.ViewModels;

namespace HCMS4.Modules.Appointments.Services
{
    /// <summary>The doctor's "daily visiting patients" report: completed visits of one day.</summary>
    public interface IDailyVisitReportService
    {
        Task<DailyVisitingPatientsReportViewModel> GetReportAsync(DateTime date);
    }

    public class DailyVisitReportService : IDailyVisitReportService
    {
        private const int DiagnosisPreviewLength = 20;

        private readonly IAppointmentService _appointmentService;
        private readonly IPrescriptionService _prescriptionService;

        public DailyVisitReportService(IAppointmentService appointmentService, IPrescriptionService prescriptionService)
        {
            _appointmentService = appointmentService;
            _prescriptionService = prescriptionService;
        }

        public async Task<DailyVisitingPatientsReportViewModel> GetReportAsync(DateTime date)
        {
            var appointments = await _appointmentService.GetCompletedOnDateAsync(date);
            var prescriptions = await _prescriptionService.GetByAppointmentIdsAsync(appointments.Select(a => a.Id).ToList());
            var completedVisits = await _appointmentService.CountCompletedByPatientAsync(
                appointments.Select(a => a.PatientId).Distinct().ToList());

            var viewModel = new DailyVisitingPatientsReportViewModel
            {
                ReportDate = date,
                TotalPatients = appointments.Count,
                Patients = appointments.Select(a =>
                {
                    var prescription = prescriptions.FirstOrDefault(p => p.AppointmentId == a.Id);
                    return new VisitingPatientItem
                    {
                        AppointmentId = a.Id,
                        PatientName = a.Patient?.User?.FullName ?? "Unknown",
                        PhoneNumber = a.Patient?.User?.PhoneNumber,
                        AppointmentTime = a.AppointmentDateTime,
                        // This visit is itself completed, so any other completed visit makes it a follow-up.
                        VisitType = completedVisits.GetValueOrDefault(a.PatientId) > 1 ? "Follow-up" : "Consultation",
                        Diagnosis = GetDiagnosis(prescription),
                        HasPrescription = prescription != null
                    };
                }).ToList()
            };

            if (!viewModel.Patients.Any())
            {
                viewModel.ErrorMessage = "No visiting patients on the selected date.";
            }

            return viewModel;
        }

        private static string? GetDiagnosis(Prescription? prescription)
        {
            var drugName = prescription?.PrescriptionItems.FirstOrDefault()?.DrugName;
            return drugName?.Length > DiagnosisPreviewLength
                ? drugName.Substring(0, DiagnosisPreviewLength) + "..."
                : drugName;
        }
    }
}
