using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Leave;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// خدمة طلبات الإجازة/الاستئذان (V1.0.1). تفرض النطاق والدور خادميًّا في كل عملية:
/// الموظّف ينشئ ويرى طلباته فقط ولا يعتمد طلبه؛ المراجِع لا يتصرّف خارج نطاقه ولا على خطوة ليست دوره؛
/// لا يتصرّف الشخص نفسه في خطوتين على الطلب ذاته. لا يؤثّر الطلب في التقارير إلا عند HrApproved.
/// </summary>
public class LeaveRequestService : ILeaveRequestService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;

    public LeaveRequestService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope,
        IAuditService audit, INotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
        _audit = audit;
        _notifications = notifications;
    }

    // حالات يعدّها الطلب فيها «قائمًا» (يحجز الفترة ويمنع التكرار).
    private static readonly LeaveRequestStatus[] PendingStatuses =
    {
        LeaveRequestStatus.Submitted, LeaveRequestStatus.TeamLeaderApproved,
        LeaveRequestStatus.ManagerApproved, LeaveRequestStatus.ReturnedForEdit
    };

    public async Task<Result<IReadOnlyList<LeaveRequestListItemDto>>> GetMyAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<LeaveRequestListItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var rows = await _db.LeaveRequests.AsNoTracking()
            .Where(r => r.RequesterUserId == uid)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        var names = await UserNamesAsync(rows.Select(r => r.RequesterUserId), ct);
        return Result<IReadOnlyList<LeaveRequestListItemDto>>.Success(
            rows.Select(r => MapList(r, names)).ToList());
    }

    public async Task<Result<IReadOnlyList<LeaveRequestListItemDto>>> GetPendingAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<LeaveRequestListItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var canManage = _currentUser.IsInAnyRole(Roles.Management);       // TL وما فوق (خطوات قائد الفريق/المدير)
        var canFinal = _currentUser.IsInAnyRole(Roles.LeaveFinalApprovers); // الاعتماد النهائي (HR/Admin/CEO/GM)
        var isGm = _currentUser.IsInRole(Roles.GeneralManager);
        var isCeoOrAdmin = _currentUser.IsInAnyRole(Roles.Ceo, Roles.Admin);

        if (!canManage && !canFinal)
            return Result<IReadOnlyList<LeaveRequestListItemDto>>.Success(new List<LeaveRequestListItemDto>());

        var scope = await _scope.ResolveAsync(ct);

        // لا يتصرّف الشخص نفسه على طلبه ولا على خطوتين منه — أساس كل الطوابير.
        var baseQuery = _db.LeaveRequests.AsNoTracking()
            .Where(r => r.RequesterUserId != uid
                        && r.TeamLeaderReviewerId != uid && r.ManagerReviewerId != uid && r.HrReviewerId != uid);

        var collected = new Dictionary<Guid, LeaveRequest>();
        async Task CollectAsync(IQueryable<LeaveRequest> q)
        {
            foreach (var r in await q.ToListAsync(ct)) collected[r.Id] = r;
        }

        // ===== الطلبات العادية =====
        // خطوتا قائد الفريق/المدير — ضمن النطاق الشخصي للمراجِع الإداري.
        if (canManage)
        {
            var q = baseQuery.Where(r => !r.IsHrRequest
                && (r.Status == LeaveRequestStatus.Submitted || r.Status == LeaveRequestStatus.TeamLeaderApproved));
            if (!scope.SeesAll)
            {
                var ids = scope.UserIds;
                q = q.Where(r => ids.Contains(r.RequesterUserId));
            }
            await CollectAsync(q);
        }
        // الاعتماد النهائي — سلطة مؤسسية على الإجازات لكل المعتمِدين النهائيين (بما فيهم HR ذو النطاق الشخصي).
        if (canFinal)
            await CollectAsync(baseQuery.Where(r => !r.IsHrRequest && r.Status == LeaveRequestStatus.ManagerApproved));

        // ===== طلبات الموارد البشرية الشخصية (مسار خاص) =====
        // خطوة مراجعة المدير العام (الطلب عند TeamLeaderApproved): المدير العام، أو المدير المباشر لمقدّم الطلب.
        if (isGm)
            await CollectAsync(baseQuery.Where(r => r.IsHrRequest && r.Status == LeaveRequestStatus.TeamLeaderApproved));
        else
            await CollectAsync(baseQuery.Where(r => r.IsHrRequest && r.Status == LeaveRequestStatus.TeamLeaderApproved
                && _db.Users.Any(u => u.Id == r.RequesterUserId && u.ManagerId == uid)));
        // الاعتماد النهائي لطلب HR (الطلب عند ManagerApproved): الإدارة العليا CEO/Admin فقط.
        if (isCeoOrAdmin)
            await CollectAsync(baseQuery.Where(r => r.IsHrRequest && r.Status == LeaveRequestStatus.ManagerApproved));

        var rows = collected.Values.OrderBy(r => r.CreatedAtUtc).ToList();
        var names = await UserNamesAsync(rows.Select(r => r.RequesterUserId), ct);
        return Result<IReadOnlyList<LeaveRequestListItemDto>>.Success(
            rows.Select(r => MapList(r, names)).ToList());
    }

    public async Task<Result<LeaveRequestDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<LeaveRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var entity = await _db.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<LeaveRequestDto>.Failure("الطلب غير موجود.", "leave_request.not_found");

        if (!await CanViewAsync(entity, uid, ct))
            return Result<LeaveRequestDto>.Failure("لا تملك صلاحية عرض هذا الطلب.", "auth.forbidden");

        return Result<LeaveRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    public async Task<Result<LeaveRequestDto>> CreateAsync(CreateLeaveRequestRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<LeaveRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<LeaveRequestDto>.Failure("سبب الطلب مطلوب.", "leave_request.reason_required");

        DateOnly start = request.StartDate;
        DateOnly end;
        TimeOnly? startTime = null, endTime = null;

        if (request.Type == LeaveRequestType.Leave)
        {
            if (request.EndDate is not DateOnly e)
                return Result<LeaveRequestDto>.Failure("تاريخ نهاية الإجازة مطلوب.", "leave_request.end_date_required");
            end = e;
            if (end < start)
                return Result<LeaveRequestDto>.Failure("تاريخ النهاية لا يسبق تاريخ البداية.", "leave_request.end_before_start");
        }
        else // Permission — يوم واحد، من وقت إلى وقت
        {
            end = start; // استئذان ليوم واحد في الإصدار الأول
            if (request.StartTime is not TimeOnly st || request.EndTime is not TimeOnly et)
                return Result<LeaveRequestDto>.Failure("وقت بداية الاستئذان ونهايته مطلوبان.", "leave_request.times_required");
            if (et <= st)
                return Result<LeaveRequestDto>.Failure("وقت النهاية يجب أن يلي وقت البداية.", "leave_request.end_time_before_start");
            startTime = st;
            endTime = et;
        }

        // منع التداخل مع طلب معتمَد نهائيًّا أو طلب قائم (بانتظار قرار) لنفس الموظّف في الفترة ذاتها.
        var conflicting = await _db.LeaveRequests.AsNoTracking()
            .Where(r => r.RequesterUserId == uid
                        && (r.Status == LeaveRequestStatus.HrApproved || PendingStatuses.Contains(r.Status))
                        && r.StartDate <= end && r.EndDate >= start)
            .AnyAsync(ct);
        if (conflicting)
            return Result<LeaveRequestDto>.Failure(
                "يوجد طلب قائم أو معتمَد يتداخل مع هذه الفترة.", "leave_request.period.conflict");

        // طلب الموارد البشرية لنفسه يسلك مسارًا خاصًّا: لا يراجعه HR، بل المدير العام يراجع ثم
        // الإدارة العليا (CEO/Admin) تعتمد. يُتخطّى قائد الفريق ويبدأ الطلب جاهزًا لمراجعة المدير العام.
        var isHrRequest = _currentUser.IsInRole(Roles.Hr);

        var entity = new LeaveRequest
        {
            RequesterUserId = uid,
            Type = request.Type,
            StartDate = start,
            EndDate = end,
            StartTime = startTime,
            EndTime = endTime,
            Reason = request.Reason.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IsHrRequest = isHrRequest,
            Status = isHrRequest ? LeaveRequestStatus.TeamLeaderApproved : LeaveRequestStatus.Submitted,
            CurrentStep = isHrRequest ? LeaveRequestStep.Manager : LeaveRequestStep.TeamLeader
        };
        _db.LeaveRequests.Add(entity);
        AddEvent(entity.Id, uid, "submitted", LeaveRequestStep.Employee,
            LeaveRequestStatus.Draft, LeaveRequestStatus.Submitted, null);
        if (isHrRequest)
            AddEvent(entity.Id, uid, "hr_routed", LeaveRequestStep.Employee,
                LeaveRequestStatus.Submitted, LeaveRequestStatus.TeamLeaderApproved,
                "طلب موارد بشرية: يُراجَع من المدير العام ثم يُعتمد نهائيًّا من الإدارة العليا.");
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "leave_request.submitted", nameof(LeaveRequest), entity.Id, ct: ct);

        // إشعار المدير المباشر (المراجِع الأرجح في الخطوة الأولى).
        var managerId = await _db.Users.Where(u => u.Id == uid).Select(u => u.ManagerId).FirstOrDefaultAsync(ct);
        if (managerId is Guid mgr)
            await _notifications.NotifyAsync(mgr, "leave_request.submitted",
                "طلب إجازة/استئذان جديد بانتظار مراجعتك", entity.Reason, $"/app/leave-requests?tab=pending", ct);

        return Result<LeaveRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    public async Task<Result<LeaveRequestDto>> CancelAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<LeaveRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var entity = await _db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<LeaveRequestDto>.Failure("الطلب غير موجود.", "leave_request.not_found");

        // الإلغاء حقّ صاحب الطلب وحده.
        if (entity.RequesterUserId != uid)
            return Result<LeaveRequestDto>.Failure("لا يمكنك إلغاء طلب لا تملكه.", "auth.forbidden");

        if (!PendingStatuses.Contains(entity.Status))
            return Result<LeaveRequestDto>.Failure(
                "لا يمكن إلغاء الطلب في حالته الحالية (اعتُمد نهائيًّا أو رُفض أو أُلغي).", "leave_request.cannot_cancel");

        var from = entity.Status;
        entity.Status = LeaveRequestStatus.Cancelled;
        entity.CurrentStep = LeaveRequestStep.Completed;
        entity.CancelledAtUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        AddEvent(entity.Id, uid, "cancelled", LeaveRequestStep.Employee, from, LeaveRequestStatus.Cancelled, null);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "leave_request.cancelled", nameof(LeaveRequest), entity.Id, ct: ct);

        return Result<LeaveRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    public Task<Result<LeaveRequestDto>> TeamLeaderApproveAsync(Guid id, LeaveApproveRequest request, CancellationToken ct = default)
        => DecideAsync(id, LeaveRequestStatus.Submitted, LeaveRequestStep.TeamLeader,
            LeaveRequestStatus.TeamLeaderApproved, LeaveRequestStep.Manager,
            "team_leader_approved", "leave_request.team_leader_approved", request.Comment, null, ct);

    public Task<Result<LeaveRequestDto>> TeamLeaderRejectAsync(Guid id, LeaveRejectRequest request, CancellationToken ct = default)
        => DecideAsync(id, LeaveRequestStatus.Submitted, LeaveRequestStep.TeamLeader,
            LeaveRequestStatus.TeamLeaderRejected, LeaveRequestStep.Completed,
            "team_leader_rejected", "leave_request.team_leader_rejected", request.Reason, request.Reason, ct);

    public Task<Result<LeaveRequestDto>> ManagerApproveAsync(Guid id, LeaveApproveRequest request, CancellationToken ct = default)
        => DecideAsync(id, LeaveRequestStatus.TeamLeaderApproved, LeaveRequestStep.Manager,
            LeaveRequestStatus.ManagerApproved, LeaveRequestStep.Hr,
            "manager_approved", "leave_request.manager_approved", request.Comment, null, ct);

    public Task<Result<LeaveRequestDto>> ManagerRejectAsync(Guid id, LeaveRejectRequest request, CancellationToken ct = default)
        => DecideAsync(id, LeaveRequestStatus.TeamLeaderApproved, LeaveRequestStep.Manager,
            LeaveRequestStatus.ManagerRejected, LeaveRequestStep.Completed,
            "manager_rejected", "leave_request.manager_rejected", request.Reason, request.Reason, ct);

    public Task<Result<LeaveRequestDto>> HrApproveAsync(Guid id, LeaveApproveRequest request, CancellationToken ct = default)
        => DecideAsync(id, LeaveRequestStatus.ManagerApproved, LeaveRequestStep.Hr,
            LeaveRequestStatus.HrApproved, LeaveRequestStep.Completed,
            "hr_approved", "leave_request.hr_approved", request.Comment, null, ct);

    public Task<Result<LeaveRequestDto>> HrRejectAsync(Guid id, LeaveRejectRequest request, CancellationToken ct = default)
        => DecideAsync(id, LeaveRequestStatus.ManagerApproved, LeaveRequestStep.Hr,
            LeaveRequestStatus.HrRejected, LeaveRequestStep.Completed,
            "hr_rejected", "leave_request.hr_rejected", request.Reason, request.Reason, ct);

    public async Task<Result<LeaveRequestDto>> ReturnAsync(Guid id, LeaveReturnRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<LeaveRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<LeaveRequestDto>.Failure("سبب الإعادة مطلوب.", "leave_request.return_reason_required");

        var entity = await _db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<LeaveRequestDto>.Failure("الطلب غير موجود.", "leave_request.not_found");

        var reviewable = entity.Status is LeaveRequestStatus.Submitted
            or LeaveRequestStatus.TeamLeaderApproved or LeaveRequestStatus.ManagerApproved;
        if (!reviewable)
            return Result<LeaveRequestDto>.Failure("لا يمكن إعادة الطلب في حالته الحالية.", "leave_request.cannot_return");

        // المراجِع لا يتصرّف خارج نطاقه ولا على طلبه.
        if (entity.RequesterUserId == uid)
            return Result<LeaveRequestDto>.Failure("لا يمكنك مراجعة طلبك الخاص.", "auth.forbidden");
        var scope = await _scope.ResolveAsync(ct);
        if (!scope.Contains(entity.RequesterUserId))
            return Result<LeaveRequestDto>.Failure("الطلب خارج نطاق صلاحيتك.", "auth.forbidden");

        var from = entity.Status;
        var step = entity.CurrentStep;
        entity.Status = LeaveRequestStatus.ReturnedForEdit;
        entity.CurrentStep = LeaveRequestStep.Employee;
        entity.ReturnReason = request.Reason.Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;
        AddEvent(entity.Id, uid, "returned", step, from, LeaveRequestStatus.ReturnedForEdit, request.Reason.Trim());
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "leave_request.returned", nameof(LeaveRequest), entity.Id, ct: ct);
        await _notifications.NotifyAsync(entity.RequesterUserId, "leave_request.returned",
            "أُعيد طلب الإجازة/الاستئذان للتعديل", request.Reason.Trim(), "/app/leave-requests", ct);

        return Result<LeaveRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    // ===== جوهر اتخاذ القرار (اعتماد/رفض لكل خطوة) =====

    private async Task<Result<LeaveRequestDto>> DecideAsync(
        Guid id, LeaveRequestStatus requiredStatus, LeaveRequestStep step,
        LeaveRequestStatus toStatus, LeaveRequestStep nextStep,
        string action, string auditAction, string? comment, string? rejectionReason, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<LeaveRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        // الرفض يستلزم سببًا إلزاميًّا.
        if (rejectionReason is not null && string.IsNullOrWhiteSpace(rejectionReason))
            return Result<LeaveRequestDto>.Failure("سبب الرفض مطلوب.", "leave_request.rejection_reason_required");

        var entity = await _db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<LeaveRequestDto>.Failure("الطلب غير موجود.", "leave_request.not_found");

        // لا يعتمد أحد طلبه.
        if (entity.RequesterUserId == uid)
            return Result<LeaveRequestDto>.Failure("لا يمكنك اتخاذ قرار على طلبك الخاص.", "auth.forbidden");

        // لا يتصرّف الشخص نفسه في خطوتين على الطلب ذاته.
        if (entity.TeamLeaderReviewerId == uid || entity.ManagerReviewerId == uid || entity.HrReviewerId == uid)
            return Result<LeaveRequestDto>.Failure("لا يمكنك اتخاذ قرار على خطوة أخرى لنفس الطلب.", "auth.forbidden");

        // فرض الدور والنطاق حسب الخطوة (يشمل المسار الخاص لطلبات الموارد البشرية).
        var authError = await AuthorizeDecisionAsync(entity, uid, step, ct);
        if (authError is not null) return authError;

        // ترتيب الخطوات: لا يُتّخذ القرار قبل اكتمال الخطوة السابقة.
        if (entity.Status != requiredStatus)
            return Result<LeaveRequestDto>.Failure(
                "حالة الطلب لا تسمح بهذا الإجراء الآن.", "leave_request.invalid_state");

        var from = entity.Status;
        var now = DateTime.UtcNow;
        entity.Status = toStatus;
        entity.CurrentStep = nextStep;
        entity.UpdatedAtUtc = now;
        if (rejectionReason is not null) entity.RejectionReason = rejectionReason.Trim();

        switch (step)
        {
            case LeaveRequestStep.TeamLeader:
                entity.TeamLeaderReviewerId = uid;
                entity.TeamLeaderDecisionAtUtc = now;
                break;
            case LeaveRequestStep.Manager:
                entity.ManagerReviewerId = uid;
                entity.ManagerDecisionAtUtc = now;
                break;
            case LeaveRequestStep.Hr:
                entity.HrReviewerId = uid;
                entity.HrDecisionAtUtc = now;
                break;
        }

        AddEvent(entity.Id, uid, action, step, from, toStatus, comment);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, auditAction, nameof(LeaveRequest), entity.Id, ct: ct);

        // إشعار صاحب الطلب بكل قرار.
        var title = toStatus switch
        {
            LeaveRequestStatus.HrApproved => "اعتُمد طلبك نهائيًّا",
            LeaveRequestStatus.TeamLeaderRejected or LeaveRequestStatus.ManagerRejected
                or LeaveRequestStatus.HrRejected => "رُفض طلبك",
            _ => "تقدّم طلبك خطوةً في الاعتماد"
        };
        await _notifications.NotifyAsync(entity.RequesterUserId, "leave_request.decision",
            title, comment, "/app/leave-requests", ct);

        return Result<LeaveRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    // ===== مساعدات =====

    /// <summary>
    /// يفرض الدور والنطاق المناسبين للخطوة. يعيد خطأً إن مُنع، أو null إن سُمح.
    /// المسار العادي: قائد الفريق/المدير ضمن النطاق الشخصي، والاعتماد النهائي سلطة مؤسسية للمعتمِدين النهائيين.
    /// مسار طلب HR الشخصي: المدير العام (أو المدير المباشر) يراجع، والإدارة العليا CEO/Admin تعتمد نهائيًّا.
    /// </summary>
    private async Task<Result<LeaveRequestDto>?> AuthorizeDecisionAsync(
        LeaveRequest entity, Guid uid, LeaveRequestStep step, CancellationToken ct)
    {
        if (entity.IsHrRequest)
        {
            if (step == LeaveRequestStep.Manager) // مراجعة المدير العام لطلب الموارد البشرية
            {
                var requesterManagerId = await _db.Users
                    .Where(u => u.Id == entity.RequesterUserId).Select(u => u.ManagerId).FirstOrDefaultAsync(ct);
                var allowed = _currentUser.IsInRole(Roles.GeneralManager) || requesterManagerId == uid;
                return allowed ? null : Result<LeaveRequestDto>.Failure(
                    "مراجعة طلب الموارد البشرية من صلاحية المدير العام.", "auth.forbidden");
            }
            if (step == LeaveRequestStep.Hr) // الاعتماد النهائي لطلب الموارد البشرية — الإدارة العليا فقط
            {
                return _currentUser.IsInAnyRole(Roles.Ceo, Roles.Admin) ? null : Result<LeaveRequestDto>.Failure(
                    "الاعتماد النهائي لطلب الموارد البشرية من صلاحية الإدارة العليا.", "auth.forbidden");
            }
        }

        // طلب عادي — الاعتماد النهائي سلطة مؤسسية على الإجازات (HR/Admin/CEO/GM) دون قيد النطاق الشخصي.
        if (step == LeaveRequestStep.Hr)
        {
            return _currentUser.IsInAnyRole(Roles.LeaveFinalApprovers) ? null : Result<LeaveRequestDto>.Failure(
                "لا تملك صلاحية الاعتماد النهائي.", "auth.forbidden");
        }

        // خطوتا قائد الفريق/المدير في الطلب العادي — ضمن النطاق الشخصي للمراجِع.
        var scope = await _scope.ResolveAsync(ct);
        if (!scope.Contains(entity.RequesterUserId))
            return Result<LeaveRequestDto>.Failure("الطلب خارج نطاق صلاحيتك.", "auth.forbidden");
        return null;
    }

    private void AddEvent(Guid leaveRequestId, Guid actorId, string action, LeaveRequestStep step,
        LeaveRequestStatus from, LeaveRequestStatus to, string? comment)
    {
        _db.LeaveRequestEvents.Add(new LeaveRequestEvent
        {
            LeaveRequestId = leaveRequestId,
            ActorUserId = actorId,
            Action = action,
            Step = step,
            FromStatus = from,
            ToStatus = to,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()
        });
    }

    /// <summary>
    /// الوصول للطلب: مالكه دائمًا، أو المعتمِدون النهائيون (سلطة مؤسسية على الإجازات: HR/Admin/CEO/GM)،
    /// أو المراجِع الإداري لصاحب الطلب ضمن نطاقه.
    /// </summary>
    private async Task<bool> CanViewAsync(LeaveRequest entity, Guid uid, CancellationToken ct)
    {
        if (entity.RequesterUserId == uid) return true;
        if (_currentUser.IsInAnyRole(Roles.LeaveFinalApprovers)) return true;
        if (!_currentUser.IsInAnyRole(Roles.Management)) return false;
        var scope = await _scope.ResolveAsync(ct);
        return scope.Contains(entity.RequesterUserId);
    }

    private async Task<LeaveRequestDto> BuildAsync(LeaveRequest entity, Guid uid, CancellationToken ct)
    {
        var events = await _db.LeaveRequestEvents.AsNoTracking()
            .Where(e => e.LeaveRequestId == entity.Id)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync(ct);

        var ids = new List<Guid> { entity.RequesterUserId };
        if (entity.TeamLeaderReviewerId is Guid tl) ids.Add(tl);
        if (entity.ManagerReviewerId is Guid mg) ids.Add(mg);
        if (entity.HrReviewerId is Guid hr) ids.Add(hr);
        ids.AddRange(events.Select(e => e.ActorUserId));
        var names = await UserNamesAsync(ids, ct);

        var canCancel = entity.RequesterUserId == uid && PendingStatuses.Contains(entity.Status);

        return new LeaveRequestDto(
            entity.Id, entity.RequesterUserId, names.GetValueOrDefault(entity.RequesterUserId, string.Empty),
            entity.Type, entity.StartDate, entity.EndDate, entity.StartTime, entity.EndTime,
            entity.Reason, entity.Notes, entity.Status, entity.CurrentStep, entity.IsHrRequest,
            entity.TeamLeaderReviewerId,
            entity.TeamLeaderReviewerId is Guid a ? names.GetValueOrDefault(a) : null,
            entity.ManagerReviewerId,
            entity.ManagerReviewerId is Guid b ? names.GetValueOrDefault(b) : null,
            entity.HrReviewerId,
            entity.HrReviewerId is Guid c ? names.GetValueOrDefault(c) : null,
            entity.TeamLeaderDecisionAtUtc, entity.ManagerDecisionAtUtc, entity.HrDecisionAtUtc,
            entity.RejectionReason, entity.ReturnReason,
            entity.Status == LeaveRequestStatus.HrApproved, canCancel,
            entity.CreatedAtUtc, entity.UpdatedAtUtc, entity.CancelledAtUtc,
            events.Select(e => new LeaveRequestEventDto(
                e.Id, e.ActorUserId, names.GetValueOrDefault(e.ActorUserId), e.Action,
                e.Step, e.FromStatus, e.ToStatus, e.Comment, e.CreatedAtUtc)).ToList());
    }

    private static LeaveRequestListItemDto MapList(LeaveRequest r, Dictionary<Guid, string> names) =>
        new(r.Id, r.RequesterUserId, names.GetValueOrDefault(r.RequesterUserId, string.Empty),
            r.Type, r.StartDate, r.EndDate, r.StartTime, r.EndTime, r.Reason, r.Status, r.CurrentStep,
            r.IsHrRequest, r.Status == LeaveRequestStatus.HrApproved, r.CreatedAtUtc);

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}
