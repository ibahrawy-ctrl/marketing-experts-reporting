using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reporting.Application.Attendance;
using Reporting.Application.Common;
using Reporting.Application.HrOperations;
using Reporting.Application.Kpi;
using Reporting.Application.Obligations;
using Reporting.Application.Security;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// P2-HR-009 — التنفيذ الوحيد للوحة عمليّات الموارد البشريّة.
///
/// <para><b>ما لا يفعله هذا الصفّ عمدًا:</b></para>
/// <list type="bullet">
/// <item>لا يعيد حساب «مطلوب/ناقص/متأخّر» — يستهلك <see cref="IObligationsService"/> حصرًا.</item>
/// <item>لا يعيد حساب مهلة الحضور — يستدعي <see cref="AttendancePolicy.CurrentSlaDueAtUtc"/> نفسها.</item>
/// <item>لا يخزّن أيّ عدّاد ولا يكتب أيّ صفّ: كلّ ما هنا قراءة واشتقاق لحظيّ.</item>
/// <item>لا يعدّ أعضاء فريق ليجعل العدد «المطلوب» — المطلوب يأتي من الإسناد وحده.</item>
/// </list>
///
/// <para><b>البطاقة والتفصيل من مصدر واحد:</b> كلّ بطاقة تُبنى من نفس قائمة الصفوف التي يعيدها
/// تفصيلها تحت نفس المرشِّح ⇒ لا يمكن بنيويًّا أن يخالف رقمُ البطاقة عددَ صفوفها.</para>
/// </summary>
public sealed class HrOperationsService : IHrOperationsService
{
    private const string NotFound = "hrOperations.not_found";
    private const string NotFoundMessage = "العنصر غير موجود أو خارج نطاقك.";

    private readonly AppDbContext _db;
    private readonly IObligationsService _obligations;
    private readonly IKpiTemplateService _kpiTemplates;
    private readonly IScopeResolver _scope;
    private readonly ICurrentUser _currentUser;
    private readonly ISystemClock _clock;
    private readonly Phase2FeatureOptions _flags;

    public HrOperationsService(
        AppDbContext db,
        IObligationsService obligations,
        IKpiTemplateService kpiTemplates,
        IScopeResolver scope,
        ICurrentUser currentUser,
        ISystemClock clock,
        IOptions<Phase2FeatureOptions> flags)
    {
        _db = db;
        _obligations = obligations;
        _kpiTemplates = kpiTemplates;
        _scope = scope;
        _currentUser = currentUser;
        _clock = clock;
        _flags = flags.Value;
    }

    // ═══════════════════════════════ نقاط الدخول ═══════════════════════════════

    public async Task<Result<HrOperationsDashboardDto>> GetDashboardAsync(
        HrOperationsFilter filter, CancellationToken ct = default)
    {
        var built = await BuildAsync(filter, HrOperationsCatalog.All, ct);
        if (!built.Succeeded) return Result<HrOperationsDashboardDto>.Failure(built.Error!, built.ErrorCode);
        var ctx = built.Value!;

        var cards = HrOperationsCatalog.All.Select(q =>
        {
            var rows = ctx.Rows.TryGetValue(q, out var r) ? r : new List<HrOperationsRowDto>();
            var breached = rows.Count(x => x.SlaBreached);
            return new HrOperationsCardDto(
                q, HrOperationsCatalog.Key(q), HrOperationsCatalog.TitleAr(q), HrOperationsCatalog.GroupAr(q),
                rows.Count, breached,
                rows.Count == 0 ? 0 : rows.Max(x => x.AgeingDays),
                HrOperationsCatalog.Severity(rows.Count, breached));
        }).ToList();

        return Result<HrOperationsDashboardDto>.Success(new HrOperationsDashboardDto(
            ctx.PeriodKeys,
            new HrOperationsScopeDto(ctx.Scope.ScopeType, ctx.ScopeUserCount),
            cards));
    }

    public async Task<Result<HrOperationsQueueDto>> GetQueueAsync(
        HrOperationsQueue queue, HrOperationsFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var built = await BuildAsync(filter, new[] { queue }, ct);
        if (!built.Succeeded) return Result<HrOperationsQueueDto>.Failure(built.Error!, built.ErrorCode);

        var rows = built.Value!.Rows.TryGetValue(queue, out var r) ? r : new List<HrOperationsRowDto>();

        var p = HrOperationsPolicy.NormalizePage(page);
        var size = HrOperationsPolicy.NormalizePageSize(pageSize);

        return Result<HrOperationsQueueDto>.Success(new HrOperationsQueueDto(
            queue, HrOperationsCatalog.Key(queue), HrOperationsCatalog.TitleAr(queue),
            rows.Count, rows.Count(x => x.SlaBreached), p, size,
            rows.Skip((p - 1) * size).Take(size).ToList()));
    }

    public async Task<Result<HrOperationsExportDto>> ExportQueueAsync(
        HrOperationsQueue queue, HrOperationsFilter filter, CancellationToken ct = default)
    {
        var built = await BuildAsync(filter, new[] { queue }, ct);
        if (!built.Succeeded) return Result<HrOperationsExportDto>.Failure(built.Error!, built.ErrorCode);

        var rows = (built.Value!.Rows.TryGetValue(queue, out var r) ? r : new List<HrOperationsRowDto>())
            .Take(HrOperationsPolicy.MaxExportRows).ToList();

        var csv = BuildCsv(rows);
        var stamp = _clock.UtcNow.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        return Result<HrOperationsExportDto>.Success(new HrOperationsExportDto(
            $"hr-operations-{HrOperationsCatalog.Key(queue)}-{stamp}.csv",
            "text/csv; charset=utf-8",
            // BOM كي تفتح Excel العربيّة صحيحةً بلا خطوة استيراد يدويّة.
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(),
            rows.Count));
    }

    // ═══════════════════════════════ البناء الموحَّد ═══════════════════════════════

    private sealed record BuildContext(
        ScopeContext Scope,
        int ScopeUserCount,
        IReadOnlyList<string> PeriodKeys,
        Dictionary<HrOperationsQueue, List<HrOperationsRowDto>> Rows);

    /// <summary>
    /// يفرض النطاق مرّة واحدة، ثمّ يبني الطوابير المطلوبة فقط من مجموعات بيانات مشتركة.
    /// كلّ مجموعة تُحمَّل باستعلام واحد للمجموعة كلّها (لا استعلام لكلّ صفّ) ⇒ لا N+1.
    /// </summary>
    private async Task<Result<BuildContext>> BuildAsync(
        HrOperationsFilter filter, IReadOnlyCollection<HrOperationsQueue> queues, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid)
            return Result<BuildContext>.Failure(NotFoundMessage, NotFound);

        var scope = await _scope.ResolveAsync(ct);

        // موظّف مطلوب صراحةً خارج النطاق ⇒ نفس استجابة «غير موجود» تمامًا، فلا يُستدلّ على وجوده.
        if (filter.UserId is Guid target && !scope.Contains(target))
            return Result<BuildContext>.Failure(NotFoundMessage, NotFound);

        var directory = await LoadDirectoryAsync(scope, filter, ct);
        var userIds = directory.Keys.ToList();

        var now = _clock.UtcNow.UtcDateTime;
        var rows = HrOperationsCatalog.All.ToDictionary(q => q, _ => new List<HrOperationsRowDto>());

        // ===== الالتزامات: المصدر الوحيد للمطلوب/الناقص/المتأخّر (لا حساب ثانٍ هنا) =====
        IReadOnlyList<string> periodKeys = Array.Empty<string>();
        var needsObligations = queues.Any(q => q is HrOperationsQueue.ReportsMissing
            or HrOperationsQueue.ReportsLate or HrOperationsQueue.KpiEvaluationsMissing);

        if (needsObligations)
        {
            var result = await _obligations.GetForScopeAsync(new ObligationsFilter(
                UserId: filter.UserId,
                RecentCycles: filter.RecentCycles,
                FromCycleKey: filter.FromCycleKey,
                ToCycleKey: filter.ToCycleKey), ct);

            if (!result.Succeeded)
                return Result<BuildContext>.Failure(NotFoundMessage, NotFound);

            periodKeys = result.Value!.PeriodKeys;
            AddObligationRows(rows, result.Value!.Items, directory, queues);
        }

        if (queues.Contains(HrOperationsQueue.KpiEvaluationsAwaitingApproval))
            rows[HrOperationsQueue.KpiEvaluationsAwaitingApproval]
                .AddRange(await BuildKpiAwaitingApprovalAsync(userIds, directory, now, ct));

        if (queues.Contains(HrOperationsQueue.KpiCoverageInsufficient))
            rows[HrOperationsQueue.KpiCoverageInsufficient]
                .AddRange(await BuildKpiCoverageGapAsync(userIds, directory, now, ct));

        if (queues.Any(HrOperationsPolicy.IsAttendanceQueue) && _flags.AttendanceEnabled)
            AddAttendanceRows(rows, await BuildAttendanceRowsAsync(userIds, directory, now, ct));

        if (queues.Contains(HrOperationsQueue.RequestsAwaitingAction))
            rows[HrOperationsQueue.RequestsAwaitingAction]
                .AddRange(await BuildRequestsAsync(userIds, directory, now, ct));

        if (queues.Contains(HrOperationsQueue.FollowUpItems))
            rows[HrOperationsQueue.FollowUpItems]
                .AddRange(await BuildFollowUpAsync(userIds, directory, now, ct));

        // المرشِّحات والترتيب في نقطة واحدة ⇒ البطاقة والتفصيل يريان القائمة ذاتها حرفيًّا.
        var final = rows.ToDictionary(
            kv => kv.Key,
            kv => HrOperationsPolicy.Order(kv.Value.Where(r => HrOperationsPolicy.Matches(r, filter))).ToList());

        return Result<BuildContext>.Success(
            new BuildContext(scope, directory.Count, periodKeys, final));
    }

    // ═══════════════════════════════ دليل المستخدمين ═══════════════════════════════

    private sealed record DirectoryRow(
        Guid Id, string FullName, bool IsActive,
        Guid? TeamId, string? TeamName, Guid? DepartmentId, string? DepartmentName, Guid? ManagerId);

    /// <summary>
    /// دليل مستخدمي النطاق باستعلام واحد. مرشِّحا الإدارة/الفريق يضيّقان هنا مبكرًا
    /// كي لا تُحمَّل بيانات لن تُعرَض أصلًا — <b>ولا يوسّعان النطاق أبدًا</b>.
    /// </summary>
    private async Task<Dictionary<Guid, DirectoryRow>> LoadDirectoryAsync(
        ScopeContext scope, HrOperationsFilter filter, CancellationToken ct)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();

        if (!scope.SeesAll)
        {
            var ids = scope.UserIds.ToList();
            query = query.Where(u => ids.Contains(u.Id));
        }

        if (filter.UserId is Guid u) query = query.Where(x => x.Id == u);
        if (filter.TeamId is Guid t) query = query.Where(x => x.TeamId == t);
        if (filter.DepartmentId is Guid d) query = query.Where(x => x.DepartmentId == d);

        var rows = await query
            .Select(x => new DirectoryRow(
                x.Id, x.FullName, x.IsActive,
                x.TeamId, _db.Teams.Where(tm => tm.Id == x.TeamId).Select(tm => tm.NameAr).FirstOrDefault(),
                x.DepartmentId, _db.Departments.Where(dp => dp.Id == x.DepartmentId).Select(dp => dp.NameAr).FirstOrDefault(),
                x.ManagerId))
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id);
    }

    private HrOperationsRowDto Row(
        HrOperationsQueue queue, Guid entityId, string entityType, DirectoryRow subject,
        string titleAr, string typeAr, string statusAr, string? periodKey,
        DateOnly? dueAt, DateTime? slaDueAtUtc, DateTime createdAtUtc, DateTime now,
        Guid? ownerUserId, string? ownerFullName, DateTime? lastActionAtUtc) =>
        new(queue, entityId, entityType, subject.Id, subject.FullName,
            subject.DepartmentId, subject.DepartmentName, subject.TeamId, subject.TeamName,
            titleAr, typeAr, statusAr, periodKey, dueAt, slaDueAtUtc,
            HrOperationsPolicy.IsBreached(slaDueAtUtc, now),
            HrOperationsPolicy.AgeingDays(createdAtUtc, now),
            ownerUserId, ownerFullName,
            HrOperationsPolicy.NextActionAr(queue), lastActionAtUtc);

    // ═══════════════════════════════ (1)(2)(3) الالتزامات ═══════════════════════════════

    private void AddObligationRows(
        Dictionary<HrOperationsQueue, List<HrOperationsRowDto>> rows,
        IReadOnlyList<ObligationDto> items,
        IReadOnlyDictionary<Guid, DirectoryRow> directory,
        IReadOnlyCollection<HrOperationsQueue> queues)
    {
        var now = _clock.UtcNow.UtcDateTime;

        foreach (var o in items)
        {
            if (!directory.TryGetValue(o.SubjectUserId, out var subject)) continue;

            // موعد الالتزام تاريخٌ لا لحظة ⇒ يُحمَل في DueAt، ويبقى SlaDueAtUtc فارغًا.
            // «الخرق» هنا يأتي من المحرّك نفسه (Missing/Late) لا من مقارنة زمنيّة ثانية.
            HrOperationsRowDto Build(HrOperationsQueue q, bool breached) => new(
                q, o.ReferenceId ?? o.SourceId ?? o.SubjectUserId,
                o.Kind == ObligationKind.Report ? "ReportObligation" : "KpiObligation",
                subject.Id, subject.FullName,
                subject.DepartmentId, subject.DepartmentName, subject.TeamId, subject.TeamName,
                o.SourceName, o.Kind == ObligationKind.Report ? "تقرير" : "تقييم أداء",
                o.StateLabel, o.PeriodKey, o.DueAt, null, breached,
                o.LateByDays, o.OwnerUserId, o.OwnerFullName,
                HrOperationsPolicy.NextActionAr(q), o.FulfilledAtUtc);

            if (o.Kind == ObligationKind.Report)
            {
                if (o.Missing && queues.Contains(HrOperationsQueue.ReportsMissing))
                    rows[HrOperationsQueue.ReportsMissing].Add(Build(HrOperationsQueue.ReportsMissing, true));

                // «متأخّر» أوسع من «ناقص» عمدًا: يشمل ما سُلِّم بعد الموعد وما لم يُسلَّم بعده.
                // الطابوران عدستان على المجموعة نفسها لا دلوان منفصلان — وكلّ رقم يطابق تفصيله.
                if (o.Expected && o.Late && queues.Contains(HrOperationsQueue.ReportsLate))
                    rows[HrOperationsQueue.ReportsLate].Add(Build(HrOperationsQueue.ReportsLate, true));
            }
            else if (o.Expected && !o.Fulfilled && queues.Contains(HrOperationsQueue.KpiEvaluationsMissing))
            {
                rows[HrOperationsQueue.KpiEvaluationsMissing]
                    .Add(Build(HrOperationsQueue.KpiEvaluationsMissing, o.Missing));
            }
        }

        _ = now;
    }

    // ═══════════════════════════════ (4) تقييمات تنتظر الاعتماد ═══════════════════════════════

    private async Task<List<HrOperationsRowDto>> BuildKpiAwaitingApprovalAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyDictionary<Guid, DirectoryRow> directory,
        DateTime now, CancellationToken ct)
    {
        if (userIds.Count == 0) return new List<HrOperationsRowDto>();

        var rows = await _db.KpiEvaluations.AsNoTracking()
            .Where(e => userIds.Contains(e.SubjectUserId)
                        && (e.Status == KpiEvaluationStatus.Submitted
                            || e.Status == KpiEvaluationStatus.UnderReview))
            .Select(e => new
            {
                e.Id, e.SubjectUserId, e.PeriodKey, e.Status, e.ReviewerId,
                e.SubmittedAtUtc, e.CreatedAtUtc, e.UpdatedAtUtc,
                Title = _db.KpiTemplateVersions
                    .Where(v => v.Id == e.KpiTemplateVersionId)
                    .Select(v => v.KpiTemplate!.Title).FirstOrDefault()
            })
            .ToListAsync(ct);

        var reviewerNames = await NamesAsync(rows.Select(r => r.ReviewerId), ct);

        return rows
            .Where(r => directory.ContainsKey(r.SubjectUserId))
            .Select(r => Row(
                HrOperationsQueue.KpiEvaluationsAwaitingApproval, r.Id, "KpiEvaluation",
                directory[r.SubjectUserId], r.Title ?? "—", "تقييم أداء",
                r.Status == KpiEvaluationStatus.Submitted ? "مُرسَل" : "قيد المراجعة",
                r.PeriodKey, null, null, r.SubmittedAtUtc ?? r.CreatedAtUtc, now,
                r.ReviewerId, r.ReviewerId is Guid rv ? reviewerNames.GetValueOrDefault(rv) : null,
                r.UpdatedAtUtc ?? r.SubmittedAtUtc))
            .ToList();
    }

    // ═══════════════════════════════ (5) تغطية تقييم غير كافية ═══════════════════════════════

    /// <summary>
    /// «تغطية غير كافية» = موظّف نشط <b>بلا أيّ قالب تقييم مُسنَد</b> ⇒ لا يمكن تقييمه أصلًا.
    /// <para>
    /// هذا الطابور <b>ليس</b> طابور نقص: الموظّف بلا إسناد لا يُعَدّ متأخّرًا في أيّ مكان
    /// (<see cref="ObligationPolicy"/> يمنع ذلك بنيويًّا) — الطابور يوجَّه إلى <b>مَن يُسنِد</b> لا إلى الموظّف.
    /// </para>
    /// </summary>
    private async Task<List<HrOperationsRowDto>> BuildKpiCoverageGapAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyDictionary<Guid, DirectoryRow> directory,
        DateTime now, CancellationToken ct)
    {
        var active = directory.Values.Where(d => d.IsActive).Select(d => d.Id).ToList();
        if (active.Count == 0) return new List<HrOperationsRowDto>();

        var assigned = await _kpiTemplates.ResolveAssignedTemplatesForUsersAsync(active, ct);

        var uncovered = active
            .Where(id => !assigned.TryGetValue(id, out var tpl) || tpl.Count == 0)
            .ToList();
        if (uncovered.Count == 0) return new List<HrOperationsRowDto>();

        var managerNames = await NamesAsync(uncovered.Select(id => directory[id].ManagerId), ct);

        return uncovered.Select(id =>
        {
            var d = directory[id];
            return Row(
                HrOperationsQueue.KpiCoverageInsufficient, id, "User", d,
                "لا قالب تقييم مُسنَد", "تغطية أداء", "غير مُغطّى", null, null, null,
                now, now, d.ManagerId,
                d.ManagerId is Guid m ? managerNames.GetValueOrDefault(m) : null, null);
        }).ToList();
    }

    // ═══════════════════════════════ (6)(7)(8)(9) الحضور ═══════════════════════════════

    private sealed record AttendanceRow(HrOperationsQueue Queue, HrOperationsRowDto Dto);

    private static void AddAttendanceRows(
        Dictionary<HrOperationsQueue, List<HrOperationsRowDto>> rows, IEnumerable<AttendanceRow> built)
    {
        foreach (var r in built) rows[r.Queue].Add(r.Dto);
    }

    private async Task<List<AttendanceRow>> BuildAttendanceRowsAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyDictionary<Guid, DirectoryRow> directory,
        DateTime now, CancellationToken ct)
    {
        if (userIds.Count == 0) return new List<AttendanceRow>();

        var open = new[]
        {
            AttendanceIncidentStatus.AwaitingEmployee,
            AttendanceIncidentStatus.EmployeeResponseTimedOut,
            AttendanceIncidentStatus.Acknowledged,
            AttendanceIncidentStatus.Disputed,
            AttendanceIncidentStatus.AwaitingHr,
            AttendanceIncidentStatus.Corrected
        };

        var incidents = await _db.AttendanceIncidents.AsNoTracking()
            .Where(i => userIds.Contains(i.SubjectUserId) && open.Contains(i.Status))
            .Join(_db.AttendanceIncidentTypes.AsNoTracking(), i => i.IncidentTypeId, t => t.Id,
                (i, t) => new
                {
                    i.Id, i.SubjectUserId, i.Status, i.IncidentDate, i.CreatedAtUtc, i.UpdatedAtUtc,
                    i.ReviewedByUserId, TypeName = t.NameAr
                })
            .ToListAsync(ct);

        var result = new List<AttendanceRow>(incidents.Count);

        foreach (var i in incidents)
        {
            if (!directory.TryGetValue(i.SubjectUserId, out var subject)) continue;

            var lastChange = i.UpdatedAtUtc ?? i.CreatedAtUtc;
            var slaDue = AttendancePolicy.CurrentSlaDueAtUtc(
                i.Status, lastChange,
                _flags.AttendanceEmployeeResponseHours, _flags.AttendanceHrReviewWorkingDays);

            var breached = HrOperationsPolicy.IsBreached(slaDue, now);

            // انقضاء نافذة الردّ حالةٌ مسجَّلة لا مهلةٌ جارية ⇒ تُصنَّف خرقًا صراحةً لا بمقارنة زمنيّة.
            var employeeStage = i.Status is AttendanceIncidentStatus.AwaitingEmployee
                or AttendanceIncidentStatus.EmployeeResponseTimedOut;
            var timedOut = i.Status == AttendanceIncidentStatus.EmployeeResponseTimedOut;

            var queue = employeeStage
                ? (breached || timedOut
                    ? HrOperationsQueue.AttendanceEmployeeSlaBreached
                    : HrOperationsQueue.AttendanceAwaitingEmployee)
                : (breached
                    ? HrOperationsQueue.AttendanceHrSlaBreached
                    : HrOperationsQueue.AttendanceAwaitingHr);

            var dto = new HrOperationsRowDto(
                queue, i.Id, "AttendanceIncident", subject.Id, subject.FullName,
                subject.DepartmentId, subject.DepartmentName, subject.TeamId, subject.TeamName,
                i.TypeName, "واقعة حضور", AttendanceTransitions.StatusAr(i.Status),
                null, i.IncidentDate, slaDue, breached || timedOut,
                HrOperationsPolicy.AgeingDays(i.CreatedAtUtc, now),
                employeeStage ? subject.Id : i.ReviewedByUserId,
                employeeStage ? subject.FullName : null,
                HrOperationsPolicy.NextActionAr(queue), i.UpdatedAtUtc);

            result.Add(new AttendanceRow(queue, dto));
        }

        return result;
    }

    // ═══════════════════════════════ (10) طلبات تنتظر إجراءً ═══════════════════════════════

    private async Task<List<HrOperationsRowDto>> BuildRequestsAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyDictionary<Guid, DirectoryRow> directory,
        DateTime now, CancellationToken ct)
    {
        if (userIds.Count == 0) return new List<HrOperationsRowDto>();

        var inFlight = new[]
        {
            LeaveRequestStatus.Submitted,
            LeaveRequestStatus.TeamLeaderApproved,
            LeaveRequestStatus.ManagerApproved
        };

        var leaves = await _db.LeaveRequests.AsNoTracking()
            .Where(l => userIds.Contains(l.RequesterUserId) && inFlight.Contains(l.Status))
            .Select(l => new
            {
                l.Id, l.RequesterUserId, l.Type, l.Status, l.CurrentStep,
                l.StartDate, l.CreatedAtUtc, l.UpdatedAtUtc,
                l.TeamLeaderReviewerId, l.ManagerReviewerId, l.HrReviewerId
            })
            .ToListAsync(ct);

        var services = await _db.EmployeeServiceRequests.AsNoTracking()
            .Where(r => userIds.Contains(r.RequesterUserId)
                        && (r.Status == EmployeeServiceRequestStatus.Submitted
                            || r.Status == EmployeeServiceRequestStatus.InReview))
            .Select(r => new
            {
                r.Id, r.RequesterUserId, r.RequestType, r.Title, r.Status,
                r.CreatedAtUtc, r.UpdatedAtUtc, r.AssignedToHrUserId
            })
            .ToListAsync(ct);

        // المالك = مراجِع الخطوة الحاليّة، لا مَن راجع سابقًا ولا مَن سيراجع لاحقًا.
        var ownerIds = leaves.Select(l => CurrentLeaveReviewer(l.CurrentStep, l.TeamLeaderReviewerId, l.ManagerReviewerId, l.HrReviewerId))
            .Concat(services.Select(s => s.AssignedToHrUserId));
        var names = await NamesAsync(ownerIds, ct);

        var result = new List<HrOperationsRowDto>(leaves.Count + services.Count);

        foreach (var l in leaves)
        {
            if (!directory.TryGetValue(l.RequesterUserId, out var subject)) continue;
            var owner = CurrentLeaveReviewer(l.CurrentStep, l.TeamLeaderReviewerId, l.ManagerReviewerId, l.HrReviewerId);
            result.Add(Row(
                HrOperationsQueue.RequestsAwaitingAction, l.Id, "LeaveRequest", subject,
                l.Type == LeaveRequestType.Leave ? "طلب إجازة" : "طلب استئذان",
                l.Type == LeaveRequestType.Leave ? "إجازة" : "استئذان",
                LeaveStepAr(l.CurrentStep), null, l.StartDate, null,
                l.CreatedAtUtc, now, owner,
                owner is Guid o ? names.GetValueOrDefault(o) : null, l.UpdatedAtUtc));
        }

        foreach (var s in services)
        {
            if (!directory.TryGetValue(s.RequesterUserId, out var subject)) continue;
            result.Add(Row(
                HrOperationsQueue.RequestsAwaitingAction, s.Id, "EmployeeServiceRequest", subject,
                s.Title, "طلب خدمة",
                s.Status == EmployeeServiceRequestStatus.Submitted ? "مُقدَّم" : "قيد المعالجة",
                null, null, null, s.CreatedAtUtc, now, s.AssignedToHrUserId,
                s.AssignedToHrUserId is Guid a ? names.GetValueOrDefault(a) : null, s.UpdatedAtUtc));
        }

        return result;
    }

    private static Guid? CurrentLeaveReviewer(
        LeaveRequestStep step, Guid? teamLeader, Guid? manager, Guid? hr) => step switch
    {
        LeaveRequestStep.TeamLeader => teamLeader,
        LeaveRequestStep.Manager => manager,
        LeaveRequestStep.Hr => hr,
        _ => null
    };

    private static string LeaveStepAr(LeaveRequestStep step) => step switch
    {
        LeaveRequestStep.Employee => "لدى الموظّف",
        LeaveRequestStep.TeamLeader => "لدى قائد الفريق",
        LeaveRequestStep.Manager => "لدى المدير",
        LeaveRequestStep.Hr => "لدى الموارد البشريّة",
        LeaveRequestStep.Completed => "مكتمل",
        _ => step.ToString()
    };

    // ═══════════════════════════════ (11) بنود تحتاج متابعة ═══════════════════════════════

    /// <summary>
    /// خطط التحسين المفتوحة وبنود إجراء الحوكمة المفتوحة.
    /// <para><b>البند الحسّاس يغيب تمامًا</b> لمن لا يملك <c>ManagementConfidential</c> —
    /// لا يُعرَض مقنّعًا ولا يُحتسَب في عدّاد البطاقة، لأنّ العدّ نفسه تسريب.</para>
    /// </summary>
    private async Task<List<HrOperationsRowDto>> BuildFollowUpAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyDictionary<Guid, DirectoryRow> directory,
        DateTime now, CancellationToken ct)
    {
        if (userIds.Count == 0) return new List<HrOperationsRowDto>();

        var canSeeSensitive = _currentUser.HasPermission(AppPermissions.ManagementConfidentialRead);

        var plans = await _db.ImprovementPlans.AsNoTracking()
            .Where(p => userIds.Contains(p.SubjectUserId)
                        && (p.Status == ImprovementPlanStatus.Open
                            || p.Status == ImprovementPlanStatus.InProgress))
            .Select(p => new { p.Id, p.SubjectUserId, p.Title, p.Status, p.OwnerId, p.DueDateUtc, p.CreatedAtUtc, p.UpdatedAtUtc })
            .ToListAsync(ct);

        var actions = await _db.GovernanceActionItems.AsNoTracking()
            .Where(a => a.AssignedToUserId != null
                        && userIds.Contains(a.AssignedToUserId.Value)
                        && (!a.IsSensitive || canSeeSensitive)
                        && (a.Status == ActionItemStatus.Open
                            || a.Status == ActionItemStatus.InProgress
                            || a.Status == ActionItemStatus.Blocked))
            .Select(a => new { a.Id, a.AssignedToUserId, a.Title, a.Status, a.DueDate, a.CreatedAtUtc, a.UpdatedAtUtc })
            .ToListAsync(ct);

        var names = await NamesAsync(plans.Select(p => (Guid?)p.OwnerId), ct);
        var result = new List<HrOperationsRowDto>(plans.Count + actions.Count);

        foreach (var p in plans)
        {
            if (!directory.TryGetValue(p.SubjectUserId, out var subject)) continue;
            result.Add(Row(
                HrOperationsQueue.FollowUpItems, p.Id, "ImprovementPlan", subject,
                p.Title, "خطّة تحسين",
                p.Status == ImprovementPlanStatus.Open ? "مفتوحة" : "قيد التنفيذ",
                null, p.DueDateUtc is DateTime due ? DateOnly.FromDateTime(due) : null, null,
                p.CreatedAtUtc, now, p.OwnerId, names.GetValueOrDefault(p.OwnerId), p.UpdatedAtUtc));
        }

        foreach (var a in actions)
        {
            if (!directory.TryGetValue(a.AssignedToUserId!.Value, out var subject)) continue;
            result.Add(Row(
                HrOperationsQueue.FollowUpItems, a.Id, "GovernanceActionItem", subject,
                a.Title, "بند حوكمة", ActionStatusAr(a.Status), null, a.DueDate, null,
                a.CreatedAtUtc, now, a.AssignedToUserId, subject.FullName, a.UpdatedAtUtc));
        }

        return result;
    }

    private static string ActionStatusAr(ActionItemStatus s) => s switch
    {
        ActionItemStatus.Open => "مفتوح",
        ActionItemStatus.InProgress => "قيد التنفيذ",
        ActionItemStatus.Blocked => "متعثّر",
        _ => s.ToString()
    };

    // ═══════════════════════════════ أدوات ═══════════════════════════════

    /// <summary>أسماء دفعة واحدة — استعلام واحد لكلّ الطابور لا استعلام لكلّ صفّ.</summary>
    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        if (list.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.AsNoTracking()
            .Where(u => list.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static string BuildCsv(IReadOnlyList<HrOperationsRowDto> rows)
    {
        var sb = new StringBuilder();
        // المعرّف عمود أوّل عمدًا: بدونه يصير الملفّ قائمة أسماء لا يمكن ردّها إلى مصدرها،
        // فيُتابَع البند بالاسم — وهو بالضبط ما يُنتج إجراءً على الشخص الخطأ.
        sb.AppendLine(string.Join(',', new[]
        {
            "المعرّف", "الطابور", "الموظّف", "الإدارة", "الفريق", "العنوان", "النوع", "الحالة",
            "الفترة", "الاستحقاق", "خرق المهلة", "التقادم بالأيّام", "المسؤول", "الإجراء التالي"
        }));

        foreach (var r in rows)
            sb.AppendLine(string.Join(',', new[]
            {
                Csv(r.EntityId.ToString()),
                Csv(HrOperationsCatalog.TitleAr(r.Queue)), Csv(r.SubjectFullName),
                Csv(r.DepartmentName), Csv(r.TeamName), Csv(r.TitleAr), Csv(r.TypeAr), Csv(r.StatusAr),
                Csv(r.PeriodKey), Csv(r.DueAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Csv(r.SlaBreached ? "نعم" : "لا"),
                Csv(r.AgeingDays.ToString(CultureInfo.InvariantCulture)),
                Csv(r.OwnerFullName), Csv(r.NextActionAr)
            }));

        return sb.ToString();
    }

    /// <summary>
    /// تهريب حقل CSV. البادئة <c>'</c> أمام الرموز الحسابيّة تمنع تفسير الخليّة كصيغة
    /// في Excel (حقن الصيغ) — الحقل بيانٌ لا برنامج.
    /// </summary>
    private static string Csv(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Length > 0 && (v[0] is '=' or '+' or '-' or '@')) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}
