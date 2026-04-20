using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UniSync.Core.Entities;

namespace UniSync.Core.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ResearchSubmission> ResearchSubmissions { get; set; }
        public DbSet<DraftSubmission> DraftSubmissions { get; set; }
        public DbSet<SubmissionGroupMember> SubmissionGroupMembers { get; set; }
        public DbSet<ExpertiseDomain> ExpertiseDomains { get; set; }
        public DbSet<LinkRequest> LinkRequests { get; set; }
        public DbSet<SupervisorExpertiseDomain> SupervisorExpertiseDomains { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<AppNotification> AppNotifications { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SystemApiKey> SystemApiKeys { get; set; }
        public DbSet<GlobalSetting> GlobalSettings { get; set; }
        public DbSet<ReallocationRequest> ReallocationRequests { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ReallocationRequest>()
                .HasOne(r => r.Submission)
                .WithMany()
                .HasForeignKey(r => r.ResearchSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ReallocationRequest>()
                .HasOne(r => r.RequestedBy)
                .WithMany()
                .HasForeignKey(r => r.RequestedById)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ResearchSubmission>()
                .HasOne(p => p.Submitter)
                .WithMany(u => u.Submissions)
                .HasForeignKey(p => p.SubmitterId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SubmissionGroupMember>()
                .HasOne(pgm => pgm.ResearchSubmission)
                .WithMany(p => p.GroupMembers)
                .HasForeignKey(pgm => pgm.ResearchSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<DraftSubmission>()
                .HasOne(d => d.Student)
                .WithMany(u => u.DraftSubmissions)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DraftSubmission>()
                .HasOne(d => d.ExpertiseDomain)
                .WithMany()
                .HasForeignKey(d => d.ExpertiseDomainId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ResearchSubmission>()
                .HasOne(p => p.LinkedSupervisor)
                .WithMany(u => u.SupervisedSubmissions)
                .HasForeignKey(p => p.LinkedSupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<LinkRequest>()
                .HasOne(m => m.Submission)
                .WithMany(p => p.LinkRequests)
                .HasForeignKey(m => m.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SupervisorExpertiseDomain>()
                .HasKey(sra => sra.Id);

            builder.Entity<SupervisorExpertiseDomain>()
                .HasOne(sra => sra.Supervisor)
                .WithMany(u => u.SupervisorAreas)
                .HasForeignKey(sra => sra.SupervisorId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SupervisorExpertiseDomain>()
                .HasOne(sra => sra.ExpertiseDomain)
                .WithMany(ra => ra.SupervisorAreas)
                .HasForeignKey(sra => sra.ExpertiseDomainId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ChatMessage>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ChatMessage>()
                .HasOne(m => m.Recipient)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(m => m.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ChatMessage>()
                .Property(m => m.Content)
                .HasMaxLength(2000);

            builder.Entity<AppNotification>()
                .HasOne(n => n.Recipient)
                .WithMany(u => u.ReceivedNotifications)
                .HasForeignKey(n => n.RecipientId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AppNotification>()
                .HasOne(n => n.Sender)
                .WithMany(u => u.SentNotifications)
                .HasForeignKey(n => n.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<AppNotification>()
                .Property(n => n.ActionUrl)
                .HasMaxLength(500);

            builder.Entity<AppNotification>()
                .HasIndex(n => new { n.RecipientId, n.IsRead, n.CreatedAt });
        }
    }
}
