using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Kpi;

/// <summary>إصدار من قالب الـKPI؛ التقييمات ترتبط بإصدار محدد.</summary>
public class KpiTemplateVersion : BaseEntity
{
    public Guid KpiTemplateId { get; set; }
    public KpiTemplate? KpiTemplate { get; set; }
    public int VersionNumber { get; set; } = 1;
    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? PublishedById { get; set; }

    public ICollection<KpiMetric> Metrics { get; set; } = new List<KpiMetric>();
}
