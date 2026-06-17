using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Development;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.Development;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class DevelopmentService : IDevelopmentService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly IScopeResolver _scope;

    public DevelopmentService(AppDbContext db, ICurrentUser currentUser,
        INotificationService notifications, IAuditService audit, IScopeResolver scope)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
        _audit = audit;
        _scope = scope;
    }

    // ===== Training Needs =====
    public async Task<Result<TrainingNeedDto>> CreateTrainingNeedAsync(CreateTrainingNeedRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<TrainingNeedDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (request.SubjectUserId == Guid.Empty) return Result<TrainingNeedDto>.Failure("الموظف مطلوب.", "training_need.subject_required");
        if (string.IsNullOrWhiteSpace(request.Title)) return Result<TrainingNeedDto>.Failure("عنوان الاحتياج مطلوب.", "training_need.title_required");

        var subjectExists = await _db.Users.AnyAsync(u => u.Id == request.SubjectUserId, ct);
        if (!subjectExists) return Result<TrainingNeedDto>.Failure("الموظف غير موجود.", "training_need.subject_not_found");

        var need = new TrainingNeed
        {
            SubjectUserId = request.SubjectUserId,
            RaisedById = uid,
            Title = request.Title.Trim(),
            Description = request.Description,
            Source = request.Source,
            Status = TrainingNeedStatus.Open,
            RelatedKpiEvaluationId = request.RelatedKpiEvaluationId
        };
        _db.TrainingNeeds.Add(need);
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(request.SubjectUserId, "training_need.raised",
            "تم رصد احتياج تدريبي لك", null, $"/training-needs/{need.Id}", ct);
        await _audit.LogAsync(uid, "training_need.created", nameof(TrainingNeed), need.Id, ct: ct);

        return Result<TrainingNeedDto>.Success(await BuildNeedAsync(need.Id, ct));
    }

    public async Task<Result<TrainingNeedDto>> UpdateTrainingNeedAsync(Guid id, UpdateTrainingNeedRequest request, CancellationToken ct = default)
    {
        var need = await _db.TrainingNeeds.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (need is null) return Result<TrainingNeedDto>.Failure("الاحتياج التدريبي غير موجود.", "training_need.not_found");
        if (string.IsNullOrWhiteSpace(request.Title)) return Result<TrainingNeedDto>.Failure("عنوان الاحتياج مطلوب.", "training_need.title_required");

        need.Title = request.Title.Trim();
        need.Description = request.Description;
        need.Status = request.Status;
        need.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(_currentUser.UserId, "training_need.updated", nameof(TrainingNeed), need.Id, ct: ct);

        return Result<TrainingNeedDto>.Success(await BuildNeedAsync(need.Id, ct));
    }

    public async Task<Result<TrainingNeedDto>> GetTrainingNeedAsync(Guid id, CancellationToken ct = default)
    {
        var need = await _db.TrainingNeeds.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (need is null) return Result<TrainingNeedDto>.Failure("الاحتياج التدريبي غير موجود.", "training_need.not_found");
        if (!await CanViewSubjectAsync(need.SubjectUserId, ct)) return Result<TrainingNeedDto>.Failure("لا تملك صلاحية الوصول.", "auth.forbidden");
        return Result<TrainingNeedDto>.Success(await BuildNeedAsync(id, ct));
    }

    public async Task<Result<IReadOnlyList<TrainingNeedDto>>> ListTrainingNeedsAsync(TrainingNeedFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<IReadOnlyList<TrainingNeedDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);
        var q = _db.TrainingNeeds.AsNoTracking().AsQueryable();
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            q = q.Where(t => ids.Contains(t.SubjectUserId) || t.RaisedById == uid);
        }

        if (filter.SubjectUserId is not null) q = q.Where(t => t.SubjectUserId == filter.SubjectUserId);
        if (filter.Status is not null) q = q.Where(t => t.Status == filter.Status);

        var rows = await q.OrderByDescending(t => t.CreatedAtUtc).ToListAsync(ct);
        var names = await UserNamesAsync(rows.SelectMany(t => new[] { t.SubjectUserId, t.RaisedById }), ct);
        return Result<IReadOnlyList<TrainingNeedDto>>.Success(
            rows.Select(t => MapNeed(t, names.GetValueOrDefault(t.SubjectUserId), names.GetValueOrDefault(t.RaisedById))).ToList());
    }

    // ===== Improvement Plans =====
    public async Task<Result<ImprovementPlanDto>> CreateImprovementPlanAsync(CreateImprovementPlanRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<ImprovementPlanDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (request.SubjectUserId == Guid.Empty) return Result<ImprovementPlanDto>.Failure("الموظف مطلوب.", "improvement_plan.subject_required");
        if (string.IsNullOrWhiteSpace(request.Title)) return Result<ImprovementPlanDto>.Failure("عنوان الخطة مطلوب.", "improvement_plan.title_required");

        var subjectExists = await _db.Users.AnyAsync(u => u.Id == request.SubjectUserId, ct);
        if (!subjectExists) return Result<ImprovementPlanDto>.Failure("الموظف غير موجود.", "improvement_plan.subject_not_found");

        var plan = new ImprovementPlan
        {
            SubjectUserId = request.SubjectUserId,
            OwnerId = uid,
            Title = request.Title.Trim(),
            Description = request.Description,
            Status = ImprovementPlanStatus.Open,
            DueDateUtc = request.DueDateUtc,
            RelatedTrainingNeedId = request.RelatedTrainingNeedId
        };
        _db.ImprovementPlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(request.SubjectUserId, "improvement_plan.created",
            "أُنشئت لك خطة تحسين", null, $"/improvement-plans/{plan.Id}", ct);
        await _audit.LogAsync(uid, "improvement_plan.created", nameof(ImprovementPlan), plan.Id, ct: ct);

        return Result<ImprovementPlanDto>.Success(await BuildPlanAsync(plan.Id, ct));
    }

    public async Task<Result<ImprovementPlanDto>> UpdateImprovementPlanAsync(Guid id, UpdateImprovementPlanRequest request, CancellationToken ct = default)
    {
        var plan = await _db.ImprovementPlans.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (plan is null) return Result<ImprovementPlanDto>.Failure("خطة التحسين غير موجودة.", "improvement_plan.not_found");
        if (string.IsNullOrWhiteSpace(request.Title)) return Result<ImprovementPlanDto>.Failure("عنوان الخطة مطلوب.", "improvement_plan.title_required");

        plan.Title = request.Title.Trim();
        plan.Description = request.Description;
        plan.Status = request.Status;
        plan.DueDateUtc = request.DueDateUtc;
        plan.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(_currentUser.UserId, "improvement_plan.updated", nameof(ImprovementPlan), plan.Id, ct: ct);

        return Result<ImprovementPlanDto>.Success(await BuildPlanAsync(plan.Id, ct));
    }

    public async Task<Result<ImprovementPlanDto>> GetImprovementPlanAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _db.ImprovementPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (plan is null) return Result<ImprovementPlanDto>.Failure("خطة التحسين غير موجودة.", "improvement_plan.not_found");
        if (_currentUser.UserId != plan.OwnerId && !await CanViewSubjectAsync(plan.SubjectUserId, ct))
            return Result<ImprovementPlanDto>.Failure("لا تملك صلاحية الوصول.", "auth.forbidden");
        return Result<ImprovementPlanDto>.Success(await BuildPlanAsync(id, ct));
    }

    public async Task<Result<IReadOnlyList<ImprovementPlanDto>>> ListImprovementPlansAsync(ImprovementPlanFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<IReadOnlyList<ImprovementPlanDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);
        var q = _db.ImprovementPlans.AsNoTracking().AsQueryable();
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            q = q.Where(p => ids.Contains(p.SubjectUserId) || p.OwnerId == uid);
        }

        if (filter.SubjectUserId is not null) q = q.Where(p => p.SubjectUserId == filter.SubjectUserId);
        if (filter.Status is not null) q = q.Where(p => p.Status == filter.Status);

        var rows = await q.OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        var names = await UserNamesAsync(rows.SelectMany(p => new[] { p.SubjectUserId, p.OwnerId }), ct);
        return Result<IReadOnlyList<ImprovementPlanDto>>.Success(
            rows.Select(p => MapPlan(p, names.GetValueOrDefault(p.SubjectUserId), names.GetValueOrDefault(p.OwnerId))).ToList());
    }

    // ===== helpers =====
    private async Task<bool> CanViewSubjectAsync(Guid subjectId, CancellationToken ct)
    {
        if (_currentUser.UserId == subjectId) return true;
        var scope = await _scope.ResolveAsync(ct);
        return scope.Contains(subjectId);
    }

    private async Task<TrainingNeedDto> BuildNeedAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.TrainingNeeds.AsNoTracking().FirstAsync(x => x.Id == id, ct);
        var names = await UserNamesAsync(new[] { t.SubjectUserId, t.RaisedById }, ct);
        return MapNeed(t, names.GetValueOrDefault(t.SubjectUserId), names.GetValueOrDefault(t.RaisedById));
    }

    private static TrainingNeedDto MapNeed(TrainingNeed t, string? subjectName, string? raisedByName) =>
        new(t.Id, t.SubjectUserId, subjectName, t.RaisedById, raisedByName, t.Title, t.Description,
            t.Source, t.Status, t.RelatedKpiEvaluationId, t.CreatedAtUtc);

    private async Task<ImprovementPlanDto> BuildPlanAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.ImprovementPlans.AsNoTracking().FirstAsync(x => x.Id == id, ct);
        var names = await UserNamesAsync(new[] { p.SubjectUserId, p.OwnerId }, ct);
        return MapPlan(p, names.GetValueOrDefault(p.SubjectUserId), names.GetValueOrDefault(p.OwnerId));
    }

    private static ImprovementPlanDto MapPlan(ImprovementPlan p, string? subjectName, string? ownerName) =>
        new(p.Id, p.SubjectUserId, subjectName, p.OwnerId, ownerName, p.Title, p.Description,
            p.Status, p.DueDateUtc, p.RelatedTrainingNeedId, p.CreatedAtUtc);

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}
