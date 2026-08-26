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
        b.Property(x => x.DeletionReason).HasMaxLength(1000);
        // عدم تكرار التسليم لنفس الفترة من نفس المُرسِل لنفس إصدار القالب (BR).
        // فهرس فريد جزئيّ يستثني المحذوف إداريًّا (IsDeleted=true) كي تُتاح إعادة التسليم لنفس الفترة بعد الحذف الناعم (ADMIN-GOVERNANCE-R1).
        b.HasIndex(x => new { x.ReportTemplateVersionId, x.SubmitterId, x.PeriodKey })
            .IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.SubmitterId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.CurrentApproverId);
        b.HasIndex(x => x.ClientId);
        b.HasIndex(x => x.ProjectId);
        b.HasMany(x => x.FieldValues).WithOne(x => x.ReportSubmission!)
            .HasForeignKey(x => x.ReportSubmissionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.ApprovalSteps).WithOne(x => x.ReportSubmission!)
            .HasForeignKey(x => x.ReportSubmissionId).OnDelete(DeleteBehavior.Cascade);
        // الحذف الإداريّ الناعم — Global Query Filter يستبعد المحذوف من كل الاستعلامات (IgnoreQueryFilters في الحوكمة فقط).
        b.HasQueryFilter(x => !x.IsDeleted);
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
        // فهرس GIN لاحتواء jsonb (PROJECT360-…-CLOSURE-R2 · ADR-R2-002): يخدم شرط اكتشاف تقارير
        // المشروع `ValueJson @> '[{"projectId":"…"}]'`. jsonb_path_ops أصغر وأسرع من الافتراضيّ لأنّه
        // يفهرس المسارات لا المفاتيح المنفردة، وهو كافٍ لأنّنا لا نستعمل إلّا الاحتواء. إضافيّ بحت.
        b.HasIndex(x => x.ValueJson)
            .HasDatabaseName("ix_submission_field_values_value_json_gin")
            .HasMethod("gin")
            .HasOperators("jsonb_path_ops");
    }
}

public class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> b)
    {
        b.ToTable("approval_steps");
        b.HasKey(x => x.Id);
        // 40 لاستيعاب CancelledByAdministrativeDeletion (33 حرفًا) — ADMIN-GOVERNANCE-R1.
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        b.HasIndex(x => x.ReportSubmissionId);
        b.HasIndex(x => x.ApproverId);
    }
}
