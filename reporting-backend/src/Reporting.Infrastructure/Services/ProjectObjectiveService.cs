using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Domain.Entities.Projects360;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// أهداف المشروع (CPW-R3 · W4 · §4).
///
/// <para>
/// **تقدّم الهدف مشتقّ لا مخزَّن** (DN-02): لا عمود <c>ProgressPercent</c> على الهدف؛ النسبة
/// تُحتسَب من تحقيق مؤشّراته الموزون عبر <see cref="ProjectHealthPolicy"/>. هذا يمنع أن يتناقض
/// رقم مخزَّن مع مؤشّراته بعد أوّل قراءة.
/// </para>
///
/// <para>
/// **حارس مكافحة اليُتم**: الحذف النهائيّ لهدف يحمل مؤشّرات مرفوض بـ
/// <c>project_objective.has_kpis.conflict</c> (409) — قرار منتج صريح لا اعتماد على
/// <c>ON DELETE CASCADE</c>، لأنّ التعاقب يمحو تاريخ قراءات بلا إنذار.
/// </para>
/// </summary>
public class ProjectObjectiveService : IProjectObjectiveService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IProject360Authorization _auth;
    private readonly IAuditService _audit;
    private readonly IProjectHealthService _health;

    public ProjectObjectiveService(
        AppDbContext db,
        ICurrentUser currentUser,
        IProject360Authorization auth,
        IAuditService audit,
        IProjectHealthService health)
    {
        _db = db;
        _currentUser = currentUser;
        _auth = auth;
        _audit = audit;
        _health = health;
    }

    public async Task<Result<IReadOnlyList<ProjectObjectiveDto>>> ListAsync(Guid projectId, bool includeInactive, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<IReadOnlyList<ProjectObjectiveDto>>.Failure(scope.Error!, scope.ErrorCode);

        var dtos = await BuildAsync(projectId, includeInactive, ct);
        return Result<IReadOnlyList<ProjectObjectiveDto>>.Success(dtos);
    }

    public async Task<Result<ProjectObjectiveDto>> GetAsync(Guid projectId, Guid objectiveId, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<ProjectObjectiveDto>.Failure(scope.Error!, scope.ErrorCode);

        var dtos = await BuildAsync(projectId, includeInactive: true, ct);
        var dto = dtos.FirstOrDefault(o => o.Id == objectiveId);
        return dto is null
            ? Result<ProjectObjectiveDto>.Failure("الهدف غير موجود ضمن هذا المشروع.", Project360ErrorCodes.ObjectiveNotFound)
            : Result<ProjectObjectiveDto>.Success(dto);
    }

    public async Task<Result<ProjectObjectiveDto>> CreateAsync(Guid projectId, CreateProjectObjectiveRequest request, CancellationToken ct = default)
    {
        var gate = await AuthorizeStructuralAsync(projectId, ct);
        if (gate.Error is not null) return Result<ProjectObjectiveDto>.Failure(gate.Error.Value.Message, gate.Error.Value.Code);

        var invalid = Project360Guards.FirstError(
            Project360Guards.ValidateName(request.Name),
            Project360Guards.ValidateWeight(request.Weight),
            Project360Guards.ValidateSortOrder(request.SortOrder),
            Project360Guards.ValidateDateRange(request.StartDate, request.DueDate));
        if (invalid is not null) return Result<ProjectObjectiveDto>.Failure(invalid.Value.Message, invalid.Value.Code);

        var workstreamError = await ValidateWorkstreamAsync(projectId, request.WorkstreamId, ct);
        if (workstreamError is not null) return Result<ProjectObjectiveDto>.Failure(workstreamError.Value.Message, workstreamError.Value.Code);

        var entity = new ProjectObjective
        {
            ProjectId = projectId,
            WorkstreamId = request.WorkstreamId,
            Name = request.Name.Trim(),
            Description = Project360Guards.Trim(request.Description),
            Priority = request.Priority,
            Weight = request.Weight,
            Status = request.Status,
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            OwnerUserId = request.OwnerUserId,
            Notes = Project360Guards.Trim(request.Notes),
            SortOrder = request.SortOrder,
        };
        _db.ProjectObjectives.Add(entity);
        // وزن الهدف مدخل فعليّ في ترجيح المستوى الثاني (DEC-W4-03) ⟹ الصحّة تُعاد في نفس المعاملة.
        await _health.SaveWithHealthAsync(projectId, ct);
        await _audit.LogAsync(gate.ActorId, "project_objective.created", nameof(ProjectObjective), entity.Id, ct: ct);

        return await ReloadAsync(projectId, entity.Id, ct);
    }

    public async Task<Result<ProjectObjectiveDto>> UpdateAsync(Guid projectId, Guid objectiveId, UpdateProjectObjectiveRequest request, CancellationToken ct = default)
    {
        var gate = await AuthorizeStructuralAsync(projectId, ct);
        if (gate.Error is not null) return Result<ProjectObjectiveDto>.Failure(gate.Error.Value.Message, gate.Error.Value.Code);

        var entity = await FindAsync(projectId, objectiveId, ct);
        if (entity is null) return Result<ProjectObjectiveDto>.Failure("الهدف غير موجود ضمن هذا المشروع.", Project360ErrorCodes.ObjectiveNotFound);

        var invalid = Project360Guards.FirstError(
            Project360Guards.ValidateName(request.Name),
            Project360Guards.ValidateWeight(request.Weight),
            Project360Guards.ValidateSortOrder(request.SortOrder),
            Project360Guards.ValidateDateRange(request.StartDate, request.DueDate));
        if (invalid is not null) return Result<ProjectObjectiveDto>.Failure(invalid.Value.Message, invalid.Value.Code);

        var workstreamError = await ValidateWorkstreamAsync(projectId, request.WorkstreamId, ct);
        if (workstreamError is not null) return Result<ProjectObjectiveDto>.Failure(workstreamError.Value.Message, workstreamError.Value.Code);

        entity.WorkstreamId = request.WorkstreamId;
        entity.Name = request.Name.Trim();
        entity.Description = Project360Guards.Trim(request.Description);
        entity.Priority = request.Priority;
        entity.Weight = request.Weight;
        entity.StartDate = request.StartDate;
        entity.DueDate = request.DueDate;
        entity.OwnerUserId = request.OwnerUserId;
        entity.Notes = Project360Guards.Trim(request.Notes);
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        // قد يتغيّر الوزن ⟹ مدخل فعليّ للصحّة.
        await _health.SaveWithHealthAsync(projectId, ct);
        await _audit.LogAsync(gate.ActorId, "project_objective.updated", nameof(ProjectObjective), entity.Id, ct: ct);

        return await ReloadAsync(projectId, entity.Id, ct);
    }

    /// <summary>
    /// تحديث الحالة فقط — **المستوى التشغيليّ** (D-07): مسموح لقائد الفريق ومدير الحساب المسؤولين
    /// عن هذا المشروع، إضافةً إلى الإدارة. لا يمسّ الوزن ولا الاسم ولا أيّ حقل بنيويّ.
    /// </summary>
    public async Task<Result<ProjectObjectiveDto>> UpdateStatusAsync(Guid projectId, Guid objectiveId, UpdateProjectObjectiveStatusRequest request, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<ProjectObjectiveDto>.Failure(scope.Error!, scope.ErrorCode);
        if (!await _auth.CanUpdateProject360ProgressAsync(scope.Value!, ct))
            return Result<ProjectObjectiveDto>.Failure("لا تملك صلاحية تحديث تقدّم هذا المشروع.", "auth.forbidden");
        if (_currentUser.UserId is not Guid uid) return Result<ProjectObjectiveDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var entity = await FindAsync(projectId, objectiveId, ct);
        if (entity is null) return Result<ProjectObjectiveDto>.Failure("الهدف غير موجود ضمن هذا المشروع.", Project360ErrorCodes.ObjectiveNotFound);

        entity.Status = request.Status;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        // **لا إعادة احتساب صحّة هنا عمدًا**: حالة الهدف لا تدخل أيّ معادلة في ProjectHealthPolicy
        // (ObjectiveInput.Status تُمرَّر للعرض فقط ولا تُقرأ في ComputeWeightedScore ولا في
        // ComputeHealth). إعادة الاحتساب هنا كانت ستكون كتابة بلا سبب.
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project_objective.status_updated", nameof(ProjectObjective), entity.Id, ct: ct);

        return await ReloadAsync(projectId, entity.Id, ct);
    }

    public async Task<Result<ProjectObjectiveDto>> SetActiveAsync(Guid projectId, Guid objectiveId, bool isActive, CancellationToken ct = default)
    {
        var gate = await AuthorizeStructuralAsync(projectId, ct);
        if (gate.Error is not null) return Result<ProjectObjectiveDto>.Failure(gate.Error.Value.Message, gate.Error.Value.Code);

        var entity = await FindAsync(projectId, objectiveId, ct);
        if (entity is null) return Result<ProjectObjectiveDto>.Failure("الهدف غير موجود ضمن هذا المشروع.", Project360ErrorCodes.ObjectiveNotFound);

        entity.IsActive = isActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        // التفعيل/التعطيل يُدخِل الهدف ومؤشّراته في التجميع أو يُخرجها ⟹ مدخل فعليّ للصحّة.
        await _health.SaveWithHealthAsync(projectId, ct);
        await _audit.LogAsync(gate.ActorId, isActive ? "project_objective.activated" : "project_objective.deactivated", nameof(ProjectObjective), entity.Id, ct: ct);

        return await ReloadAsync(projectId, entity.Id, ct);
    }

    public async Task<Result> DeleteAsync(Guid projectId, Guid objectiveId, CancellationToken ct = default)
    {
        var gate = await AuthorizeStructuralAsync(projectId, ct);
        if (gate.Error is not null) return Result.Failure(gate.Error.Value.Message, gate.Error.Value.Code);

        var entity = await FindAsync(projectId, objectiveId, ct);
        if (entity is null) return Result.Failure("الهدف غير موجود ضمن هذا المشروع.", Project360ErrorCodes.ObjectiveNotFound);

        // قرار منتج: المنع الصريح — لا اعتماد على التعاقب الذي يمحو تاريخ القراءات صامتًا.
        if (await _db.ProjectKpis.AnyAsync(k => k.ObjectiveId == objectiveId, ct))
            return Result.Failure("لا يمكن حذف هدف يحتوي مؤشّرات. عطّله أو انقل مؤشّراته أوّلًا.", Project360ErrorCodes.ObjectiveHasKpis);

        _db.ProjectObjectives.Remove(entity);
        // حذف هدف يغيّر مقام ترجيح الأهداف ⟹ مدخل فعليّ للصحّة.
        await _health.SaveWithHealthAsync(projectId, ct);
        await _audit.LogAsync(gate.ActorId, "project_objective.deleted", nameof(ProjectObjective), objectiveId, ct: ct);
        return Result.Success();
    }

    // ===== helpers =====

    private async Task<(Guid ActorId, (string Message, string Code)? Error)> AuthorizeStructuralAsync(Guid projectId, CancellationToken ct)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return (Guid.Empty, (scope.Error!, scope.ErrorCode!));
        if (!await _auth.CanManageProject360Async(scope.Value!, ct))
            return (Guid.Empty, ("لا تملك صلاحية إدارة أهداف هذا المشروع.", "auth.forbidden"));
        if (_currentUser.UserId is not Guid uid) return (Guid.Empty, ("غير مصرّح.", "auth.unauthenticated"));
        return (uid, null);
    }

    private Task<ProjectObjective?> FindAsync(Guid projectId, Guid objectiveId, CancellationToken ct) =>
        _db.ProjectObjectives.FirstOrDefaultAsync(o => o.Id == objectiveId && o.ProjectId == projectId, ct);

    private async Task<Result<ProjectObjectiveDto>> ReloadAsync(Guid projectId, Guid objectiveId, CancellationToken ct)
    {
        var dtos = await BuildAsync(projectId, includeInactive: true, ct);
        var dto = dtos.First(o => o.Id == objectiveId);
        return Result<ProjectObjectiveDto>.Success(dto);
    }

    private async Task<(string Message, string Code)?> ValidateWorkstreamAsync(Guid projectId, Guid? workstreamId, CancellationToken ct)
    {
        if (workstreamId is not Guid wid) return null;
        var ok = await _db.ProjectWorkstreams.AsNoTracking().AnyAsync(w => w.Id == wid && w.ProjectId == projectId, ct);
        return ok ? null : ("تيّار العمل غير موجود ضمن هذا المشروع.", Project360ErrorCodes.DeliverableWorkstreamMismatch);
    }

    /// <summary>
    /// يبني أهداف المشروع بتقدّمها المشتقّ — **ثلاثة استعلامات ثابتة** (أهداف · مؤشّرات · أسماء الملّاك)
    /// مهما بلغ عدد الأهداف أو المؤشّرات. لا استعلام داخل حلقة (§15).
    /// </summary>
    internal async Task<List<ProjectObjectiveDto>> BuildAsync(Guid projectId, bool includeInactive, CancellationToken ct)
    {
        var objectivesQuery = _db.ProjectObjectives.AsNoTracking().Where(o => o.ProjectId == projectId);
        if (!includeInactive) objectivesQuery = objectivesQuery.Where(o => o.IsActive);
        var objectives = await objectivesQuery.OrderBy(o => o.SortOrder).ThenBy(o => o.Name).ToListAsync(ct);
        if (objectives.Count == 0) return new List<ProjectObjectiveDto>();

        var kpis = await _db.ProjectKpis.AsNoTracking()
            .Where(k => k.ProjectId == projectId && k.IsActive)
            .Select(k => new { k.ObjectiveId, k.Weight, k.CurrentValue, k.TargetValue, k.Direction })
            .ToListAsync(ct);

        var ownerIds = objectives.Where(o => o.OwnerUserId != null).Select(o => o.OwnerUserId!.Value).Distinct().ToList();
        var ownerNames = ownerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var kpisByObjective = kpis.GroupBy(k => k.ObjectiveId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ProjectHealthPolicy.WeightedAchievement>)g
                .Select(k => new ProjectHealthPolicy.WeightedAchievement(
                    k.Weight,
                    ProjectHealthPolicy.ComputeAchievement(k.CurrentValue, k.TargetValue, k.Direction)))
                .ToList());

        var inputs = objectives.Select(o => new ProjectHealthPolicy.ObjectiveInput(
            o.Id, o.Status, o.Weight,
            kpisByObjective.GetValueOrDefault(o.Id, Array.Empty<ProjectHealthPolicy.WeightedAchievement>())))
            .ToList();

        var progress = ProjectHealthPolicy.ComputeObjectiveProgress(inputs)
            .ToDictionary(p => p.ObjectiveId);

        return objectives.Select(o =>
        {
            var p = progress[o.Id];
            return new ProjectObjectiveDto(
                o.Id, o.ProjectId, o.WorkstreamId, o.Name, o.Description, o.Priority,
                o.Weight, p.NormalizedWeight, o.Status, o.StartDate, o.DueDate,
                o.OwnerUserId, o.OwnerUserId is Guid oid ? ownerNames.GetValueOrDefault(oid) : null,
                o.Notes, o.SortOrder, o.IsActive,
                p.KpiScore, p.TotalKpiCount, p.ComputedKpiCount,
                o.CreatedAtUtc, o.UpdatedAtUtc);
        }).ToList();
    }
}
