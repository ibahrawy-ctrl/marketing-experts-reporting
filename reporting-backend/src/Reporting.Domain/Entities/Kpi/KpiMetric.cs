using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Kpi;

/// <summary>مؤشر مفرد ضمن إصدار KPI، له وزن وهدف وطريقة احتساب.</summary>
public class KpiMetric : BaseEntity
{
    public Guid KpiTemplateVersionId { get; set; }
    public KpiTemplateVersion? KpiTemplateVersion { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
    /// <summary>الوزن النسبي (مجموع أوزان الإصدار = 100).</summary>
    public decimal Weight { get; set; }
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public KpiCalcMethod CalcMethod { get; set; } = KpiCalcMethod.Manual;
    /// <summary>تعريف الاحتساب التلقائي/الهجين (المصدر، الصيغة) كـ JSONB.</summary>
    public string? CalcConfigJson { get; set; }
}
