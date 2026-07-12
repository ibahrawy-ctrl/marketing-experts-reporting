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
    private readonly IScopeResolver _scope;
    private readonly IAuditService _audit;

    public ClientService(AppDbContext db, ICurrentUser currentUser, IClientProjectAccess access, IScopeResolver scope, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _scope = scope;
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

        var validation = ValidateClientProfile(
            request.ClientTypeCode, request.SectorCode, request.SourceCode, request.Website,
            request.Notes, request.MainContactName, request.MainContactInfo, request.LegalName, request.TradeNameEn);
        if (validation is not null) return Result<ClientDto>.Failure(validation.Value.message, validation.Value.code);

        // النطاق: Admin/CEO/GM (رؤية كاملة) يُسنِدون بحرّية؛ المدير يُنشئ ضمن نطاقه فقط
        // وأيّ AccountManagerId مُسنَد يجب أن يكون داخل نطاقه.
        var scope = await _scope.ResolveAsync(ct);
        if (!scope.SeesAll && request.AccountManagerId is Guid amId && !scope.Contains(amId))
            return Result<ClientDto>.Failure("مدير الحساب المُسنَد خارج نطاق صلاحيتك.", "auth.forbidden");

        var client = new Client
        {
            Name = request.Name.Trim(),
            Status = request.Status,
            AccountManagerId = request.AccountManagerId,
            MainContactName = request.MainContactName,
            MainContactInfo = request.MainContactInfo,
            Notes = request.Notes,
            TradeNameEn = Trim(request.TradeNameEn),
            LegalName = Trim(request.LegalName),
            ClientTypeCode = Trim(request.ClientTypeCode),
            SectorCode = Trim(request.SectorCode),
            Country = Trim(request.Country),
            City = Trim(request.City),
            Website = Trim(request.Website),
            SourceCode = Trim(request.SourceCode),
            RelationshipStartDate = request.RelationshipStartDate
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

        var validation = ValidateClientProfile(
            request.ClientTypeCode, request.SectorCode, request.SourceCode, request.Website,
            request.Notes, request.MainContactName, request.MainContactInfo, request.LegalName, request.TradeNameEn);
        if (validation is not null) return Result<ClientDto>.Failure(validation.Value.message, validation.Value.code);

        // النطاق: أيّ AccountManagerId مُسنَد يجب أن يكون داخل نطاق المستخدم (ما لم تكن له رؤية كاملة).
        var scope = await _scope.ResolveAsync(ct);
        if (!scope.SeesAll && request.AccountManagerId is Guid amId && !scope.Contains(amId))
            return Result<ClientDto>.Failure("مدير الحساب المُسنَد خارج نطاق صلاحيتك.", "auth.forbidden");

        client.Name = request.Name.Trim();
        client.Status = request.Status;
        client.AccountManagerId = request.AccountManagerId;
        client.MainContactName = request.MainContactName;
        client.MainContactInfo = request.MainContactInfo;
        client.Notes = request.Notes;
        client.TradeNameEn = Trim(request.TradeNameEn);
        client.LegalName = Trim(request.LegalName);
        client.ClientTypeCode = Trim(request.ClientTypeCode);
        client.SectorCode = Trim(request.SectorCode);
        client.Country = Trim(request.Country);
        client.City = Trim(request.City);
        client.Website = Trim(request.Website);
        client.SourceCode = Trim(request.SourceCode);
        client.RelationshipStartDate = request.RelationshipStartDate;
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

    public async Task<Result<ClientDto>> ReactivateAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ClientDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null) return Result<ClientDto>.Failure("العميل غير موجود.", "client.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewClient(id)) return Result<ClientDto>.Failure("هذا العميل خارج نطاق صلاحيتك.", "auth.forbidden");

        if (client.Status != ClientStatus.Closed)
            return Result<ClientDto>.Failure("العميل غير مؤرشف.", "client.not_archived.conflict");

        client.Status = ClientStatus.Active;
        client.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "client.reactivated", nameof(Client), client.Id, ct: ct);

        return Result<ClientDto>.Success((await MapManyAsync(new[] { client }, ct))[0]);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result.Failure("غير مصرّح.", "auth.unauthenticated");
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null) return Result.Failure("العميل غير موجود.", "client.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewClient(id)) return Result.Failure("هذا العميل خارج نطاق صلاحيتك.", "auth.forbidden");

        var reason = await DeleteBlockReasonAsync(client.Id, ct);
        if (reason is not null)
            return Result.Failure(reason, "client.delete_forbidden.conflict");

        _db.Clients.Remove(client);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "client.deleted", nameof(Client), id, ct: ct);
        return Result.Success();
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
    // قصّ نصّ اختياري: يُرجِع null للفارغ/الفراغات، وإلا النصّ مقصوصًا.
    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // تحقّق موحّد لملف العميل: صحّة الرموز المعتمدة + صحّة الرابط + منع تخزين أيّ أسرار.
    // يُرجِع null عند الصلاحية، وإلا (رسالة، كود خطأ).
    private static (string message, string code)? ValidateClientProfile(
        string? clientTypeCode, string? sectorCode, string? sourceCode, string? website,
        params string?[] freeTextFields)
    {
        if (!ClientCodeConstants.IsValidClientType(clientTypeCode))
            return ("نوع العميل غير معتمَد.", "client.client_type_invalid");
        if (!ClientCodeConstants.IsValidSector(sectorCode))
            return ("قطاع العميل غير معتمَد.", "client.sector_invalid");
        if (!ClientCodeConstants.IsValidSource(sourceCode))
            return ("مصدر العلاقة غير معتمَد.", "client.source_invalid");
        if (!ClientFieldGuards.IsValidUrl(website))
            return ("رابط الموقع غير صالح (يجب أن يكون http/https).", "client.website_invalid");
        if (ClientFieldGuards.AnyContainsSecret(freeTextFields))
            return ("لا يجوز تخزين كلمات مرور أو رموز وصول في حقول العميل.", "client.secret_forbidden");
        return null;
    }

    // يبني سبب منع الحذف النهائي لعميل واحد (يُستخدم في DeleteAsync). يُرجع null عند السماح.
    private async Task<string?> DeleteBlockReasonAsync(Guid clientId, CancellationToken ct)
    {
        var projects = await _db.Projects.CountAsync(p => p.ClientId == clientId, ct);
        var reports = await _db.ReportSubmissions.CountAsync(s => s.ClientId == clientId, ct);
        var risks = await _db.Risks.CountAsync(r => r.ClientId == clientId, ct);
        var notes = await _db.ManagementNotes.CountAsync(
            n => n.EntityType == ManagementNoteEntityType.Client && n.EntityId == clientId, ct);
        return BuildDeleteGuard(projects, reports, risks, notes).reason;
    }

    // قاعدة موحّدة لبناء حالة/سبب منع الحذف من العدّادات.
    private static (bool canHardDelete, string? reason) BuildDeleteGuard(int projects, int reports, int risks, int notes)
    {
        var parts = new List<string>();
        if (projects > 0) parts.Add($"{projects} مشروعًا");
        if (reports > 0) parts.Add($"{reports} تقريرًا");
        if (risks > 0) parts.Add($"{risks} مخاطرة");
        if (notes > 0) parts.Add($"{notes} ملاحظة إدارية");
        if (parts.Count == 0) return (true, null);
        return (false, "لا يمكن الحذف النهائي — مرتبط بـ " + string.Join("، ", parts) + ". استخدم الأرشفة بدلًا من ذلك.");
    }

    private async Task<List<ClientDto>> MapManyAsync(IReadOnlyCollection<Client> clients, CancellationToken ct)
    {
        var ids = clients.Select(c => c.Id).ToList();
        var projects = ids.Count == 0
            ? new List<(Guid ClientId, ProjectStatus Status)>()
            : (await _db.Projects.AsNoTracking().Where(p => ids.Contains(p.ClientId))
                .Select(p => new { p.ClientId, p.Status }).ToListAsync(ct))
                .Select(x => (x.ClientId, x.Status)).ToList();
        var amNames = await UserNamesAsync(clients.Where(c => c.AccountManagerId != null).Select(c => c.AccountManagerId!.Value), ct);

        // عدّادات الارتباط لاحتساب canHardDelete دفعةً واحدة.
        var reportsByClient = ids.Count == 0 ? new Dictionary<Guid, int>()
            : await _db.ReportSubmissions.Where(s => s.ClientId != null && ids.Contains(s.ClientId.Value))
                .GroupBy(s => s.ClientId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var risksByClient = ids.Count == 0 ? new Dictionary<Guid, int>()
            : await _db.Risks.Where(r => r.ClientId != null && ids.Contains(r.ClientId.Value))
                .GroupBy(r => r.ClientId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var notesByClient = ids.Count == 0 ? new Dictionary<Guid, int>()
            : await _db.ManagementNotes.Where(n => n.EntityType == ManagementNoteEntityType.Client && ids.Contains(n.EntityId))
                .GroupBy(n => n.EntityId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return clients.Select(c =>
        {
            var cps = projects.Where(p => p.ClientId == c.Id).ToList();
            var (canHardDelete, reason) = BuildDeleteGuard(
                cps.Count, reportsByClient.GetValueOrDefault(c.Id),
                risksByClient.GetValueOrDefault(c.Id), notesByClient.GetValueOrDefault(c.Id));
            return new ClientDto(
                c.Id, c.Name, c.Status, c.AccountManagerId,
                c.AccountManagerId is Guid aid ? amNames.GetValueOrDefault(aid) : null,
                c.MainContactName, c.MainContactInfo, c.Notes,
                cps.Count,
                cps.Count(p => p.Status == ProjectStatus.Active),
                cps.Count(p => p.Status == ProjectStatus.AtRisk),
                c.CreatedAtUtc, c.UpdatedAtUtc,
                TradeNameEn: c.TradeNameEn,
                LegalName: c.LegalName,
                ClientTypeCode: c.ClientTypeCode,
                SectorCode: c.SectorCode,
                Country: c.Country,
                City: c.City,
                Website: c.Website,
                SourceCode: c.SourceCode,
                RelationshipStartDate: c.RelationshipStartDate,
                CanHardDelete: canHardDelete,
                DeleteBlockReason: reason);
        }).ToList();
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}
