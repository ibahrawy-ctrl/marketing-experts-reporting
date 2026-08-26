namespace Reporting.Application.Common;

/// <summary>وصول مجرّد للمستخدم الحالي — أساس فحوص التفويض القائمة على المورد (منع IDOR/BOLA).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>
    /// مفاتيح الصلاحيّات الدقيقة الممنوحة صراحةً (مطالبات <c>perm</c>) — P2.
    /// فارغة لكلّ مستخدم لم تُسنَد له مطالبة صريحة؛ **لا دور يمنحها ضمنًا** ولا حتّى Admin.
    /// </summary>
    IReadOnlyCollection<string> Permissions { get; }

    bool IsInRole(string role);
    bool IsInAnyRole(params string[] roles);

    /// <summary>هل مُنِح مفتاح الصلاحيّة الدقيقة صراحةً؟</summary>
    bool HasPermission(string permissionKey);
}
