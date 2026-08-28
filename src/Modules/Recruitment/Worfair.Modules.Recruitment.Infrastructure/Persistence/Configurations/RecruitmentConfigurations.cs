namespace Worfair.Modules.Recruitment.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Worfair.Modules.Recruitment.Domain.Aggregates.Candidate;
using Worfair.Modules.Recruitment.Domain.Aggregates.Interview;
using Worfair.Modules.Recruitment.Domain.Aggregates.JobRequisition;

/// <summary>
/// Mapeamento snake_case explícito (R-09). CHECKs/RLS/índices funcionais
/// (lower(email)) ficam nas migrations (migrationBuilder.Sql — docs/database/04).
/// </summary>
public sealed class JobRequisitionConfiguration : IEntityTypeConfiguration<JobRequisition>
{
    public void Configure(EntityTypeBuilder<JobRequisition> builder)
    {
        builder.ToTable("job_requisitions", "recruitment");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new JobRequisitionId(value));

        builder.Property(r => r.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(r => r.CompanyId).HasColumnName("company_id");

        builder.Property(r => r.CreatedBy).HasColumnName("created_by");

        builder.Property(r => r.Title)
            .HasColumnName("title")
            .HasMaxLength(JobTitle.MaxLength)
            .HasConversion(t => t.Value, value => JobTitle.Create(value).Value)
            .IsRequired();

        builder.Property(r => r.Description).HasColumnName("description").IsRequired();

        // VOs aninhados: Money dentro de SalaryRange (docs/architecture/05 §6).
        builder.OwnsOne(r => r.SalaryRange, salary =>
        {
            salary.OwnsOne(s => s.Minimum, money =>
            {
                money.Property(m => m.Amount).HasColumnName("salary_min").HasColumnType("numeric(12,2)");
                money.Property(m => m.Currency).HasColumnName("salary_currency").HasMaxLength(3);
            });
            salary.OwnsOne(s => s.Maximum!, money =>
            {
                money.Property(m => m.Amount).HasColumnName("salary_max").HasColumnType("numeric(12,2)");
                money.Property(m => m.Currency).HasColumnName("salary_max_currency").HasMaxLength(3);
            });
        });

        builder.Navigation(r => r.SalaryRange).IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.PublishedAtUtc).HasColumnName("published_at");
        builder.Property(r => r.ClosedAtUtc).HasColumnName("closed_at");
        builder.Property(r => r.CloseReason).HasColumnName("close_reason");
        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at");

        builder.HasIndex(r => new { r.TenantId, r.Status })
            .HasDatabaseName("ix_job_requisitions_tenant_status");
        builder.HasIndex(r => new { r.TenantId, r.CompanyId })
            .HasDatabaseName("ix_job_requisitions_tenant_company");

        // Time de contratação — tabela filha com tenant_id explícito (RLS própria).
        builder.OwnsMany(r => r.HiringTeam, team =>
        {
            team.ToTable("hiring_team_members", "recruitment");

            team.Property<JobRequisitionId>("job_requisition_id")
                .HasColumnName("job_requisition_id")
                .HasConversion(id => id.Value, value => new JobRequisitionId(value));
            team.WithOwner().HasForeignKey("job_requisition_id");

            team.HasKey("job_requisition_id", nameof(HiringTeamMember.RecruiterUserId));

            team.Property(m => m.TenantId)
                .HasColumnName("tenant_id")
                .HasConversion(id => id.Value, value => new TenantId(value))
                .IsRequired();

            team.Property(m => m.RecruiterUserId).HasColumnName("recruiter_user_id");
            team.Property(m => m.Role)
                .HasColumnName("role")
                .HasConversion<int>()
                .IsRequired();
            team.Property(m => m.AddedAtUtc).HasColumnName("added_at");
        });

        builder.Navigation(r => r.HiringTeam).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("candidates", "recruitment");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new CandidateId(value));

        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(c => c.UserId).HasColumnName("user_id");

        builder.Property(c => c.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(Candidate.FullNameMaxLength)
            .IsRequired();

        builder.Property(c => c.Email)
            .HasColumnName("email")
            .HasMaxLength(ContactEmail.MaxLength)
            .HasConversion(e => e.Value, value => ContactEmail.Create(value).Value)
            .IsRequired();

        builder.Property(c => c.Phone)
            .HasColumnName("phone")
            .HasMaxLength(Candidate.PhoneMaxLength);

        builder.Property(c => c.Source)
            .HasColumnName("source")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.ResumeUrl).HasColumnName("resume_url");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at");

        builder.HasIndex(c => new { c.TenantId, c.Status })
            .HasDatabaseName("ix_candidates_tenant_status");
        builder.HasIndex(c => new { c.TenantId, c.UserId })
            .HasDatabaseName("ix_candidates_tenant_user");

        // Histórico append-only de estágios.
        builder.OwnsMany(c => c.History, history =>
        {
            history.ToTable("candidate_stage_history", "recruitment");

            history.Property<CandidateId>("candidate_id")
                .HasColumnName("candidate_id")
                .HasConversion(id => id.Value, value => new CandidateId(value));
            history.WithOwner().HasForeignKey("candidate_id");

            history.HasKey(h => h.Id);
            history.Property(h => h.Id).HasColumnName("id");

            history.Property(h => h.TenantId)
                .HasColumnName("tenant_id")
                .HasConversion(id => id.Value, value => new TenantId(value))
                .IsRequired();

            history.Property(h => h.FromStatus).HasColumnName("from_status").HasConversion<int>();
            history.Property(h => h.ToStatus).HasColumnName("to_status").HasConversion<int>().IsRequired();
            history.Property(h => h.ChangedBy).HasColumnName("changed_by");
            history.Property(h => h.ChangedAtUtc).HasColumnName("changed_at");

            history.HasIndex("candidate_id").HasDatabaseName("ix_candidate_stage_history_candidate");
        });

        builder.Navigation(c => c.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public void Configure(EntityTypeBuilder<Interview> builder)
    {
        builder.ToTable("interviews", "recruitment");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new InterviewId(value));

        builder.Property(i => i.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(i => i.JobRequisitionId).HasColumnName("job_requisition_id");
        builder.Property(i => i.CandidateId).HasColumnName("candidate_id");
        builder.Property(i => i.ScheduledAtUtc).HasColumnName("scheduled_at");

        builder.Property(i => i.DurationMinutes)
            .HasColumnName("duration_minutes")
            .HasColumnType("smallint");

        builder.Property(i => i.Type)
            .HasColumnName("type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.CompletedAtUtc).HasColumnName("completed_at");
        builder.Property(i => i.CancelledAtUtc).HasColumnName("cancelled_at");
        builder.Property(i => i.CreatedAtUtc).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAtUtc).HasColumnName("updated_at");

        builder.HasIndex(i => new { i.TenantId, i.ScheduledAtUtc })
            .HasDatabaseName("ix_interviews_tenant_date");
        builder.HasIndex(i => new { i.TenantId, i.CandidateId })
            .HasDatabaseName("ix_interviews_tenant_candidate");

        builder.OwnsMany(i => i.Feedbacks, feedback =>
        {
            feedback.ToTable("interview_feedbacks", "recruitment");

            feedback.Property<InterviewId>("interview_id")
                .HasColumnName("interview_id")
                .HasConversion(id => id.Value, value => new InterviewId(value));
            feedback.WithOwner().HasForeignKey("interview_id");

            feedback.HasKey(f => f.Id);
            feedback.Property(f => f.Id).HasColumnName("id");

            feedback.Property(f => f.TenantId)
                .HasColumnName("tenant_id")
                .HasConversion(id => id.Value, value => new TenantId(value))
                .IsRequired();

            feedback.Property(f => f.InterviewerUserId).HasColumnName("interviewer_user_id");
            feedback.Property(f => f.Rating).HasColumnName("rating").HasColumnType("smallint");
            feedback.Property(f => f.Notes).HasColumnName("notes").HasMaxLength(InterviewFeedback.NotesMaxLength);
            feedback.Property(f => f.SubmittedAtUtc).HasColumnName("submitted_at");

            // UNIQUE (interview_id, interviewer_user_id) — docs/database/03 §5.
            feedback.HasIndex("interview_id", nameof(InterviewFeedback.InterviewerUserId))
                .IsUnique()
                .HasDatabaseName("uq_interview_feedbacks_interview_interviewer");
        });

        builder.Navigation(i => i.Feedbacks).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(i => i.DomainEvents);
    }
}
