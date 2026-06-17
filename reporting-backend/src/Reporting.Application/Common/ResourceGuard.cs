namespace Reporting.Application.Common;

/// <summary>
/// التفويض القائم على المورد (Resource-Based Authorization) — يمنع IDOR/BOLA.
/// يُستدعى في الخدمات بعد تحميل المورد للتأكد أنه يخص المستخدم الحالي
/// أو أنّ المستخدم يملك دورًا أعلى مخوّلًا بالاطّلاع.
/// </summary>
public static class ResourceGuard
{
    public static Result EnsureOwnerOrElevated(ICurrentUser user, Guid ownerId, params string[] elevatedRoles)
    {
        if (!user.IsAuthenticated || user.UserId is null)
            return Result.Failure("غير مصرّح.", "auth.unauthenticated");

        if (user.UserId == ownerId)
            return Result.Success();

        if (user.IsInRole(Roles.Admin))
            return Result.Success();

        if (elevatedRoles.Length > 0 && user.IsInAnyRole(elevatedRoles))
            return Result.Success();

        return Result.Failure("لا تملك صلاحية الوصول لهذا المورد.", "auth.forbidden");
    }

    public static bool CanAccess(ICurrentUser user, Guid ownerId, params string[] elevatedRoles)
        => EnsureOwnerOrElevated(user, ownerId, elevatedRoles).Succeeded;
}
