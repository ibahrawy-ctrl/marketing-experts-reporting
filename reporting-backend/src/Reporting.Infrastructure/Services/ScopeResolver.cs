using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// يحسب نطاق رؤية المستخدم من شجرة التسلسل الإداري (ManagerId):
/// own = نفسه فقط، team = نفسه + مرؤوسوه المباشرون، department = شجرته الكاملة،
/// company/governance = كل المستخدمين النشطين.
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
        return new ScopeContext(scopeType, ids, seesAll);
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
