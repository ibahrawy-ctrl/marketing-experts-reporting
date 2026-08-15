using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Domain.Entities.Projects360;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// المخرَجات التعاقديّة (CPW-R3 · W4 · §11) — **Contract Deliverable** حصرًا.
///
/// <para>
/// **المصطلح مقفل**: هذا الكيان يمثّل ما **وُعِد به العميل**؛ وهو ليس <c>WorkstreamDeliverable</c>
/// (التخطيط الإنتاجيّ). لم يُمَسّ الأخير بحرف في هذه الطبقة.
/// </para>
///
/// <para>
/// **رمز النوع لقطة ثابتة**: يُتحقَّق منه مقابل كتالوج <c>contract_deliverable</c> عند الإنشاء فقط،
/// ثمّ لا يُعدَّل أبدًا — تعديله لاحقًا يغيّر معنى التزام تعاقديّ منشور بأثر رجعيّ.
/// </para>
///
/// <para>
/// **طبقتا كتابة (D-07)**: التعديل البنيويّ للإدارة، وتحديث التقدّم/الحالة يشمل قائد الفريق
/// ومدير الحساب المسؤولين عن هذا المشروع بعينه.
/// </para>
/// </summary>
public class ProjectContractDeliverableService : IProjectContractDeliverableService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IProject360Authorization _auth;
    private readonly IAuditService _audit;

    public ProjectContractDeliverableService(AppDbContext db, ICurrentUser currentUser, IProject360Authorization auth, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _auth = auth;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ProjectContractDeliverableDto>>> ListAsync(Guid projectId, bool includeInactive, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<IReadOnlyList<ProjectContractDeliverableDto>>.Failure(scope.Error!, scope.ErrorCode);

        var dtos = await BuildAsync(projectId, includeInactive, ct);
        return Result<IReadOnlyList<ProjectContractDeliverableDto>>.Success(dtos);
    }

    public async Task<Result<ProjectContractDeliverableDto>> GetAsync(Guid projectId, Guid deliverableId, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<ProjectContractDeliverableDto>.Failure(scope.Error!, scope.ErrorCode);

        var dtos = await BuildAsync(projectId, includeInactive: true, ct);
        var dto = dtos.FirstOrDefault(d => d.Id == deliverableId);
        return dto is null
            ? Result<ProjectContractDeliverableDto>.Failure("المخرَج التعاقديّ غير موجود ضمن هذا المشروع.", Project360ErrorCodes.DeliverableNotFound)
            : Result<ProjectContractDeliverableDto>.Success(dto);
    }

    public async Task<Result<ProjectContractDeliverableDto>> CreateAsync(Guid projectId, CreateProjectContractDeliverableRequest request, CancellationToken ct = default)
    {
        var gate = await AuthorizeStructuralAsync(projectId, ct);
        if (gate.Error is not null) return Result<ProjectContractDeliverableDto>.Failure(gate.Error.Value.Message, gate.Error.Value.Code);

        var typeCode = Project360Guards.Trim(request.DeliverableTypeCode);
        if (typeCode is null)
            return Result<ProjectContractDeliverableDto>.Failure("نوع المخرَج التعاقديّ مطلوب.", Project360ErrorCodes.DeliverableTypeInvalid);

        var invalid = Project360Guards.FirstError(
            Project360Guards.ValidateSortOrder(request.SortOrder),
            Project360Guards.ValidateDateRange(request.StartDate, request.DueDate),
            ValidateQuantity(request.PlannedQuantity, completedQuantity: 0));
        if (invalid is not null) return Result<ProjectContractDeliverableDto>.Failure(invalid.Value.Message, invalid.Value.Code);

        var typeNames = await Project360Guards.ResolveActiveCodeNamesAsync(
            _db, Project360CatalogDomains.ContractDeliverable, new[] { typeCode }, ct);
        if (!typeNames.TryGetValue(typeCode, out var typeNameAr))
            return Result<ProjectContractDeliverableDto>.Failure("نوع المخرَج التعاقديّ غير معروف أو غير مفعَّل.", Project360ErrorCodes.DeliverableTypeInvalid);

        var linkError = await ValidateLinksAsync(projectId, request.ObjectiveId, request.WorkstreamId, ct);
        if (linkError is not null) return Result<ProjectContractDeliverableDto>.Failure(linkError.Value.Message, linkError.Value.Code);

        // الاسم اختياريّ عند الإنشاء: يتراجع إلى الاسم العربيّ للنوع بدل رفض الطلب أو تخزين فراغ.
        var name = Project360Guards.Trim(request.Name) ?? typeNameAr;

        var entity = new ProjectDeliverable
        {
            ProjectId = projectId,
            ObjectiveId = request.ObjectiveId,
            WorkstreamId = request.WorkstreamId,
            DeliverableTypeCode = typeCode,
            Name = name,
            Description = Project360Guards.Trim(request.Description),
            PlannedQuantity = request.PlannedQuantity,
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            Priority = request.Priority,
            OwnerUserId = request.OwnerUserId,
            Notes = Project360Guards.Trim(request.Notes),
            SortOrder = request.SortOrder,
        };
        _db.ProjectDeliverables.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(gate.ActorId, "project_contract_deliverable.created", nameof(ProjectDeliverable), entity.Id, ct: ct);

        return await ReloadAsync(projectId, entity.Id, ct);
    }

    /// <summary>
    /// تعديل بنيويّ. <c>DeliverableTypeCode</c> **لا يُعدَّل** — لا يُقرأ من الطلب أصلًا،
    /// فحارس <c>deliverable_type_immutable</c> يقع في طبقة الواجهة إن أرسلت رمزًا مخالفًا.
    /// </summary>
    public async Task<Result<ProjectContractDeliverableDto>> UpdateAsync(Guid projectId, Guid deliverableId, UpdateProjectContractDeliverableRequest request, CancellationToken ct = default)
    {
        var gate = await AuthorizeStructuralAsync(projectId, ct);
        if (gate.Error is not null) return Result<ProjectContractDeliverableDto>.Failure(gate.Error.Value.Message, gate.Error.Value.Code);

        var entity = await FindAsync(projectId, deliverableId, ct);
        if (entity is null) return Result<ProjectContractDeliverableDto>.Failure("المخرَج التعاقديّ غير موجود ضمن هذا المشروع.", Project360ErrorCodes.DeliverableNotFound);

        var invalid = Project360Guards.FirstError(
            Project360Guards.ValidateName(request.Name),
            Project360Guards.ValidateSortOrder(request.SortOrder),
            Project360Guards.ValidateDateRange(request.StartDate, request.DueDate),
            ValidateQuantity(request.PlannedQuantity, entity.CompletedQuantity));
        if (invalid is not null) return Result<ProjectContractDeliverableDto>.Failure(invalid.Value.Message, invalid.Value.Code);

        var linkError = await ValidateLinksAsync(projectId, request.ObjectiveId, request.WorkstreamId, ct);
        if (linkError is not null) return Result<ProjectContractDeliverableDto>.Failure(linkError.Value.Message, linkError.Value.Code);

        entity.ObjectiveId = request.ObjectiveId;
        entity.WorkstreamId = request.WorkstreamId;
        entity.Name = request.Name.Trim();
        entity.Description = Project360Guards.Trim(request.Description);
        entity.PlannedQuantity = request.PlannedQuantity;
        entity.StartDate = request.StartDate;
        entity.DueDate = request.DueDate;
        entity.Priority = request.Priority;
        entity.OwnerUserId = request.OwnerUserId;
        entity.Notes = Project360Guards.Trim(request.Notes);
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(gate.ActorId, "project_contract_deliverable.updated", nameof(ProjectDeliverable), entity.Id, ct: ct);

        return await ReloadAsync(projectId, entity.Id, ct);
    }

    /// <summary>
    /// المستوى التشغيليّ (D-07). لا اشتقاق آليّ بين الكمّيّة والنسبة (Manual-First)،
    /// ولا اشتقاق للحالة من التاريخ — <c>Delayed</c> تُعلَن ولا تُستنتَج.
    /// </summary>
    public async Task<Result<ProjectContractDeliverableDto>> UpdateProgressAsync(Guid projectId, Guid deliverableId, UpdateProjectContractDeliverableProgressRequest request, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<ProjectContractDeliverableDto>.Failure(scope.Error!, scope.ErrorCode);
        if (!await _auth.CanUpdateProject360ProgressAsync(scope.Value!, ct))
            return Result<ProjectContractDeliverableDto>.Failure("لا تملك صلاحية تحديث تقدّم هذا المشروع.", "auth.forbidden");
        if (_currentUser.UserId is not Guid uid) return Result<ProjectContractDeliverableDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var entity = await FindAsync(projectId, deliverableId, ct);
        if (entity is null) return Result<ProjectContractDeliverableDto>.Failure("المخرَج التعاقديّ غير موجود ضمن هذا المشروع.", Project360ErrorCodes.DeliverableNotFound);

        var completed = request.CompletedQuantity ?? entity.CompletedQuantity;
        var invalid = Project360Guards.FirstError(
            Project360Guards.ValidatePercent(request.ProgressPercent, "نسبة الإنجاز"),
            ValidateQuantity(entity.PlannedQuantity, completed));
        if (invalid is not null) return Result<ProjectContractDeliverableDto>.Failure(invalid.Value.Message, invalid.Value.Code);

        entity.Status = request.Status;
        entity.ProgressPercent = request.ProgressPercent;
        entity.CompletedQuantity = completed;
        entity.DeliveredAtUtc = request.DeliveredAtUtc ?? entity.DeliveredAtUtc;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project_contract_deliverable.progress_updated", nameof(ProjectDeliverable), entity.Id, ct: ct);

        return await ReloadAsync(projectId, entity.Id, ct);
    }

    public async Task<Result<ProjectContractDeliverableDto>> SetActiveAsync(Guid projectId, Guid deliverableId, bool isActive, CancellationToken ct = default)
    {
        var gate = await AuthorizeStructuralAsync(projectId, ct);
        if (gate.Error is not null) return Result<ProjectContractDeliverableDto>.Failure(gate.Error.Value.Message, gate.Error.Value.Code);

        var entity = await FindAsync(projectId, deliverableId, ct);
        if (entity is null) return Result<ProjectContractDeliverableDto>.Failure("المخرَج التعاقديّ غير موجود ضمن هذا المشروع.", Project360ErrorCodes.DeliverableNotFound);

        entity.IsActive = isActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(gate.ActorId, isActive ? "project_contract_deliverable.activated" : "project_contract_deliverable.deactivated", nameof(ProjectDeliverable), entity.Id, ct: ct);

        return await ReloadAsync(projectId, entity.Id, ct);
    }

    public async Task<Result<IReadOnlyList<ProjectCatalogOptionDto>>> ListTypesAsync(Guid projectId, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded)
            return Result<IReadOnlyList<ProjectCatalogOptionDto>>.Failure(scope.Error!, scope.ErrorCode);

        // كتالوج أنواع المخرَجات التعاقديّة كامل بلا ترشيح بنوع الخدمة: المخرَج التعاقديّ يخضع
        // للعقد لا لتصنيف الخدمة، وقد يتضمّن عقد واحد مخرَجات من أكثر من تخصّص.
        var options = await _db.ExecutionTaxonomyValues.AsNoTracking()
            .Where(v => v.IsActive && v.Domain == Project360CatalogDomains.ContractDeliverable)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.NameAr)
            .Select(v => new ProjectCatalogOptionDto(v.Code, v.NameAr, v.SortOrder))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ProjectCatalogOptionDto>>.Success(options);
    }

    // ===== helpers =====

    private async Task<(Guid ActorId, (string Message, string Code)? Error)> AuthorizeStructuralAsync(Guid projectId, CancellationToken ct)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return (Guid.Empty, (scope.Error!, scope.ErrorCode!));
        if (!await _auth.CanManageProject360Async(scope.Value!, ct))
            return (Guid.Empty, ("لا تملك صلاحية إدارة المخرَجات التعاقديّة لهذا المشروع.", "auth.forbidden"));
        if (_currentUser.UserId is not Guid uid) return (Guid.Empty, ("غير مصرّح.", "auth.unauthenticated"));
        return (uid, null);
    }

    private Task<ProjectDeliverable?> FindAsync(Guid projectId, Guid deliverableId, CancellationToken ct) =>
        _db.ProjectDeliverables.FirstOrDefaultAsync(d => d.Id == deliverableId && d.ProjectId == projectId, ct);

    private async Task<Result<ProjectContractDeliverableDto>> ReloadAsync(Guid projectId, Guid deliverableId, CancellationToken ct)
    {
        var dtos = await BuildAsync(projectId, includeInactive: true, ct);
        return Result<ProjectContractDeliverableDto>.Success(dtos.First(d => d.Id == deliverableId));
    }

    private static (string Message, string Code)? ValidateQuantity(int plannedQuantity, int completedQuantity)
    {
        if (plannedQuantity < 1)
            return ("الكمّيّة المتعاقَد عليها يجب أن تكون 1 على الأقلّ.", Project360ErrorCodes.DeliverableQuantityInvalid);
        if (completedQuantity < 0)
            return ("الكمّيّة المُسلَّمة لا يمكن أن تكون سالبة.", Project360ErrorCodes.DeliverableQuantityInvalid);
        if (completedQuantity > plannedQuantity)
            return ("الكمّيّة المُسلَّمة لا يمكن أن تتجاوز الكمّيّة المتعاقَد عليها.", Project360ErrorCodes.DeliverableQuantityInvalid);
        return null;
    }

    /// <summary>
    /// حارسا الاتّساق المرجعيّ: الهدف وتيّار العمل المرتبطان يجب أن يكونا من **نفس المشروع**
    /// — وإلّا صار المخرَج جسرًا يعبر حدود المشاريع فيسرّب بيانات عبر التقارير.
    /// </summary>
    private async Task<(string Message, string Code)?> ValidateLinksAsync(Guid projectId, Guid? objectiveId, Guid? workstreamId, CancellationToken ct)
    {
        if (objectiveId is Guid oid)
        {
            var ok = await _db.ProjectObjectives.AsNoTracking().AnyAsync(o => o.Id == oid && o.ProjectId == projectId, ct);
            if (!ok) return ("الهدف المرتبط ليس من نفس المشروع.", Project360ErrorCodes.DeliverableObjectiveMismatch);
        }

        if (workstreamId is Guid wid)
        {
            var ok = await _db.ProjectWorkstreams.AsNoTracking().AnyAsync(w => w.Id == wid && w.ProjectId == projectId, ct);
            if (!ok) return ("تيّار العمل المرتبط ليس من نفس المشروع.", Project360ErrorCodes.DeliverableWorkstreamMismatch);
        }

        return null;
    }

    /// <summary>
    /// **ثلاثة استعلامات ثابتة** (المخرَجات · أسماء أنواع الكتالوج · أسماء الملّاك) مهما بلغ العدد (§15).
    /// </summary>
    internal async Task<List<ProjectContractDeliverableDto>> BuildAsync(Guid projectId, bool includeInactive, CancellationToken ct)
    {
        var query = _db.ProjectDeliverables.AsNoTracking().Where(d => d.ProjectId == projectId);
        if (!includeInactive) query = query.Where(d => d.IsActive);
        var rows = await query.OrderBy(d => d.SortOrder).ThenBy(d => d.Name).ToListAsync(ct);
        if (rows.Count == 0) return new List<ProjectContractDeliverableDto>();

        var typeCodes = rows.Select(d => d.DeliverableTypeCode).Distinct().ToList();
        var typeNames = await Project360Guards.ResolveActiveCodeNamesAsync(
            _db, Project360CatalogDomains.ContractDeliverable, typeCodes, ct);

        var ownerIds = rows.Where(d => d.OwnerUserId != null).Select(d => d.OwnerUserId!.Value).Distinct().ToList();
        var ownerNames = ownerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return rows.Select(d => new ProjectContractDeliverableDto(
            d.Id, d.ProjectId, d.ObjectiveId, d.WorkstreamId,
            d.DeliverableTypeCode, typeNames.GetValueOrDefault(d.DeliverableTypeCode),
            d.Name, d.Description, d.PlannedQuantity, d.CompletedQuantity,
            d.Status, d.ProgressPercent, d.StartDate, d.DueDate, d.DeliveredAtUtc,
            d.Priority, d.OwnerUserId,
            d.OwnerUserId is Guid oid ? ownerNames.GetValueOrDefault(oid) : null,
            d.Notes, d.SortOrder, d.IsActive,
            d.CreatedAtUtc, d.UpdatedAtUtc)).ToList();
    }
}
