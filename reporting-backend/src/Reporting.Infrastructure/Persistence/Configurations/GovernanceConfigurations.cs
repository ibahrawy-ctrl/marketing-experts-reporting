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

public class GovernanceItemConfiguration : IEntityTypeConfiguration<GovernanceItem>
{
    public void Configure(EntityTypeBuilder<GovernanceItem> b)
    {
        b.ToTable("governance_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ApplicationScope).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ResolutionSummary).HasMaxLength(2000);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.ApplicationScope);
        b.HasIndex(x => x.Category);
        b.HasIndex(x => x.CreatedById);
        b.HasIndex(x => x.AssignedToUserId);
        b.HasIndex(x => x.DepartmentId);
        b.HasIndex(x => x.TeamId);
        b.HasIndex(x => x.RelatedSubmissionId);
        b.HasIndex(x => x.RelatedUserId);
        b.HasMany(x => x.Updates)
            .WithOne(u => u.GovernanceItem!)
            .HasForeignKey(u => u.GovernanceItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GovernanceItemUpdateConfiguration : IEntityTypeConfiguration<GovernanceItemUpdate>
{
    public void Configure(EntityTypeBuilder<GovernanceItemUpdate> b)
    {
        b.ToTable("governance_item_updates");
        b.HasKey(x => x.Id);
        b.Property(x => x.UpdateType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Body).HasMaxLength(2000);
        b.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.GovernanceItemId);
        b.HasIndex(x => x.AuthorId);
    }
}

// ===== التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1) — كيان مستقلّ =====

public class GovernanceEscalationConfiguration : IEntityTypeConfiguration<GovernanceEscalation>
{
    public void Configure(EntityTypeBuilder<GovernanceEscalation> b)
    {
        b.ToTable("governance_escalations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.EscalationType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.TargetType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Resolution).HasMaxLength(2000);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.Severity);
        b.HasIndex(x => x.EscalationType);
        b.HasIndex(x => x.RaisedByUserId);
        b.HasIndex(x => x.AssignedToUserId);
        b.HasIndex(x => x.TargetUserId);
        b.HasIndex(x => x.TargetDepartmentId);
        b.HasIndex(x => x.TargetTeamId);
        b.HasIndex(x => x.RelatedSubmissionId);
        b.HasIndex(x => x.RelatedGovernanceItemId);
        b.HasIndex(x => x.CreatedAtUtc);
        b.HasMany(x => x.Updates)
            .WithOne(u => u.Escalation!)
            .HasForeignKey(u => u.EscalationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GovernanceEscalationUpdateConfiguration : IEntityTypeConfiguration<GovernanceEscalationUpdate>
{
    public void Configure(EntityTypeBuilder<GovernanceEscalationUpdate> b)
    {
        b.ToTable("governance_escalation_updates");
        b.HasKey(x => x.Id);
        b.Property(x => x.UpdateType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Body).HasMaxLength(2000);
        b.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => x.EscalationId);
        b.HasIndex(x => x.AuthorId);
    }
}

// ===== إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1) — كيان مستقلّ =====

public class GovernanceActionItemConfiguration : IEntityTypeConfiguration<GovernanceActionItem>
{
    public void Configure(EntityTypeBuilder<GovernanceActionItem> b)
    {
        b.ToTable("governance_action_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.CompletionNote).HasMaxLength(2000);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.Priority);
        b.HasIndex(x => x.AssignedToUserId);
        b.HasIndex(x => x.CreatedByUserId);
        b.HasIndex(x => x.DueDate);
        b.HasIndex(x => new { x.SourceType, x.SourceId });
        b.HasIndex(x => x.CreatedAtUtc);
        b.HasMany(x => x.Updates)
            .WithOne(u => u.ActionItem!)
            .HasForeignKey(u => u.ActionItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GovernanceActionItemUpdateConfiguration : IEntityTypeConfiguration<GovernanceActionItemUpdate>
{
    public void Configure(EntityTypeBuilder<GovernanceActionItemUpdate> b)
    {
        b.ToTable("governance_action_item_updates");
        b.HasKey(x => x.Id);
        b.Property(x => x.UpdateType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Body).HasMaxLength(2000);
        b.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.ActionItemId);
        b.HasIndex(x => x.AuthorId);
    }
}
