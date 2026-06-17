using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Governance;

/// <summary>تصعيد مرتبط بتسليم/مخاطرة، يُرفع لجهة أعلى.</summary>
public class Escalation : BaseEntity
{
    public Guid RaisedById { get; set; }
    public Guid TargetUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public EscalationStatus Status { get; set; } = EscalationStatus.Open;
    public Guid? ReportSubmissionId { get; set; }
    public Guid? RiskId { get; set; }
    public Guid? KpiEvaluationId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? Resolution { get; set; }

    /// <summary>الإجراء التالي المطلوب بعد التصعيد (مثلاً: قرار إداري).</summary>
    public string? NextAction { get; set; }
}
