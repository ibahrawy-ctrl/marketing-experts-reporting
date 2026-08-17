using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Clients;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// إدارة تيّارات العمل (أهداف العمل) داخل المشروع (P1). القراءة مُنَطَّقة بنطاق رؤية المشروع (IClientProjectAccess).
/// الكتابة محكومة بنطاق المشروع عبر <see cref="CanManagePlanAsync"/> (لا بالدور وحده): أدوار إدارة الخطّة
/// (ProjectPlanManagers = Admin/CEO/GM/Manager) أو **مالك المشروع أو قائد الفريق أو مدير الحساب المسنَد على هذا
/// المشروع بعينه** — كنمط خطّة المخرَجات (P2).
/// لا حذف نهائيّ — التعطيل فقط. نوع تيار العمل يُتحقَّق منه مقابل كتالوج التصنيفات (Domain=workstream_type) بلا مفتاح أجنبيّ.
/// </summary>
public class ProjectWorkstreamService : IProjectWorkstreamService
{
    private const string WorkstreamTypeDomain = "workstream_type";

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProjectAccess _access;
    private readonly IAuditService _audit;

    public ProjectWorkstreamService(AppDbContext db, ICurrentUser currentUser, IClientProjectAccess access, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ProjectWorkstreamDto>>> ListAsync(Guid projectId, bool includeInactive, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<IReadOnlyList<ProjectWorkstreamDto>>.Failure("غير مصرّح.", "auth.unauthenticated");
        var exists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!exists) return Result<IReadOnlyList<ProjectWorkstreamDto>>.Failure("المشروع غير موجود.", "project.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(projectId)) return Result<IReadOnlyList<ProjectWorkstreamDto>>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        var q = _db.ProjectWorkstreams.AsNoTracking().Where(w => w.ProjectId == projectId);
        if (!includeInactive) q = q.Where(w => w.IsActive);
        var rows = await q.OrderBy(w => w.SortOrder).ThenBy(w => w.Name).ToListAsync(ct);
        var dtos = await MapManyAsync(rows, ct);
        return Result<IReadOnlyList<ProjectWorkstreamDto>>.Success(dtos);
    }

    public async Task<Result<ProjectWorkstreamDto>> CreateAsync(Guid projectId, CreateProjectWorkstreamRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ProjectWorkstreamDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Result<ProjectWorkstreamDto>.Failure("المشروع غير موجود.", "project.not_found");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(projectId)) return Result<ProjectWorkstreamDto>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        if (!await CanManagePlanAsync(projectId, uid, ct))
            return Result<ProjectWorkstreamDto>.Failure("إدارة مسارات العمل من صلاحية إدارة الخطّة أو مسؤولي هذا المشروع (مالكه أو قائد فريقه أو مدير حسابه) فقط.", "auth.forbidden");

        var code = (request.WorkstreamTypeCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
            return Result<ProjectWorkstreamDto>.Failure("رمز نوع تيار العمل مطلوب.", "workstream.type_required");

        var typeNameAr = await ResolveActiveTypeNameArAsync(code, ct);
        if (typeNameAr is null)
            return Result<ProjectWorkstreamDto>.Failure("نوع تيار العمل غير معروف أو غير نشط في كتالوج التصنيفات.", "workstream.type_invalid");

        if (request.ResponsibleTeamId == Guid.Empty || !await _db.Teams.AnyAsync(t => t.Id == request.ResponsibleTeamId, ct))
            return Result<ProjectWorkstreamDto>.Failure("الفريق المسؤول غير موجود.", "workstream.team_not_found");

        if (request.ResponsibleManagerId is Guid mgr && !await _db.Users.AnyAsync(u => u.Id == mgr, ct))
            return Result<ProjectWorkstreamDto>.Failure("المدير المسؤول غير موجود.", "workstream.manager_not_found");

        // تفرّد منطقيّ: لا يتكرّر نفس (المشروع، نوع تيار العمل، الفريق المسؤول).
        var duplicate = await _db.ProjectWorkstreams.AnyAsync(
            w => w.ProjectId == projectId && w.WorkstreamTypeCode == code && w.ResponsibleTeamId == request.ResponsibleTeamId, ct);
        if (duplicate)
            return Result<ProjectWorkstreamDto>.Failure("يوجد تيار عمل بنفس النوع والفريق المسؤول في هذا المشروع بالفعل.", "workstream.duplicate.conflict");

        var name = string.IsNullOrWhiteSpace(request.Name) ? (typeNameAr ?? code) : request.Name!.Trim();

        var entity = new ProjectWorkstream
        {
            ProjectId = projectId,
            WorkstreamTypeCode = code,
            Name = name,
            ResponsibleTeamId = request.ResponsibleTeamId,
            ResponsibleManagerId = request.ResponsibleManagerId,
            SortOrder = request.SortOrder,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes!.Trim(),
        };
        _db.ProjectWorkstreams.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project_workstream.created", nameof(ProjectWorkstream), entity.Id, ct: ct);

        return Result<ProjectWorkstreamDto>.Success((await MapManyAsync(new[] { entity }, ct))[0]);
    }

    public async Task<Result<ProjectWorkstreamDto>> UpdateAsync(Guid projectId, Guid id, UpdateProjectWorkstreamRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ProjectWorkstreamDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(projectId)) return Result<ProjectWorkstreamDto>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        if (!await CanManagePlanAsync(projectId, uid, ct))
            return Result<ProjectWorkstreamDto>.Failure("إدارة مسارات العمل من صلاحية إدارة الخطّة أو مسؤولي هذا المشروع (مالكه أو قائد فريقه أو مدير حسابه) فقط.", "auth.forbidden");

        var entity = await _db.ProjectWorkstreams.FirstOrDefaultAsync(w => w.Id == id && w.ProjectId == projectId, ct);
        if (entity is null) return Result<ProjectWorkstreamDto>.Failure("تيار العمل غير موجود.", "workstream.not_found");

        if (request.ResponsibleTeamId == Guid.Empty || !await _db.Teams.AnyAsync(t => t.Id == request.ResponsibleTeamId, ct))
            return Result<ProjectWorkstreamDto>.Failure("الفريق المسؤول غير موجود.", "workstream.team_not_found");

        if (request.ResponsibleManagerId is Guid mgr && !await _db.Users.AnyAsync(u => u.Id == mgr, ct))
            return Result<ProjectWorkstreamDto>.Failure("المدير المسؤول غير موجود.", "workstream.manager_not_found");

        // النوع (WorkstreamTypeCode) لا يتغيّر — يبقى مفتاح التفرّد المنطقيّ. التحقّق من تكرار (النوع، الفريق) بعد التغيير.
        var duplicate = await _db.ProjectWorkstreams.AnyAsync(
            w => w.Id != id && w.ProjectId == projectId && w.WorkstreamTypeCode == entity.WorkstreamTypeCode && w.ResponsibleTeamId == request.ResponsibleTeamId, ct);
        if (duplicate)
            return Result<ProjectWorkstreamDto>.Failure("يوجد تيار عمل بنفس النوع والفريق المسؤول في هذا المشروع بالفعل.", "workstream.duplicate.conflict");

        var typeNameAr = await ResolveActiveTypeNameArAsync(entity.WorkstreamTypeCode, ct);
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? (typeNameAr ?? entity.WorkstreamTypeCode) : request.Name!.Trim();
        entity.ResponsibleTeamId = request.ResponsibleTeamId;
        entity.ResponsibleManagerId = request.ResponsibleManagerId;
        entity.Status = request.Status;
        entity.SortOrder = request.SortOrder;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes!.Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project_workstream.updated", nameof(ProjectWorkstream), entity.Id, ct: ct);

        return Result<ProjectWorkstreamDto>.Success((await MapManyAsync(new[] { entity }, ct))[0]);
    }

    public async Task<Result<ProjectWorkstreamDto>> SetActiveAsync(Guid projectId, Guid id, bool isActive, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ProjectWorkstreamDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(projectId)) return Result<ProjectWorkstreamDto>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        if (!await CanManagePlanAsync(projectId, uid, ct))
            return Result<ProjectWorkstreamDto>.Failure("إدارة مسارات العمل من صلاحية إدارة الخطّة أو مسؤولي هذا المشروع (مالكه أو قائد فريقه أو مدير حسابه) فقط.", "auth.forbidden");

        var entity = await _db.ProjectWorkstreams.FirstOrDefaultAsync(w => w.Id == id && w.ProjectId == projectId, ct);
        if (entity is null) return Result<ProjectWorkstreamDto>.Failure("تيار العمل غير موجود.", "workstream.not_found");

        if (entity.IsActive == isActive)
            return Result<ProjectWorkstreamDto>.Failure(isActive ? "تيار العمل مُفعّل بالفعل." : "تيار العمل معطّل بالفعل.", "workstream.state_unchanged.conflict");

        entity.IsActive = isActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, isActive ? "project_workstream.activated" : "project_workstream.deactivated", nameof(ProjectWorkstream), entity.Id, ct: ct);

        return Result<ProjectWorkstreamDto>.Success((await MapManyAsync(new[] { entity }, ct))[0]);
    }

    // ===== helpers =====
    /// <summary>
    /// بوّابة إدارة مسارات العمل بنطاق المشروع (لا بالدور وحده): إمّا دور إداريّ
    /// (<c>ProjectPlanManagers</c> = Admin/CEO/GM/Manager، بلا TeamLeader)، أو **أحد مسؤولي هذا
    /// المشروع بعينه**: مالكه أو قائد فريقه المسنَد أو مدير حسابه.
    ///
    /// <para>
    /// **لماذا وُسِّعت في P360-WF-R2؟** مسار العمل تنظيمُ تنفيذٍ لا التزامٌ تعاقديّ (§9)، ومن
    /// يقود التنفيذ هو من يجب أن ينظّمه. قصرها على مدير الحساب كان يترك قائد الفريق المسؤول
    /// عن التسليم عاجزًا عن إنشاء مسار عمل واحد لفريقه، فيدير تنفيذه خارج المنظومة.
    /// التوسّع **بالمورد لا بالدور**: لا يمنح شيئًا على مشروع لم يُسنَد إليه.
    /// </para>
    ///
    /// <para>تُستدعى بعد فحص <c>CanViewProject</c> في الكتابة. مطابقة لعقد <c>CanUpdateProject360ProgressAsync</c>.</para>
    /// </summary>
    private async Task<bool> CanManagePlanAsync(Guid projectId, Guid uid, CancellationToken ct)
    {
        if (_currentUser.IsInAnyRole(Roles.ProjectPlanManagers)) return true;
        return await _db.Projects.AsNoTracking()
            .AnyAsync(p => p.Id == projectId
                        && (p.AccountManagerId == uid || p.TeamLeaderId == uid || p.ProjectOwnerId == uid), ct);
    }

    private async Task<string?> ResolveActiveTypeNameArAsync(string code, CancellationToken ct) =>
        await _db.ExecutionTaxonomyValues.AsNoTracking()
            .Where(v => v.IsActive && v.Domain == WorkstreamTypeDomain && v.Code == code)
            .Select(v => v.NameAr)
            .FirstOrDefaultAsync(ct);

    private async Task<List<ProjectWorkstreamDto>> MapManyAsync(IReadOnlyCollection<ProjectWorkstream> rows, CancellationToken ct)
    {
        var teamIds = rows.Select(w => w.ResponsibleTeamId).Distinct().ToList();
        var teamNames = teamIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.Teams.Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);

        var managerIds = rows.Where(w => w.ResponsibleManagerId != null).Select(w => w.ResponsibleManagerId!.Value).Distinct().ToList();
        var managerNames = managerIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.Users.Where(u => managerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var codes = rows.Select(w => w.WorkstreamTypeCode).Distinct().ToList();
        var typeNames = codes.Count == 0 ? new Dictionary<string, string>()
            : await _db.ExecutionTaxonomyValues.AsNoTracking()
                .Where(v => v.Domain == WorkstreamTypeDomain && codes.Contains(v.Code))
                .ToDictionaryAsync(v => v.Code, v => v.NameAr, ct);

        return rows.Select(w => new ProjectWorkstreamDto(
            w.Id, w.ProjectId, w.WorkstreamTypeCode, typeNames.GetValueOrDefault(w.WorkstreamTypeCode),
            w.Name, w.ResponsibleTeamId, teamNames.GetValueOrDefault(w.ResponsibleTeamId),
            w.ResponsibleManagerId, w.ResponsibleManagerId is Guid mid ? managerNames.GetValueOrDefault(mid) : null,
            w.Status, w.SortOrder, w.Notes, w.IsActive, w.CreatedAtUtc, w.UpdatedAtUtc)).ToList();
    }
}
