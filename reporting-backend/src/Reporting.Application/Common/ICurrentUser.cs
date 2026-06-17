namespace Reporting.Application.Common;

/// <summary>وصول مجرّد للمستخدم الحالي — أساس فحوص التفويض القائمة على المورد (منع IDOR/BOLA).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsInRole(string role);
    bool IsInAnyRole(params string[] roles);
}
