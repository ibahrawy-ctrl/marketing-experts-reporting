using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Kpi;

/// <summary>نتيجة مؤشر مفرد ضمن تقييم: القيمة الخام والدرجة المحتسبة.</summary>
public class KpiResult : BaseEntity
{
    public Guid KpiEvaluationId { get; set; }
    public KpiEvaluation? KpiEvaluation { get; set; }
    public Guid KpiMetricId { get; set; }

    public decimal? RawValue { get; set; }
    /// <summary>درجة المؤشر بعد التطبيع (0–100) قبل ضرب الوزن.</summary>
    public decimal? Score { get; set; }
    /// <summary>الوزن وقت الاحتساب (لقطة تاريخية).</summary>
    public decimal Weight { get; set; }
    public string? Note { get; set; }
}
