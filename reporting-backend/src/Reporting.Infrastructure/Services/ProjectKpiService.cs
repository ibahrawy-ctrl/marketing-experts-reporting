using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Domain.Entities.Projects360;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// مؤشّرات المشروع وقراءاتها (CPW-R3 · W4 · §5 + §6 + §7).
///
/// <para>
/// **لا مؤشّر بلا هدف** (D-02): كلّ مسار إنشاء يمرّ بـ<c>objectiveId</c>، ويُفرَض حارس
/// <c>objective.ProjectId == projectId</c> فيُرجَع <c>project_kpi.objective_mismatch.conflict</c> (409)
/// عند أيّ محاولة لتعليق مؤشّر بهدف مشروع آخر — وهو تحديدًا سيناريو IDOR المتوقَّع.
/// </para>
///
/// <para>
/// **القراءة اليدويّة تُنتج تاريخًا لا قيمة عائمة** (§6): كلّ تحديث يدويّ يُنشئ صفّ
/// <c>ProjectKpiReading</c> مؤرَّخًا يحمل لقطتَي الهدف والتحقيق، ثمّ تُعاد كتابة
/// <c>CurrentValue</c>/<c>LastReadingDate</c> من **أحدث قراءة باقية** لا من القراءة المُدخَلة —
/// فتصحيح قراءة قديمة لا يقلب اللقطة السريعة إلى الماضي.
/// </para>
///
/// <para>
/// **حارس المصدر** (D-06): الكتابة اليدويّة مسموحة فقط حين <c>SourceType = Manual</c>؛ وإلّا
/// <c>project_kpi.source_not_manual.conflict</c> (409). الحارس خامل عمليًّا اليوم لأنّ كلّ المؤشّرات
/// تُنشأ يدويّة، وهو ما يجعل المرحلتين 2 و3 تبديل مزوّد لا إعادة كتابة.
/// </para>
/// </summary>
public class ProjectKpiService : IProjectKpiService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IProject360Authorization _auth;
    private readonly IAuditService _audit;
    private readonly IProjectHealthService _health;

    public ProjectKpiService(
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

    // ======================================================================
    // القراءة
    // ======================================================================

    public async Task<Result<IReadOnlyList<ProjectKpiDto>>> ListByProjectAsync(Guid projectId, bool includeInactive, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<IReadOnlyList<ProjectKpiDto>>.Failure(scope.Error!, scope.ErrorCode);

        var dtos = await BuildAsync(projectId, objectiveId: null, includeInactive, ct);
        return Result<IReadOnlyList<ProjectKpiDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<ProjectKpiDto>>> ListByObjectiveAsync(Guid projectId, Guid objectiveId, bool includeInactive, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<IReadOnlyList<ProjectKpiDto>>.Failure(scope.Error!, scope.ErrorCode);
        if (!await ObjectiveBelongsAsync(projectId, objectiveId, ct))
            return Result<IReadOnlyList<ProjectKpiDto>>.Failure("الهدف غير موجود ضمن هذا المشروع.", Project360ErrorCodes.ObjectiveNotFound);

        var dtos = await BuildAsync(projectId, objectiveId, includeInactive, ct);
        return Result<IReadOnlyList<ProjectKpiDto>>.Success(dtos);
    }

    public async Task<Result<ProjectKpiDto>> GetAsync(Guid projectId, Guid objectiveId, Guid kpiId, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<ProjectKpiDto>.Failure(scope.Error!, scope.ErrorCode);

        var dtos = await BuildAsync(projectId, objectiveId, includeInactive: true, ct);
        var dto = dtos.FirstOrDefault(k => k.Id == kpiId);
        return dto is null
            ? Result<ProjectKpiDto>.Failure("المؤشّر غير موجود ضمن هذا الهدف.", Project360ErrorCodes.KpiNotFound)
            : Result<ProjectKpiDto>.Success(dto);
    }

    // ======================================================================
    // الكتابة البنيويّة
    // ======================================================================

    public async Task<Result<ProjectKpiDto>> CreateAsync(Guid projectId, Guid objectiveId, CreateProjectKpiRequest request, CancellationToken ct = default)
    {
        var gate = await AuthorizeStructuralAsync(projectId, ct);
        if (gate.Error is not null) return Result<ProjectKpiDto>.Failure(gate.Error.Value.Message, gate.Error.Value.Code);

        if (objectiveId == Guid.Empty)
            return Result<ProjectKpiDto>.Failure("المؤشّر لا يُنشأ إلّا تحت هدف.", Project360ErrorCodes.KpiObjectiveRequired);

        var objectiveCheck = await ValidateObjectiveOwnershipAsync(projectId, objectiveId, ct);
        if (objectiveCheck is not null) return Result<ProjectKpiDto>.Failure(objectiveCheck.Value.Message, objectiveCheck.Value.Code);

        var invalid = ValidateKpiFields(request.Name, request.TargetValue, request.Weight, request.SortOrder);
        if (invalid is not null) return Result<ProjectKpiDto>.Failure(invalid.Value.Message, invalid.Value.Code);

        var entity = new ProjectKpi
        {
            ProjectId = projectId,
            ObjectiveId = objectiveId,
            Name = request.Name.Trim(),
            Description = Project360Guards.Trim(request.Description),
            Category = request.Category,
            Unit = request.Unit,
            CustomUnitLabel = Project360Guards.Trim(request.CustomUnitLabel),
            Direction = request.Direction,
            Frequency = request.Frequency,
            BaselineValue = request.BaselineValue,
            TargetValue = request.TargetValue,
            Weight = request.Weight,
            SourceType = ProjectKpiSourceType.Manual, // المرحلة 1 حصرًا (D-06)
            Notes = Project360Guards.Trim(request.Notes),
            SortOrder = request.SortOrder,
        };
        _db.ProjectKpis.Add(entity);
        // وزن المؤشّر وهدفه واتّجاهه مدخلات فعليّة للصحّة ⟹ إعادة احتساب في نفس المعاملة.
        await _health.SaveWithHealthAsync(projectId, ct);
        await _audit.LogAsync(gate.ActorId, "project_kpi.created", nameof(ProjectKpi), entity.Id, ct: ct);

        return await ReloadAsync(projectId, objectiveId, entity.Id, ct);
    }

    public async Task<Result<ProjectKpiDto>> UpdateAsync(Guid projectId, Guid objectiveId, Guid kpiId, UpdateProjectKpiRequest request, CancellationToken ct = default)
    {
        var gate = await AuthorizeStructuralAsync(projectId, ct);
        if (gate.Error is not null) return Result<ProjectKpiDto>.Failure(gate.Error.Value.Message, gate.Error.Value.Code);

        var entity = await FindAsync(projectId, objectiveId, kpiId, ct);
        if (entity.Error is not null) return Result<ProjectKpiDto>.Failure(entity.Error.Value.Message, entity.Error.Value.Code);

        var invalid = ValidateKpiFields(request.Name, request.TargetValue, request.Weight, request.SortOrder);
        if (invalid is not null) return Result<ProjectKpiDto>.Failure(invalid.Value.Message, invalid.Value.Code);

        var kpi = entity.Kpi!;
        kpi.Name = request.Name.Trim();
        kpi.Description = Project360Guards.Trim(request.Description);
        kpi.Category = request.Category;
        kpi.Unit = request.Unit;
        kpi.CustomUnitLabel = Project360Guards.Trim(request.CustomUnitLabel);
        kpi.Direction = request.Direction;
        kpi.Frequency = request.Frequency;
        kpi.BaselineValue = request.BaselineValue;
        kpi.TargetValue = request.TargetValue;
        kpi.Weight = request.Weight;
        kpi.Notes = Project360Guards.Trim(request.Notes);
        kpi.SortOrder = request.SortOrder;
        kpi.UpdatedAtUtc = DateTime.UtcNow;

        // Target/Weight/Direction كلّها مدخلات مباشرة في ComputeAchievement والتجميع الموزون.
        await _health.SaveWithHealthAsync(projectId, ct);
        await _audit.LogAsync(gate.ActorId, "project_kpi.updated", nameof(ProjectKpi), kpi.Id, ct: ct);

        return await ReloadAsync(projectId, objectiveId, kpi.Id, ct);
    }

    public async Task<Result<ProjectKpiDto>> SetActiveAsync(Guid projectId, Guid objectiveId, Guid kpiId, bool isActive, CancellationToken ct = default)
    {
        var gate = await AuthorizeStructuralAsync(projectId, ct);
        if (gate.Error is not null) return Result<ProjectKpiDto>.Failure(gate.Error.Value.Message, gate.Error.Value.Code);

        var found = await FindAsync(projectId, objectiveId, kpiId, ct);
        if (found.Error is not null) return Result<ProjectKpiDto>.Failure(found.Error.Value.Message, found.Error.Value.Code);

        found.Kpi!.IsActive = isActive;
        found.Kpi.UpdatedAtUtc = DateTime.UtcNow;
        // التفعيل/التعطيل يُدخِل المؤشّر في التجميع أو يُخرجه ⟹ مدخل فعليّ للصحّة.
        await _health.SaveWithHealthAsync(projectId, ct);
        await _audit.LogAsync(gate.ActorId, isActive ? "project_kpi.activated" : "project_kpi.deactivated", nameof(ProjectKpi), kpiId, ct: ct);

        return await ReloadAsync(projectId, objectiveId, kpiId, ct);
    }

    // ======================================================================
    // §6 — القراءات اليدويّة
    // ======================================================================

    public async Task<Result<IReadOnlyList<ProjectKpiReadingDto>>> ListReadingsAsync(Guid projectId, Guid objectiveId, Guid kpiId, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<IReadOnlyList<ProjectKpiReadingDto>>.Failure(scope.Error!, scope.ErrorCode);

        var found = await FindAsync(projectId, objectiveId, kpiId, ct, tracking: false);
        if (found.Error is not null) return Result<IReadOnlyList<ProjectKpiReadingDto>>.Failure(found.Error.Value.Message, found.Error.Value.Code);

        var rows = await _db.ProjectKpiReadings.AsNoTracking()
            .Where(r => r.ProjectKpiId == kpiId)
            .OrderByDescending(r => r.ReadingDate)
            .ToListAsync(ct);

        var recorderIds = rows.Select(r => r.RecordedByUserId).Distinct().ToList();
        var recorderNames = recorderIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking().Where(u => recorderIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var dtos = rows.Select(r => new ProjectKpiReadingDto(
            r.Id, r.ProjectKpiId, r.ReadingDate, r.Value, r.TargetSnapshot, r.AchievementSnapshot,
            r.RecordedByUserId, recorderNames.GetValueOrDefault(r.RecordedByUserId), r.Notes, r.CreatedAtUtc)).ToList();

        return Result<IReadOnlyList<ProjectKpiReadingDto>>.Success(dtos);
    }

    public async Task<Result<ProjectKpiReadingDto>> AddReadingAsync(Guid projectId, Guid objectiveId, Guid kpiId, CreateProjectKpiReadingRequest request, CancellationToken ct = default)
    {
        var gate = await AuthorizeProgressAsync(projectId, ct);
        if (gate.Error is not null) return Result<ProjectKpiReadingDto>.Failure(gate.Error.Value.Message, gate.Error.Value.Code);

        var found = await FindAsync(projectId, objectiveId, kpiId, ct);
        if (found.Error is not null) return Result<ProjectKpiReadingDto>.Failure(found.Error.Value.Message, found.Error.Value.Code);
        var kpi = found.Kpi!;

        if (kpi.SourceType != ProjectKpiSourceType.Manual)
            return Result<ProjectKpiReadingDto>.Failure("لا يمكن إدخال قراءة يدويّة لمؤشّر مصدره غير يدويّ.", Project360ErrorCodes.KpiSourceNotManual);

        if (await _db.ProjectKpiReadings.AnyAsync(r => r.ProjectKpiId == kpiId && r.ReadingDate == request.ReadingDate, ct))
            return Result<ProjectKpiReadingDto>.Failure("توجد قراءة لهذا المؤشّر بنفس التاريخ. صحّح القراءة القائمة بدل إضافة ثانية.", Project360ErrorCodes.ReadingDuplicateDate);

        var reading = new ProjectKpiReading
        {
            ProjectKpiId = kpiId,
            ReadingDate = request.ReadingDate,
            Value = request.Value,
            TargetSnapshot = kpi.TargetValue,
            AchievementSnapshot = ProjectHealthPolicy.ComputeAchievement(request.Value, kpi.TargetValue, kpi.Direction),
            RecordedByUserId = gate.ActorId,
            Notes = Project360Guards.Trim(request.Notes),
        };
        _db.ProjectKpiReadings.Add(reading);
        // نقطة حفظ واحدة: القراءة + اللقطة السريعة + الصحّة في معاملة واحدة.
        await RefreshSnapshotAsync(kpi, ct);
        await _audit.LogAsync(gate.ActorId, "project_kpi_reading.created", nameof(ProjectKpiReading), reading.Id, ct: ct);

        return Result<ProjectKpiReadingDto>.Success(await MapReadingAsync(reading, ct));
    }

    public async Task<Result<ProjectKpiReadingDto>> UpdateReadingAsync(Guid projectId, Guid objectiveId, Guid kpiId, Guid readingId, UpdateProjectKpiReadingRequest request, CancellationToken ct = default)
    {
        var gate = await AuthorizeProgressAsync(projectId, ct);
        if (gate.Error is not null) return Result<ProjectKpiReadingDto>.Failure(gate.Error.Value.Message, gate.Error.Value.Code);

        var found = await FindAsync(projectId, objectiveId, kpiId, ct);
        if (found.Error is not null) return Result<ProjectKpiReadingDto>.Failure(found.Error.Value.Message, found.Error.Value.Code);
        var kpi = found.Kpi!;

        if (kpi.SourceType != ProjectKpiSourceType.Manual)
            return Result<ProjectKpiReadingDto>.Failure("لا يمكن تعديل قراءة يدويّة لمؤشّر مصدره غير يدويّ.", Project360ErrorCodes.KpiSourceNotManual);

        var reading = await _db.ProjectKpiReadings.FirstOrDefaultAsync(r => r.Id == readingId && r.ProjectKpiId == kpiId, ct);
        if (reading is null) return Result<ProjectKpiReadingDto>.Failure("القراءة غير موجودة لهذا المؤشّر.", Project360ErrorCodes.ReadingNotFound);

        // تاريخ القراءة لا يتغيّر — التغيير يعني قراءة أخرى، وهو ما يحرسه الفهرس الفريد أصلًا.
        reading.Value = request.Value;
        reading.TargetSnapshot = kpi.TargetValue;
        reading.AchievementSnapshot = ProjectHealthPolicy.ComputeAchievement(request.Value, kpi.TargetValue, kpi.Direction);
        reading.Notes = Project360Guards.Trim(request.Notes);
        reading.UpdatedAtUtc = DateTime.UtcNow;
        // نقطة حفظ واحدة: التصحيح + اللقطة السريعة + الصحّة في معاملة واحدة.
        await RefreshSnapshotAsync(kpi, ct);
        await _audit.LogAsync(gate.ActorId, "project_kpi_reading.updated", nameof(ProjectKpiReading), reading.Id, ct: ct);

        return Result<ProjectKpiReadingDto>.Success(await MapReadingAsync(reading, ct));
    }

    // ======================================================================
    // helpers
    // ======================================================================

    private async Task<(Guid ActorId, (string Message, string Code)? Error)> AuthorizeStructuralAsync(Guid projectId, CancellationToken ct)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return (Guid.Empty, (scope.Error!, scope.ErrorCode!));
        if (!await _auth.CanManageProject360Async(scope.Value!, ct))
            return (Guid.Empty, ("لا تملك صلاحية إدارة مؤشّرات هذا المشروع.", "auth.forbidden"));
        if (_currentUser.UserId is not Guid uid) return (Guid.Empty, ("غير مصرّح.", "auth.unauthenticated"));
        return (uid, null);
    }

    private async Task<(Guid ActorId, (string Message, string Code)? Error)> AuthorizeProgressAsync(Guid projectId, CancellationToken ct)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return (Guid.Empty, (scope.Error!, scope.ErrorCode!));
        if (!await _auth.CanUpdateProject360ProgressAsync(scope.Value!, ct))
            return (Guid.Empty, ("لا تملك صلاحية تحديث قراءات هذا المشروع.", "auth.forbidden"));
        if (_currentUser.UserId is not Guid uid) return (Guid.Empty, ("غير مصرّح.", "auth.unauthenticated"));
        return (uid, null);
    }

    private Task<bool> ObjectiveBelongsAsync(Guid projectId, Guid objectiveId, CancellationToken ct) =>
        _db.ProjectObjectives.AsNoTracking().AnyAsync(o => o.Id == objectiveId && o.ProjectId == projectId, ct);

    /// <summary>
    /// حارس ملكيّة الهدف: غير موجود إطلاقًا ⟹ <c>project_objective.not_found</c>؛
    /// موجود لكن لمشروع آخر ⟹ <c>project_kpi.objective_mismatch.conflict</c> (409).
    /// التمييز مقصود: الأوّل خطأ إدخال، والثاني محاولة عبور حدود مشروع.
    /// </summary>
    private async Task<(string Message, string Code)?> ValidateObjectiveOwnershipAsync(Guid projectId, Guid objectiveId, CancellationToken ct)
    {
        var owner = await _db.ProjectObjectives.AsNoTracking()
            .Where(o => o.Id == objectiveId)
            .Select(o => (Guid?)o.ProjectId)
            .FirstOrDefaultAsync(ct);

        if (owner is null) return ("الهدف غير موجود.", Project360ErrorCodes.ObjectiveNotFound);
        if (owner.Value != projectId) return ("الهدف يتبع مشروعًا آخر.", Project360ErrorCodes.KpiObjectiveMismatch);
        return null;
    }

    private async Task<(ProjectKpi? Kpi, (string Message, string Code)? Error)> FindAsync(
        Guid projectId, Guid objectiveId, Guid kpiId, CancellationToken ct, bool tracking = true)
    {
        var query = tracking ? _db.ProjectKpis : _db.ProjectKpis.AsNoTracking();
        var kpi = await query.FirstOrDefaultAsync(k => k.Id == kpiId, ct);
        if (kpi is null) return (null, ("المؤشّر غير موجود.", Project360ErrorCodes.KpiNotFound));
        if (kpi.ProjectId != projectId) return (null, ("المؤشّر يتبع مشروعًا آخر.", Project360ErrorCodes.KpiObjectiveMismatch));
        if (kpi.ObjectiveId != objectiveId) return (null, ("المؤشّر يتبع هدفًا آخر.", Project360ErrorCodes.KpiObjectiveMismatch));
        return (kpi, null);
    }

    private static (string Message, string Code)? ValidateKpiFields(string? name, decimal targetValue, decimal weight, int sortOrder)
    {
        var basic = Project360Guards.FirstError(
            Project360Guards.ValidateName(name),
            Project360Guards.ValidateWeight(weight),
            Project360Guards.ValidateSortOrder(sortOrder));
        if (basic is not null) return basic;

        // هدف ≤ 0 يجعل معادلة التحقيق غير معرَّفة (قسمة على صفر أو نسبة سالبة) ⟹ يُرفَض عند المصدر.
        return targetValue <= 0m
            ? ("القيمة المستهدَفة يجب أن تكون أكبر من صفر.", Project360ErrorCodes.KpiTargetInvalid)
            : null;
    }

    /// <summary>
    /// يعيد كتابة اللقطة السريعة من **أحدث قراءة باقية** — لا من القراءة المُدخَلة.
    /// الفارق يظهر عند تصحيح قراءة قديمة: اللقطة تبقى على الأحدث ولا ترتدّ إلى الوراء.
    ///
    /// <para>
    /// **نقطة الحفظ الوحيدة لمسار القراءات**: معاملة واحدة تغطّي تثبيت القراءة المعلّقة ثمّ إعادة
    /// كتابة اللقطة ثمّ إعادة احتساب الصحّة. فشل أيّ خطوة يُرجِع الثلاث معًا، فلا تبقى قراءة
    /// محفوظة بلقطة قديمة ولا لقطة جديدة بصحّة بائتة.
    /// </para>
    /// </summary>
    private async Task RefreshSnapshotAsync(ProjectKpi kpi, CancellationToken ct)
    {
        var owned = _db.Database.CurrentTransaction is null;
        var tx = owned ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            await RefreshSnapshotCoreAsync(kpi, ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    private async Task RefreshSnapshotCoreAsync(ProjectKpi kpi, CancellationToken ct)
    {
        // تثبيت القراءة المعلّقة أوّلًا كي يراها استعلام «أحدث قراءة باقية».
        await _db.SaveChangesAsync(ct);

        var latest = await _db.ProjectKpiReadings.AsNoTracking()
            .Where(r => r.ProjectKpiId == kpi.Id)
            .OrderByDescending(r => r.ReadingDate)
            .Select(r => new { r.ReadingDate, r.Value })
            .FirstOrDefaultAsync(ct);

        kpi.CurrentValue = latest?.Value;
        kpi.LastReadingDate = latest?.ReadingDate;
        kpi.UpdatedAtUtc = DateTime.UtcNow;
        // `CurrentValue` هو مدخل ComputeAchievement مباشرةً ⟹ كلّ قراءة تغيّر الصحّة.
        // نقطة الحفظ الوحيدة لمسار القراءات، فلا تتعدّد استدعاءات الحفظ.
        await _health.SaveWithHealthAsync(kpi.ProjectId, ct);
    }

    private async Task<ProjectKpiReadingDto> MapReadingAsync(ProjectKpiReading r, CancellationToken ct)
    {
        var name = await _db.Users.AsNoTracking()
            .Where(u => u.Id == r.RecordedByUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return new ProjectKpiReadingDto(
            r.Id, r.ProjectKpiId, r.ReadingDate, r.Value, r.TargetSnapshot, r.AchievementSnapshot,
            r.RecordedByUserId, name, r.Notes, r.CreatedAtUtc);
    }

    private async Task<Result<ProjectKpiDto>> ReloadAsync(Guid projectId, Guid objectiveId, Guid kpiId, CancellationToken ct)
    {
        var dtos = await BuildAsync(projectId, objectiveId, includeInactive: true, ct);
        return Result<ProjectKpiDto>.Success(dtos.First(k => k.Id == kpiId));
    }

    /// <summary>
    /// يبني المؤشّرات بتحقيقها واتّجاهها — **ثلاثة استعلامات ثابتة** (مؤشّرات · قراءات · ملّاك الأهداف)
    /// مهما بلغ عددها. الاتّجاه يحتاج آخر قراءتين، وتُجلَبان ضمن استعلام القراءات الواحد لا بواحد لكلّ مؤشّر.
    /// </summary>
    internal async Task<List<ProjectKpiDto>> BuildAsync(Guid projectId, Guid? objectiveId, bool includeInactive, CancellationToken ct)
    {
        var query = _db.ProjectKpis.AsNoTracking().Where(k => k.ProjectId == projectId);
        if (objectiveId is Guid oid) query = query.Where(k => k.ObjectiveId == oid);
        if (!includeInactive) query = query.Where(k => k.IsActive);

        var kpis = await query.OrderBy(k => k.SortOrder).ThenBy(k => k.Name).ToListAsync(ct);
        if (kpis.Count == 0) return new List<ProjectKpiDto>();

        var kpiIds = kpis.Select(k => k.Id).ToList();

        var readings = await _db.ProjectKpiReadings.AsNoTracking()
            .Where(r => kpiIds.Contains(r.ProjectKpiId))
            .OrderByDescending(r => r.ReadingDate)
            .Select(r => new { r.ProjectKpiId, r.ReadingDate, r.Value })
            .ToListAsync(ct);
        var previousByKpi = readings
            .GroupBy(r => r.ProjectKpiId)
            .ToDictionary(g => g.Key, g => g.Skip(1).Select(r => (decimal?)r.Value).FirstOrDefault());

        var objectiveIds = kpis.Select(k => k.ObjectiveId).Distinct().ToList();
        var objectiveOwners = await _db.ProjectObjectives.AsNoTracking()
            .Where(o => objectiveIds.Contains(o.Id))
            .Select(o => new { o.Id, o.OwnerUserId })
            .ToDictionaryAsync(o => o.Id, o => o.OwnerUserId, ct);

        return kpis.Select(k =>
        {
            var model = ProjectHealthPolicy.ComputeKpi(
                k.CurrentValue, k.TargetValue, k.Direction, previousByKpi.GetValueOrDefault(k.Id));

            return new ProjectKpiDto(
                k.Id, k.ProjectId, k.ObjectiveId, k.Name, k.Description, k.Category, k.Unit, k.CustomUnitLabel,
                k.Direction, k.Frequency, k.BaselineValue, k.TargetValue, k.CurrentValue, k.LastReadingDate,
                k.Weight, k.SourceType, k.ExternalSourceKey, k.ExternalMetricCode, k.LastSyncedAtUtc,
                k.Notes, k.SortOrder, k.IsActive,
                model.Achievement, model.Variance, model.Trend,
                objectiveOwners.GetValueOrDefault(k.ObjectiveId),
                k.CreatedAtUtc, k.UpdatedAtUtc);
        }).ToList();
    }
}
