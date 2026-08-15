using Reporting.Application.Common;
using Reporting.Domain.Enums;

namespace Reporting.Application.Documents;

/// <summary>
/// مجموعات الأدوار المقابلة لأنواع سياسة رؤية المستندات (CPW-R2).
/// مصدر واحد للحقيقة يستهلكه المقيّم المركزيّ والتحقّق من طلبات الكتابة والاختبارات.
/// <para><b>تنبيه:</b> <c>ManagementAndFinance</c> ليست <c>FinanceOnly</c> — الأولى تجمع الإدارة والمالية، والثانية تقصر الرؤية على المالية.</para>
/// </summary>
public static class DocumentVisibilityPolicy
{
    /// <summary>الإدارة العليا/المديرون — نفس مجموعة إدارة بيانات العميل الأساسيّة.</summary>
    public static readonly string[] Management = Roles.ClientCoreManagers;

    /// <summary>المالية فقط (لا تشمل الإدارة).</summary>
    public static readonly string[] Finance = { Roles.FinanceManager, Roles.Accountant };

    /// <summary>الموارد البشريّة + القيادة التنفيذيّة.</summary>
    public static readonly string[] HrManagement = { Roles.Hr, Roles.Admin, Roles.Ceo, Roles.GeneralManager };

    /// <summary>أنواع السياسة التي تتطلّب قائمة أدوار صريحة.</summary>
    public static bool RequiresRoles(DocumentVisibilityType type) => type == DocumentVisibilityType.CustomRoles;

    /// <summary>أنواع السياسة التي تتطلّب قائمة مستخدمين صريحة.</summary>
    public static bool RequiresUsers(DocumentVisibilityType type) => type == DocumentVisibilityType.CustomUsers;

    /// <summary>هل اسم الدور ضمن أدوار النظام المعتمَدة (<c>Roles.All</c>)؟ — يُرفَض أيّ اسم خارجها.</summary>
    public static bool IsKnownRole(string? roleName)
        => !string.IsNullOrWhiteSpace(roleName)
           && Roles.All.Any(r => string.Equals(r, roleName.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>يُعيد الاسم القانونيّ للدور كما هو في <c>Roles.All</c> (تطبيع حالة الأحرف).</summary>
    public static string? CanonicalRole(string? roleName)
        => string.IsNullOrWhiteSpace(roleName)
            ? null
            : Roles.All.FirstOrDefault(r => string.Equals(r, roleName.Trim(), StringComparison.OrdinalIgnoreCase));
}
