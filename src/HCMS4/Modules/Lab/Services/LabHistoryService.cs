using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Modules.Records.Services;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Lab.Services
{
    /// <summary>A patient's issued lab results over time (UC-L09).</summary>
    public interface ILabHistoryService
    {
        /// <summary>The issued requests of a patient with their results, newest first.</summary>
        Task<List<LabRequest>> GetHistoryAsync(int patientId);

        /// <summary>The values of one test for a patient, oldest first.</summary>
        Task<List<LabTrendPoint>> GetTestTrendAsync(int patientId, int labTestId);

        /// <summary>Builds the page: search by <paramref name="term"/>, or show one patient's history and trend.</summary>
        Task<PatientHistoryViewModel> BuildPageAsync(string? term, int? patientId, int? testId);
    }

    public class LabHistoryService : ILabHistoryService
    {
        public const int MaxMatches = 20;

        private readonly ApplicationDbContext _context;
        private readonly IPatientService _patients;

        public LabHistoryService(ApplicationDbContext context, IPatientService patients)
        {
            _context = context;
            _patients = patients;
        }

        private IQueryable<LabRequest> Issued(int patientId) => _context.LabRequests.AsNoTracking()
            .Where(r => r.PatientId == patientId && r.Status == LabRequestStatus.ResultIssued);

        public Task<List<LabRequest>> GetHistoryAsync(int patientId) =>
            Issued(patientId)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .Include(r => r.Items).ThenInclude(i => i.LabTest)
                .Include(r => r.Items).ThenInclude(i => i.Result)
                .OrderByDescending(r => r.ResultIssuedAt).ThenByDescending(r => r.Id)
                .ToListAsync();

        public async Task<List<LabTrendPoint>> GetTestTrendAsync(int patientId, int labTestId)
        {
            var rows = await Issued(patientId)
                .SelectMany(r => r.Items.Where(i => i.LabTestId == labTestId && i.Result != null)
                    .Select(i => new { RequestId = r.Id, IssuedAt = r.ResultIssuedAt ?? r.StatusChangedAt, i.Result }))
                .ToListAsync();

            return rows.OrderBy(x => x.IssuedAt).ThenBy(x => x.RequestId)
                .Select(x => new LabTrendPoint(x.RequestId, x.IssuedAt, x.Result!)).ToList();
        }

        public async Task<PatientHistoryViewModel> BuildPageAsync(string? term, int? patientId, int? testId)
        {
            var model = new PatientHistoryViewModel { Term = term?.Trim() };

            if (patientId.HasValue)
            {
                model.Patient = await _patients.GetByIdAsync(patientId.Value);
                if (model.Patient == null)
                {
                    model.NotFound = true;
                    return model;
                }

                model.History = await GetHistoryAsync(patientId.Value);
                model.Tests = model.History.SelectMany(r => r.Items).Select(i => i.LabTest)
                    .GroupBy(t => t.Id).Select(g => g.First()).OrderBy(t => t.Name).ToList();

                if (testId.HasValue)
                {
                    model.TrendTest = model.Tests.FirstOrDefault(t => t.Id == testId.Value);
                    if (model.TrendTest != null)
                    {
                        model.Trend = await GetTestTrendAsync(patientId.Value, testId.Value);
                        model.Chart = LabTrendChart.Build(model.TrendTest, model.Trend);
                    }
                }

                return model;
            }

            if (!string.IsNullOrEmpty(model.Term))
            {
                model.Matches = await _patients.SearchAsync(model.Term, MaxMatches);
                model.NotFound = model.Matches.Count == 0;
            }

            return model;
        }
    }
}
