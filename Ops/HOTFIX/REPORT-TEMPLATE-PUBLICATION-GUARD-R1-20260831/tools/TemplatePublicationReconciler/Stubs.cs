using Reporting.Application.Common;

namespace TemplatePublicationReconciler;

/// <summary>
/// بدائل غير متّصلة لتبعيّات <c>ReportTemplateService</c> التي لا يلمسها مسار النشر إطلاقًا.
/// <c>PublishVersionAsync</c> يستعمل <c>AppDbContext</c> وحده (لا <c>ICurrentUser</c> ولا <c>IScopeResolver</c>)،
/// ولذلك تُمرَّر هذه البدائل بلا أثر على السلوك؛ التدقيق يمرّ بخدمة التدقيق الحقيقيّة.
/// </summary>
internal sealed class OfflineCurrentUser : ICurrentUser
{
    public OfflineCurrentUser(Guid actorId) => UserId = actorId;

    public Guid? UserId { get; }
    public bool IsAuthenticated => true;
    public IReadOnlyCollection<string> Roles { get; } = new[] { "Admin" };
    public IReadOnlyCollection<string> Permissions { get; } = Array.Empty<string>();

    public bool IsInRole(string role) => Roles.Contains(role);
    public bool IsInAnyRole(params string[] roles) => roles.Any(Roles.Contains);
    public bool HasPermission(string permissionKey) => false;
}

internal sealed class OfflineScopeResolver : IScopeResolver
{
    public Task<ScopeContext> ResolveAsync(CancellationToken ct = default)
        => Task.FromResult(new ScopeContext("governance", Array.Empty<Guid>(), true));

    public Task<ScopeContext> ResolveForAsync(Guid userId, IEnumerable<string> roles, CancellationToken ct = default)
        => Task.FromResult(new ScopeContext("governance", Array.Empty<Guid>(), true));
}
