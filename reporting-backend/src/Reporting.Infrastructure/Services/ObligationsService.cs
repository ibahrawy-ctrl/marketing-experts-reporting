using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Application.Obligations;
using Reporting.Application.Reports;
using Reporting.Application.Security;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// P2-HR-008 — التنفيذ الوحيد لمحرّك الالتزامات.
/// <para><b>ما يفوَّض ولا يُكرَّر:</b></para>
/// <list type="bullet">
/// <item>التزامات التقارير ⟵ <see cref="IExpectedSubmissionStatusResolver"/> كما هي (بما فيها أرضيّة الانطباق).</item>
/// <item>التقويم والدورات ومواعيد الاستحقاق بحسب الدور ⟵ <see cref="ReportingCalendarPolicy"/>.</item>
/// <item>منتقي إسناد القوالب ⟵ <see cref="IReportTemplateService"/> و<see cref="IKpiTemplateService"/>.</item>
/// </list>
/// <para><b>ما يضيفه هذا المحرّك حصرًا:</b> إعفاء الإجازة المعتمَدة (غائب عن المُشتقّ القائم عمدًا)،
/// والتزامات تقييم KPI (غير مغطّاة في أيّ مكان)، وتوحيد الشكل في عقد واحد.</para>
/// <para><b>لا كتابة ولا جدول موازٍ</b>: كلّ ما هنا قراءة واشتقاق لحظيّ.</para>
/// </summary>
public sealed class ObligationsService : IObligationsService
{
    private readonly AppDbContext _db;
    private readonly IExpectedSubmissionStatusResolver _expected;
    private readonly IKpiTemplateService _kpiTemplates;
    private readonly IScopeResolver _scope;
    private readonly ICurrentUser _currentUser;
    private readonly ISystemClock _clock;

    private const string NotFound = "obligations.not_found";
    private const string NotFoundMessage = "الموظّف غير موجود أو خارج نطاقك.";

    public ObligationsService(
        AppDbContext db,
        IExpectedSubmissionStatusResolver expected,
        IKpiTemplateService kpiTemplates,
        IScopeResolver scope,
        ICurrentUser currentUser,
        ISystemClock clock)
    {
        _db = db;
        _expected = expected;
        _kpiTemplates = kpiTemplates;
        _scope = scope;
        _currentUser = currentUser;
        _clock = clock;
    }

    // ===================== نقاط الدخول المخوَّلة =====================

    public async Task<Result<ObligationsResultDto>> GetForSelfAsync(
        ObligationsFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid me)
            return Result<ObligationsResultDto>.Failure(NotFoundMessage, NotFound);

        // المعرّف من التوكن حصرًا — أيّ userId في المرشِّح يُتجاهَل هنا بالكامل.
        return await BuildAsync(new[] { me }, filter, ct);
    }

    public async Task<Result<ObligationsResultDto>> GetForScopeAsync(
        ObligationsFilter filter, CancellationToken ct = default)
    {
        var scope = await _scope.ResolveAsync(ct);

        if (filter.UserId is Guid target)
        {
            // خارج النطاق ⇒ نفس استجابة «غير موجود» تمامًا، فلا يُستدلّ على وجود الموظّف.
            if (!scope.Contains(target))
                return Result<ObligationsResultDto>.Failure(NotFoundMessage, NotFound);
            return await BuildAsync(new[] { target }, filter, ct);
        }

        var userIds = scope.SeesAll
            ? await _db.Users.AsNoTracking().Select(u => u.Id).ToListAsync(ct)
            : scope.UserIds.ToList();

        return await BuildAsync(userIds, filter, ct);
    }

    private async Task<Result<ObligationsResultDto>> BuildAsync(
        IReadOnlyCollection<Guid> userIds, ObligationsFilter filter, CancellationToken ct)
    {
        var keys = ResolveCycleKeys(filter);
        if (keys.Count == 0 || userIds.Count == 0)
            return Result<ObligationsResultDto>.Success(new ObligationsResultDto(
                keys, new ObligationSummaryDto(0, 0, 0, 0, 0, 0), Array.Empty<ObligationDto>()));

        var items = await ComputeAsync(new ObligationQuery(userIds, keys, filter.Kind), ct);

        // العدّادات تُحسَب على المجموعة الكاملة قبل ترشيح العرض، فلا تتغيّر الأرقام بتغيّر المرشِّح.
        var summary = Summarize(items);

        if (filter.OnlyActionable)
            items = items.Where(i => i.State is ObligationState.Pending or ObligationState.Missing).ToList();

        var ordered = items
            .OrderByDescending(i => i.Missing)
            .ThenBy(i => i.DueAt)
            .ThenBy(i => i.SubjectFullName, StringComparer.Ordinal)
            .ToList();

        return Result<ObligationsResultDto>.Success(new ObligationsResultDto(keys, summary, ordered));
    }

    private static ObligationSummaryDto Summarize(IReadOnlyList<ObligationDto> items) => new(
        Expected: items.Count(i => i.Expected),
        Fulfilled: items.Count(i => i.Expected && i.Fulfilled),
        Pending: items.Count(i => i.State == ObligationState.Pending),
        Missing: items.Count(i => i.Missing),
        Late: items.Count(i => i.Expected && i.Late),
        Exempt: items.Count(i => i.State == ObligationState.Exempt));

    private IReadOnlyList<string> ResolveCycleKeys(ObligationsFilter filter)
    {
        var today = ReportingCalendarPolicy.RiyadhDate(_clock.UtcNow.UtcDateTime);

        if (!string.IsNullOrWhiteSpace(filter.FromCycleKey) && !string.IsNullOrWhiteSpace(filter.ToCycleKey)
            && ReportingCalendarPolicy.IsValidCycleKey(filter.FromCycleKey)
            && ReportingCalendarPolicy.IsValidCycleKey(filter.ToCycleKey))
        {
            var from = ReportingCalendarPolicy.CycleRange(filter.FromCycleKey!).Start;
            var to = ReportingCalendarPolicy.CycleRange(filter.ToCycleKey!).Start;
            if (to < from) (from, to) = (to, from);

            var keys = new List<string>();
            for (var d = from; d <= to && keys.Count < ObligationPolicy.MaxCycles; d = d.AddDays(7))
                keys.Add(ReportingCalendarPolicy.CycleKeyFor(d));
            return keys;
        }

        var count = Math.Clamp(filter.RecentCycles ?? ObligationPolicy.DefaultRecentCycles, 1, ObligationPolicy.MaxCycles);
        return ReportingCalendarPolicy.RecentCycleKeys(today, count).OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    // ===================== الاشتقاق الخام (بلا تخويل) =====================

    public async Task<IReadOnlyList<ObligationDto>> ComputeAsync(
        ObligationQuery query, CancellationToken ct = default)
    {
        var userIds = query.UserIds.Distinct().ToList();
        var keys = query.CycleKeys
            .Where(ReportingCalendarPolicy.IsValidCycleKey)
            .Distinct()
            .Take(ObligationPolicy.MaxCycles)
            .ToList();
        if (userIds.Count == 0 || keys.Count == 0) return Array.Empty<ObligationDto>();

        var today = ReportingCalendarPolicy.RiyadhDate(_clock.UtcNow.UtcDateTime);
        var leaves = await LoadApprovedLeavesAsync(userIds, keys, ct);

        var items = new List<ObligationDto>();

        if (query.Kind is null or ObligationKind.Report)
            items.AddRange(await ComputeReportObligationsAsync(userIds, keys, leaves, today, ct));

        if (query.Kind is null or ObligationKind.KpiEvaluation)
            items.AddRange(await ComputeKpiObligationsAsync(userIds, keys, leaves, today, ct));

        return items;
    }

    // ===================== التقارير: تفويض كامل للمُشتقّ القائم =====================

    private async Task<List<ObligationDto>> ComputeReportObligationsAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyList<string> keys,
        IReadOnlyDictionary<Guid, List<ObligationPolicy.DateSpan>> leaves,
        DateOnly today, CancellationToken ct)
    {
        var rows = await _expected.ResolveAsync(new ExpectedStatusQuery(userIds, keys, null), ct);
        var result = new List<ObligationDto>(rows.Count);

        foreach (var r in rows)
        {
            // «مُنجَز» = وصل إلى حالة لا تتطلّب فعلًا من الموظّف. المسودّة والمُعاد ليسا إنجازًا.
            var fulfilled = r.Status is ExpectedSubmissionStatus.Submitted
                or ExpectedSubmissionStatus.Approved
                or ExpectedSubmissionStatus.Closed;

            var userLeaves = leaves.TryGetValue(r.UserId, out var ls) ? ls : new List<ObligationPolicy.DateSpan>();
            var exemptByLeave = !fulfilled
                && ObligationPolicy.IsCoveredByApprovedLeave(r.PeriodStart, r.DueAt, userLeaves);

            // الموظّف غير النشط والدورة قبل الأرضيّة يصلان من المُشتقّ موسومَين — نحترم وسمه ولا نعيد حسابه.
            var isActive = r.Status != ExpectedSubmissionStatus.InactiveUser;
            var withinApplicability = r.ExclusionReasonCode != CycleExclusionReason.BeforeApplicabilityFloor;

            var outcome = ObligationPolicy.Derive(
                isAssigned: true,                    // المُشتقّ لا يُصدِر صفًّا أصلًا لمن لا إسناد له.
                isUserActive: isActive,
                isWithinApplicability: withinApplicability,
                isExemptByLeave: exemptByLeave,
                isFulfilled: fulfilled,
                dueAt: r.DueAt,
                fulfilledOn: null,                   // تاريخ التسليم غير مُعاد في العقد؛ التأخّر يأتي من المُشتقّ.
                today: today);

            // التأخّر للتقارير مصدره المُشتقّ الموحّد (UnifiedCycleStatusPolicy) لا حساب ثانٍ هنا.
            var late = outcome.Expected && r.IsLate;
            var lateBy = late ? Math.Max(r.DelayDays, outcome.LateByDays) : 0;

            result.Add(new ObligationDto(
                Kind: ObligationKind.Report,
                SubjectUserId: r.UserId,
                SubjectFullName: r.UserFullName,
                OwnerUserId: r.UserId,               // التقرير التزام على الموظّف نفسه.
                OwnerFullName: r.UserFullName,
                SourceKind: nameof(Reporting.Domain.Entities.Templates.ReportTemplateAssignment),
                SourceId: r.TemplateId,
                SourceName: r.TemplateName,
                PeriodKey: r.PeriodKey,
                PeriodStart: r.PeriodStart,
                PeriodEnd: r.PeriodEnd,
                DueAt: r.DueAt,
                Expected: outcome.Expected,
                Fulfilled: outcome.Fulfilled,
                Missing: outcome.Missing,
                Late: late,
                LateByDays: lateBy,
                State: outcome.State,
                ExemptionReason: outcome.ExemptionReason,
                StateLabel: outcome.Label,
                FulfilledAtUtc: null,
                ReferenceId: r.SubmissionId));
        }

        return result;
    }

    // ===================== تقييمات KPI: منطق جديد (غير مغطّى سابقًا) =====================

    private async Task<List<ObligationDto>> ComputeKpiObligationsAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyList<string> keys,
        IReadOnlyDictionary<Guid, List<ObligationPolicy.DateSpan>> leaves,
        DateOnly today, CancellationToken ct)
    {
        var assigned = await _kpiTemplates.ResolveAssignedTemplatesForUsersAsync(userIds, ct);
        var assignedIds = assigned.Values.SelectMany(x => x).Distinct().ToList();
        if (assignedIds.Count == 0) return new List<ObligationDto>();

        var templates = await _db.KpiTemplates.AsNoTracking()
            .Where(t => assignedIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Title, t.Cadence })
            .ToListAsync(ct);
        var templateById = templates.ToDictionary(t => t.Id);

        // نسب الإصدارات: التقييم يشير إلى نسخة، والالتزام يخصّ القالب ⇒ نربط النسخة بقالبها.
        var versions = await _db.KpiTemplateVersions.AsNoTracking()
            .Where(v => assignedIds.Contains(v.KpiTemplateId))
            .Select(v => new { v.Id, v.KpiTemplateId })
            .ToListAsync(ct);
        var templateByVersion = versions.ToDictionary(v => v.Id, v => v.KpiTemplateId);

        var users = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id, u.FullName, u.IsActive, u.ManagerId, u.KpiReviewerOverrideUserId
            })
            .ToListAsync(ct);
        var userById = users.ToDictionary(u => u.Id);

        // المالك = المُقيِّم: تجاوز المراجِع إن وُجد وإلّا المدير المباشر. لا يُخترَع مالك عند غيابهما.
        var ownerIds = users
            .Select(u => u.KpiReviewerOverrideUserId ?? u.ManagerId)
            .Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        var ownerNames = ownerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var rolesByUser = await UserPrimaryRolesAsync(ownerIds.Concat(userIds).Distinct().ToList(), ct);

        // مفاتيح الفترات المطلوبة لكلّ دوريّة (الربعيّ يُشتقّ من مرجع الثلاثاء فلا تُحتسب دورة لربعين).
        var weeklyKeys = keys.Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();
        var quarterlyKeys = keys.Select(ObligationPolicy.QuarterKeyForCycle)
            .Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();

        var allPeriodKeys = weeklyKeys.Concat(quarterlyKeys).Distinct().ToList();

        // استعلام واحد لكلّ التقييمات المعنيّة (لا N+1): المستخدمون × الفترات × نسب القوالب المُسنَدة.
        var versionIds = versions.Select(v => v.Id).ToList();
        var evaluations = versionIds.Count == 0
            ? new List<EvalRow>()
            : (await _db.KpiEvaluations.AsNoTracking()
                .Where(e => userIds.Contains(e.SubjectUserId)
                            && allPeriodKeys.Contains(e.PeriodKey)
                            && versionIds.Contains(e.KpiTemplateVersionId))
                .Select(e => new { e.Id, e.SubjectUserId, e.PeriodKey, e.Status, e.SubmittedAtUtc, e.KpiTemplateVersionId })
                .ToListAsync(ct))
                .Select(e => new EvalRow(e.Id, e.SubjectUserId, e.PeriodKey, e.Status, e.SubmittedAtUtc,
                    templateByVersion.GetValueOrDefault(e.KpiTemplateVersionId)))
                .ToList();

        var evalByKey = new Dictionary<(Guid User, Guid Template, string Period), EvalRow>();
        foreach (var g in evaluations.Where(e => e.TemplateId != Guid.Empty)
                     .GroupBy(e => (e.SubjectUserId, e.TemplateId, e.PeriodKey)))
        {
            // الممثِّل = الأكثر تقدّمًا في المراجعة.
            evalByKey[g.Key] = g.OrderByDescending(x => KpiProgressRank(x.Status)).First();
        }

        var result = new List<ObligationDto>();

        foreach (var userId in userIds)
        {
            if (!userById.TryGetValue(userId, out var u)) continue;
            if (!assigned.TryGetValue(userId, out var tplIds) || tplIds.Count == 0)
                continue; // لا إسناد ⇒ لا التزام ⇒ لا يُعَدّ ناقصًا أبدًا.

            var ownerId = u.KpiReviewerOverrideUserId ?? u.ManagerId;
            var ownerRole = ownerId is Guid oid
                ? RoleAccess.PrimaryRole(rolesByUser.GetValueOrDefault(oid) ?? new List<string>())
                : Roles.Manager;
            var userLeaves = leaves.TryGetValue(userId, out var ls) ? ls : new List<ObligationPolicy.DateSpan>();

            foreach (var tplId in tplIds)
            {
                if (!templateById.TryGetValue(tplId, out var tpl)) continue;

                var periodKeys = tpl.Cadence == KpiCadence.Quarterly ? quarterlyKeys : weeklyKeys;

                foreach (var periodKey in periodKeys)
                {
                    var (start, end, dueAt) = tpl.Cadence == KpiCadence.Quarterly
                        ? QuarterWindow(periodKey)
                        : WeeklyWindow(periodKey, ownerRole);

                    evalByKey.TryGetValue((userId, tplId, periodKey), out var ev);
                    var fulfilled = ev is not null && IsKpiFulfilled(ev.Status);
                    var fulfilledOn = ev?.SubmittedAtUtc is DateTime sa
                        ? ReportingCalendarPolicy.RiyadhDate(EnsureUtc(sa))
                        : (DateOnly?)null;

                    var exemptByLeave = !fulfilled
                        && ObligationPolicy.IsCoveredByApprovedLeave(start, dueAt, userLeaves);

                    var outcome = ObligationPolicy.Derive(
                        isAssigned: true,
                        isUserActive: u.IsActive,
                        isWithinApplicability: true,   // لا أرضيّة انطباق موثَّقة لـKPI — لا نخترع واحدة.
                        isExemptByLeave: exemptByLeave,
                        isFulfilled: fulfilled,
                        dueAt: dueAt,
                        fulfilledOn: fulfilledOn,
                        today: today);

                    result.Add(new ObligationDto(
                        Kind: ObligationKind.KpiEvaluation,
                        SubjectUserId: userId,
                        SubjectFullName: u.FullName,
                        OwnerUserId: ownerId,
                        OwnerFullName: ownerId is Guid o ? ownerNames.GetValueOrDefault(o) : null,
                        SourceKind: nameof(Reporting.Domain.Entities.Kpi.KpiTemplateAssignment),
                        SourceId: tplId,
                        SourceName: tpl.Title,
                        PeriodKey: periodKey,
                        PeriodStart: start,
                        PeriodEnd: end,
                        DueAt: dueAt,
                        Expected: outcome.Expected,
                        Fulfilled: outcome.Fulfilled,
                        Missing: outcome.Missing,
                        Late: outcome.Late,
                        LateByDays: outcome.LateByDays,
                        State: outcome.State,
                        ExemptionReason: outcome.ExemptionReason,
                        StateLabel: outcome.Label,
                        FulfilledAtUtc: ev?.SubmittedAtUtc,
                        ReferenceId: ev?.Id));
                }
            }
        }

        return result;
    }

    private static (DateOnly Start, DateOnly End, DateOnly Due) WeeklyWindow(string cycleKey, string ownerRole)
    {
        var (start, end) = ReportingCalendarPolicy.CycleRange(cycleKey);
        return (start, end, ReportingCalendarPolicy.RoleDueDate(cycleKey, ownerRole));
    }

    private static (DateOnly Start, DateOnly End, DateOnly Due) QuarterWindow(string quarterKey)
    {
        var (start, end) = ObligationPolicy.QuarterRange(quarterKey);
        return (start, end, ObligationPolicy.QuarterlyDueDate(quarterKey));
    }

    /// <summary>«مُنجَز» لتقييم KPI = خرج من يد المُقيِّم. المسودّة/الجاري/طلب التعديل ليست إنجازًا.</summary>
    private static bool IsKpiFulfilled(KpiEvaluationStatus s) => s is
        KpiEvaluationStatus.Submitted or KpiEvaluationStatus.Approved
        or KpiEvaluationStatus.Closed or KpiEvaluationStatus.UnderReview;

    private static int KpiProgressRank(KpiEvaluationStatus s) => s switch
    {
        KpiEvaluationStatus.Draft => 0,
        KpiEvaluationStatus.NeedsRevision => 1,
        KpiEvaluationStatus.InProgress => 2,
        KpiEvaluationStatus.Rejected => 3,
        KpiEvaluationStatus.Submitted => 4,
        KpiEvaluationStatus.UnderReview => 5,
        KpiEvaluationStatus.Approved => 6,
        KpiEvaluationStatus.Closed => 7,
        _ => -1
    };

    // ===================== الإجازات المعتمَدة =====================

    /// <summary>
    /// الإجازات المعتمَدة من الموارد البشريّة فقط (<see cref="LeaveRequestStatus.HrApproved"/>)
    /// والنوع «إجازة» فقط — الاستئذان بالساعات لا يُعفي من التزام دوريّ. استعلام واحد للجميع.
    /// </summary>
    private async Task<Dictionary<Guid, List<ObligationPolicy.DateSpan>>> LoadApprovedLeavesAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyList<string> keys, CancellationToken ct)
    {
        var windowStart = keys.Min(k => ReportingCalendarPolicy.CycleRange(k).Start);
        // نوسّع الطرف الأيمن بمهلة الاستحقاق الأقصى (ربعيّ) كي تُلتقَط إجازة تغطّي ما بعد نهاية الفترة.
        var windowEnd = keys.Max(k => ReportingCalendarPolicy.CycleRange(k).End)
            .AddDays(ObligationPolicy.QuarterlyEvaluationGraceDays + 92);

        var rows = await _db.LeaveRequests.AsNoTracking()
            .Where(l => userIds.Contains(l.RequesterUserId)
                        && l.Status == LeaveRequestStatus.HrApproved
                        && l.Type == LeaveRequestType.Leave
                        && l.EndDate >= windowStart
                        && l.StartDate <= windowEnd)
            .Select(l => new { l.RequesterUserId, l.StartDate, l.EndDate })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.RequesterUserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new ObligationPolicy.DateSpan(r.StartDate, r.EndDate)).ToList());
    }

    private async Task<Dictionary<Guid, List<string>>> UserPrimaryRolesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, List<string>>();
        var pairs = await (from ur in _db.UserRoles
                           join r in _db.Roles on ur.RoleId equals r.Id
                           where userIds.Contains(ur.UserId) && r.Name != null
                           select new { ur.UserId, r.Name }).ToListAsync(ct);
        return pairs.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());
    }

    private static DateTime EnsureUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private sealed record EvalRow(
        Guid Id, Guid SubjectUserId, string PeriodKey,
        KpiEvaluationStatus Status, DateTime? SubmittedAtUtc, Guid TemplateId);
}
