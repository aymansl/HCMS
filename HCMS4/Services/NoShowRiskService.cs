using System.Net.Http.Json;
using System.Text.Json;
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
        Task<Dictionary<int, double>> CalculateRiskScoresAsync(IEnumerable<int> appointmentIds);
        bool IsServiceAvailable { get; }
        bool IsUsingAI { get; }
        string? LastErrorMessage { get; }
    }

    public class NoShowRiskService : INoShowRiskService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NoShowRiskService> _logger;
        private readonly string _apiBaseUrl;
        private bool _serviceAvailable = true;
        private bool _isUsingAI = false;
        private string? _lastErrorMessage;

        public bool IsServiceAvailable => _serviceAvailable;
        public bool IsUsingAI => _isUsingAI;
        public string? LastErrorMessage => _lastErrorMessage;

        public NoShowRiskService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<NoShowRiskService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _apiBaseUrl = configuration["NoShowAPI:BaseUrl"] ?? "http://localhost:9696";
        }

        public async Task<Dictionary<int, double>> CalculateRiskScoresAsync(IEnumerable<int> appointmentIds)
        {
            var scores = new Dictionary<int, double>();
            var appointmentList = appointmentIds.ToList();
            _isUsingAI = false;

            if (!appointmentList.Any())
                return scores;

            try
            {
                var appointments = await _context.Appointments
                    .Include(a => a.Patient)
                        .ThenInclude(p => p.User)
                    .Include(a => a.Doctor)
                    .Where(a => appointmentList.Contains(a.Id))
                    .ToListAsync();

                if (!appointments.Any())
                    return scores;

                foreach (var appointment in appointments)
                {
                    if (appointment.NoShowRiskScore.HasValue)
                    {
                        scores[appointment.Id] = Math.Clamp(appointment.NoShowRiskScore.Value, 0.0, 1.0);
                        continue;
                    }

                    var prediction = await GetPredictionFromAPIAsync(appointment);
                    if (prediction.HasValue)
                    {
                        scores[appointment.Id] = prediction.Value;
                        _isUsingAI = true;
                    }
                    else
                    {
                        var localScore = await CalculateRiskScoreAsync(appointment.PatientId);
                        scores[appointment.Id] = localScore;
                    }
                }

                if (_isUsingAI)
                {
                    _serviceAvailable = true;
                    _logger.LogInformation("Retrieved {Count} predictions from AI API", scores.Count);
                }

                return scores;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to connect to No-Show Prediction API at {Url}", _apiBaseUrl);
                _serviceAvailable = false;
                _lastErrorMessage = "Failed to connect to AI Prediction service";

                return await CalculateRiskScoresLocallyAsync(appointmentList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating risk scores via AI API");
                _serviceAvailable = false;
                _lastErrorMessage = "Error communicating with AI Prediction service";

                return await CalculateRiskScoresLocallyAsync(appointmentList);
            }
        }

        private async Task<double?> GetPredictionFromAPIAsync(Appointment appointment)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("NoShowAPI");
                client.Timeout = TimeSpan.FromSeconds(10);

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

                var response = await client.PostAsJsonAsync($"{_apiBaseUrl}/predict", request);

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
                _logger.LogWarning(ex, "Failed to get prediction from API for appointment {Id}", appointment.Id);
            }

            return null;
        }

        private async Task<Dictionary<int, double>> CalculateRiskScoresLocallyAsync(List<int> appointmentIds)
        {
            var scores = new Dictionary<int, double>();
            _isUsingAI = false;

            foreach (var appointmentId in appointmentIds)
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Patient)
                    .FirstOrDefaultAsync(a => a.Id == appointmentId);

                if (appointment != null)
                {
                    if (appointment.NoShowRiskScore.HasValue)
                    {
                        scores[appointmentId] = Math.Clamp(appointment.NoShowRiskScore.Value, 0.0, 1.0);
                        continue;
                    }

                    var score = await CalculateRiskScoreAsync(appointment.PatientId);
                    scores[appointmentId] = score;
                }
            }

            return scores;
        }

        private int CalculateAge(DateTime? dateOfBirth)
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
                _serviceAvailable = false;
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
