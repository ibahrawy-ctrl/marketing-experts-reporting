using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.EmployeeServices;

/// <summary>
/// سياسة أرصدة قابلة للإعداد (لا hardcode لقانون الإجازات). الكروت والحدود تُقرأ منها.
/// غياب سياسة سنة ما ⇒ لا حدود + الرصيد الافتتاحي يدوي. JobRoleId = null ⇒ السياسة العامة الافتراضية.
/// </summary>
public class BalancePolicy : BaseEntity
{
    /// <summary>السنة التي تنطبق عليها السياسة.</summary>
    public int Year { get; set; }

    /// <summary>null = سياسة عامة افتراضية؛ (لكل مسمّى وظيفي لاحقًا — إضافي).</summary>
    public Guid? JobRoleId { get; set; }

    /// <summary>الرصيد السنوي الافتراضي للإجازات.</summary>
    public decimal AnnualLeaveDefaultDays { get; set; }

    /// <summary>وحدة احتساب الأذونات (الافتراضي: Count).</summary>
    public PermissionUnit PermissionUnit { get; set; } = PermissionUnit.Count;

    /// <summary>حد الأذونات الشهري (اختياري).</summary>
    public decimal? PermissionMonthlyLimit { get; set; }

    /// <summary>حد الأذونات السنوي (اختياري).</summary>
    public decimal? PermissionAnnualLimit { get; set; }

    /// <summary>هل يُسمح بالرصيد السالب (MVP: true مع تحذير في الواجهة).</summary>
    public bool AllowNegativeBalance { get; set; } = true;
}
