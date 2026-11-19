using HCMS4.Shared.Common;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using System.Net;
using System.Net.Http.Json;
using HCMS4.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HCMS4.Tests
{
    // Regression tests for issues #4/#5: the AI call must send X-API-Key and degrade gracefully.
    public class NoShowRiskServiceTests
    {
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
            public List<HttpRequestMessage> Requests { get; } = new();

            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                _respond = respond;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(_respond(request));
            }
        }

        private static NoShowRiskService CreateService(ApplicationDbContext context, FakeHandler handler, string? apiKey = "secret-key")
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AiService:BaseUrl"] = "http://ai.test",
                    ["AiService:ApiKey"] = apiKey
                })
                .Build();
            return new NoShowRiskService(context, factory.Object, NullLogger<NoShowRiskService>.Instance, configuration);
        }

        private static async Task<List<int>> CreateAppointmentsAsync(ApplicationDbContext context, int count)
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(context);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var ids = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var appointment = new Appointment
                {
                    PatientId = patient.Id,
                    DoctorId = doctor.Id,
                    AppointmentDateTime = ClinicTime.Now.AddDays(i + 1),
                    Status = AppointmentStatus.Scheduled
                };
                context.Appointments.Add(appointment);
                await context.SaveChangesAsync();
                ids.Add(appointment.Id);
            }
            return ids;
        }

        [Fact]
        public async Task CalculateRiskScores_SendsApiKeyHeader_AndUsesPrediction()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var ids = await CreateAppointmentsAsync(context, 1);
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { no_show_probability = 0.82, no_show = true })
            });
            var service = CreateService(context, handler);

            var result = await service.CalculateRiskScoresAsync(ids);

            var scores = result.Scores;

            var request = Assert.Single(handler.Requests);
            Assert.Equal("secret-key", request.Headers.GetValues("X-API-Key").Single());
            Assert.Equal("http://ai.test/predict", request.RequestUri!.ToString());
            Assert.Equal(0.82, scores[ids[0]], 3);
            Assert.True(result.IsUsingAI);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task CalculateRiskScores_ErrorStatus_FallsBackToLocalEstimate(HttpStatusCode status)
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var ids = await CreateAppointmentsAsync(context, 1);
            var service = CreateService(context, new FakeHandler(_ => new HttpResponseMessage(status)));

            var result = await service.CalculateRiskScoresAsync(ids);

            var scores = result.Scores;

            Assert.True(scores.ContainsKey(ids[0]));
            Assert.InRange(scores[ids[0]], 0.0, 1.0);
            Assert.False(result.IsUsingAI);
            Assert.True(result.IsServiceAvailable); // local estimate is still shown
        }

        [Fact]
        public async Task CalculateRiskScores_Timeout_FallsBackAndStopsCallingTheService()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var ids = await CreateAppointmentsAsync(context, 3);
            var handler = new FakeHandler(_ => throw new TaskCanceledException("timeout"));
            var service = CreateService(context, handler);

            var result = await service.CalculateRiskScoresAsync(ids);

            var scores = result.Scores;

            Assert.Equal(3, scores.Count);
            Assert.Single(handler.Requests);
            Assert.False(result.IsUsingAI);
        }

        [Fact]
        public async Task CalculateRiskScores_NoApiKey_DoesNotCallService()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var ids = await CreateAppointmentsAsync(context, 2);
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var service = CreateService(context, handler, apiKey: null);

            var result = await service.CalculateRiskScoresAsync(ids);

            var scores = result.Scores;

            Assert.Equal(2, scores.Count);
            Assert.Empty(handler.Requests);
            Assert.True(result.IsServiceAvailable); // local estimate is still shown
        }
        [Fact]
        public async Task CalculateRiskScores_KeepsNoStateBetweenCalls()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var ids = await CreateAppointmentsAsync(context, 1);
            var calls = 0;
            var handler = new FakeHandler(_ => ++calls == 1
                ? throw new TaskCanceledException("timeout")
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { no_show_probability = 0.5, no_show = false })
                });
            var service = CreateService(context, handler);

            var first = await service.CalculateRiskScoresAsync(ids);
            var second = await service.CalculateRiskScoresAsync(ids);

            // The failure of the first call must not stop the second call from using the AI service (issue #27).
            Assert.False(first.IsUsingAI);
            Assert.True(second.IsUsingAI);
            Assert.Null(second.ErrorMessage);
            Assert.Equal(2, handler.Requests.Count);
        }
    }
}