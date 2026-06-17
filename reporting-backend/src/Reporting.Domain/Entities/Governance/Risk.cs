using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Governance;

/// <summary>مخاطرة تشغيلية مرصودة، لها شدة وحالة ومالك.</summary>
public class Risk : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RiskSeverity Severity { get; set; } = RiskSeverity.Medium;
    public RiskStatus Status { get; set; } = RiskStatus.Open;
    public Guid OwnerId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? MitigationPlan { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    // ربط سياقي: المخاطرة قد تنشأ عن تقرير متأخر أو KPI منخفض أو تخص موظفًا/فريقًا.
    public Guid? RelatedSubmissionId { get; set; }
    public Guid? RelatedKpiEvaluationId { get; set; }
    public Guid? SubjectUserId { get; set; }
    public Guid? TeamId { get; set; }

    // ربط ببُعد العميل/المشروع (Phase 6) — اختياري وإضافي.
    public Guid? ClientId { get; set; }
    public Guid? ProjectId { get; set; }

    /// <summary>الإجراء التالي المطلوب لمعالجة المخاطرة.</summary>
    public string? NextAction { get; set; }
}
