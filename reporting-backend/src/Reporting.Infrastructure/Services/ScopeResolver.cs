using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// يحسب نطاق رؤية المستخدم من شجرة التسلسل الإداري (ManagerId):
/// own = نفسه فقط، team = نفسه + مرؤوسوه المباشرون، department = شجرته الكاملة،
/// company/governance = كل المستخدمين النشطين.
/// ثم يُوحَّد ذلك مع نطاقات الرؤية من المناصب المرنة المُسنَدة للمستخدم (Phase 1A — رؤية فقط):
/// النطاق النهائي = نطاق الدور ∪ نطاقات المناصب المُفعّلة التي تحمل صلاحية رؤية.
/// هذا يوسّع الرؤية فقط؛ لا يمنح أي قدرة اعتماد (الاعتماد محكوم بـ CurrentApproverId).
/// </summary>
public class ScopeResolver : IScopeResolver
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ScopeResolver(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public Task<ScopeContext> ResolveAsync(CancellationToken ct = default)
    {
        var uid = _currentUser.UserId ?? Guid.Empty;
        return ResolveForAsync(uid, _currentUser.Roles, ct);
    }

    public async Task<ScopeContext> ResolveForAsync(Guid userId, IEnumerable<string> roles, CancellationToken ct = default)
    {
        var scopeType = RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(roles));
        var seesAll = scopeType is "company" or "governance";
        var ids = await ResolveScopeIdsAsync(userId, scopeType, ct);

        // اتحاد نطاقات المناصب المرنة (Phase 1A — رؤية فقط) فوق نطاق الدور.
        var (positionIds, positionSeesAll) = await ResolvePositionScopeAsync(userId, ct);
        if (positionSeesAll) seesAll = true;
        if (positionIds.Count > 0)
            ids = ids.Concat(positionIds).Distinct().ToList();

        return new ScopeContext(scopeType, ids, seesAll);
    }

    /// <summary>
    /// يترجم نطاقات المناصب المُفعّلة المُسنَدة للمستخدم إلى مجموعة مستخدمين قابلة للاتحاد.
    /// يُحتسَب فقط للمناصب المُفعّلة التي تحمل صلاحية رؤية (reports.view أو dashboard.view).
    /// AllCompany ⇒ رؤية الكل. لا يمسّ منطق الاعتماد إطلاقًا.
    /// </summary>
    private async Task<(List<Guid> Ids, bool SeesAll)> ResolvePositionScopeAsync(Guid userId, CancellationToken ct)
    {
        var scopes = await (
            from up in _db.UserPositions
            join p in _db.Positions on up.PositionId equals p.Id
            join sc in _db.PositionScopes on p.Id equals sc.PositionId
            where up.UserId == userId
                  && p.IsActive
                  && p.Permissions.Any(perm =>
                        perm.PermissionKey == PositionPermissions.ReportsView
                        || perm.PermissionKey == PositionPermissions.DashboardView)
            select new { sc.Kind, sc.DepartmentId, sc.TeamId, sc.TargetUserId })
            .ToListAsync(ct);

        if (scopes.Count == 0) return (new List<Guid>(), false);
        if (scopes.Any(s => s.Kind == PositionScopeKind.AllCompany))
        {
            var all = await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct);
            return (all, true);
        }

        var deptIds = scopes.Where(s => s.Kind == PositionScopeKind.Department && s.DepartmentId != null)
            .Select(s => s.DepartmentId!.Value).Distinct().ToList();
        var teamIds = scopes.Where(s => s.Kind == PositionScopeKind.Team && s.TeamId != null)
            .Select(s => s.TeamId!.Value).Distinct().ToList();
        var directUserIds = scopes.Where(s => s.Kind == PositionScopeKind.SpecificUsers && s.TargetUserId != null)
            .Select(s => s.TargetUserId!.Value).Distinct().ToList();

        var result = new HashSet<Guid>(directUserIds);
        if (deptIds.Count > 0)
        {
            var deptUsers = await _db.Users.Where(u => u.DepartmentId != null && deptIds.Contains(u.DepartmentId.Value))
                .Select(u => u.Id).ToListAsync(ct);
            foreach (var id in deptUsers) result.Add(id);
        }
        if (teamIds.Count > 0)
        {
            var teamUsers = await _db.Users.Where(u => u.TeamId != null && teamIds.Contains(u.TeamId.Value))
                .Select(u => u.Id).ToListAsync(ct);
            foreach (var id in teamUsers) result.Add(id);
        }
        return (result.ToList(), false);
    }

    private async Task<List<Guid>> ResolveScopeIdsAsync(Guid uid, string scopeType, CancellationToken ct)
    {
        switch (scopeType)
        {
            case "own":
                return new List<Guid> { uid };

            case "team":
            {
                var reports = await _db.Users.Where(u => u.ManagerId == uid).Select(u => u.Id).ToListAsync(ct);
                reports.Add(uid);
                return reports.Distinct().ToList();
            }

            case "department":
            {
                // BFS على شجرة المرؤوسين (تقارير التقارير).
                var all = await _db.Users
                    .Where(u => u.ManagerId != null)
                    .Select(u => new { u.Id, u.ManagerId })
                    .ToListAsync(ct);
                var byManager = all.GroupBy(x => x.ManagerId!.Value)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

                var result = new HashSet<Guid> { uid };
                var queue = new Queue<Guid>();
                queue.Enqueue(uid);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (!byManager.TryGetValue(current, out var children)) continue;
                    foreach (var child in children)
                        if (result.Add(child)) queue.Enqueue(child);
                }
                return result.ToList();
            }

            default: // company / governance
                return await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct);
        }
    }
}
