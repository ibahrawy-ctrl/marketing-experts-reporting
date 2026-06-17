using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Kpi;

/// <summary>قالب مؤشرات أداء مرتبط بمسمى وظيفي ودورية (نبض أسبوعي/ربع سنوي).</summary>
public class KpiTemplate : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? JobRoleId { get; set; }
    public KpiCadence Cadence { get; set; } = KpiCadence.WeeklyPulse;
    public TemplateStatus Status { get; set; } = TemplateStatus.Draft;
    public Guid OwnerId { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<KpiTemplateVersion> Versions { get; set; } = new List<KpiTemplateVersion>();
}
