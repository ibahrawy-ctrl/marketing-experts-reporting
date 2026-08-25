using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Attendance;

/// <summary>
/// كتالوج أنواع حوادث الحضور (P2-ATT-005). جدول مرجعيّ قابل للتوسيع من البيانات لا من الكود،
/// يُبذَر بذرًا **مُتكافئ التنفيذ** (idempotent) على مفتاح <see cref="Code"/>.
/// </summary>
public class AttendanceIncidentType : BaseEntity
{
    /// <summary>الرمز الثابت غير المترجَم: <c>Late</c>, <c>Absence</c>, … مفتاح البذر والتفرّد.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>الاسم العربيّ المعروض.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>هل يستلزم هذا النوع أوقاتًا (بداية/عودة) لحساب المدّة.</summary>
    public bool RequiresTimes { get; set; }

    /// <summary>هل يستلزم إسنادًا إلى مرجع سياسة/لائحة.</summary>
    public bool RequiresPolicyReference { get; set; }

    /// <summary>هل يسمح بأكثر من حادثة من نفس النوع للموظّف نفسه في اليوم نفسه.</summary>
    public bool AllowsMultiplePerDay { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>ترتيب العرض في الواجهات.</summary>
    public int Order { get; set; }
}
