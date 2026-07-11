using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// تيار عمل داخل مشروع (P1 — منصّة التنفيذ العامة). يسمح للمشروع الواحد باحتواء عدّة تيّارات تنفيذ،
/// لكلٍّ منها فريق مسؤول ومدير مسؤول اختياري. مثال: مشروع موقع ⇐ تطوير الموقع/فريق البرمجة،
/// تصميم الواجهة/فريق التصميم، محتوى الموقع/فريق المحتوى، ضبط SEO/فريق السيو.
/// </summary>
public class ProjectWorkstream : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>رمز نوع تيار العمل من كتالوج التصنيفات (Domain=workstream_type). نصّ فقط — لا مفتاح أجنبيّ (تفادي الترابط الزائد).</summary>
    public string WorkstreamTypeCode { get; set; } = string.Empty;

    /// <summary>اسم تيار العمل (اختياريّ عند الإدخال — يُشتقّ من NameAr للتصنيف أو الرمز إن تُرك فارغًا).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>الفريق المسؤول عن تنفيذ تيار العمل (إلزاميّ).</summary>
    public Guid ResponsibleTeamId { get; set; }

    /// <summary>المدير المسؤول عن تيار العمل (اختياريّ — قد تكون بيانات المديرين غير مستقرّة بعد).</summary>
    public Guid? ResponsibleManagerId { get; set; }

    public WorkstreamStatus Status { get; set; } = WorkstreamStatus.Active;
    public int SortOrder { get; set; }
    public string? Notes { get; set; }

    /// <summary>التعطيل بدل الحذف — BaseEntity لا يحمل IsActive فيُعرَّف هنا (نمط Team/Department).</summary>
    public bool IsActive { get; set; } = true;
}
