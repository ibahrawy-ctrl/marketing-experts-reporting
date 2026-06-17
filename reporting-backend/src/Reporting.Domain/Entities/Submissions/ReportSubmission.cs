using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Submissions;

/// <summary>تسليم تقرير لفترة محددة؛ يمر بدورة حياة من 8 حالات.</summary>
public class ReportSubmission : BaseEntity
{
    public Guid ReportTemplateVersionId { get; set; }
    public Guid SubmitterId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? DepartmentId { get; set; }

    // ربط اختياري ببُعد العميل/المشروع (Phase 6) — إضافي وغير ملزم؛
    // التقرير العام يبقى بلا ربط، والتقارير الخاصة بمشروع تحمل المعرّفين.
    public Guid? ClientId { get; set; }
    public Guid? ProjectId { get; set; }

    public PeriodType PeriodType { get; set; } = PeriodType.Weekly;
    /// <summary>مفتاح الفترة القابل للمقارنة، مثل 2026-W23 أو 2026-Q2 — يضمن عدم تكرار التسليم للفترة.</summary>
    public string PeriodKey { get; set; } = string.Empty;

    public SubmissionStatus Status { get; set; } = SubmissionStatus.Draft;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public Guid? CurrentApproverId { get; set; }

    public ICollection<SubmissionFieldValue> FieldValues { get; set; } = new List<SubmissionFieldValue>();
    public ICollection<ApprovalStep> ApprovalSteps { get; set; } = new List<ApprovalStep>();
}
