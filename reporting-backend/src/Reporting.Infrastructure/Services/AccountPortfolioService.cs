using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.AccountPortfolio;
using Reporting.Application.Common;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// محفظة مدير الحساب (عرض فقط). كل الاستعلامات AsNoTracking. النطاق مفروض خادمًا حصرًا على
/// مشاريع المستخدم الحالي نفسه (Project.AccountManagerId == uid) — لا IClientProjectAccess (الذي
/// يوسّع عبر الفريق/التسليمات)، لا منح رؤية، لا عضوية فِرق، لا مسمّى وظيفي، لا Client.AccountManagerId.
/// العملاء مشتقّون فقط من المشاريع المرئية. المخرجات تُقصَر على الحالات المعتمَدة (تستثني المسودّة/المُعادة).
/// </summary>
public class AccountPortfolioService : IAccountPortfolioService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AccountPortfolioService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // الحالات التي تُعدّ «مخرَجًا» قابلًا للعرض لمدير الحساب — لا تشمل Draft ولا Returned.
    private static readonly SubmissionStatus[] OutputStatuses =
    {
        SubmissionStatus.Submitted,
        SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel,
        SubmissionStatus.Escalated,
        SubmissionStatus.Closed,
        SubmissionStatus.Visible
    };

    public async Task<Result<IReadOnlyList<PortfolioProjectDto>>> GetMyProjectsAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<PortfolioProjectDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var projects = await _db.Projects.AsNoTracking()
            .Where(p => p.AccountManagerId == uid)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(ct);

        var dtos = await MapProjectsAsync(projects, ct);
        return Result<IReadOnlyList<PortfolioProjectDto>>.Success(dtos);
    }

    public async Task<Result<PortfolioProjectDto>> GetMyProjectAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<PortfolioProjectDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
            return Result<PortfolioProjectDto>.Failure("المشروع غير موجود.", "project.not_found");
        if (project.AccountManagerId != uid)
            return Result<PortfolioProjectDto>.Failure("هذا المشروع خارج نطاق محفظتك.", "auth.forbidden");

        var dto = (await MapProjectsAsync(new[] { project }, ct))[0];
        return Result<PortfolioProjectDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<PortfolioClientDto>>> GetMyClientsAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<PortfolioClientDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var clients = await BuildMyClientsAsync(uid, ct);
        return Result<IReadOnlyList<PortfolioClientDto>>.Success(clients);
    }

    public async Task<Result<PortfolioClientDetailDto>> GetMyClientAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<PortfolioClientDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null)
            return Result<PortfolioClientDetailDto>.Failure("العميل غير موجود.", "client.not_found");

        // العميل مرئيّ فقط إن كان للمستخدم مشروع واحد على الأقل تابع له (مشتقّ من المشاريع لا من Client.AccountManagerId).
        var myProjects = await _db.Projects.AsNoTracking()
            .Where(p => p.AccountManagerId == uid && p.ClientId == id)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(ct);
        if (myProjects.Count == 0)
            return Result<PortfolioClientDetailDto>.Failure("هذا العميل خارج نطاق محفظتك.", "auth.forbidden");

        var projectDtos = await MapProjectsAsync(myProjects, ct);
        var clientDto = new PortfolioClientDto(
            client.Id, client.Name, client.Status,
            projectDtos.Count,
            projectDtos.Count(p => p.Status == ProjectStatus.Active));

        return Result<PortfolioClientDetailDto>.Success(new PortfolioClientDetailDto(clientDto, projectDtos));
    }

    public async Task<Result<IReadOnlyList<PortfolioOutputDto>>> GetProjectOutputsAsync(Guid projectId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<PortfolioOutputDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
            return Result<IReadOnlyList<PortfolioOutputDto>>.Failure("المشروع غير موجود.", "project.not_found");
        if (project.AccountManagerId != uid)
            return Result<IReadOnlyList<PortfolioOutputDto>>.Failure("هذا المشروع خارج نطاق محفظتك.", "auth.forbidden");

        var submissionIds = await LinkedSubmissionIdsAsync(projectId, ct);
        if (submissionIds.Count == 0)
            return Result<IReadOnlyList<PortfolioOutputDto>>.Success(Array.Empty<PortfolioOutputDto>());

        var subs = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => submissionIds.Contains(s.Id) && OutputStatuses.Contains(s.Status))
            .OrderByDescending(s => s.SubmittedAtUtc ?? s.CreatedAtUtc)
            .ToListAsync(ct);

        var names = await UserNamesAsync(subs.Select(s => s.SubmitterId), ct);
        var dtos = subs.Select(s => new PortfolioOutputDto(
            s.Id, s.SubmitterId, names.GetValueOrDefault(s.SubmitterId),
            s.PeriodType, s.PeriodKey, s.Status, s.SubmittedAtUtc)).ToList();

        return Result<IReadOnlyList<PortfolioOutputDto>>.Success(dtos);
    }

    // ===== helpers =====

    private async Task<List<PortfolioClientDto>> BuildMyClientsAsync(Guid uid, CancellationToken ct)
    {
        // العملاء مشتقّون حصرًا من مشاريع المستخدم المرئية (ClientId الفريد) — لا اعتماد على Client.AccountManagerId.
        var myProjects = await _db.Projects.AsNoTracking()
            .Where(p => p.AccountManagerId == uid)
            .Select(p => new { p.ClientId, p.Status })
            .ToListAsync(ct);
        if (myProjects.Count == 0) return new List<PortfolioClientDto>();

        var clientIds = myProjects.Select(p => p.ClientId).Distinct().ToList();
        var clients = await _db.Clients.AsNoTracking()
            .Where(c => clientIds.Contains(c.Id))
            .ToListAsync(ct);

        return clients
            .OrderBy(c => c.Name)
            .Select(c => new PortfolioClientDto(
                c.Id, c.Name, c.Status,
                myProjects.Count(p => p.ClientId == c.Id),
                myProjects.Count(p => p.ClientId == c.Id && p.Status == ProjectStatus.Active)))
            .ToList();
    }

    private async Task<List<PortfolioProjectDto>> MapProjectsAsync(IReadOnlyCollection<Domain.Entities.Clients.Project> projects, CancellationToken ct)
    {
        if (projects.Count == 0) return new List<PortfolioProjectDto>();

        var clientIds = projects.Select(p => p.ClientId).Distinct().ToList();
        var clientNames = await _db.Clients.AsNoTracking()
            .Where(c => clientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var projectIds = projects.Select(p => p.Id).ToList();
        var outputs = await OutputStatsAsync(projectIds, ct);

        return projects.Select(p =>
        {
            var stat = outputs.GetValueOrDefault(p.Id);
            return new PortfolioProjectDto(
                p.Id, p.Name, p.ClientId, clientNames.GetValueOrDefault(p.ClientId),
                p.ServiceType, p.Status, p.StartDate, p.EndDate,
                stat.Count, stat.Last);
        }).ToList();
    }

    // إحصاء المخرجات المعتمَدة لكل مشروع (عدّ + آخر تسليم) — ربط مباشر (ProjectId) أو عبر أقسام PRS.
    private async Task<Dictionary<Guid, (int Count, DateTime? Last)>> OutputStatsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, (int Count, DateTime? Last)>();
        if (projectIds.Count == 0) return result;

        // 1) الربط المباشر عبر ProjectId.
        var direct = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => s.ProjectId != null && projectIds.Contains(s.ProjectId!.Value) && OutputStatuses.Contains(s.Status))
            .Select(s => new { ProjectId = s.ProjectId!.Value, s.Id, s.SubmittedAtUtc, s.CreatedAtUtc })
            .ToListAsync(ct);

        // مجموعة (projectId -> submissionId) لتفادي ازدواج العدّ عند تطابق المباشر مع PRS.
        var linked = new Dictionary<Guid, Dictionary<Guid, DateTime?>>();
        void Add(Guid pid, Guid sid, DateTime? at)
        {
            if (!projectIds.Contains(pid)) return;
            if (!linked.TryGetValue(pid, out var map)) { map = new Dictionary<Guid, DateTime?>(); linked[pid] = map; }
            map[sid] = at;
        }
        foreach (var d in direct) Add(d.ProjectId, d.Id, d.SubmittedAtUtc ?? d.CreatedAtUtc);

        // 2) الربط عبر أقسام المشاريع المتكررة (PRS ValueJson) — للتسليمات المعتمَدة فقط.
        var prs = await PrsSubmissionProjectPairsAsync(ct);
        if (prs.Count > 0)
        {
            var prsSubIds = prs.Select(x => x.SubmissionId).Distinct().ToList();
            var subInfo = await _db.ReportSubmissions.AsNoTracking()
                .Where(s => prsSubIds.Contains(s.Id) && OutputStatuses.Contains(s.Status))
                .Select(s => new { s.Id, s.SubmittedAtUtc, s.CreatedAtUtc })
                .ToDictionaryAsync(s => s.Id, s => (DateTime?)(s.SubmittedAtUtc ?? s.CreatedAtUtc), ct);
            foreach (var pair in prs)
                if (subInfo.TryGetValue(pair.SubmissionId, out var at))
                    Add(pair.ProjectId, pair.SubmissionId, at);
        }

        foreach (var (pid, map) in linked)
            result[pid] = (map.Count, map.Values.Where(v => v != null).DefaultIfEmpty(null).Max());

        return result;
    }

    // مجموعة معرّفات التسليمات المرتبطة بمشروع (مباشر ProjectId أو عبر PRS) — قبل فلترة الحالة.
    private async Task<HashSet<Guid>> LinkedSubmissionIdsAsync(Guid projectId, CancellationToken ct)
    {
        var ids = new HashSet<Guid>();
        var direct = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .Select(s => s.Id)
            .ToListAsync(ct);
        foreach (var id in direct) ids.Add(id);

        foreach (var pair in await PrsSubmissionProjectPairsAsync(ct))
            if (pair.ProjectId == projectId) ids.Add(pair.SubmissionId);

        return ids;
    }

    private static readonly JsonSerializerOptions SectionJson = new() { PropertyNameCaseInsensitive = true };

    private sealed class SectionEntry
    {
        public Guid? ProjectId { get; set; }
    }

    // يستخرج أزواج (SubmissionId, ProjectId) من قيم حقول ProjectRepeatableSection.
    private async Task<List<(Guid SubmissionId, Guid ProjectId)>> PrsSubmissionProjectPairsAsync(CancellationToken ct)
    {
        var pairs = new List<(Guid, Guid)>();
        var sectionFieldIds = await _db.TemplateFields.AsNoTracking()
            .Where(f => f.FieldType == FieldType.ProjectRepeatableSection)
            .Select(f => f.Id).ToListAsync(ct);
        if (sectionFieldIds.Count == 0) return pairs;

        var rows = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => sectionFieldIds.Contains(v.TemplateFieldId) && v.ValueJson != null)
            .Select(v => new { v.ReportSubmissionId, v.ValueJson })
            .ToListAsync(ct);

        foreach (var row in rows)
            foreach (var pid in ExtractProjectIds(row.ValueJson!))
                pairs.Add((row.ReportSubmissionId, pid));

        return pairs;
    }

    private static List<Guid> ExtractProjectIds(string valueJson)
    {
        var ids = new List<Guid>();
        try
        {
            var entries = JsonSerializer.Deserialize<List<SectionEntry>>(valueJson, SectionJson);
            if (entries is not null)
                foreach (var e in entries)
                    if (e.ProjectId is Guid pid && pid != Guid.Empty) ids.Add(pid);
        }
        catch { /* ValueJson تالف يُتجاهَل بأمان */ }
        return ids;
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.AsNoTracking().Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}
