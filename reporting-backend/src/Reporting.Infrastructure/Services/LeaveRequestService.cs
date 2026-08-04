using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Leave;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.EmployeeServices;
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
    private readonly IEmailNotificationService _emailNotifications;

    public LeaveRequestService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope,
        IAuditService audit, INotificationService notifications, IEmailNotificationService emailNotifications)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
        _audit = audit;
        _notifications = notifications;
        _emailNotifications = emailNotifications;
    }

    /// <summary>مُعرّفات مستخدمي الموارد البشرية النشطين (Role="HR") — لإشعارات وصول الطلب لمرحلة HR.</summary>
    private async Task<List<Guid>> HrUserIdsAsync(CancellationToken ct)
    {
        var hrRoleIds = await _db.Roles.Where(r => r.Name == Roles.Hr).Select(r => r.Id).ToListAsync(ct);
        if (hrRoleIds.Count == 0) return new();
        return await _db.UserRoles.Where(ur => hrRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId).Distinct().ToListAsync(ct);
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
            // حدّ مدّة الاستئذان: لا يتجاوز ساعتين. الأطول يجب أن يُقدَّم كطلب إجازة/غياب وفق السياسة (يُفرَض خادميًّا أيضًا).
            if ((et.ToTimeSpan() - st.ToTimeSpan()).TotalHours > 2)
                return Result<LeaveRequestDto>.Failure(
                    "مدة الاستئذان لا يمكن أن تتجاوز ساعتين. إذا كانت المدة أطول، يرجى تقديم طلب إجازة/غياب وفق السياسة.",
                    "leave_request.permission_duration_exceeded");
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

        // ===== حارسا كفاية الرصيد عند الإنشاء (V1.1 إجازات / V1.1.1 أذونات) =====
        // كلاهما يحسب لقطةً إعلامية ويطلب إقرار الموظّف عند النقص دون أي خصم آلي أو ربط رواتب وقت الإنشاء.
        // الإجازة ⇒ رصيد الإجازات السنوي (بالأيام). الاستئذان ⇒ رصيد الأذونات الشهري (بالعدد) من السياسة المنطبقة.
        decimal? balanceAtRequest = null;
        int? requestedLeaveDays = null;
        decimal? uncoveredLeaveDays = null;
        var isPotentialUnpaid = false;
        var permissionResolution = PermissionShortfallResolution.None;

        if (request.Type == LeaveRequestType.Leave)
        {
            // حارس كفاية رصيد الإجازات السنوي — إن تجاوز المطلوبُ المتاحَ ولم يُقرّ الموظّف ⇒ يُرفض الطلب.
            var year = start.Year;
            var ledger = await _db.EmployeeBalanceLedger.AsNoTracking()
                .Where(e => e.EmployeeId == uid && e.Year == year && e.BalanceType == BalanceType.AnnualLeave)
                .ToListAsync(ct);
            var credited = ledger.Where(e => e.Direction == BalanceDirection.Credit).Sum(e => e.Amount);
            var debited = ledger.Where(e => e.Direction == BalanceDirection.Debit).Sum(e => e.Amount);
            var remaining = credited - debited;
            var requested = (end.DayNumber - start.DayNumber) + 1; // أيام شاملة الطرفين
            var uncovered = requested - remaining;
            if (uncovered > 0)
            {
                if (!request.AcknowledgedUnpaidDeduction)
                    return Result<LeaveRequestDto>.Failure(
                        "رصيد الإجازات السنوي المتاح لديك غير كافٍ لهذا الطلب. يلزم إقرارك بأن الأيام غير المغطّاة قد تُحتسب إجازةً بدون راتب.",
                        "leave.balance_ack_required");
                balanceAtRequest = remaining;
                requestedLeaveDays = requested;
                uncoveredLeaveDays = uncovered;
                isPotentialUnpaid = true;
            }
        }
        else // Permission — حارس رصيد الأذونات الشهري (V1.1.1)
        {
            // الرصيد الشهري = الحدّ الشهري من السياسة المنطبقة على مسمّى الموظّف. لا سياسة/لا حدّ ⇒ لا قيد (يمرّ بلا إقرار).
            // إن تجاوز هذا الإذن الرصيدَ المتبقّي ولم يختر الموظّف حلًّا ⇒ يُرفض (400) كي يُعرض عليه الخياران بالواجهة؛
            // مع اختياره أحد الحلَّين (تعويض الوقت بعد الدوام / المتابعة الإدارية-المالية) ⇒ يُقبل ويُخزَّن لقطة + قرار الحلّ.
            var jobRoleId = await _db.Users.Where(u => u.Id == uid)
                .Select(u => u.JobRoleId).FirstOrDefaultAsync(ct);
            var policy = await ResolvePolicyAsync(start.Year, jobRoleId, ct);
            if (policy?.PermissionMonthlyLimit is decimal monthlyLimit)
            {
                // العدّ يَعُدّ المعتمَد + القائم في الشهر (يحجز الفتحة مبكرًا) — يطابق حارس الإنشاء السابق.
                var used = await CountPermissionsInMonthAsync(uid, start.Year, start.Month, includePending: true, excludeRequestId: null, ct);
                var remainingMonthly = monthlyLimit - used;       // الرصيد الشهري المتبقّي وقت الطلب
                var uncovered = (used + 1) - monthlyLimit;        // عدد الأذونات غير المغطّاة بالرصيد الشهري
                if (uncovered > 0)
                {
                    if (request.PermissionShortfallResolution == PermissionShortfallResolution.None)
                        return Result<LeaveRequestDto>.Failure(
                            "رصيد الاستئذانات الشهري المتاح لديك غير كافٍ لهذا الطلب. اختر تعويض الوقت بعد مواعيد العمل، أو المتابعة مع علمك باحتمال المعالجة الإدارية/المالية.",
                            "permission.balance_ack_required");
                    permissionResolution = request.PermissionShortfallResolution;
                    balanceAtRequest = remainingMonthly;
                    requestedLeaveDays = 1; // إذن واحد لكل طلب (الوحدة Count)
                    uncoveredLeaveDays = uncovered;
                    isPotentialUnpaid = true;
                }
            }
        }

        // طلب الموارد البشرية لنفسه يسلك مسارًا خاصًّا: لا يراجعه HR، بل المدير العام يراجع ثم
        // الإدارة العليا (CEO/Admin) تعتمد. يُتخطّى قائد الفريق ويبدأ الطلب جاهزًا لمراجعة المدير العام.
        var isHrRequest = _currentUser.IsInRole(Roles.Hr);

        // طلب قائد الفريق العادي (إجازة/استئذان) يتخطّى خطوة قائد الفريق تلقائيًّا لأنه لا يمكن أن
        // يعتمد طلبه الشخصي؛ يبدأ مباشرةً عند المدير. لا ينطبق على طلب الموارد البشرية (له مساره).
        var isTeamLeaderRequest = !isHrRequest && _currentUser.IsInRole(Roles.TeamLeader);

        // تجاوز خطوة قائد الفريق (Direct Reporting Override): قاعدة عامة — إن كان الموظّف مضبوطًا على
        // BypassTeamLeaderApproval=true فلا قائد فعلي له في مسار الاعتماد رغم بقائه ضمن فريق له قائد،
        // فيُوجَّه الطلب مباشرةً إلى مديره المباشر (ثم fallback المعتمد). لا يمسّ TeamId ولا قائد الفريق.
        var requesterBypassesTeamLeader = await _db.Users.Where(u => u.Id == uid)
            .Select(u => u.BypassTeamLeaderApproval).FirstOrDefaultAsync(ct);

        // قائد الفريق الفعلي للموظّف (T-WF2): خطوة قائد الفريق يعتمدها قائد فريق الموظّف الفعلي حصرًا.
        // إن لم يكن للموظّف فريق، أو لا قائد محدّد للفريق، أو القائد هو الموظّف نفسه، أو الموظّف Direct
        // Reporting ⇒ لا قائد فعلي ⇒ يُتخطّى قائد الفريق عند الإنشاء ويُوجَّه الطلب مباشرةً إلى المدير.
        Guid? effectiveTeamLeaderId = null;
        if (!isHrRequest && !isTeamLeaderRequest && !requesterBypassesTeamLeader)
        {
            var teamId = await _db.Users.Where(u => u.Id == uid).Select(u => u.TeamId).FirstOrDefaultAsync(ct);
            if (teamId is Guid tid)
                effectiveTeamLeaderId = await _db.Teams.Where(t => t.Id == tid)
                    .Select(t => t.TeamLeaderId).FirstOrDefaultAsync(ct);
        }
        var noEffectiveTeamLeader = !isHrRequest && !isTeamLeaderRequest
            && (effectiveTeamLeaderId is null || effectiveTeamLeaderId == uid);

        var skipsTeamLeader = isHrRequest || isTeamLeaderRequest || noEffectiveTeamLeader;

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
            Status = skipsTeamLeader ? LeaveRequestStatus.TeamLeaderApproved : LeaveRequestStatus.Submitted,
            CurrentStep = skipsTeamLeader ? LeaveRequestStep.Manager : LeaveRequestStep.TeamLeader,
            BalanceAtRequest = balanceAtRequest,
            RequestedLeaveDays = requestedLeaveDays,
            UncoveredLeaveDays = uncoveredLeaveDays,
            IsPotentialUnpaidLeave = isPotentialUnpaid,
            EmployeeAcknowledgedUnpaidDeduction = isPotentialUnpaid,
            EmployeeAcknowledgedAtUtc = isPotentialUnpaid ? DateTime.UtcNow : null,
            PermissionShortfallResolution = permissionResolution
        };
        _db.LeaveRequests.Add(entity);
        AddEvent(entity.Id, uid, "submitted", LeaveRequestStep.Employee,
            LeaveRequestStatus.Draft, LeaveRequestStatus.Submitted, null);
        if (isHrRequest)
            AddEvent(entity.Id, uid, "hr_routed", LeaveRequestStep.Employee,
                LeaveRequestStatus.Submitted, LeaveRequestStatus.TeamLeaderApproved,
                "طلب موارد بشرية: يُراجَع من المدير العام ثم يُعتمد نهائيًّا من الإدارة العليا.");
        else if (isTeamLeaderRequest)
            AddEvent(entity.Id, uid, "team_leader_step_skipped", LeaveRequestStep.Employee,
                LeaveRequestStatus.Submitted, LeaveRequestStatus.TeamLeaderApproved,
                "تم تخطي مراجعة قائد الفريق لأن مقدم الطلب هو قائد الفريق.");
        else if (requesterBypassesTeamLeader)
            AddEvent(entity.Id, uid, "team_leader_step_skipped", LeaveRequestStep.Employee,
                LeaveRequestStatus.Submitted, LeaveRequestStatus.TeamLeaderApproved,
                "تم تخطي مراجعة قائد الفريق (تبعية مباشرة للمدير)، وتم توجيه الطلب إلى المدير المباشر.");
        else if (noEffectiveTeamLeader)
            AddEvent(entity.Id, uid, "team_leader_step_skipped", LeaveRequestStep.Employee,
                LeaveRequestStatus.Submitted, LeaveRequestStatus.TeamLeaderApproved,
                "تم تخطي مراجعة قائد الفريق لعدم وجود قائد فريق محدد، وتم توجيه الطلب إلى المدير.");
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "leave_request.submitted", nameof(LeaveRequest), entity.Id, ct: ct);

        // إشعار المدير المباشر (المراجِع الأرجح في الخطوة الأولى).
        var managerId = await _db.Users.Where(u => u.Id == uid).Select(u => u.ManagerId).FirstOrDefaultAsync(ct);
        if (managerId is Guid mgr)
            await _notifications.NotifyAsync(mgr, "leave_request.submitted",
                "طلب إجازة/استئذان جديد بانتظار مراجعتك", entity.Reason, $"/app/leave-requests?tab=pending", ct);

        // إشعار بريد (EMAIL-OPERATIONAL-NOTIFICATIONS-R1، DryRun): مُراجِع الخطوة الأولى (قائد الفريق الفعلي، أو المدير عند تخطّيه).
        var reviewerIds = new List<Guid>();
        if (skipsTeamLeader) { if (managerId is Guid m1) reviewerIds.Add(m1); }
        else if (effectiveTeamLeaderId is Guid tl) reviewerIds.Add(tl);
        try { await _emailNotifications.NotifyLeaveRequestCreatedAsync(entity, reviewerIds, ct); }
        catch { /* تُسجَّل داخل الخدمة؛ لا تكسر العملية الأساسية. */ }

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

    public async Task<Result<LeaveRequestDto>> RevokeApprovedAsync(Guid id, LeaveRevokeRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<LeaveRequestDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        // مسار محروس للإدارة/HR فقط (إبطال إجازة معتمَدة نهائيًّا).
        if (!_currentUser.IsInAnyRole(Roles.BalanceManagers))
            return Result<LeaveRequestDto>.Failure("لا تملك صلاحية إبطال طلب معتمَد.", "auth.forbidden");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<LeaveRequestDto>.Failure("سبب الإبطال مطلوب.", "leave_request.revoke_reason_required");

        var entity = await _db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return Result<LeaveRequestDto>.Failure("الطلب غير موجود.", "leave_request.not_found");

        if (entity.Status != LeaveRequestStatus.HrApproved)
            return Result<LeaveRequestDto>.Failure(
                "لا يمكن إبطال إلا طلبًا معتمَدًا نهائيًّا.", "leave_request.not_approved.conflict");

        var from = entity.Status;
        entity.Status = LeaveRequestStatus.Cancelled;
        entity.CurrentStep = LeaveRequestStep.Completed;
        entity.CancelledAtUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        AddEvent(entity.Id, uid, "revoked", LeaveRequestStep.Completed, from, LeaveRequestStatus.Cancelled, request.Reason.Trim());

        // عكس الخصم الآلي بإضافة حركة Credit معاكسة (لا حذف للحركة الأصلية) — في نفس المعاملة، idempotent.
        await ApplyRevocationReversalAsync(entity, uid, request.Reason.Trim(), ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "leave_request.revoked", nameof(LeaveRequest), entity.Id,
            JsonSerializer.Serialize(new { leaveRequestId = entity.Id, entity.Type }), ct: ct);
        await _notifications.NotifyAsync(entity.RequesterUserId, "leave_request.revoked",
            "أُبطل طلب إجازة/استئذان معتمَد", request.Reason.Trim(), "/app/leave-requests", ct);

        return Result<LeaveRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    public async Task<Result<TeamLeaderStuckRemediationResultDto>> RemediateTeamLeaderStuckRequestsAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<TeamLeaderStuckRemediationResultDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        // مسار محروس للأدمن فقط (معالجة لمرّة واحدة للطلبات العالقة قبل تفعيل تخطّي قائد الفريق).
        if (!_currentUser.IsInRole(Roles.Admin))
            return Result<TeamLeaderStuckRemediationResultDto>.Failure(
                "لا تملك صلاحية معالجة الطلبات العالقة.", "auth.forbidden");

        // معرّفات المستخدمين الذين يحملون دور قائد الفريق.
        var tlRoleId = await _db.Roles
            .Where(r => r.Name == Roles.TeamLeader)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (tlRoleId == Guid.Empty)
            return Result<TeamLeaderStuckRemediationResultDto>.Success(
                new TeamLeaderStuckRemediationResultDto(0, 0, Array.Empty<Guid>()));

        var tlUserIds = await _db.UserRoles
            .Where(ur => ur.RoleId == tlRoleId)
            .Select(ur => ur.UserId)
            .ToListAsync(ct);

        // الطلبات العالقة: مقدّمها قائد فريق، Status=Submitted، CurrentStep=TeamLeader،
        // غير طلب موارد بشرية، ولم يُراجَع بعد (TeamLeaderReviewerId=null). idempotent (لا يطال المعالَج).
        var stuck = await _db.LeaveRequests
            .Where(r => r.Status == LeaveRequestStatus.Submitted
                && r.CurrentStep == LeaveRequestStep.TeamLeader
                && !r.IsHrRequest
                && r.TeamLeaderReviewerId == null
                && tlUserIds.Contains(r.RequesterUserId))
            .ToListAsync(ct);

        var remediatedIds = new List<Guid>();
        foreach (var entity in stuck)
        {
            entity.Status = LeaveRequestStatus.TeamLeaderApproved;
            entity.CurrentStep = LeaveRequestStep.Manager;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            AddEvent(entity.Id, uid, "team_leader_step_skipped", LeaveRequestStep.Employee,
                LeaveRequestStatus.Submitted, LeaveRequestStatus.TeamLeaderApproved,
                "معالجة الطلبات العالقة: تم تخطي مراجعة قائد الفريق لأن مقدم الطلب هو قائد الفريق.");
            remediatedIds.Add(entity.Id);
        }

        if (remediatedIds.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync(uid, "leave_request.team_leader_stuck_remediated", nameof(LeaveRequest), null,
                JsonSerializer.Serialize(new { count = remediatedIds.Count, requestIds = remediatedIds }), ct: ct);
        }

        return Result<TeamLeaderStuckRemediationResultDto>.Success(
            new TeamLeaderStuckRemediationResultDto(stuck.Count, remediatedIds.Count, remediatedIds));
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

        // حارس الحد الشهري للأذونات عند الاعتماد النهائي — أهمّ موضع: يُمنع قبل أي تعديل للحالة أو كتابة حركة.
        // يَعُدّ المعتمَد فقط في الشهر (يستبعد هذا الطلب نفسه؛ المبطَل Cancelled مستبعَد ضمنًا).
        // يُتخطّى هذا الحارس إذا كان الموظّف قد أقرّ بتجاوز الرصيد الشهري عند الإنشاء واختار حلًّا
        // (PermissionShortfallResolution != None) — عندئذٍ الطلب متجاوِزٌ مُقَرٌّ به ويُسمح باعتماده الإداري.
        if (toStatus == LeaveRequestStatus.HrApproved && entity.Type == LeaveRequestType.Permission
            && entity.PermissionShortfallResolution == PermissionShortfallResolution.None)
        {
            var limitError = await CheckPermissionMonthlyLimitAsync(
                entity.RequesterUserId, entity.StartDate, includePending: false, excludeRequestId: entity.Id, ct);
            if (limitError is not null) return limitError;
        }

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

        // طيّ خطوة المدير تشغيليًّا عند غياب مدير تشغيليّ بديل (P2 — LEAVE-WORKFLOW-DEADLOCK-HOTFIX):
        // إن اعتُمدت خطوة قائد الفريق بنجاح وكان المُعتمِد نفسه هو المدير المباشر لمقدّم الطلب، فسيمنعه
        // حارس «عدم تصرّف الشخص نفسه في خطوتين» (أعلاه) من اعتماد خطوة المدير لاحقًا. لكنّ هذا التطابق
        // الاسميّ وحده لا يعني جمودًا: خطوة المدير scope-based، وقد يوجد مديرٌ تشغيليّ أعلى داخل الشجرة
        // الإدارية للموظّف يمكنه اعتمادها طبيعيًّا. لذلك نطوي الخطوة *فقط* حين لا يوجد أيّ مدير تشغيليّ بديل
        // داخل الشجرة (جمود بنيويّ حقيقي). عندئذٍ ننقل الطلب إلى ManagerApproved/Hr في المعاملة ذاتها دون
        // تلفيق قرار يدويّ منفصل، ونسجّل المدير كمُعتمِد لهذه الخطوة (فهو مديره المباشر فعلًا)، ونضيف حدث
        // تدقيق مميّزًا يوثّق أنّ الطيّ آليّ لغياب مدير تشغيليّ بديل — وأنّ أدوار الشركة/الحوكمة العامة
        // (Admin/CEO/GM/CeoSupport) لا تُعتبر بديلًا تشغيليًّا حسب P2. إن وُجد مدير تشغيليّ بديل ⇒ لا طيّ،
        // يبقى المسار الطبيعي TeamLeaderApproved/Manager (يحفظ ضمانة T-WF2). الطلبات القديمة لا تتغيّر.
        if (step == LeaveRequestStep.TeamLeader && toStatus == LeaveRequestStatus.TeamLeaderApproved)
        {
            var requesterManagerId = await _db.Users.Where(u => u.Id == entity.RequesterUserId)
                .Select(u => u.ManagerId).FirstOrDefaultAsync(ct);
            if (requesterManagerId == uid
                && !await HasOperationalManagerAlternativeAsync(entity.RequesterUserId, uid, ct))
            {
                entity.Status = LeaveRequestStatus.ManagerApproved;
                entity.CurrentStep = LeaveRequestStep.Hr;
                entity.ManagerReviewerId = uid;
                entity.ManagerDecisionAtUtc = now;
                AddEvent(entity.Id, uid, "manager_step_auto_folded_no_operational_manager", LeaveRequestStep.Manager,
                    LeaveRequestStatus.TeamLeaderApproved, LeaveRequestStatus.ManagerApproved,
                    "طيّ خطوة المدير تلقائيًّا: المُعتمِد هو المدير المباشر لمقدّم الطلب ولا يوجد مدير تشغيليّ بديل داخل شجرته الإدارية يمكنه اعتماد الخطوة (أدوار الشركة/الحوكمة العامة لا تُعدّ بديلًا تشغيليًّا). انتقل الطلب مباشرةً إلى الاعتماد النهائي (HR) دون قرار مدير يدويّ ثانٍ.");
            }
        }

        // الخصم التلقائي من الرصيد عند الاعتماد النهائي — في نفس المعاملة، idempotent عبر (RelatedRequestId, Source).
        if (toStatus == LeaveRequestStatus.HrApproved)
            await ApplyApprovalDeductionAsync(entity, uid, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, auditAction, nameof(LeaveRequest), entity.Id, ct: ct);

        // إشعار صاحب الطلب بكل قرار. نعتمد الحالة الفعليّة للطلب (entity.Status) لا toStatus كي
        // يعكس العنوانُ الطيَّ الآليّ لخطوة المدير (ManagerApproved). للمسارات غير المطويّة entity.Status == toStatus.
        var title = entity.Status switch
        {
            LeaveRequestStatus.HrApproved => "اعتُمد طلبك نهائيًّا",
            LeaveRequestStatus.TeamLeaderRejected or LeaveRequestStatus.ManagerRejected
                or LeaveRequestStatus.HrRejected => "رُفض طلبك",
            _ => "تقدّم طلبك خطوةً في الاعتماد"
        };
        await _notifications.NotifyAsync(entity.RequesterUserId, "leave_request.decision",
            title, comment, "/app/leave-requests", ct);

        // إشعارات بريد (EMAIL-OPERATIONAL-NOTIFICATIONS-R1، DryRun) — بعد نجاح القرار، محميّة داخليًّا.
        try
        {
            // نعتمد الحالة الفعليّة (entity.Status) لا toStatus: عند طيّ خطوة المدير آليًّا تصبح الحالة
            // ManagerApproved ⇒ يجب إشعار HR. للمسارات غير المطويّة entity.Status == toStatus (بلا تغيير سلوك).
            switch (entity.Status)
            {
                // الوصول لمرحلة الموارد البشرية (اعتماد المدير ⇒ الخطوة التالية Hr): إشعار مستخدمي HR.
                case LeaveRequestStatus.ManagerApproved:
                    await _emailNotifications.NotifyLeaveRequestNeedsHrActionAsync(entity, await HrUserIdsAsync(ct), ct);
                    break;
                // الاعتماد النهائي: إشعار صاحب الطلب.
                case LeaveRequestStatus.HrApproved:
                    await _emailNotifications.NotifyLeaveRequestApprovedAsync(entity, ct);
                    break;
                // الرفض في أيّ خطوة: إشعار صاحب الطلب بصياغة هادئة.
                case LeaveRequestStatus.TeamLeaderRejected:
                case LeaveRequestStatus.ManagerRejected:
                case LeaveRequestStatus.HrRejected:
                    await _emailNotifications.NotifyLeaveRequestRejectedAsync(entity, ct);
                    break;
            }
        }
        catch { /* تُسجَّل داخل الخدمة؛ لا تكسر العملية الأساسية. */ }

        return Result<LeaveRequestDto>.Success(await BuildAsync(entity, uid, ct));
    }

    // ===== مساعدات =====

    /// <summary>
    /// يفرض الدور والنطاق المناسبين للخطوة. يعيد خطأً إن مُنع، أو null إن سُمح.
    /// المسار العادي: خطوة قائد الفريق يعتمدها قائد فريق الموظّف الفعلي حصرًا (T-WF2 — لا يكفي اتساع النطاق)؛
    /// خطوة المدير ضمن النطاق الشخصي؛ والاعتماد النهائي سلطة مؤسسية للمعتمِدين النهائيين.
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

        // خطوة قائد الفريق في الطلب العادي (T-WF2): يعتمدها قائد فريق الموظّف الفعلي حصرًا.
        // لا يكفي اتساع النطاق الإداري — فالمدير/المدير العام/الإدارة العليا لا يعتمدون هذه الخطوة
        // لمجرد أنّ نطاقهم يشمل الموظّف. إن وُجد قائد فريق محدّد ⇒ هو وحده يُسمح له (وإلا 403).
        if (step == LeaveRequestStep.TeamLeader)
        {
            // تجاوز خطوة قائد الفريق (Direct Reporting Override): طلب الموظّف Direct Reporting
            // لا يمرّ بخطوة قائد الفريق أصلًا (يُوجَّه للمدير عند الإنشاء)، لكن كحارس دفاعي
            // للطلبات القديمة نُعامِله كأنّه بلا قائد فريق فعلي فيؤول للمراجعة ضمن النطاق الإداري.
            var requesterBypassesTeamLeader = await _db.Users.Where(u => u.Id == entity.RequesterUserId)
                .Select(u => u.BypassTeamLeaderApproval).FirstOrDefaultAsync(ct);
            var teamId = await _db.Users.Where(u => u.Id == entity.RequesterUserId)
                .Select(u => u.TeamId).FirstOrDefaultAsync(ct);
            Guid? teamLeaderId = (!requesterBypassesTeamLeader && teamId is Guid tid)
                ? await _db.Teams.Where(t => t.Id == tid).Select(t => t.TeamLeaderId).FirstOrDefaultAsync(ct)
                : null;
            if (teamLeaderId is Guid tl)
                return tl == uid ? null : Result<LeaveRequestDto>.Failure(
                    "اعتماد خطوة قائد الفريق من صلاحية قائد فريق الموظّف الفعلي فقط.", "auth.forbidden");
            // لا قائد فريق محدّد ⇒ الطلب يُوجَّه للمدير عند الإنشاء فلا يصل هذه الخطوة عادةً؛
            // حارس دفاعي للحالات القديمة: ضمن النطاق الشخصي للمراجِع الإداري.
            var tlScope = await _scope.ResolveAsync(ct);
            return tlScope.Contains(entity.RequesterUserId) ? null : Result<LeaveRequestDto>.Failure(
                "الطلب خارج نطاق صلاحيتك.", "auth.forbidden");
        }

        // خطوة المدير في الطلب العادي — ضمن النطاق الشخصي للمراجِع (لم تتغيّر في T-WF2).
        var scope = await _scope.ResolveAsync(ct);
        if (!scope.Contains(entity.RequesterUserId))
            return Result<LeaveRequestDto>.Failure("الطلب خارج نطاق صلاحيتك.", "auth.forbidden");
        return null;
    }

    /// <summary>
    /// يحدّد هل يوجد «مدير تشغيليّ بديل» يمكنه اعتماد خطوة المدير للطلب (P2 — قرار طيّ خطوة المدير).
    /// يصعد سلسلة ManagerId التصاعدية من مقدّم الطلب باحثًا عن أوّل مستخدم:
    ///  (1) نشط IsActive، و(2) مختلف عن مُعتمِد خطوة قائد الفريق (<paramref name="excludeUserId"/>)،
    ///  و(3) يحمل دور Manager فعليًّا. أيّ مدير على السلسلة التصاعدية يشمل نطاقه (department = شجرة
    /// المرؤوسين) الموظّفَ بنيويًّا، فوجوده = بديل تشغيليّ حقيقيّ لخطوة المدير (scope-based).
    /// أدوار الشركة/الحوكمة العامة (Admin/CEO/GeneralManager/CeoSupport) لا تُعدّ بديلًا تشغيليًّا حسب P2
    /// (لأنها ليست دور Manager، فلا يلتقطها هذا البحث حتى لو ظهرت على السلسلة). يمنع الحلقات في ManagerId،
    /// ويتعامل بأمان مع ManagerId المفقود أو المستخدم غير النشط. يعيد true إن وُجد بديل (⇒ لا طيّ)، وإلا false.
    /// </summary>
    private async Task<bool> HasOperationalManagerAlternativeAsync(Guid requesterUserId, Guid excludeUserId, CancellationToken ct)
    {
        var managerRoleId = await _db.Roles.Where(r => r.Name == Roles.Manager)
            .Select(r => r.Id).FirstOrDefaultAsync(ct);
        if (managerRoleId == Guid.Empty) return false; // لا دور Manager معرّف ⇒ لا بديل تشغيليّ

        var visited = new HashSet<Guid> { requesterUserId };
        var currentId = requesterUserId;
        while (true)
        {
            var managerId = await _db.Users.Where(u => u.Id == currentId)
                .Select(u => u.ManagerId).FirstOrDefaultAsync(ct);
            if (managerId is not Guid mid) return false; // نهاية السلسلة الإدارية
            if (!visited.Add(mid)) return false;         // حلقة في ManagerId ⇒ توقّف آمن

            if (mid != excludeUserId) // لا يُعدّ مُعتمِد خطوة قائد الفريق بديلًا لنفسه
            {
                var isActive = await _db.Users.Where(u => u.Id == mid)
                    .Select(u => u.IsActive).FirstOrDefaultAsync(ct);
                if (isActive)
                {
                    var isManager = await _db.UserRoles
                        .AnyAsync(ur => ur.UserId == mid && ur.RoleId == managerRoleId, ct);
                    if (isManager) return true; // مدير تشغيليّ بديل داخل الشجرة
                }
            }

            currentId = mid; // اصعد خطوةً في السلسلة
        }
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

    // ===== الخصم/العكس الآلي للرصيد (V1.1) =====

    /// <summary>
    /// يسجّل حركة خصم (Debit) عند الاعتماد النهائي: الإجازة = عدد الأيام على رصيد الإجازات؛
    /// الاستئذان = وحدة السياسة (افتراضيًّا Count=1) على رصيد الأذونات. idempotent عبر (RelatedRequestId, Source).
    /// السنة من تاريخ بداية الطلب. لا يرمي عند الرصيد السالب (مسموح بتحذير).
    /// </summary>
    private async Task ApplyApprovalDeductionAsync(LeaveRequest entity, Guid actor, CancellationToken ct)
    {
        var (balanceType, source) = entity.Type == LeaveRequestType.Leave
            ? (BalanceType.AnnualLeave, BalanceSource.ApprovedLeave)
            : (BalanceType.Permission, BalanceSource.ApprovedPermission);

        var exists = await _db.EmployeeBalanceLedger
            .AnyAsync(e => e.RelatedRequestId == entity.Id && e.Source == source, ct);
        if (exists) return; // idempotent — لا تكرار للخصم لنفس الطلب

        decimal amount;
        if (entity.Type == LeaveRequestType.Leave)
        {
            amount = (entity.EndDate.DayNumber - entity.StartDate.DayNumber) + 1; // أيام شاملة
        }
        else
        {
            var jobRoleId = await _db.Users.Where(u => u.Id == entity.RequesterUserId)
                .Select(u => u.JobRoleId).FirstOrDefaultAsync(ct);
            var policy = await ResolvePolicyAsync(entity.StartDate.Year, jobRoleId, ct);
            // الوحدة الافتراضية Count = إذن واحد. (الساعات مدعومة في السياسة لكنها ليست وحدة V1.1 الافتراضية.)
            amount = 1;
            _ = policy; // السياسة تُحلّ للحدود/الوحدة مستقبلًا؛ V1.1 تعتمد Count=1.
        }

        _db.EmployeeBalanceLedger.Add(new EmployeeBalanceLedger
        {
            EmployeeId = entity.RequesterUserId,
            BalanceType = balanceType,
            Amount = amount,
            Direction = BalanceDirection.Debit,
            Source = source,
            RelatedRequestId = entity.Id,
            Year = entity.StartDate.Year,
            CreatedBy = actor
        });
    }

    /// <summary>
    /// يعكس الخصم الآلي بحركة Credit معاكسة (Source=Reversal) مطابِقة في المقدار والنوع والسنة، دون حذف الأصلية.
    /// idempotent عبر (RelatedRequestId, Reversal). إن لم يوجد خصم أصلي لا يفعل شيئًا.
    /// </summary>
    private async Task ApplyRevocationReversalAsync(LeaveRequest entity, Guid actor, string reason, CancellationToken ct)
    {
        var alreadyReversed = await _db.EmployeeBalanceLedger
            .AnyAsync(e => e.RelatedRequestId == entity.Id && e.Source == BalanceSource.Reversal, ct);
        if (alreadyReversed) return; // idempotent — عكس واحد لكل طلب

        var original = await _db.EmployeeBalanceLedger
            .Where(e => e.RelatedRequestId == entity.Id && e.Direction == BalanceDirection.Debit
                && (e.Source == BalanceSource.ApprovedLeave || e.Source == BalanceSource.ApprovedPermission))
            .FirstOrDefaultAsync(ct);
        if (original is null) return; // لا خصم لعكسه

        _db.EmployeeBalanceLedger.Add(new EmployeeBalanceLedger
        {
            EmployeeId = original.EmployeeId,
            BalanceType = original.BalanceType,
            Amount = original.Amount,
            Direction = BalanceDirection.Credit,
            Source = BalanceSource.Reversal,
            RelatedRequestId = entity.Id,
            Year = original.Year,
            Notes = reason,
            CreatedBy = actor
        });
    }

    private async Task<BalancePolicy?> ResolvePolicyAsync(int year, Guid? jobRoleId, CancellationToken ct)
    {
        var candidates = await _db.BalancePolicies.AsNoTracking()
            .Where(p => p.Year == year && (p.JobRoleId == null || p.JobRoleId == jobRoleId))
            .ToListAsync(ct);
        return candidates.FirstOrDefault(p => p.JobRoleId == jobRoleId) ?? candidates.FirstOrDefault(p => p.JobRoleId == null);
    }

    /// <summary>
    /// يَعُدّ أذونات الموظّف ضمن شهر تقويمي واحد (حسب StartDate). العدّ من leave_requests لا من Ledger
    /// لأن الشهر معرَّف على الطلب نفسه (StartDate)، بينما حركة Ledger تحمل السنة فقط ووقت الاعتماد؛
    /// كما أن الإبطال يحوّل الطلب إلى Cancelled فيُستبعَد تلقائيًّا هنا. هذا المصدر مرجعي قبل كتابة أي حركة.
    /// includePending=true عند الإنشاء (يحجز الفتحة مبكرًا)، false عند الاعتماد النهائي (المعتمَد فقط).
    /// </summary>
    private async Task<int> CountPermissionsInMonthAsync(
        Guid userId, int year, int month, bool includePending, Guid? excludeRequestId, CancellationToken ct)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var query = _db.LeaveRequests.AsNoTracking()
            .Where(r => r.RequesterUserId == userId
                && r.Type == LeaveRequestType.Permission
                && r.StartDate >= monthStart && r.StartDate <= monthEnd);
        if (excludeRequestId is Guid ex) query = query.Where(r => r.Id != ex);
        query = includePending
            ? query.Where(r => r.Status == LeaveRequestStatus.HrApproved || PendingStatuses.Contains(r.Status))
            : query.Where(r => r.Status == LeaveRequestStatus.HrApproved);
        return await query.CountAsync(ct);
    }

    /// <summary>
    /// حارس الحد الشهري للأذونات. يُحلّ السياسة المنطبقة على مسمّى الموظّف؛ إن لم يكن لها حدّ شهري
    /// (أو لا سياسة) فلا قيد. وإلا يَعُدّ أذونات الشهر ويرفض إن كانت إضافة إذن جديد ستتجاوز الحد.
    /// يُرجِع Failure عند التجاوز، وإلا null (مرّ الحارس).
    /// </summary>
    private async Task<Result<LeaveRequestDto>?> CheckPermissionMonthlyLimitAsync(
        Guid requesterId, DateOnly start, bool includePending, Guid? excludeRequestId, CancellationToken ct)
    {
        var jobRoleId = await _db.Users.Where(u => u.Id == requesterId)
            .Select(u => u.JobRoleId).FirstOrDefaultAsync(ct);
        var policy = await ResolvePolicyAsync(start.Year, jobRoleId, ct);
        if (policy?.PermissionMonthlyLimit is not decimal limit) return null;

        var used = await CountPermissionsInMonthAsync(requesterId, start.Year, start.Month, includePending, excludeRequestId, ct);
        if (used + 1 > limit)
            return Result<LeaveRequestDto>.Failure(
                "تم تجاوز الحد الشهري للأذونات لهذا الشهر.", "leave_request.permission_monthly_limit.conflict");
        return null;
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
            entity.BalanceAtRequest, entity.RequestedLeaveDays, entity.UncoveredLeaveDays,
            entity.IsPotentialUnpaidLeave, entity.EmployeeAcknowledgedUnpaidDeduction, entity.EmployeeAcknowledgedAtUtc,
            entity.PermissionShortfallResolution,
            events.Select(e => new LeaveRequestEventDto(
                e.Id, e.ActorUserId, names.GetValueOrDefault(e.ActorUserId), e.Action,
                e.Step, e.FromStatus, e.ToStatus, e.Comment, e.CreatedAtUtc)).ToList());
    }

    private static LeaveRequestListItemDto MapList(LeaveRequest r, Dictionary<Guid, string> names) =>
        new(r.Id, r.RequesterUserId, names.GetValueOrDefault(r.RequesterUserId, string.Empty),
            r.Type, r.StartDate, r.EndDate, r.StartTime, r.EndTime, r.Reason, r.Status, r.CurrentStep,
            r.IsHrRequest, r.Status == LeaveRequestStatus.HrApproved, r.IsPotentialUnpaidLeave, r.CreatedAtUtc);

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}
