using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Development;

/// <summary>احتياج تدريبي لموظف، يُرصد يدويًا أو من تحليل المؤشرات.</summary>
public class TrainingNeed : BaseEntity
{
    public Guid SubjectUserId { get; set; }
    public Guid RaisedById { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Source { get; set; }
    public TrainingNeedStatus Status { get; set; } = TrainingNeedStatus.Open;
    public Guid? RelatedKpiEvaluationId { get; set; }
}
