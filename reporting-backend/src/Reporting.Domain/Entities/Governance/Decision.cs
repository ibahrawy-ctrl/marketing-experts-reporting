using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Governance;

/// <summary>قرار إداري موثّق، قد يرتبط بتسليم أو مخاطرة أو تصعيد.</summary>
public class Decision : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid MadeById { get; set; }
    public DecisionStatus Status { get; set; } = DecisionStatus.Proposed;
    public Guid? RelatedSubmissionId { get; set; }
    public Guid? RelatedRiskId { get; set; }
    public Guid? RelatedEscalationId { get; set; }
    public Guid? RelatedKpiEvaluationId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }

    /// <summary>الإجراء التالي لمتابعة تنفيذ القرار.</summary>
    public string? NextAction { get; set; }
}
