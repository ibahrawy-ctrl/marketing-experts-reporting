using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// مُخرَج ضمن خطّة إنتاج تيار العمل (P2 — منصّة التنفيذ العامة). يحتوي تيّار العمل الواحد على عدد
/// غير محدود من المخرَجات المخطَّطة (مثل: سوشيال ⇐ منشورات/كاروسيل/ريلز/ستوري/موشن).
/// هذا سجلّ **تخطيط** فقط — لا يُسجَّل هنا أيّ تنفيذ فعليّ (يأتي التنفيذ في مرحلة لاحقة P4).
/// </summary>
public class WorkstreamDeliverable : BaseEntity
{
    public Guid WorkstreamId { get; set; }
    public ProjectWorkstream? Workstream { get; set; }

    /// <summary>رمز نوع المخرَج من كتالوج التصنيفات (Domain=deliverable). نصّ فقط — لا مفتاح أجنبيّ.</summary>
    public string DeliverableTypeCode { get; set; } = string.Empty;

    /// <summary>رمز سياق الاستخدام من كتالوج التصنيفات (Domain=usage_context). اختياريّ — نصّ فقط بلا FK.</summary>
    public string? UsageContextCode { get; set; }

    /// <summary>اسم المخرَج المعروض (يُشتقّ من NameAr لنوع المخرَج أو الرمز إن تُرك فارغًا).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>الكمية المخطَّطة (عدد النُّسخ/الوحدات المطلوب إنتاجها).</summary>
    public int PlannedQuantity { get; set; }

    /// <summary>الساعات المقدَّرة لإنجاز المخرَج (اختياريّة).</summary>
    public decimal? EstimatedHours { get; set; }

    /// <summary>تاريخ بدء العمل المخطَّط (اختياريّ).</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>تاريخ الاستحقاق المخطَّط (اختياريّ).</summary>
    public DateOnly? DueDate { get; set; }

    public DeliverablePriority Priority { get; set; } = DeliverablePriority.Medium;

    /// <summary>المسؤول عن إنتاج المخرَج (مرجع مستخدم اختياريّ).</summary>
    public Guid? ResponsibleUserId { get; set; }

    public string? Notes { get; set; }
    public int SortOrder { get; set; }

    /// <summary>التعطيل بدل الحذف — نمط تيار العمل. BaseEntity لا يحمل IsActive فيُعرَّف هنا.</summary>
    public bool IsActive { get; set; } = true;
}
