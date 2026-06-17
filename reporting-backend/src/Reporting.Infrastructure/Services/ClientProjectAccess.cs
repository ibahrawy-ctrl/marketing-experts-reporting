using Microsoft.EntityFrameworkCore;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// يحسب نطاق رؤية العملاء/المشاريع للمستخدم الحالي (Phase 6) من نطاق التسلسل الإداري
/// (IScopeResolver) + علاقات مدير الحساب/الفريق المسؤول/التسليمات. مفروض خادمًا في كل خدمات Phase 6.
/// </summary>
public class ClientProjectAccess : IClientProjectAccess
{
    private readonly AppDbContext _db;
    private readonly IScopeResolver _scope;

    public ClientProjectAccess(AppDbContext db, IScopeResolver scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<ClientProjectVisibility> ResolveAsync(CancellationToken ct = default)
    {
        var scope = await _scope.ResolveAsync(ct);
        if (scope.SeesAll)
            return new ClientProjectVisibility(true, new HashSet<Guid>(), new HashSet<Guid>());

        var uids = scope.UserIds.ToHashSet();

        // الفِرق التي يقودها مستخدم داخل النطاق (own/team/department).
        var teamIds = await _db.Teams
            .Where(t => t.TeamLeaderId != null && uids.Contains(t.TeamLeaderId.Value))
            .Select(t => t.Id)
            .ToListAsync(ct);
        var teamSet = teamIds.ToHashSet();

        // مشاريع: مدير حسابها داخل النطاق أو فريقها المسؤول يقوده أحد داخل النطاق.
        var byAmOrTeam = await _db.Projects
            .Where(p => (p.AccountManagerId != null && uids.Contains(p.AccountManagerId.Value))
                     || (p.OwnerTeamId != null && teamSet.Contains(p.OwnerTeamId.Value)))
            .Select(p => p.Id)
            .ToListAsync(ct);

        // مشاريع وُجد لها تسليم من داخل النطاق.
        var bySubmission = await _db.ReportSubmissions
            .Where(s => s.ProjectId != null && uids.Contains(s.SubmitterId))
            .Select(s => s.ProjectId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var projectIds = byAmOrTeam.Concat(bySubmission).ToHashSet();

        // عملاء: مدير حسابهم داخل النطاق، أو لديهم مشروع مرئي.
        var clientByAm = await _db.Clients
            .Where(c => c.AccountManagerId != null && uids.Contains(c.AccountManagerId.Value))
            .Select(c => c.Id)
            .ToListAsync(ct);

        var clientByProject = projectIds.Count == 0
            ? new List<Guid>()
            : await _db.Projects.Where(p => projectIds.Contains(p.Id)).Select(p => p.ClientId).Distinct().ToListAsync(ct);

        var clientIds = clientByAm.Concat(clientByProject).ToHashSet();

        return new ClientProjectVisibility(false, projectIds, clientIds);
    }
}
