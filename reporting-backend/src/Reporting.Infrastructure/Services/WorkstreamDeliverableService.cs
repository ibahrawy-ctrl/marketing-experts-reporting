using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Clients;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// إدارة مخرَجات خطّة الإنتاج داخل هدف العمل (P2). كل صفّ = <b>مجموعة مخرَج مخطَّطة (Planned Deliverable Group)</b>
/// ذات معرّف ثابت (مثل «١٢ منشور») — <b>ليست عنصرًا فرديًّا منفَّذًا</b>؛ لا يوجد كيان DeliverableItem الآن.
/// القراءة مُنَطَّقة بنطاق رؤية المشروع (IClientProjectAccess). الكتابة محكومة داخل الخدمة عبر
/// <see cref="CanManagePlanAsync"/> بنطاق المشروع (لا بالدور وحده): أدوار الإدارة (ProjectPlanManagers)
/// أو مدير حسابات المشروع نفسه (Project.AccountManagerId == المستخدم الحالي). لا حذف نهائيّ — التعطيل فقط.
/// نوع المخرَج يُتحقَّق منه مقابل كتالوج التصنيفات (Domain=deliverable) وسياق الاستخدام (Domain=usage_context)
/// بلا مفتاح أجنبيّ. **سجلّ تخطيط فقط — لا يُسجَّل أيّ تنفيذ فعليّ هنا (يأتي في مرحلة لاحقة P4).**
/// </summary>
public class WorkstreamDeliverableService : IWorkstreamDeliverableService
{
    private const string DeliverableTypeDomain = "deliverable";
    private const string UsageContextDomain = "usage_context";

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProjectAccess _access;
    private readonly IAuditService _audit;

    public WorkstreamDeliverableService(AppDbContext db, ICurrentUser currentUser, IClientProjectAccess access, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<WorkstreamDeliverableDto>>> ListAsync(Guid projectId, Guid workstreamId, bool includeInactive, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null) return Result<IReadOnlyList<WorkstreamDeliverableDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(projectId)) return Result<IReadOnlyList<WorkstreamDeliverableDto>>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        var workstreamOk = await _db.ProjectWorkstreams.AnyAsync(w => w.Id == workstreamId && w.ProjectId == projectId, ct);
        if (!workstreamOk) return Result<IReadOnlyList<WorkstreamDeliverableDto>>.Failure("تيار العمل غير موجود ضمن هذا المشروع.", "workstream.not_found");

        var q = _db.WorkstreamDeliverables.AsNoTracking().Where(d => d.WorkstreamId == workstreamId);
        if (!includeInactive) q = q.Where(d => d.IsActive);
        var rows = await q.OrderBy(d => d.SortOrder).ThenBy(d => d.Name).ToListAsync(ct);
        var dtos = await MapManyAsync(rows, ct);
        return Result<IReadOnlyList<WorkstreamDeliverableDto>>.Success(dtos);
    }

    public async Task<Result<WorkstreamDeliverableDto>> CreateAsync(Guid projectId, Guid workstreamId, CreateWorkstreamDeliverableRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<WorkstreamDeliverableDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(projectId)) return Result<WorkstreamDeliverableDto>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        if (!await CanManagePlanAsync(projectId, uid, ct))
            return Result<WorkstreamDeliverableDto>.Failure("لا تملك صلاحية إدارة خطّة إنتاج هذا المشروع.", "auth.forbidden");

        var workstream = await _db.ProjectWorkstreams.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workstreamId && w.ProjectId == projectId, ct);
        if (workstream is null) return Result<WorkstreamDeliverableDto>.Failure("هدف العمل غير موجود ضمن هذا المشروع.", "workstream.not_found");
        if (!workstream.IsActive) return Result<WorkstreamDeliverableDto>.Failure("لا يمكن إضافة مخرَج إلى هدف عمل معطّل.", "workstream.inactive.conflict");

        var code = (request.DeliverableTypeCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
            return Result<WorkstreamDeliverableDto>.Failure("رمز نوع المخرَج مطلوب.", "deliverable.type_required");

        var typeNameAr = await ResolveActiveNameArAsync(DeliverableTypeDomain, code, ct);
        if (typeNameAr is null)
            return Result<WorkstreamDeliverableDto>.Failure("نوع المخرَج غير معروف أو غير نشط في كتالوج التصنيفات.", "deliverable.type_invalid");

        var usageCode = string.IsNullOrWhiteSpace(request.UsageContextCode) ? null : request.UsageContextCode!.Trim();
        if (usageCode is not null && await ResolveActiveNameArAsync(UsageContextDomain, usageCode, ct) is null)
            return Result<WorkstreamDeliverableDto>.Failure("سياق الاستخدام غير معروف أو غير نشط في كتالوج التصنيفات.", "deliverable.usage_invalid");

        if (request.ResponsibleUserId is Guid resp && !await _db.Users.AnyAsync(u => u.Id == resp, ct))
            return Result<WorkstreamDeliverableDto>.Failure("المسؤول عن المخرَج غير موجود.", "deliverable.responsible_not_found");

        var planError = ValidatePlanningFields(request.PlannedQuantity, request.EstimatedHours, request.StartDate, request.DueDate);
        if (planError is not null) return Result<WorkstreamDeliverableDto>.Failure(planError.Value.Message, planError.Value.Code);

        var name = string.IsNullOrWhiteSpace(request.Name) ? (typeNameAr ?? code) : request.Name!.Trim();

        var entity = new WorkstreamDeliverable
        {
            WorkstreamId = workstreamId,
            DeliverableTypeCode = code,
            UsageContextCode = usageCode,
            Name = name,
            PlannedQuantity = request.PlannedQuantity,
            EstimatedHours = request.EstimatedHours,
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            Priority = request.Priority,
            ResponsibleUserId = request.ResponsibleUserId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes!.Trim(),
            SortOrder = request.SortOrder,
        };
        _db.WorkstreamDeliverables.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "workstream_deliverable.created", nameof(WorkstreamDeliverable), entity.Id, ct: ct);

        return Result<WorkstreamDeliverableDto>.Success((await MapManyAsync(new[] { entity }, ct))[0]);
    }

    public async Task<Result<WorkstreamDeliverableDto>> UpdateAsync(Guid projectId, Guid workstreamId, Guid id, UpdateWorkstreamDeliverableRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<WorkstreamDeliverableDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(projectId)) return Result<WorkstreamDeliverableDto>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        if (!await CanManagePlanAsync(projectId, uid, ct))
            return Result<WorkstreamDeliverableDto>.Failure("لا تملك صلاحية إدارة خطّة إنتاج هذا المشروع.", "auth.forbidden");

        var entity = await _db.WorkstreamDeliverables
            .FirstOrDefaultAsync(d => d.Id == id && d.WorkstreamId == workstreamId
                && _db.ProjectWorkstreams.Any(w => w.Id == workstreamId && w.ProjectId == projectId), ct);
        if (entity is null) return Result<WorkstreamDeliverableDto>.Failure("المخرَج غير موجود.", "deliverable.not_found");

        var usageCode = string.IsNullOrWhiteSpace(request.UsageContextCode) ? null : request.UsageContextCode!.Trim();
        if (usageCode is not null && await ResolveActiveNameArAsync(UsageContextDomain, usageCode, ct) is null)
            return Result<WorkstreamDeliverableDto>.Failure("سياق الاستخدام غير معروف أو غير نشط في كتالوج التصنيفات.", "deliverable.usage_invalid");

        if (request.ResponsibleUserId is Guid resp && !await _db.Users.AnyAsync(u => u.Id == resp, ct))
            return Result<WorkstreamDeliverableDto>.Failure("المسؤول عن المخرَج غير موجود.", "deliverable.responsible_not_found");

        var planError = ValidatePlanningFields(request.PlannedQuantity, request.EstimatedHours, request.StartDate, request.DueDate);
        if (planError is not null) return Result<WorkstreamDeliverableDto>.Failure(planError.Value.Message, planError.Value.Code);

        // نوع المخرَج (DeliverableTypeCode) لا يتغيّر — لقطة ثابتة كنمط نوع تيار العمل.
        var typeNameAr = await ResolveActiveNameArAsync(DeliverableTypeDomain, entity.DeliverableTypeCode, ct);
        entity.UsageContextCode = usageCode;
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? (typeNameAr ?? entity.DeliverableTypeCode) : request.Name!.Trim();
        entity.PlannedQuantity = request.PlannedQuantity;
        entity.EstimatedHours = request.EstimatedHours;
        entity.StartDate = request.StartDate;
        entity.DueDate = request.DueDate;
        entity.Priority = request.Priority;
        entity.ResponsibleUserId = request.ResponsibleUserId;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes!.Trim();
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "workstream_deliverable.updated", nameof(WorkstreamDeliverable), entity.Id, ct: ct);

        return Result<WorkstreamDeliverableDto>.Success((await MapManyAsync(new[] { entity }, ct))[0]);
    }

    public async Task<Result<WorkstreamDeliverableDto>> SetActiveAsync(Guid projectId, Guid workstreamId, Guid id, bool isActive, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<WorkstreamDeliverableDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(projectId)) return Result<WorkstreamDeliverableDto>.Failure("هذا المشروع خارج نطاق صلاحيتك.", "auth.forbidden");

        if (!await CanManagePlanAsync(projectId, uid, ct))
            return Result<WorkstreamDeliverableDto>.Failure("لا تملك صلاحية إدارة خطّة إنتاج هذا المشروع.", "auth.forbidden");

        var entity = await _db.WorkstreamDeliverables
            .FirstOrDefaultAsync(d => d.Id == id && d.WorkstreamId == workstreamId
                && _db.ProjectWorkstreams.Any(w => w.Id == workstreamId && w.ProjectId == projectId), ct);
        if (entity is null) return Result<WorkstreamDeliverableDto>.Failure("المخرَج غير موجود.", "deliverable.not_found");

        if (entity.IsActive == isActive)
            return Result<WorkstreamDeliverableDto>.Failure(isActive ? "المخرَج مُفعّل بالفعل." : "المخرَج معطّل بالفعل.", "deliverable.state_unchanged.conflict");

        entity.IsActive = isActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, isActive ? "workstream_deliverable.activated" : "workstream_deliverable.deactivated", nameof(WorkstreamDeliverable), entity.Id, ct: ct);

        return Result<WorkstreamDeliverableDto>.Success((await MapManyAsync(new[] { entity }, ct))[0]);
    }

    // ===== helpers =====

    /// <summary>
    /// حارس إدارة خطّة الإنتاج بنطاق المشروع (لا بالدور وحده). يُطبَّق بعد التحقّق من رؤية المشروع:
    /// إمّا دور إداري (ProjectPlanManagers = Admin/CEO/GM/Manager، بلا TeamLeader)، أو مدير حسابات
    /// هذا المشروع نفسه (Project.AccountManagerId == المستخدم الحالي). يمنع القائد/الموظّف من الكتابة،
    /// ويمنع مدير حسابات مشروع آخر من الكتابة على مشروع ليس ضمن محفظته.
    /// </summary>
    private async Task<bool> CanManagePlanAsync(Guid projectId, Guid uid, CancellationToken ct)
    {
        if (_currentUser.IsInAnyRole(Roles.ProjectPlanManagers)) return true;
        return await _db.Projects.AsNoTracking()
            .AnyAsync(p => p.Id == projectId && p.AccountManagerId == uid, ct);
    }

    /// <summary>
    /// تحقّق حقول التخطيط المشتركة بين الإنشاء والتحديث: الكمية المخطَّطة > 0، الساعات المقدَّرة >= 0،
    /// وتاريخ الاستحقاق ليس قبل تاريخ البداية. يُرجِع رسالة+رمز الخطأ أو null عند الصحّة.
    /// </summary>
    private static (string Message, string Code)? ValidatePlanningFields(int plannedQuantity, decimal? estimatedHours, DateOnly? startDate, DateOnly? dueDate)
    {
        if (plannedQuantity <= 0)
            return ("الكمية المخطَّطة يجب أن تكون أكبر من صفر.", "deliverable.quantity_invalid");
        if (estimatedHours is decimal hours && hours < 0)
            return ("الساعات المقدَّرة لا يمكن أن تكون سالبة.", "deliverable.hours_invalid");
        if (startDate is DateOnly s && dueDate is DateOnly d && d < s)
            return ("تاريخ الاستحقاق لا يمكن أن يكون قبل تاريخ البداية.", "deliverable.date_range_invalid");
        return null;
    }

    private async Task<string?> ResolveActiveNameArAsync(string domain, string code, CancellationToken ct) =>
        await _db.ExecutionTaxonomyValues.AsNoTracking()
            .Where(v => v.IsActive && v.Domain == domain && v.Code == code)
            .Select(v => v.NameAr)
            .FirstOrDefaultAsync(ct);

    private async Task<List<WorkstreamDeliverableDto>> MapManyAsync(IReadOnlyCollection<WorkstreamDeliverable> rows, CancellationToken ct)
    {
        var userIds = rows.Where(d => d.ResponsibleUserId != null).Select(d => d.ResponsibleUserId!.Value).Distinct().ToList();
        var userNames = userIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var typeCodes = rows.Select(d => d.DeliverableTypeCode).Distinct().ToList();
        var typeNames = typeCodes.Count == 0 ? new Dictionary<string, string>()
            : await _db.ExecutionTaxonomyValues.AsNoTracking()
                .Where(v => v.Domain == DeliverableTypeDomain && typeCodes.Contains(v.Code))
                .ToDictionaryAsync(v => v.Code, v => v.NameAr, ct);

        var usageCodes = rows.Where(d => d.UsageContextCode != null).Select(d => d.UsageContextCode!).Distinct().ToList();
        var usageNames = usageCodes.Count == 0 ? new Dictionary<string, string>()
            : await _db.ExecutionTaxonomyValues.AsNoTracking()
                .Where(v => v.Domain == UsageContextDomain && usageCodes.Contains(v.Code))
                .ToDictionaryAsync(v => v.Code, v => v.NameAr, ct);

        return rows.Select(d => new WorkstreamDeliverableDto(
            d.Id, d.WorkstreamId,
            d.DeliverableTypeCode, typeNames.GetValueOrDefault(d.DeliverableTypeCode),
            d.UsageContextCode, d.UsageContextCode is string uc ? usageNames.GetValueOrDefault(uc) : null,
            d.Name, d.PlannedQuantity, d.EstimatedHours,
            d.StartDate, d.DueDate, d.Priority,
            d.ResponsibleUserId, d.ResponsibleUserId is Guid rid ? userNames.GetValueOrDefault(rid) : null,
            d.Notes, d.SortOrder, d.IsActive, d.CreatedAtUtc, d.UpdatedAtUtc)).ToList();
    }
}
