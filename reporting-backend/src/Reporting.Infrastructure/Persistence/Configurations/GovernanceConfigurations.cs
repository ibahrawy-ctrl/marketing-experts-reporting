using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Development;
using Reporting.Domain.Entities.Governance;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class TrainingNeedConfiguration : IEntityTypeConfiguration<TrainingNeed>
{
    public void Configure(EntityTypeBuilder<TrainingNeed> b)
    {
        b.ToTable("training_needs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Source).HasMaxLength(100);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => x.SubjectUserId);
    }
}

public class ImprovementPlanConfiguration : IEntityTypeConfiguration<ImprovementPlan>
{
    public void Configure(EntityTypeBuilder<ImprovementPlan> b)
    {
        b.ToTable("improvement_plans");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => x.SubjectUserId);
    }
}

public class RiskConfiguration : IEntityTypeConfiguration<Risk>
{
    public void Configure(EntityTypeBuilder<Risk> b)
    {
        b.ToTable("risks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.NextAction).HasMaxLength(1000);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.DepartmentId);
        b.HasIndex(x => x.RelatedSubmissionId);
        b.HasIndex(x => x.RelatedKpiEvaluationId);
        b.HasIndex(x => x.SubjectUserId);
        b.HasIndex(x => x.ClientId);
        b.HasIndex(x => x.ProjectId);
    }
}

public class EscalationConfiguration : IEntityTypeConfiguration<Escalation>
{
    public void Configure(EntityTypeBuilder<Escalation> b)
    {
        b.ToTable("escalations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).IsRequired().HasMaxLength(1000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.NextAction).HasMaxLength(1000);
        b.HasIndex(x => x.TargetUserId);
        b.HasIndex(x => x.ReportSubmissionId);
        b.HasIndex(x => x.RiskId);
        b.HasIndex(x => x.KpiEvaluationId);
    }
}

public class DecisionConfiguration : IEntityTypeConfiguration<Decision>
{
    public void Configure(EntityTypeBuilder<Decision> b)
    {
        b.ToTable("decisions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.NextAction).HasMaxLength(1000);
        b.HasIndex(x => x.MadeById);
        b.HasIndex(x => x.RelatedSubmissionId);
        b.HasIndex(x => x.RelatedRiskId);
        b.HasIndex(x => x.RelatedEscalationId);
    }
}

public class ManagementNoteConfiguration : IEntityTypeConfiguration<ManagementNote>
{
    public void Configure(EntityTypeBuilder<ManagementNote> b)
    {
        b.ToTable("management_notes");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.NoteType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Body).IsRequired().HasMaxLength(2000);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.AuthorId);
    }
}
