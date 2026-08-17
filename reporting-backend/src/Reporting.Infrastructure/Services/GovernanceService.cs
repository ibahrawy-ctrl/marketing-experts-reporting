using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Application.Notifications;
using Reporting.Application.Projects360;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class GovernanceService : IGovernanceService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;

    /// <summary>
    /// **الحوكمة تُغيّر صحّة المشروع فعلًا** (P360-WF-R2 §7): خطر حرج مفتوح يعني «معطَّل»،
    /// وإغلاقه يرفع التعطيل. بلا هذه الوصلة كانت المخاطرة تُسجَّل وتُغلَق والعمود المخزَّن
    /// لا يتحرّك حتّى يمرّ حدث مخرَجات لا علاقة له بها.
    /// </summary>
    private readonly IProjectHealthService _health;

    public GovernanceService(AppDbContext db, ICurrentUser currentUser,
        INotificationService notifications, IAuditService audit, IProjectHealthService health)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
        _audit = audit;
        _health = health;
    }

    // ===== Risks =====
    public async Task<Result<RiskDto>> CreateRiskAsync(CreateRiskRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<RiskDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Title)) return Result<RiskDto>.Failure("عنوان المخاطرة مطلوب.", "risk.title_required");
        // نفس حارس القرار حرفيًّا: ربطٌ بمشروع غير موجود يخلق مخاطرة يتيمة لا تظهر في أيّ لوحة.
        if (request.ProjectId is Guid newPid && !await _db.Projects.AnyAsync(p => p.Id == newPid, ct))
            return Result<RiskDto>.Failure("المشروع المرتبط غير موجود.", "risk.project.not_found");

        var risk = new Risk
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            Severity = request.Severity,
            Status = RiskStatus.Open,
            OwnerId = request.OwnerId ?? uid,
            DepartmentId = request.DepartmentId,
            MitigationPlan = request.MitigationPlan,
            RelatedSubmissionId = request.RelatedSubmissionId,
            RelatedKpiEvaluationId = request.RelatedKpiEvaluationId,
            SubjectUserId = request.SubjectUserId,
            TeamId = request.TeamId,
            NextAction = request.NextAction,
            ClientId = request.ClientId,
            ProjectId = request.ProjectId
        };
        _db.Risks.Add(risk);
        await SaveRiskAsync(risk, ct);
        await _audit.LogAsync(uid, "risk.created", nameof(Risk), risk.Id, ct: ct);

        return Result<RiskDto>.Success(await BuildRiskAsync(risk.Id, ct));
    }

    public async Task<Result<RiskDto>> UpdateRiskAsync(Guid id, UpdateRiskRequest request, CancellationToken ct = default)
    {
        var risk = await _db.Risks.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (risk is null) return Result<RiskDto>.Failure("المخاطرة غير موجودة.", "risk.not_found");
        if (string.IsNullOrWhiteSpace(request.Title)) return Result<RiskDto>.Failure("عنوان المخاطرة مطلوب.", "risk.title_required");

        risk.Title = request.Title.Trim();
        risk.Description = request.Description;
        risk.Severity = request.Severity;
        risk.MitigationPlan = request.MitigationPlan;
        risk.NextAction = request.NextAction;
        if (request.Status != risk.Status)
        {
            risk.Status = request.Status;
            risk.ClosedAtUtc = request.Status == RiskStatus.Closed ? DateTime.UtcNow : null;
        }
        risk.UpdatedAtUtc = DateTime.UtcNow;
        // تغيّر الشدّة أو الحالة يقلب حالة المشروع الظاهرة (معطَّل ⇄ سليم) ⟹ إعادة احتساب.
        await SaveRiskAsync(risk, ct);
        await _audit.LogAsync(_currentUser.UserId, "risk.updated", nameof(Risk), risk.Id, ct: ct);

        return Result<RiskDto>.Success(await BuildRiskAsync(risk.Id, ct));
    }

    /// <summary>
    /// يحفظ المخاطرة، وإن كانت مربوطة بمشروع أعاد احتساب صحّته في **نفس** وحدة العمل.
    /// المخاطرة غير المربوطة تُحفَظ كما كانت بلا أيّ كلفة إضافيّة.
    /// </summary>
    private async Task SaveRiskAsync(Risk risk, CancellationToken ct)
    {
        if (risk.ProjectId is Guid pid) await _health.SaveWithHealthAsync(pid, ct);
        else await _db.SaveChangesAsync(ct);
    }

    public async Task<Result<RiskDto>> GetRiskAsync(Guid id, CancellationToken ct = default)
    {
        if (!CanView()) return Result<RiskDto>.Failure("لا تملك صلاحية الوصول.", "auth.forbidden");
        var exists = await _db.Risks.AnyAsync(r => r.Id == id, ct);
        if (!exists) return Result<RiskDto>.Failure("المخاطرة غير موجودة.", "risk.not_found");
        return Result<RiskDto>.Success(await BuildRiskAsync(id, ct));
    }

    public async Task<Result<IReadOnlyList<RiskDto>>> ListRisksAsync(RiskFilter filter, CancellationToken ct = default)
    {
        if (!CanView()) return Result<IReadOnlyList<RiskDto>>.Failure("لا تملك صلاحية الوصول.", "auth.forbidden");

        var q = _db.Risks.AsNoTracking().AsQueryable();
        if (filter.Status is not null) q = q.Where(r => r.Status == filter.Status);
        if (filter.Severity is not null) q = q.Where(r => r.Severity == filter.Severity);
        if (filter.DepartmentId is not null) q = q.Where(r => r.DepartmentId == filter.DepartmentId);
        if (filter.OwnerId is not null) q = q.Where(r => r.OwnerId == filter.OwnerId);

        var rows = await q.OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct);
        var names = await UserNamesAsync(rows.Select(r => r.OwnerId), ct);
        return Result<IReadOnlyList<RiskDto>>.Success(rows.Select(r => MapRisk(r, names.GetValueOrDefault(r.OwnerId))).ToList());
    }

    // ===== Escalations =====
    public async Task<Result<EscalationDto>> CreateEscalationAsync(CreateEscalationRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<EscalationDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (request.TargetUserId == Guid.Empty) return Result<EscalationDto>.Failure("الجهة المُصعَّد إليها مطلوبة.", "escalation.target_required");
        if (string.IsNullOrWhiteSpace(request.Reason)) return Result<EscalationDto>.Failure("سبب التصعيد مطلوب.", "escalation.reason_required");

        var targetExists = await _db.Users.AnyAsync(u => u.Id == request.TargetUserId, ct);
        if (!targetExists) return Result<EscalationDto>.Failure("الجهة المُصعَّد إليها غير موجودة.", "escalation.target_not_found");

        var escalation = new Escalation
        {
            RaisedById = uid,
            TargetUserId = request.TargetUserId,
            Reason = request.Reason.Trim(),
            Status = EscalationStatus.Open,
            ReportSubmissionId = request.ReportSubmissionId,
            RiskId = request.RiskId,
            KpiEvaluationId = request.KpiEvaluationId
        };
        _db.Escalations.Add(escalation);
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(request.TargetUserId, "escalation.raised",
            "تصعيد جديد بانتظار معالجتك", null, "/app/governance/escalations", ct);
        await _audit.LogAsync(uid, "escalation.raised", nameof(Escalation), escalation.Id, ct: ct);

        return Result<EscalationDto>.Success(await BuildEscalationAsync(escalation.Id, ct));
    }

    public async Task<Result<EscalationDto>> ResolveEscalationAsync(Guid id, ResolveEscalationRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<EscalationDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var e = await _db.Escalations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<EscalationDto>.Failure("التصعيد غير موجود.", "escalation.not_found");

        // المُصعَّد إليه أو إداري فقط يُعالج التصعيد.
        if (uid != e.TargetUserId && !_currentUser.IsInAnyRole(Roles.Management))
            return Result<EscalationDto>.Failure("لا تملك صلاحية معالجة هذا التصعيد.", "auth.forbidden");

        // الرفض يتطلب سببًا إلزاميًا؛ والإغلاق (الحل) يتطلب تعليقًا إلزاميًا.
        if (request.Status == EscalationStatus.Dismissed && string.IsNullOrWhiteSpace(request.Resolution))
            return Result<EscalationDto>.Failure("رفض التصعيد يتطلب ذكر السبب.", "escalation.reason_required");
        if (request.Status == EscalationStatus.Resolved && string.IsNullOrWhiteSpace(request.Resolution))
            return Result<EscalationDto>.Failure("إغلاق التصعيد يتطلب تعليقًا.", "escalation.comment_required");

        // اتجاه التصعيد: «نازل» إذا كان مُطلِقه أعلى إداريًا من المُستهدَف (وفق سلسلة المديرين).
        // المُستهدَف بتصعيد نازل لا يملك «رفض» سلطة التصعيد — يستلم ويُعالج أو يطلب توضيحًا أو يرفعه لأعلى فقط.
        if (request.Status == EscalationStatus.Dismissed && uid == e.TargetUserId
            && await IsDownwardAsync(e.RaisedById, e.TargetUserId, ct))
            return Result<EscalationDto>.Failure(
                "لا يمكنك رفض تصعيد وارد من مستوى إداري أعلى. يمكنك استلامه ومعالجته أو طلب توضيح أو رفعه لأعلى.",
                "escalation.cannot_dismiss_downward");

        e.Status = request.Status;
        e.Resolution = request.Resolution;
        e.NextAction = request.NextAction;
        if (request.Status is EscalationStatus.Resolved or EscalationStatus.Dismissed)
            e.ResolvedAtUtc = DateTime.UtcNow;
        e.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(e.RaisedById, "escalation.updated",
            "تم تحديث حالة تصعيدك", null, "/app/governance/escalations", ct);
        await _audit.LogAsync(uid, "escalation." + request.Status.ToString().ToLowerInvariant(), nameof(Escalation), e.Id, ct: ct);

        return Result<EscalationDto>.Success(await BuildEscalationAsync(e.Id, ct));
    }

    public async Task<Result<EscalationDto>> GetEscalationAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.Escalations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<EscalationDto>.Failure("التصعيد غير موجود.", "escalation.not_found");
        if (!CanViewEscalation(e)) return Result<EscalationDto>.Failure("لا تملك صلاحية الوصول.", "auth.forbidden");
        return Result<EscalationDto>.Success(await BuildEscalationAsync(id, ct));
    }

    public async Task<Result<IReadOnlyList<EscalationDto>>> ListEscalationsAsync(EscalationFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<IReadOnlyList<EscalationDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var q = _db.Escalations.AsNoTracking().AsQueryable();
        if (!RoleAccess.CanViewGovernance(_currentUser.Roles))
            q = q.Where(e => e.RaisedById == uid || e.TargetUserId == uid);

        if (filter.Status is not null) q = q.Where(e => e.Status == filter.Status);
        if (filter.TargetUserId is not null) q = q.Where(e => e.TargetUserId == filter.TargetUserId);
        if (filter.RaisedById is not null) q = q.Where(e => e.RaisedById == filter.RaisedById);

        var rows = await q.OrderByDescending(e => e.CreatedAtUtc).ToListAsync(ct);
        var names = await UserNamesAsync(rows.SelectMany(e => new[] { e.RaisedById, e.TargetUserId }), ct);
        return Result<IReadOnlyList<EscalationDto>>.Success(
            rows.Select(e => MapEscalation(e, names.GetValueOrDefault(e.RaisedById), names.GetValueOrDefault(e.TargetUserId))).ToList());
    }

    // ===== Decisions =====
    public async Task<Result<DecisionDto>> CreateDecisionAsync(CreateDecisionRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid) return Result<DecisionDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Title)) return Result<DecisionDto>.Failure("عنوان القرار مطلوب.", "decision.title_required");
        if (request.ProjectId is Guid pid && !await _db.Projects.AnyAsync(p => p.Id == pid, ct))
            return Result<DecisionDto>.Failure("المشروع المرتبط غير موجود.", "decision.project.not_found");

        var decision = new Decision
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            MadeById = uid,
            Status = DecisionStatus.Proposed,
            RelatedSubmissionId = request.RelatedSubmissionId,
            RelatedRiskId = request.RelatedRiskId,
            RelatedEscalationId = request.RelatedEscalationId,
            RelatedKpiEvaluationId = request.RelatedKpiEvaluationId,
            ProjectId = request.ProjectId
        };
        _db.Decisions.Add(decision);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "decision.created", nameof(Decision), decision.Id, ct: ct);

        return Result<DecisionDto>.Success(await BuildDecisionAsync(decision.Id, ct));
    }

    public async Task<Result<DecisionDto>> UpdateDecisionAsync(Guid id, UpdateDecisionRequest request, CancellationToken ct = default)
    {
        var d = await _db.Decisions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return Result<DecisionDto>.Failure("القرار غير موجود.", "decision.not_found");
        if (string.IsNullOrWhiteSpace(request.Title)) return Result<DecisionDto>.Failure("عنوان القرار مطلوب.", "decision.title_required");

        d.Title = request.Title.Trim();
        d.Description = request.Description;
        d.NextAction = request.NextAction;
        if (request.Status != d.Status)
        {
            d.Status = request.Status;
            if (request.Status is DecisionStatus.Approved or DecisionStatus.Rejected or DecisionStatus.Implemented)
                d.DecidedAtUtc = DateTime.UtcNow;
        }
        d.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(_currentUser.UserId, "decision.updated", nameof(Decision), d.Id, ct: ct);

        return Result<DecisionDto>.Success(await BuildDecisionAsync(d.Id, ct));
    }

    public async Task<Result<DecisionDto>> GetDecisionAsync(Guid id, CancellationToken ct = default)
    {
        if (!CanView()) return Result<DecisionDto>.Failure("لا تملك صلاحية الوصول.", "auth.forbidden");
        var exists = await _db.Decisions.AnyAsync(d => d.Id == id, ct);
        if (!exists) return Result<DecisionDto>.Failure("القرار غير موجود.", "decision.not_found");
        return Result<DecisionDto>.Success(await BuildDecisionAsync(id, ct));
    }

    public async Task<Result<IReadOnlyList<DecisionDto>>> ListDecisionsAsync(DecisionFilter filter, CancellationToken ct = default)
    {
        if (!CanView()) return Result<IReadOnlyList<DecisionDto>>.Failure("لا تملك صلاحية الوصول.", "auth.forbidden");

        var q = _db.Decisions.AsNoTracking().AsQueryable();
        if (filter.Status is not null) q = q.Where(d => d.Status == filter.Status);

        var rows = await q.OrderByDescending(d => d.CreatedAtUtc).ToListAsync(ct);
        var names = await UserNamesAsync(rows.Select(d => d.MadeById), ct);
        return Result<IReadOnlyList<DecisionDto>>.Success(rows.Select(d => MapDecision(d, names.GetValueOrDefault(d.MadeById))).ToList());
    }

    // ===== helpers =====
    // المخاطر والقرارات بيانات حوكمة مؤسسية → مقصورة على أصحاب صلاحية الحوكمة.
    private bool CanView() => RoleAccess.CanViewGovernance(_currentUser.Roles);

    private bool CanViewEscalation(Escalation e)
    {
        if (_currentUser.UserId is not Guid uid) return false;
        if (uid == e.RaisedById || uid == e.TargetUserId) return true;
        return RoleAccess.CanViewGovernance(_currentUser.Roles);
    }

    // تصعيد «نازل»: مُطلِقه أعلى إداريًا من المُستهدَف. نصعد سلسلة المديرين من المُستهدَف؛
    // فإن وصلنا إلى المُطلِق فهو أحد رؤسائه ⇒ التصعيد نازل (سلطة أعلى) ولا يجوز للمُستهدَف رفضه.
    private async Task<bool> IsDownwardAsync(Guid raisedById, Guid targetUserId, CancellationToken ct)
    {
        if (raisedById == targetUserId) return false;
        var managerById = await _db.Users
            .Where(u => u.ManagerId != null)
            .Select(u => new { u.Id, ManagerId = u.ManagerId!.Value })
            .ToDictionaryAsync(u => u.Id, u => u.ManagerId, ct);

        var current = targetUserId;
        var guard = 0;
        while (managerById.TryGetValue(current, out var managerId) && guard++ < 64)
        {
            if (managerId == raisedById) return true;
            current = managerId;
        }
        return false;
    }

    private async Task<RiskDto> BuildRiskAsync(Guid id, CancellationToken ct)
    {
        var r = await _db.Risks.AsNoTracking().FirstAsync(x => x.Id == id, ct);
        var names = await UserNamesAsync(new[] { r.OwnerId }, ct);
        return MapRisk(r, names.GetValueOrDefault(r.OwnerId));
    }

    private static RiskDto MapRisk(Risk r, string? ownerName) =>
        new(r.Id, r.Title, r.Description, r.Severity, r.Status, r.OwnerId, ownerName,
            r.DepartmentId, r.MitigationPlan, r.ClosedAtUtc, r.CreatedAtUtc,
            r.RelatedSubmissionId, r.RelatedKpiEvaluationId, r.SubjectUserId, r.TeamId, r.NextAction,
            r.ClientId, r.ProjectId);

    private async Task<EscalationDto> BuildEscalationAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.Escalations.AsNoTracking().FirstAsync(x => x.Id == id, ct);
        var names = await UserNamesAsync(new[] { e.RaisedById, e.TargetUserId }, ct);
        return MapEscalation(e, names.GetValueOrDefault(e.RaisedById), names.GetValueOrDefault(e.TargetUserId));
    }

    private static EscalationDto MapEscalation(Escalation e, string? raisedByName, string? targetName) =>
        new(e.Id, e.RaisedById, raisedByName, e.TargetUserId, targetName, e.Reason, e.Status,
            e.ReportSubmissionId, e.RiskId, e.ResolvedAtUtc, e.Resolution, e.CreatedAtUtc,
            e.KpiEvaluationId, e.NextAction);

    private async Task<DecisionDto> BuildDecisionAsync(Guid id, CancellationToken ct)
    {
        var d = await _db.Decisions.AsNoTracking().FirstAsync(x => x.Id == id, ct);
        var names = await UserNamesAsync(new[] { d.MadeById }, ct);
        return MapDecision(d, names.GetValueOrDefault(d.MadeById));
    }

    private static DecisionDto MapDecision(Decision d, string? madeByName) =>
        new(d.Id, d.Title, d.Description, d.MadeById, madeByName, d.Status,
            d.RelatedSubmissionId, d.RelatedRiskId, d.RelatedEscalationId, d.DecidedAtUtc, d.CreatedAtUtc,
            d.RelatedKpiEvaluationId, d.NextAction, d.ProjectId);

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}
