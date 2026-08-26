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

    /// <summary>
    /// B-6 — عتبة «دون المستهدف» المملوكة لهذا الإصدار تحديدًا. وُضِعت هنا لا على <c>KpiMetric</c>
    /// لأنّ الإصدار هو الكيان الذي ترتبط به التقييمات وتتجمّد عنده القواعد؛ فتغييرها لاحقًا
    /// يُنتج إصدارًا جديدًا ولا يعيد كتابة حكم التقييمات التاريخيّة بأثر رجعيّ.
    /// <c>null</c> = لا عتبة على مستوى الإصدار ⇒ يُسقَط إلى الإعداد المركزيّ (بلا Backfill).
    /// </summary>
    public decimal? BelowTargetThreshold { get; set; }

    public ICollection<KpiMetric> Metrics { get; set; } = new List<KpiMetric>();
}
