using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Audit.Models;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Reporting.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }


        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Drug> Drugs { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<PrescriptionItem> PrescriptionItems { get; set; }
        public DbSet<ClinicalNote> ClinicalNotes { get; set; }
        public DbSet<Invoice> Invoices { get; set; }

        public DbSet<Pharmacist> Pharmacists { get; set; }
        public DbSet<DailyReport> DailyReports { get; set; }
        public DbSet<PurchaseRequest> PurchaseRequests { get; set; }
        public DbSet<PurchaseRequestItem> PurchaseRequestItems { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Specialization> Specializations { get; set; }
        public DbSet<DoctorLeave> DoctorLeaves { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<VisitRating> VisitRatings { get; set; }
        public DbSet<PrescriptionReissueRequest> PrescriptionReissueRequests { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<DoctorReviewRequest> DoctorReviewRequests { get; set; }
        public DbSet<PharmacistPrescriptionNote> PharmacistPrescriptionNotes { get; set; }
        public DbSet<MedicalArticle> MedicalArticles { get; set; }
        public DbSet<Survey> Surveys { get; set; }
        public DbSet<SurveyQuestion> SurveyQuestions { get; set; }
        public DbSet<SurveyQuestionOption> SurveyQuestionOptions { get; set; }
        public DbSet<SurveyAssignment> SurveyAssignments { get; set; }
        public DbSet<SurveyAnswer> SurveyAnswers { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<SystemActivityLog> SystemActivityLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Each module configures its own entities (Modules/*/Data, Shared/*/Data).
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
