using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Submissions;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class ReportSubmissionConfiguration : IEntityTypeConfiguration<ReportSubmission>
{
    public void Configure(EntityTypeBuilder<ReportSubmission> b)
    {
        b.ToTable("report_submissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.PeriodKey).IsRequired().HasMaxLength(30);
        b.Property(x => x.PeriodType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        // عدم تكرار التسليم لنفس الفترة من نفس المُرسِل لنفس إصدار القالب (BR).
        b.HasIndex(x => new { x.ReportTemplateVersionId, x.SubmitterId, x.PeriodKey }).IsUnique();
        b.HasIndex(x => x.SubmitterId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.CurrentApproverId);
        b.HasIndex(x => x.ClientId);
        b.HasIndex(x => x.ProjectId);
        b.HasMany(x => x.FieldValues).WithOne(x => x.ReportSubmission!)
            .HasForeignKey(x => x.ReportSubmissionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.ApprovalSteps).WithOne(x => x.ReportSubmission!)
            .HasForeignKey(x => x.ReportSubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SubmissionFieldValueConfiguration : IEntityTypeConfiguration<SubmissionFieldValue>
{
    public void Configure(EntityTypeBuilder<SubmissionFieldValue> b)
    {
        b.ToTable("submission_field_values");
        b.HasKey(x => x.Id);
        b.Property(x => x.ValueJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.ReportSubmissionId, x.TemplateFieldId }).IsUnique();
    }
}

public class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> b)
    {
        b.ToTable("approval_steps");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => x.ReportSubmissionId);
        b.HasIndex(x => x.ApproverId);
    }
}
