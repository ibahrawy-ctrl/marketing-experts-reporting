using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class ProjectService : IProjectService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProjectAccess _access;
    private readonly IAuditService _audit;

    public ProjectService(AppDbContext db, ICurrentUser currentUser, IClientProjectAccess access, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ProjectDto>>> ListAsync(ProjectFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<IReadOnlyList<ProjectDto>>.Failure("غير مصرّح.", "auth.unauthenticated");
        var vis = await _access.ResolveAsync(ct);

        var q = _db.Projects.AsNoTracking().AsQueryable();
        if (!vis.SeesAll) q = q.Where(p => vis.ProjectIds.Contains(p.Id));
        if (filter.ClientId is not null) q = q.Where(p => p.ClientId == filter.ClientId);
        if (filter.Status is not null) q = q.Where(p => p.Status == filter.Status);
        if (filter.ServiceType is not null) q = q.Where(p => p.ServiceType == filter.ServiceType);
        if (filter.OwnerTeamId is not null) q = q.Where(p => p.OwnerTeamId == filter.OwnerTeamId);
        if (filter.AccountManagerId is not null) q = q.Where(p => p.AccountManagerId == filter.AccountManagerId);
        if (!filter.IncludeClosed) q = q.Where(p => p.Status != ProjectStatus.Closed);
        // قائمة الاختيار (dropdown): مشاريع نشطة فقط تابعة لعميل غير مؤرشف، ضمن النطاق المُطبَّق أعلاه.
        if (filter.SelectableOnly)
        {
            q = q.Where(p => p.Status == ProjectStatus.Active);
            q = q.Where(p => _db.Clients.Any(c => c.Id == p.ClientId && c.Status != ClientStatus.Closed));
        }

        var rows = await q.OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        var dtos = await MapManyAsync(rows, ct);
        return Result<IReadOnlyList<ProjectDto>>.Success(dtos);
    }

    public async Task<Result<ProjectDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<ProjectDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        var p = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return Result<ProjectDto>.Failure("المشروع غير موجود.", "project.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<ProjectDto>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        return Result<ProjectDto>.Success((await MapManyAsync(new[] { p }, ct))[0]);
    }

    public async Task<Result<ProjectDto>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ProjectDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Name)) return Result<ProjectDto>.Failure("اسم المشروع مطلوب.", "project.name_required");

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, ct);
        if (client is null) return Result<ProjectDto>.Failure("العميل غير موجود.", "client.not_found");

        var vis = await _access.ResolveAsync(ct);
        // غير ذوي الرؤية الكاملة: لا يُنشئون مشروعًا إلا داخل نطاقهم (عميل مرئي
        // أو يضعون أنفسهم مديري حساب أو فريقهم هو المسؤول).
        if (!vis.SeesAll && !await CanOwnAsync(vis, request.AccountManagerId, request.OwnerTeamId, request.ClientId, uid, ct))
            return Result<ProjectDto>.Failure("لا يمكنك إنشاء مشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        var project = new Project
        {
            ClientId = request.ClientId,
            Name = request.Name.Trim(),
            ServiceType = request.ServiceType,
            Status = request.Status,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            OwnerTeamId = request.OwnerTeamId,
            AccountManagerId = request.AccountManagerId,
            Notes = request.Notes
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project.created", nameof(Project), project.Id, ct: ct);

        return Result<ProjectDto>.Success((await MapManyAsync(new[] { project }, ct))[0]);
    }

    public async Task<Result<ProjectDto>> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ProjectDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Name)) return Result<ProjectDto>.Failure("اسم المشروع مطلوب.", "project.name_required");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return Result<ProjectDto>.Failure("المشروع غير موجود.", "project.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<ProjectDto>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        project.Name = request.Name.Trim();
        project.ServiceType = request.ServiceType;
        project.Status = request.Status;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.OwnerTeamId = request.OwnerTeamId;
        project.AccountManagerId = request.AccountManagerId;
        project.Notes = request.Notes;
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project.updated", nameof(Project), project.Id, ct: ct);

        return Result<ProjectDto>.Success((await MapManyAsync(new[] { project }, ct))[0]);
    }

    public async Task<Result<ProjectDto>> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ProjectDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return Result<ProjectDto>.Failure("المشروع غير موجود.", "project.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<ProjectDto>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        project.Status = ProjectStatus.Closed;
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project.archived", nameof(Project), project.Id, ct: ct);

        return Result<ProjectDto>.Success((await MapManyAsync(new[] { project }, ct))[0]);
    }

    public async Task<Result<ProjectDto>> ReactivateAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ProjectDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return Result<ProjectDto>.Failure("المشروع غير موجود.", "project.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<ProjectDto>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        if (project.Status != ProjectStatus.Closed)
            return Result<ProjectDto>.Failure("المشروع غير مؤرشف.", "project.not_archived.conflict");

        // لا يُعاد تفعيل مشروع تابع لعميل مؤرشف.
        var clientClosed = await _db.Clients.AnyAsync(c => c.Id == project.ClientId && c.Status == ClientStatus.Closed, ct);
        if (clientClosed)
            return Result<ProjectDto>.Failure("لا يمكن إعادة تفعيل مشروع تابع لعميل مؤرشف. أعِد تفعيل العميل أولًا.", "project.client_archived.conflict");

        project.Status = ProjectStatus.Active;
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project.reactivated", nameof(Project), project.Id, ct: ct);

        return Result<ProjectDto>.Success((await MapManyAsync(new[] { project }, ct))[0]);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result.Failure("غير مصرّح.", "auth.unauthenticated");
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return Result.Failure("المشروع غير موجود.", "project.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        var reason = await DeleteBlockReasonAsync(project.Id, ct);
        if (reason is not null)
            return Result.Failure(reason, "project.delete_forbidden.conflict");

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project.deleted", nameof(Project), id, ct: ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<LinkedReportRow>>> GetReportsAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<IReadOnlyList<LinkedReportRow>>.Failure("غير مصرّح.", "auth.unauthenticated");
        var exists = await _db.Projects.AnyAsync(p => p.Id == id, ct);
        if (!exists) return Result<IReadOnlyList<LinkedReportRow>>.Failure("المشروع غير موجود.", "project.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<IReadOnlyList<LinkedReportRow>>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        var rows = await LinkedReportsAsync(s => s.ProjectId == id, ct);
        return Result<IReadOnlyList<LinkedReportRow>>.Success(rows);
    }

    public async Task<Result<ProjectSummaryDto>> GetSummaryAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<ProjectSummaryDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return Result<ProjectSummaryDto>.Failure("المشروع غير موجود.", "project.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(id)) return Result<ProjectSummaryDto>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        var subs = await _db.ReportSubmissions.AsNoTracking().Where(s => s.ProjectId == id).ToListAsync(ct);
        var total = subs.Count;
        var closed = subs.Count(s => s.Status == SubmissionStatus.Closed);
        var pending = subs.Count(s => s.Status is SubmissionStatus.Submitted or SubmissionStatus.ApprovedByDirectManager
            or SubmissionStatus.ApprovedByNextLevel or SubmissionStatus.Escalated);
        var last = subs.Where(s => s.SubmittedAtUtc != null).Max(s => (DateTime?)s.SubmittedAtUtc);

        var openRisks = await _db.Risks.CountAsync(r => r.ProjectId == id && r.Status != RiskStatus.Closed, ct);
        var openNotes = await _db.ManagementNotes.CountAsync(
            n => n.EntityType == ManagementNoteEntityType.Project && n.EntityId == id && n.Status == ManagementNoteStatus.Open, ct);

        var dto = (await MapManyAsync(new[] { project }, ct))[0];
        return Result<ProjectSummaryDto>.Success(new ProjectSummaryDto(dto, total, closed, pending, last, openRisks, openNotes));
    }

    // ===== helpers =====
    private async Task<bool> CanOwnAsync(ClientProjectVisibility vis, Guid? amId, Guid? ownerTeamId, Guid clientId, Guid uid, CancellationToken ct)
    {
        if (vis.CanViewClient(clientId)) return true;
        if (amId == uid) return true;
        if (ownerTeamId is Guid t)
            return await _db.Teams.AnyAsync(x => x.Id == t && x.TeamLeaderId == uid, ct);
        return false;
    }

    private async Task<IReadOnlyList<LinkedReportRow>> LinkedReportsAsync(
        System.Linq.Expressions.Expression<Func<Domain.Entities.Submissions.ReportSubmission, bool>> predicate, CancellationToken ct)
    {
        var subs = await _db.ReportSubmissions.AsNoTracking().Where(predicate)
            .OrderByDescending(s => s.CreatedAtUtc).ToListAsync(ct);
        var names = await UserNamesAsync(subs.Select(s => s.SubmitterId), ct);
        return subs.Select(s => new LinkedReportRow(
            s.Id, s.SubmitterId, names.GetValueOrDefault(s.SubmitterId),
            s.PeriodType, s.PeriodKey, s.Status, s.SubmittedAtUtc, s.ClientId, s.ProjectId)).ToList();
    }

    private async Task<List<ProjectDto>> MapManyAsync(IReadOnlyCollection<Project> projects, CancellationToken ct)
    {
        var ids = projects.Select(p => p.Id).ToList();
        var clientIds = projects.Select(p => p.ClientId).Distinct().ToList();
        var clientNames = await _db.Clients.Where(c => clientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var teamIds = projects.Where(p => p.OwnerTeamId != null).Select(p => p.OwnerTeamId!.Value).Distinct().ToList();
        var teamNames = teamIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.Teams.Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
        var amIds = projects.Where(p => p.AccountManagerId != null).Select(p => p.AccountManagerId!.Value);
        var amNames = await UserNamesAsync(amIds, ct);

        // عدّادات الارتباط لاحتساب canHardDelete دفعةً واحدة.
        var directReports = ids.Count == 0 ? new Dictionary<Guid, int>()
            : await _db.ReportSubmissions.Where(s => s.ProjectId != null && ids.Contains(s.ProjectId.Value))
                .GroupBy(s => s.ProjectId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var risksByProject = ids.Count == 0 ? new Dictionary<Guid, int>()
            : await _db.Risks.Where(r => r.ProjectId != null && ids.Contains(r.ProjectId.Value))
                .GroupBy(r => r.ProjectId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var notesByProject = ids.Count == 0 ? new Dictionary<Guid, int>()
            : await _db.ManagementNotes.Where(n => n.EntityType == ManagementNoteEntityType.Project && ids.Contains(n.EntityId))
                .GroupBy(n => n.EntityId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        // الاستخدام داخل أقسام المشاريع المتكررة (Multi-Project ValueJson).
        var inSections = await ReferencedProjectIdsInSectionsAsync(ct);

        return projects.Select(p =>
        {
            var (canHardDelete, reason) = BuildDeleteGuard(
                directReports.GetValueOrDefault(p.Id), inSections.Contains(p.Id),
                risksByProject.GetValueOrDefault(p.Id), notesByProject.GetValueOrDefault(p.Id));
            return new ProjectDto(
                p.Id, p.ClientId, clientNames.GetValueOrDefault(p.ClientId), p.Name, p.ServiceType, p.Status,
                p.StartDate, p.EndDate, p.OwnerTeamId, p.OwnerTeamId is Guid tid ? teamNames.GetValueOrDefault(tid) : null,
                p.AccountManagerId, p.AccountManagerId is Guid aid ? amNames.GetValueOrDefault(aid) : null,
                p.Notes, p.CreatedAtUtc, p.UpdatedAtUtc, canHardDelete, reason);
        }).ToList();
    }

    // ===== فحص الاستخدام / منع الحذف النهائي =====
    private async Task<string?> DeleteBlockReasonAsync(Guid projectId, CancellationToken ct)
    {
        var reports = await _db.ReportSubmissions.CountAsync(s => s.ProjectId == projectId, ct);
        var risks = await _db.Risks.CountAsync(r => r.ProjectId == projectId, ct);
        var notes = await _db.ManagementNotes.CountAsync(
            n => n.EntityType == ManagementNoteEntityType.Project && n.EntityId == projectId, ct);
        var inSection = (await ReferencedProjectIdsInSectionsAsync(ct)).Contains(projectId);
        return BuildDeleteGuard(reports, inSection, risks, notes).reason;
    }

    private static (bool canHardDelete, string? reason) BuildDeleteGuard(int reports, bool inSection, int risks, int notes)
    {
        var parts = new List<string>();
        if (reports > 0) parts.Add($"{reports} تقريرًا مباشرًا");
        if (inSection) parts.Add("مستخدم داخل أقسام تقارير متعدّدة المشاريع");
        if (risks > 0) parts.Add($"{risks} مخاطرة");
        if (notes > 0) parts.Add($"{notes} ملاحظة إدارية");
        if (parts.Count == 0) return (true, null);
        return (false, "لا يمكن الحذف النهائي — " + string.Join("، ", parts) + ". استخدم الأرشفة بدلًا من ذلك.");
    }

    private static readonly JsonSerializerOptions SectionJson = new() { PropertyNameCaseInsensitive = true };

    private sealed class SectionEntry
    {
        public Guid? ProjectId { get; set; }
    }

    // يجمع كل معرّفات المشاريع المُشار إليها داخل ValueJson لأقسام ProjectRepeatableSection.
    private async Task<HashSet<Guid>> ReferencedProjectIdsInSectionsAsync(CancellationToken ct)
    {
        var result = new HashSet<Guid>();
        var sectionFieldIds = await _db.TemplateFields
            .Where(f => f.FieldType == FieldType.ProjectRepeatableSection)
            .Select(f => f.Id).ToListAsync(ct);
        if (sectionFieldIds.Count == 0) return result;

        var jsons = await _db.SubmissionFieldValues
            .Where(v => sectionFieldIds.Contains(v.TemplateFieldId) && v.ValueJson != null)
            .Select(v => v.ValueJson!).ToListAsync(ct);
        foreach (var json in jsons)
            foreach (var pid in ExtractProjectIds(json))
                result.Add(pid);
        return result;
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
        return await _db.Users.Where(u => distinct.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}
