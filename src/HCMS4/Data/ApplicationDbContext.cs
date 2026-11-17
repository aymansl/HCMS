using HCMS4.Models;
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

           
            modelBuilder.Entity<Patient>()
                .HasMany(p => p.Appointments)
                .WithOne(a => a.Patient)
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Cascade); 

            modelBuilder.Entity<Patient>()
                .HasMany(p => p.Prescriptions)
                .WithOne(pr => pr.Patient)
                .HasForeignKey(pr => pr.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Patient>()
                .HasMany(p => p.Invoices)
                .WithOne(i => i.Patient)
                .HasForeignKey(i => i.PatientId)
                .OnDelete(DeleteBehavior.Cascade); 

            modelBuilder.Entity<Doctor>()
                .HasMany(d => d.Appointments)
                .WithOne(a => a.Doctor)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict); 

            modelBuilder.Entity<Doctor>()
                .HasMany(d => d.Prescriptions)
                .WithOne(p => p.Doctor)
                .HasForeignKey(p => p.DoctorId)
                .OnDelete(DeleteBehavior.Restrict); 

            modelBuilder.Entity<ClinicalNote>()
                .HasOne(cn => cn.Doctor)
                .WithMany()
                .HasForeignKey(cn => cn.DoctorId)
                .OnDelete(DeleteBehavior.Restrict); 

            // 3. Drug relationships - Restrict prevents deletion of drugs referenced by prescriptions
            modelBuilder.Entity<PrescriptionItem>()
                .HasOne(pi => pi.Drug)
                .WithMany(d => d.PrescriptionItems)
                .HasForeignKey(pi => pi.DrugId)
                .OnDelete(DeleteBehavior.Restrict); 

            // 4. Prescription relationships
            modelBuilder.Entity<Prescription>()
                .HasOne(p => p.Appointment)
                .WithMany()
                .HasForeignKey(p => p.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull); 

            modelBuilder.Entity<PrescriptionItem>()
                .HasOne(pi => pi.Prescription)
                .WithMany(p => p.PrescriptionItems)
                .HasForeignKey(pi => pi.PrescriptionId)
                .OnDelete(DeleteBehavior.Cascade); 

      
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict); 

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            // 6. Invoice relationships
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Appointment)
                .WithMany()
                .HasForeignKey(i => i.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull); 

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Prescription)
                .WithMany()
                .HasForeignKey(i => i.PrescriptionId)
                .OnDelete(DeleteBehavior.SetNull); 

            modelBuilder.Entity<Appointment>()
                .Property(a => a.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Drug>()
                .Property(d => d.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Prescription>()
                .Property(p => p.PrescriptionDate)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<ClinicalNote>()
                .Property(cn => cn.Date)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Invoice>()
                .Property(i => i.InvoiceDate)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Doctor>()
           .HasOne(d => d.Specialization)
           .WithMany(s => s.Doctors)
           .HasForeignKey(d => d.SpecializationId)
           .OnDelete(DeleteBehavior.SetNull);

            
            modelBuilder.Entity<Doctor>()
                .Property(d => d.AverageRating)
                .HasColumnType("decimal(3,2)");

            modelBuilder.Entity<Specialization>().HasData(
                new Specialization { Id = 1, Name = "Cardiology", ConsultationFee = 150, IsActive = true, Description = "قلبية" },
                new Specialization { Id = 2, Name = "Dermatology", ConsultationFee = 120, IsActive = true, Description = "جلدية" },
                new Specialization { Id = 3, Name = "Orthopedics", ConsultationFee = 130, IsActive = true, Description = "عظمية" },
                new Specialization { Id = 4, Name = "Neurology", ConsultationFee = 180, IsActive = true, Description = "عصبية" },
                new Specialization { Id = 5, Name = "Pediatrics", ConsultationFee = 110, IsActive = true, Description = "أطفال" },
                new Specialization { Id = 6, Name = "General Medicine", ConsultationFee = 100, IsActive = true, Description = "طب عام" }
            );

            modelBuilder.Entity<DoctorLeave>()
                .HasOne(dl => dl.Doctor)
                .WithMany(d => d.DoctorLeaves)
                .HasForeignKey(dl => dl.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LeaveRequest>()
                .HasOne(lr => lr.Doctor)
                .WithMany()
                .HasForeignKey(lr => lr.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            // VisitRating relationships
            modelBuilder.Entity<VisitRating>()
                .HasOne(vr => vr.Patient)
                .WithMany()
                .HasForeignKey(vr => vr.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VisitRating>()
                .HasOne(vr => vr.Doctor)
                .WithMany()
                .HasForeignKey(vr => vr.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VisitRating>()
                .HasOne(vr => vr.Appointment)
                .WithMany()
                .HasForeignKey(vr => vr.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VisitRating>()
                .Property(vr => vr.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // PrescriptionReissueRequest relationships
            modelBuilder.Entity<PrescriptionReissueRequest>()
                .HasOne(prr => prr.Patient)
                .WithMany()
                .HasForeignKey(prr => prr.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PrescriptionReissueRequest>()
                .HasOne(prr => prr.Prescription)
                .WithMany()
                .HasForeignKey(prr => prr.PrescriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PrescriptionReissueRequest>()
                .HasOne(prr => prr.Doctor)
                .WithMany()
                .HasForeignKey(prr => prr.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PrescriptionReissueRequest>()
                .Property(prr => prr.RequestDate)
                .HasDefaultValueSql("GETUTCDATE()");

            // Complaint relationships
            modelBuilder.Entity<Complaint>()
                .HasOne(c => c.Patient)
                .WithMany()
                .HasForeignKey(c => c.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Complaint>()
                .Property(c => c.SubmissionDate)
                .HasDefaultValueSql("GETUTCDATE()");

            // DoctorReviewRequest relationships
            modelBuilder.Entity<DoctorReviewRequest>()
                .HasOne(drr => drr.Pharmacist)
                .WithMany()
                .HasForeignKey(drr => drr.PharmacistId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DoctorReviewRequest>()
                .HasOne(drr => drr.Prescription)
                .WithMany()
                .HasForeignKey(drr => drr.PrescriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DoctorReviewRequest>()
                .HasOne(drr => drr.Doctor)
                .WithMany()
                .HasForeignKey(drr => drr.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DoctorReviewRequest>()
                .Property(drr => drr.RequestDate)
                .HasDefaultValueSql("GETUTCDATE()");

            // PharmacistPrescriptionNote relationships
            modelBuilder.Entity<PharmacistPrescriptionNote>()
                .HasOne(ppn => ppn.Prescription)
                .WithMany()
                .HasForeignKey(ppn => ppn.PrescriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PharmacistPrescriptionNote>()
                .HasOne(ppn => ppn.Pharmacist)
                .WithMany()
                .HasForeignKey(ppn => ppn.PharmacistId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PharmacistPrescriptionNote>()
                .Property(ppn => ppn.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<MedicalArticle>()
                .HasOne(ma => ma.Doctor)
                .WithMany(d => d.MedicalArticles)
                .HasForeignKey(ma => ma.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MedicalArticle>()
                .Property(ma => ma.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Survey>()
                .HasOne(s => s.CreatedByUser)
                .WithMany()
                .HasForeignKey(s => s.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Survey>()
                .Property(s => s.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<SurveyQuestion>()
                .HasOne(q => q.Survey)
                .WithMany(s => s.Questions)
                .HasForeignKey(q => q.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SurveyQuestionOption>()
                .HasOne(o => o.SurveyQuestion)
                .WithMany(q => q.Options)
                .HasForeignKey(o => o.SurveyQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SurveyAssignment>()
                .HasOne(sa => sa.Survey)
                .WithMany(s => s.Assignments)
                .HasForeignKey(sa => sa.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SurveyAssignment>()
                .HasOne(sa => sa.Patient)
                .WithMany()
                .HasForeignKey(sa => sa.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SurveyAssignment>()
                .HasIndex(sa => new { sa.SurveyId, sa.PatientId })
                .IsUnique();

            modelBuilder.Entity<SurveyAnswer>()
                .HasOne(sa => sa.SurveyAssignment)
                .WithMany(a => a.Answers)
                .HasForeignKey(sa => sa.SurveyAssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SurveyAnswer>()
                .HasOne(sa => sa.SurveyQuestion)
                .WithMany(q => q.Answers)
                .HasForeignKey(sa => sa.SurveyQuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserNotification>()
                .HasOne(un => un.User)
                .WithMany()
                .HasForeignKey(un => un.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserNotification>()
                .HasIndex(un => new { un.UserId, un.IsRead });

            modelBuilder.Entity<UserNotification>()
                .Property(un => un.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<SystemActivityLog>()
                .Property(al => al.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        }
    }
}
