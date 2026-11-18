using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HCMS4.Data;
using HCMS4.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HCMS4.Services
{
    public interface INoShowRiskService
    {
        Task<double> CalculateRiskScoreAsync(int patientId);

        /// <summary>Scores the appointments; the result says whether the AI model or the local estimate was used.</summary>
        Task<NoShowRiskResult> CalculateRiskScoresAsync(IEnumerable<int> appointmentIds);
    }

    /// <summary>Outcome of one scoring call. The service keeps no state between calls (issue #27).</summary>
    public record NoShowRiskResult(
        Dictionary<int, double> Scores,
        bool IsServiceAvailable,
        bool IsUsingAI,
        string? ErrorMessage);

    public class NoShowRiskService : INoShowRiskService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NoShowRiskService> _logger;
        private readonly string _apiBaseUrl;
        private readonly string? _apiKey;

        public NoShowRiskService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<NoShowRiskService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _apiBaseUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:9696";
            _apiKey = configuration["AiService:ApiKey"];
        }

        // Per-call state, so concurrent calls on the same scoped instance cannot interfere.
        private sealed class ScoringRun
        {
            public bool ApiFailed { get; set; }
            public bool UsedAI { get; set; }
            public string? ErrorMessage { get; set; }
        }

        public async Task<NoShowRiskResult> CalculateRiskScoresAsync(IEnumerable<int> appointmentIds)
        {
            var scores = new Dictionary<int, double>();
            var appointmentList = appointmentIds.ToList();
            var run = new ScoringRun();

            if (!appointmentList.Any())
                return new NoShowRiskResult(scores, true, false, null);

            try
            {
                var appointments = await _context.Appointments
                    .Include(a => a.Patient)
                        .ThenInclude(p => p.User)
                    .Include(a => a.Doctor)
                    .Where(a => appointmentList.Contains(a.Id))
                    .ToListAsync();

                foreach (var appointment in appointments)
                {
                    if (appointment.NoShowRiskScore.HasValue)
                    {
                        scores[appointment.Id] = Math.Clamp(appointment.NoShowRiskScore.Value, 0.0, 1.0);
                        continue;
                    }

                    var prediction = await GetPredictionFromAPIAsync(appointment, run);
                    if (prediction.HasValue)
                    {
                        scores[appointment.Id] = prediction.Value;
                        run.UsedAI = true;
                    }
                    else
                    {
                        scores[appointment.Id] = await CalculateRiskScoreAsync(appointment.PatientId);
                    }
                }

                if (run.UsedAI)
                {
                    _logger.LogInformation("Retrieved {Count} predictions from AI API", scores.Count);
                }

                // The local estimate is still shown when the AI service is unavailable ("Rule-Based Mode").
                return new NoShowRiskResult(scores, true, run.UsedAI, run.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating no-show risk scores");
                return new NoShowRiskResult(scores, false, false, "Error calculating no-show risk scores");
            }
        }

        private async Task<double?> GetPredictionFromAPIAsync(Appointment appointment, ScoringRun run)
        {
            // Without a key the service would reject the call; after one failure, skip the remaining
            // appointments so a slow or stopped service cannot hold the page (graceful degradation).
            if (string.IsNullOrWhiteSpace(_apiKey) || run.ApiFailed)
            {
                run.ErrorMessage ??= string.IsNullOrWhiteSpace(_apiKey)
                    ? "AI service API key is not configured"
                    : "AI Prediction service is unavailable";
                return null;
            }

            try
            {
                var client = _httpClientFactory.CreateClient("NoShowAPI");
                client.Timeout = TimeSpan.FromSeconds(10);

                // TODO(A1): send real patient features (gender, chronic conditions, SMS reminders, ...);
                // the model currently sees only age and the two dates (CODEBASE_ANALYSIS #26).
                var request = new NoShowAppointmentRequest
                {
                    PatientId = appointment.PatientId,
                    AppointmentID = appointment.Id,
                    Gender = "m",
                    ScheduledDay = (appointment.CreatedAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm:ss"),
                    AppointmentDay = appointment.AppointmentDateTime.ToString("yyyy-MM-dd 00:00:00"),
                    Age = CalculateAge(appointment.Patient?.DateOfBirth),
                    Neighbourhood = "clinic",
                    Scholarship = false,
                    Hipertension = false,
                    Diabetes = false,
                    Alcoholism = false,
                    Handcap = 0,
                    SMSReceived = false
                };

                using var message = new HttpRequestMessage(HttpMethod.Post, $"{_apiBaseUrl}/predict")
                {
                    Content = JsonContent.Create(request)
                };
                message.Headers.Add("X-API-Key", _apiKey);
                var response = await client.SendAsync(message);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<NoShowPredictionResponse>();
                    if (result != null)
                    {
                        _logger.LogDebug("AI prediction for appointment {Id}: {Prob}", appointment.Id, result.NoShowProbability);
                        return result.NoShowProbability;
                    }
                }
                else
                {
                    _logger.LogWarning("AI API returned status {StatusCode} for appointment {Id}", response.StatusCode, appointment.Id);
                }
            }
            catch (Exception ex)
            {
                // Includes timeouts (TaskCanceledException) and connection errors.
                _logger.LogWarning(ex, "Failed to get prediction from API for appointment {Id}", appointment.Id);
            }

            run.ApiFailed = true;
            run.ErrorMessage ??= "AI Prediction service is unavailable";
            return null;
        }

        private static int CalculateAge(DateTime? dateOfBirth)
        {
            if (!dateOfBirth.HasValue)
                return 30;

            var age = DateTime.Today.Year - dateOfBirth.Value.Year;
            if (dateOfBirth.Value.Date > DateTime.Today.AddYears(-age))
                age--;
            return Math.Max(0, Math.Min(age, 120));
        }

        public async Task<double> CalculateRiskScoreAsync(int patientId)
        {
            try
            {
                var patient = await _context.Patients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == patientId);

                if (patient == null)
                {
                    _logger.LogWarning("Patient {PatientId} not found for risk calculation", patientId);
                    return 0.3;
                }

                var noShowCount = patient.NoShowCount;
                var totalAppointments = await _context.Appointments
                    .CountAsync(a => a.PatientId == patientId);

                if (totalAppointments == 0)
                {
                    return 0.3;
                }

                var noShowRate = (double)noShowCount / totalAppointments;

                var recentNoShows = await _context.Appointments
                    .Where(a => a.PatientId == patientId && a.WasNoShow)
                    .OrderByDescending(a => a.AppointmentDateTime)
                    .Take(3)
                    .CountAsync();

                var recentFactor = Math.Min(recentNoShows * 0.15, 0.45);

                var baseRisk = Math.Min(noShowRate * 0.5 + recentFactor, 1.0);

                var distanceFactor = !string.IsNullOrEmpty(patient.Address) ? 0.05 : 0.0;

                var finalScore = Math.Min(baseRisk + distanceFactor, 1.0);

                _logger.LogDebug("Risk score for patient {PatientId}: {Score}", patientId, finalScore);

                return Math.Round(finalScore, 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating risk score for patient {PatientId}", patientId);
                return 0.3;
            }
        }
    }

    public class NoShowAppointmentRequest
    {
        [JsonPropertyName("PatientId")]
        public double PatientId { get; set; }

        [JsonPropertyName("AppointmentID")]
        public int AppointmentID { get; set; }

        [JsonPropertyName("Gender")]
        public string Gender { get; set; } = "m";

        [JsonPropertyName("ScheduledDay")]
        public string ScheduledDay { get; set; } = "";

        [JsonPropertyName("AppointmentDay")]
        public string AppointmentDay { get; set; } = "";

        [JsonPropertyName("Age")]
        public int Age { get; set; }

        [JsonPropertyName("Neighbourhood")]
        public string Neighbourhood { get; set; } = "";

        [JsonPropertyName("Scholarship")]
        public bool Scholarship { get; set; }

        [JsonPropertyName("Hipertension")]
        public bool Hipertension { get; set; }

        [JsonPropertyName("Diabetes")]
        public bool Diabetes { get; set; }

        [JsonPropertyName("Alcoholism")]
        public bool Alcoholism { get; set; }

        [JsonPropertyName("Handcap")]
        public int Handcap { get; set; }

        [JsonPropertyName("SMS_received")]
        public bool SMSReceived { get; set; }
    }

    public class NoShowPredictionResponse
    {
        [JsonPropertyName("no_show_probability")]
        public double NoShowProbability { get; set; }

        [JsonPropertyName("no_show")]
        public bool NoShow { get; set; }
    }
}
