using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Kpi;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class KpiTemplateConfiguration : IEntityTypeConfiguration<KpiTemplate>
{
    public void Configure(EntityTypeBuilder<KpiTemplate> b)
    {
        b.ToTable("kpi_templates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Cadence).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => x.JobRoleId);
        b.HasMany(x => x.Versions).WithOne(x => x.KpiTemplate!)
            .HasForeignKey(x => x.KpiTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class KpiTemplateVersionConfiguration : IEntityTypeConfiguration<KpiTemplateVersion>
{
    public void Configure(EntityTypeBuilder<KpiTemplateVersion> b)
    {
        b.ToTable("kpi_template_versions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.KpiTemplateId, x.VersionNumber }).IsUnique();
        b.HasMany(x => x.Metrics).WithOne(x => x.KpiTemplateVersion!)
            .HasForeignKey(x => x.KpiTemplateVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class KpiMetricConfiguration : IEntityTypeConfiguration<KpiMetric>
{
    public void Configure(EntityTypeBuilder<KpiMetric> b)
    {
        b.ToTable("kpi_metrics");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(300);
        b.Property(x => x.Unit).HasMaxLength(50);
        b.Property(x => x.CalcMethod).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.CalcConfigJson).HasColumnType("jsonb");
        b.HasIndex(x => x.KpiTemplateVersionId);
    }
}

public class KpiEvaluationConfiguration : IEntityTypeConfiguration<KpiEvaluation>
{
    public void Configure(EntityTypeBuilder<KpiEvaluation> b)
    {
        b.ToTable("kpi_evaluations");
        b.HasKey(x => x.Id);
        b.Property(x => x.PeriodKey).IsRequired().HasMaxLength(30);
        b.Property(x => x.PeriodType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Trend).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.DeletionReason).HasMaxLength(1000);
        b.Property(x => x.ReviewNote).HasMaxLength(2000);
        b.HasIndex(x => new { x.KpiTemplateVersionId, x.SubjectUserId, x.PeriodKey }).IsUnique();
        b.HasIndex(x => x.SubjectUserId);
        b.HasMany(x => x.Results).WithOne(x => x.KpiEvaluation!)
            .HasForeignKey(x => x.KpiEvaluationId).OnDelete(DeleteBehavior.Cascade);
        // سجلّ الحوكمة: RESTRICT كي لا يُمحى التاريخ عند أيّ حذف (الحذف إداريّ ناعم أصلًا، لا حذف صفوف).
        b.HasMany(x => x.ReviewEvents).WithOne(x => x.KpiEvaluation!)
            .HasForeignKey(x => x.KpiEvaluationId).OnDelete(DeleteBehavior.Restrict);
        // الحذف الإداريّ الناعم — Global Query Filter يستبعد المحذوف من كل الاستعلامات (IgnoreQueryFilters في الحوكمة فقط).
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class KpiEvaluationReviewEventConfiguration : IEntityTypeConfiguration<KpiEvaluationReviewEvent>
{
    public void Configure(EntityTypeBuilder<KpiEvaluationReviewEvent> b)
    {
        b.ToTable("kpi_evaluation_review_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired().HasMaxLength(40);
        b.Property(x => x.FromStatus).HasMaxLength(30);
        b.Property(x => x.ToStatus).HasMaxLength(30);
        b.Property(x => x.PreviousValuesJson).HasColumnType("jsonb");
        b.Property(x => x.Reason).HasMaxLength(2000);
        b.HasIndex(x => new { x.KpiEvaluationId, x.CreatedAtUtc });
        // لا Global Query Filter على السجلّ نفسه — يُقرأ صراحةً في شاشات الحوكمة (بما فيها لتقييمات محذوفة).
    }
}

public class KpiResultConfiguration : IEntityTypeConfiguration<KpiResult>
{
    public void Configure(EntityTypeBuilder<KpiResult> b)
    {
        b.ToTable("kpi_results");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.KpiEvaluationId, x.KpiMetricId }).IsUnique();
    }
}

public class KpiTemplateAssignmentConfiguration : IEntityTypeConfiguration<KpiTemplateAssignment>
{
    public void Configure(EntityTypeBuilder<KpiTemplateAssignment> b)
    {
        b.ToTable("kpi_template_assignments");
        b.HasKey(x => x.Id);
        b.Property(x => x.ScopeType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => x.KpiTemplateId);
        b.HasIndex(x => new { x.KpiTemplateId, x.ScopeType, x.ScopeId, x.Kind }).IsUnique();
        b.HasOne(x => x.KpiTemplate).WithMany()
            .HasForeignKey(x => x.KpiTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}
