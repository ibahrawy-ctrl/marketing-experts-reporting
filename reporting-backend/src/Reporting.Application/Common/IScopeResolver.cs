namespace Reporting.Application.Common;

/// <summary>
/// نطاق رؤية المستخدم الحالي: نوع النطاق + معرّفات المستخدمين الداخلين فيه.
/// </summary>
public sealed record ScopeContext(string ScopeType, IReadOnlyList<Guid> UserIds, bool SeesAll)
{
    private HashSet<Guid>? _set;
    private HashSet<Guid> Set => _set ??= UserIds.ToHashSet();

    /// <summary>هل يقع المستخدم المحدّد ضمن نطاق رؤية المستخدم الحالي؟</summary>
    public bool Contains(Guid userId) => SeesAll || Set.Contains(userId);
}

/// <summary>
/// مصدر موحّد لحساب نطاق رؤية المستخدم الحالي (own/team/department/company/governance)
/// من شجرة التسلسل الإداري (ManagerId). يُستخدم في كل الخدمات لمنع تسريب البيانات خارج النطاق.
/// </summary>
public interface IScopeResolver
{
    /// <summary>يحسب نطاق رؤية المستخدم الحالي اعتمادًا على أدواره.</summary>
    Task<ScopeContext> ResolveAsync(CancellationToken ct = default);

    /// <summary>يحسب نطاق الرؤية لمستخدم معيّن وأدواره (لاستخدامات خاصة).</summary>
    Task<ScopeContext> ResolveForAsync(Guid userId, IEnumerable<string> roles, CancellationToken ct = default);
}
