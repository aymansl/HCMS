using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Feedback.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace HCMS4.Tests
{
    // Complaint, survey, visit-rating and medical-article services (cleanup phase 5b).
    public class FeedbackServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();

        // 2026-11-18 09:00 UTC = 12:00 Damascus.
        private readonly ClinicClock _clock = new(
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero)), ClinicTime.TimeZone);

        private ComplaintService Complaints => new(_db, _clock);
        private SurveyService Surveys => new(_db, _clock);
        private VisitRatingService Ratings => new(_db, _clock);
        private MedicalArticleService Articles => new(_db, _clock);

        public void Dispose() => _db.Dispose();

        // ---- Complaints ----

        [Fact]
        public async Task Complaint_CreateOpenAndResolve_FollowsStatusFlow()
        {
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);

            var complaint = await Complaints.CreateAsync(patient.Id,
                new PatientComplaintFormViewModel { Title = " Long wait ", Description = " 2 hours " }, "file.pdf");

            Assert.StartsWith("CMP-20261118-", complaint.TrackingNumber);
            Assert.Equal("Long wait", complaint.Title);
            Assert.Equal(ComplaintStatus.UnderReview, (await Complaints.OpenForReviewAsync(complaint.Id))!.Status);

            var resolved = await Complaints.UpdateStatusAsync(complaint.Id, ComplaintStatus.Resolved, "apologised");

            Assert.True(resolved.Success);
            Assert.Equal(new DateTime(2026, 11, 18, 12, 0, 0), resolved.Data.ResolvedAt);
            Assert.Equal(patient.UserId, resolved.Data.Patient.UserId);
            Assert.Single(await Complaints.GetForPatientAsync(patient.Id));
            Assert.Single(await Complaints.GetAllAsync());
            Assert.False((await Complaints.UpdateStatusAsync(999999, ComplaintStatus.Closed, null)).Success);
            Assert.Null(await Complaints.OpenForReviewAsync(999999));
        }

        // ---- Surveys ----

        [Fact]
        public void Survey_Validate_ReportsDatesQuestionsAndTargets()
        {
            var errors = Surveys.Validate(new SurveyEditorViewModel
            {
                StartDate = new DateTime(2026, 12, 1),
                EndDate = new DateTime(2026, 11, 1),
                Questions = { new SurveyQuestionEditorViewModel { QuestionText = " " } },
                TargetAudience = SurveyTargetAudience.SpecificPatients
            });

            Assert.Equal(3, errors.Count);
            Assert.Contains(errors, e => e.Field == nameof(SurveyEditorViewModel.EndDate));
        }

        [Fact]
        public async Task Survey_SentImmediately_AssignsActivePatients_AndCanBeAnsweredOnce()
        {
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            var created = await Surveys.CreateAsync(new SurveyEditorViewModel
            {
                Title = "Satisfaction",
                StartDate = new DateTime(2026, 11, 1),
                SendImmediately = true,
                TargetAudience = SurveyTargetAudience.AllPatients,
                Questions =
                {
                    new SurveyQuestionEditorViewModel { QuestionText = "Pick", QuestionType = SurveyQuestionType.MultipleChoice, IsRequired = true, OptionsText = "A\nB\nA" },
                    new SurveyQuestionEditorViewModel { QuestionText = "" }
                }
            }, "admin-id");

            Assert.Equal(SurveyStatus.Active, created.Survey.Status);
            Assert.Equal(new[] { patient.UserId }, created.AssignedUserIds);
            var question = Assert.Single(created.Survey.Questions);
            Assert.Equal(2, question.Options.Count);

            var assignment = Assert.Single(await Surveys.GetAssignmentsAsync(patient.Id));
            var form = await Surveys.GetResponseFormAsync(assignment.Id, patient.Id);
            Assert.NotNull(form);

            var unanswered = await Surveys.SubmitAsync(patient.Id, form!);
            Assert.False(unanswered.Success);
            Assert.NotEmpty(unanswered.Errors);

            form.Questions[0].AnswerText = "A";
            var answered = await Surveys.SubmitAsync(patient.Id, form);
            Assert.True(answered.Success);
            Assert.Equal(created.Survey.Id, answered.Data);
            Assert.Single(await _db.SurveyAnswers.ToListAsync());

            var again = await Surveys.SubmitAsync(patient.Id, form);
            Assert.False(again.Success);
            Assert.True(again.Errors == null || again.Errors.Count == 0);
            Assert.Null(await Surveys.GetResponseFormAsync(assignment.Id, patient.Id));
        }

        [Fact]
        public async Task Survey_Draft_HasNoAssignments_AndPatientOptionsListActivePatients()
        {
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);

            var draft = await Surveys.CreateAsync(new SurveyEditorViewModel
            {
                Title = "Draft",
                StartDate = new DateTime(2026, 11, 1),
                Questions = { new SurveyQuestionEditorViewModel { QuestionText = "Q" } }
            }, null);

            Assert.Equal(SurveyStatus.Draft, draft.Survey.Status);
            Assert.Empty(draft.AssignedUserIds);
            Assert.Single(await Surveys.GetAllAsync());
            Assert.Contains(await Surveys.GetPatientOptionsAsync(), p => p.Id == patient.Id);
        }

        // ---- Visit ratings ----

        [Fact]
        public async Task VisitRating_OnlyCompletedUnratedVisits_AndUpdatesDoctorAverage()
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            var completed = new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2026, 11, 1, 10, 0, 0), Status = AppointmentStatus.Completed };
            var scheduled = new Appointment { DoctorId = doctor.Id, PatientId = patient.Id, AppointmentDateTime = new DateTime(2026, 12, 1, 10, 0, 0), Status = AppointmentStatus.Scheduled };
            _db.Appointments.AddRange(completed, scheduled);
            await _db.SaveChangesAsync();

            Assert.False((await Ratings.GetRatingFormAsync(patient.Id, scheduled.Id)).Success);
            Assert.True((await Ratings.GetRatingFormAsync(patient.Id, completed.Id)).Success);
            Assert.False((await Ratings.GetRatingFormAsync(patient.Id + 1000, completed.Id)).Success);

            var rated = await Ratings.RateAsync(patient.Id, new VisitRatingFormViewModel { AppointmentId = completed.Id, Rating = 4 });

            Assert.True(rated.Success);
            var storedDoctor = await _db.Doctors.AsNoTracking().SingleAsync(d => d.Id == doctor.Id);
            Assert.Equal(1, storedDoctor.RatingCount);
            Assert.Equal(4m, storedDoctor.AverageRating);
            Assert.False((await Ratings.RateAsync(patient.Id, new VisitRatingFormViewModel { AppointmentId = completed.Id, Rating = 5 })).Success);
        }

        // ---- Medical articles ----

        [Fact]
        public async Task Articles_NeedPermission_DraftCanBePublishedOnce()
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            var model = new MedicalArticleEditorViewModel { Title = " Flu ", Content = " Rest. " };
            var tracked = await _db.Doctors.SingleAsync(d => d.Id == doctor.Id);
            tracked.CanPublishArticles = false;
            await _db.SaveChangesAsync();

            Assert.False(await Articles.CanPublishAsync(doctor.Id));
            Assert.False((await Articles.CreateAsync(doctor.Id, model, publishNow: false)).Success);

            tracked.CanPublishArticles = true;
            await _db.SaveChangesAsync();

            var draft = await Articles.CreateAsync(doctor.Id, model, publishNow: false);
            Assert.True(draft.Success);
            Assert.Equal(ArticleStatus.Draft, draft.Data.Status);
            Assert.Equal("Flu", draft.Data.Title);

            var published = await Articles.PublishAsync(doctor.Id, draft.Data.Id);
            Assert.True(published.Success);
            Assert.Equal(new DateTime(2026, 11, 18, 12, 0, 0), published.Data.PublishedAt);
            Assert.False((await Articles.PublishAsync(doctor.Id, draft.Data.Id)).Success);
            Assert.False((await Articles.PublishAsync(doctor.Id, 999999)).Success);
            Assert.Single(await Articles.GetForDoctorAsync(doctor.Id));
        }
    }
}
