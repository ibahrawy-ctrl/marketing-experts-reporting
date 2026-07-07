using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Services;

/// <summary>
/// كتالوج خدمات B2B (مصدر أسماء الخدمات في تقرير مبيعات B2B حسب الخدمة).
/// إضافة بحتة على غرار كتالوج الدورات — لا يمسّ القوالب/التقارير القائمة.
/// </summary>
public class Service : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
