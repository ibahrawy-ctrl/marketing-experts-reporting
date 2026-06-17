using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Development;

/// <summary>خطة تحسين أداء لموظف، مرتبطة باحتياج/تقييم.</summary>
public class ImprovementPlan : BaseEntity
{
    public Guid SubjectUserId { get; set; }
    public Guid OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ImprovementPlanStatus Status { get; set; } = ImprovementPlanStatus.Open;
    public DateTime? DueDateUtc { get; set; }
    public Guid? RelatedTrainingNeedId { get; set; }
}
