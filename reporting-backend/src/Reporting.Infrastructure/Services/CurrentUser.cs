using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Reporting.Application.Common;
using Reporting.Application.Security;

namespace Reporting.Infrastructure.Services;

/// <summary>تنفيذ ICurrentUser بقراءة مطالبات JWT من HttpContext.</summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    /// <summary>مطالبات <c>perm</c> فقط — بلا أيّ اشتقاق من الأدوار (P2-SEC-001).</summary>
    public IReadOnlyCollection<string> Permissions =>
        Principal?.FindAll(AppPermissions.ClaimType).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;

    public bool IsInAnyRole(params string[] roles) => roles.Any(IsInRole);

    public bool HasPermission(string permissionKey) =>
        Principal?.HasClaim(AppPermissions.ClaimType, permissionKey) == true;
}
