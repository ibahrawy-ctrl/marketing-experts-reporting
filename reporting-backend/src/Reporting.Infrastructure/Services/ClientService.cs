using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class ClientService : IClientService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProjectAccess _access;
    private readonly IAuditService _audit;

    public ClientService(AppDbContext db, ICurrentUser currentUser, IClientProjectAccess access, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ClientDto>>> ListAsync(ClientFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<IReadOnlyList<ClientDto>>.Failure("غير مصرّح.", "auth.unauthenticated");
        var vis = await _access.ResolveAsync(ct);

        var q = _db.Clients.AsNoTracking().AsQueryable();
        if (!vis.SeesAll) q = q.Where(c => vis.ClientIds.Contains(c.Id));
        if (filter.Status is not null) q = q.Where(c => c.Status == filter.Status);
        if (filter.AccountManagerId is not null) q = q.Where(c => c.AccountManagerId == filter.AccountManagerId);
        if (!filter.IncludeClosed) q = q.Where(c => c.Status != ClientStatus.Closed);

        var rows = await q.OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct);
        var dtos = await MapManyAsync(rows, ct);
        return Result<IReadOnlyList<ClientDto>>.Success(dtos);
    }

    public async Task<Result<ClientDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<ClientDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        var c = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<ClientDto>.Failure("العميل غير موجود.", "client.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewClient(id)) return Result<ClientDto>.Failure("هذا العميل خارج نطاق صلاحيتك.", "auth.forbidden");

        return Result<ClientDto>.Success((await MapManyAsync(new[] { c }, ct))[0]);
    }

    public async Task<Result<ClientDto>> CreateAsync(CreateClientRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Name)) return Result<ClientDto>.Failure("اسم العميل مطلوب.", "client.name_required");

        var vis = await _access.ResolveAsync(ct);
        // غير ذوي الرؤية الكاملة: لا يُنشئون عميلًا إلا إذا وضعوا أنفسهم مديري حساب له.
        if (!vis.SeesAll && request.AccountManagerId != uid)
            return Result<ClientDto>.Failure("لا يمكنك إنشاء عميل خارج نطاق صلاحيتك.", "auth.forbidden");

        var client = new Client
        {
            Name = request.Name.Trim(),
            Status = request.Status,
            AccountManagerId = request.AccountManagerId,
            MainContactName = request.MainContactName,
            MainContactInfo = request.MainContactInfo,
            Notes = request.Notes
        };
        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "client.created", nameof(Client), client.Id, ct: ct);

        return Result<ClientDto>.Success((await MapManyAsync(new[] { client }, ct))[0]);
    }

    public async Task<Result<ClientDto>> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Name)) return Result<ClientDto>.Failure("اسم العميل مطلوب.", "client.name_required");

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null) return Result<ClientDto>.Failure("العميل غير موجود.", "client.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewClient(id)) return Result<ClientDto>.Failure("هذا العميل خارج نطاق صلاحيتك.", "auth.forbidden");

        client.Name = request.Name.Trim();
        client.Status = request.Status;
        client.AccountManagerId = request.AccountManagerId;
        client.MainContactName = request.MainContactName;
        client.MainContactInfo = request.MainContactInfo;
        client.Notes = request.Notes;
        client.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "client.updated", nameof(Client), client.Id, ct: ct);

        return Result<ClientDto>.Success((await MapManyAsync(new[] { client }, ct))[0]);
    }

    public async Task<Result<ClientDto>> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null) return Result<ClientDto>.Failure("العميل غير موجود.", "client.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewClient(id)) return Result<ClientDto>.Failure("هذا العميل خارج نطاق صلاحيتك.", "auth.forbidden");

        client.Status = ClientStatus.Closed;
        client.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "client.archived", nameof(Client), client.Id, ct: ct);

        return Result<ClientDto>.Success((await MapManyAsync(new[] { client }, ct))[0]);
    }

    public async Task<Result<IReadOnlyList<LinkedReportRow>>> GetReportsAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<IReadOnlyList<LinkedReportRow>>.Failure("غير مصرّح.", "auth.unauthenticated");
        var exists = await _db.Clients.AnyAsync(c => c.Id == id, ct);
        if (!exists) return Result<IReadOnlyList<LinkedReportRow>>.Failure("العميل غير موجود.", "client.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewClient(id)) return Result<IReadOnlyList<LinkedReportRow>>.Failure("هذا العميل خارج نطاق صلاحيتك.", "auth.forbidden");

        // تقارير العميل = ما رُبط مباشرة بالعميل أو بأحد مشاريعه.
        var projectIds = await _db.Projects.Where(p => p.ClientId == id).Select(p => p.Id).ToListAsync(ct);
        var subs = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => s.ClientId == id || (s.ProjectId != null && projectIds.Contains(s.ProjectId.Value)))
            .OrderByDescending(s => s.CreatedAtUtc).ToListAsync(ct);
        var names = await UserNamesAsync(subs.Select(s => s.SubmitterId), ct);
        var rows = subs.Select(s => new LinkedReportRow(
            s.Id, s.SubmitterId, names.GetValueOrDefault(s.SubmitterId),
            s.PeriodType, s.PeriodKey, s.Status, s.SubmittedAtUtc, s.ClientId, s.ProjectId)).ToList();
        return Result<IReadOnlyList<LinkedReportRow>>.Success(rows);
    }

    public async Task<Result<ClientHealthReport>> GetHealthAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<ClientHealthReport>.Failure("غير مصرّح.", "auth.unauthenticated");
        var vis = await _access.ResolveAsync(ct);

        var q = _db.Clients.AsNoTracking().Where(c => c.Status != ClientStatus.Closed);
        if (!vis.SeesAll) q = q.Where(c => vis.ClientIds.Contains(c.Id));
        var clients = await q.ToListAsync(ct);

        var clientIds = clients.Select(c => c.Id).ToList();
        var projects = clientIds.Count == 0
            ? new List<Project>()
            : await _db.Projects.AsNoTracking().Where(p => clientIds.Contains(p.ClientId)).ToListAsync(ct);
        var amNames = await UserNamesAsync(clients.Where(c => c.AccountManagerId != null).Select(c => c.AccountManagerId!.Value), ct);

        // مخاطر/ملاحظات/آخر تقرير لكل عميل.
        var openRisksByClient = clientIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _db.Risks.Where(r => r.ClientId != null && clientIds.Contains(r.ClientId.Value) && r.Status != RiskStatus.Closed)
                .GroupBy(r => r.ClientId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var openNotesByClient = clientIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _db.ManagementNotes.Where(n => n.EntityType == ManagementNoteEntityType.Client
                    && clientIds.Contains(n.EntityId) && n.Status == ManagementNoteStatus.Open)
                .GroupBy(n => n.EntityId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var projectIds = projects.Select(p => p.Id).ToList();
        var lastReportByClient = new Dictionary<Guid, DateTime>();
        if (clientIds.Count > 0)
        {
            var subs = await _db.ReportSubmissions.AsNoTracking()
                .Where(s => (s.ClientId != null && clientIds.Contains(s.ClientId.Value))
                         || (s.ProjectId != null && projectIds.Contains(s.ProjectId.Value)))
                .Where(s => s.SubmittedAtUtc != null)
                .Select(s => new { s.ClientId, s.ProjectId, s.SubmittedAtUtc })
                .ToListAsync(ct);
            var projectToClient = projects.ToDictionary(p => p.Id, p => p.ClientId);
            foreach (var s in subs)
            {
                var cid = s.ClientId ?? (s.ProjectId is Guid pid && projectToClient.TryGetValue(pid, out var c) ? c : (Guid?)null);
                if (cid is not Guid key || s.SubmittedAtUtc is not DateTime when) continue;
                if (!lastReportByClient.TryGetValue(key, out var cur) || when > cur) lastReportByClient[key] = when;
            }
        }

        var rows = new List<ClientHealthRow>();
        var renewal = 0;
        foreach (var c in clients)
        {
            var cps = projects.Where(p => p.ClientId == c.Id).ToList();
            var atRiskProjects = cps.Count(p => p.Status == ProjectStatus.AtRisk);
            var openRisks = openRisksByClient.GetValueOrDefault(c.Id);
            var openNotes = openNotesByClient.GetValueOrDefault(c.Id);
            var last = lastReportByClient.TryGetValue(c.Id, out var lr) ? (DateTime?)lr : null;

            // مخاطر التسرّب مشتقّة من الحالة + المؤشرات (بلا KPI جديد).
            var churn = c.Status == ClientStatus.AtRisk || atRiskProjects > 0 || openRisks > 0 ? "عالٍ"
                : c.Status == ClientStatus.Paused || openNotes > 0 ? "متوسط"
                : "منخفض";
            var decisionNeeded = c.Status == ClientStatus.AtRisk || atRiskProjects > 0 || openRisks > 0;
            // فرصة تجديد/ترقية: عميل نشط ومستقر بلا مخاطر.
            if (c.Status == ClientStatus.Active && atRiskProjects == 0 && openRisks == 0) renewal++;

            rows.Add(new ClientHealthRow(
                c.Id, c.Name, c.Status, c.AccountManagerId,
                c.AccountManagerId is Guid aid ? amNames.GetValueOrDefault(aid) : null,
                cps.Count, atRiskProjects, openRisks, openNotes, last, churn, decisionNeeded));
        }

        var totalClients = rows.Count;
        var atRiskClients = rows.Count(r => r.Status == ClientStatus.AtRisk || r.AtRiskProjectCount > 0);
        var decisionCount = rows.Count(r => r.DecisionNeeded);

        // مستوى الرؤية: ذوو الرؤية الكاملة (CEO/GM/Admin) يحصلون على ملخّص بلا صفوف أعضاء.
        var canViewRows = !vis.SeesAll;
        var viewLevel = vis.SeesAll ? "summary" : "scoped";
        var shaped = canViewRows ? rows : new List<ClientHealthRow>();

        var report = new ClientHealthReport(
            "صحّة العملاء — الحالة الراهنة",
            shaped, totalClients, atRiskClients, decisionCount, renewal, viewLevel, canViewRows);
        return Result<ClientHealthReport>.Success(report);
    }

    // ===== helpers =====
    private async Task<List<ClientDto>> MapManyAsync(IReadOnlyCollection<Client> clients, CancellationToken ct)
    {
        var ids = clients.Select(c => c.Id).ToList();
        var projects = ids.Count == 0
            ? new List<(Guid ClientId, ProjectStatus Status)>()
            : (await _db.Projects.AsNoTracking().Where(p => ids.Contains(p.ClientId))
                .Select(p => new { p.ClientId, p.Status }).ToListAsync(ct))
                .Select(x => (x.ClientId, x.Status)).ToList();
        var amNames = await UserNamesAsync(clients.Where(c => c.AccountManagerId != null).Select(c => c.AccountManagerId!.Value), ct);

        return clients.Select(c =>
        {
            var cps = projects.Where(p => p.ClientId == c.Id).ToList();
            return new ClientDto(
                c.Id, c.Name, c.Status, c.AccountManagerId,
                c.AccountManagerId is Guid aid ? amNames.GetValueOrDefault(aid) : null,
                c.MainContactName, c.MainContactInfo, c.Notes,
                cps.Count,
                cps.Count(p => p.Status == ProjectStatus.Active),
                cps.Count(p => p.Status == ProjectStatus.AtRisk),
                c.CreatedAtUtc, c.UpdatedAtUtc);
        }).ToList();
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}
