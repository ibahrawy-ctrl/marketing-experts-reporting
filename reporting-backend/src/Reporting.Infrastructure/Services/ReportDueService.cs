using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// RPT-DUE1 — محرّك مواعيد التقارير والتأخّر (محسوب عند الطلب، بلا جدول/هجرة). قراءة فقط: لا إرسال بريد،
/// لا إنشاء إشعارات، لا تعديل أيّ تسليم أو سير عمل. كلّ التواريخ/الاستحقاق/التأخّر بتوقيت الرياض عبر
/// ReportCalendarPolicy. يميّز الأسبوعي عن اليومي (مبيعات B2C/B2B) عبر ReportCadencePolicy؛ القاعدة مبنيّة
/// على (نوع التقرير/القالب + الدور + الفريق/الإدارة) لا الدور وحده. النطاق مفروض بـ ScopeResolver، عدا
/// my-status فهو self-only متاح لأيّ مستخدم موثَّق. لا يستهلك ولا يعدّل ReportingService إطلاقًا.
/// </summary>
public class ReportDueService : IReportDueService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;

    // حالات «أُرسِل التقرير» — تُعدّ تسليمًا قائمًا (أيّ حالة بعد المسودّة). مطابقة لـ ReportingService.
    private static readonly SubmissionStatus[] SubmittedStatuses =
    {
        SubmissionStatus.Submitted, SubmissionStatus.Returned, SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel, SubmissionStatus.Escalated, SubmissionStatus.Closed,
        SubmissionStatus.Visible
    };

    // حالات «بانتظار اعتماد» — تقرير قد يكون عالقًا عند معتمِد. مطابقة لـ ReportingService.
    private static readonly SubmissionStatus[] PendingApprovalStatuses =
    {
        SubmissionStatus.Submitted, SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel, SubmissionStatus.Escalated
    };

    public ReportDueService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
    }

    // ===== self-only: حالة تقرير الأسبوع الحالي للمستخدم نفسه (متاح للموظّف) =====
    public async Task<Result<ReportDueMyStatus>> MyStatusAsync(string? weekKey, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid uid)
            return Result<ReportDueMyStatus>.Failure("غير مصرّح.", "auth.unauthenticated");

        var key = NormalizeWeekKey(weekKey);
        var (start, end) = ReportCalendarPolicy.WeekRange(key);
        var today = ReportCalendarPolicy.RiyadhToday();

        var me = await _db.Users.AsNoTracking()
            .Where(u => u.Id == uid)
            .Select(u => new { u.Id, u.FullName, u.JobRoleId })
            .FirstOrDefaultAsync(ct);
        if (me is null)
            return Result<ReportDueMyStatus>.Failure("المستخدم غير موجود.", "user.not_found");

        var primaryRole = RoleAccess.PrimaryRole(_currentUser.Roles);
        var reportingRoleIds = await ReportingRoleIdsAsync(ct);
        var expected = me.JobRoleId is Guid jrid && reportingRoleIds.Contains(jrid);

        if (!expected)
        {
            return Result<ReportDueMyStatus>.Success(new ReportDueMyStatus(
                key, ReportCalendarPolicy.WeekLabel(key), start, end, end,
                Expected: false, Submitted: false, IsOverdue: false,
                DelayType.NoDelay, "لا يُتوقَّع منك تقرير أسبوعي ضمن هذا المسار.", null));
        }

        var code = me.JobRoleId is Guid jc ? await JobRoleCodeAsync(jc, ct) : null;
        var cadence = ReportCadencePolicy.ExpectedCadence(code);

        if (cadence == PeriodType.Daily)
        {
            var dates = DailyExpectedDates(key, today);
            var dateKeys = dates.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList();
            var submittedDays = dateKeys.Count == 0
                ? new HashSet<string>()
                : (await _db.ReportSubmissions.AsNoTracking()
                    .Where(s => s.SubmitterId == uid && s.PeriodType == PeriodType.Daily
                                && dateKeys.Contains(s.PeriodKey) && SubmittedStatuses.Contains(s.Status))
                    .Select(s => s.PeriodKey).ToListAsync(ct)).ToHashSet();

            var submittedCount = dates.Count(d => submittedDays.Contains(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            var overdue = dates.Any(d => today > d && !submittedDays.Contains(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            var dueDay = dates.Count > 0 ? dates[^1] : end;
            var label = $"سلّمت {submittedCount} من {dates.Count} يوم عمل" + (overdue ? " (يوجد تأخّر)" : "");

            return Result<ReportDueMyStatus>.Success(new ReportDueMyStatus(
                key, ReportCalendarPolicy.WeekLabel(key), start, end, dueDay,
                Expected: true, Submitted: dates.Count > 0 && submittedCount >= dates.Count,
                IsOverdue: overdue, overdue ? DelayType.EmployeeReportNotSubmitted : DelayType.NoDelay,
                label, null));
        }

        // أسبوعي: وحدة واحدة، موعدها بحسب دور المستخدم.
        var due = ReportCalendarPolicy.DueDateForRole(key, primaryRole);
        var sub = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => s.SubmitterId == uid && s.PeriodType == PeriodType.Weekly
                        && s.PeriodKey == key && SubmittedStatuses.Contains(s.Status))
            .OrderBy(s => s.SubmittedAtUtc ?? DateTime.MaxValue)
            .Select(s => new { s.Id, s.SubmittedAtUtc })
            .FirstOrDefaultAsync(ct);

        if (sub is not null)
        {
            var submittedDate = sub.SubmittedAtUtc is DateTime at ? ReportCalendarPolicy.RiyadhDate(at) : today;
            var late = submittedDate > due;
            return Result<ReportDueMyStatus>.Success(new ReportDueMyStatus(
                key, ReportCalendarPolicy.WeekLabel(key), start, end, due,
                Expected: true, Submitted: true, IsOverdue: false, DelayType.NoDelay,
                late ? "سُلّم متأخرًا" : "سُلّم في الموعد", sub.Id));
        }

        var isOverdue = today > due;
        return Result<ReportDueMyStatus>.Success(new ReportDueMyStatus(
            key, ReportCalendarPolicy.WeekLabel(key), start, end, due,
            Expected: true, Submitted: false, IsOverdue: isOverdue,
            isOverdue ? DelayType.EmployeeReportNotSubmitted : DelayType.NoDelay,
            isOverdue ? "متأخّر — لم يُسلَّم" : "لم يُسلَّم بعد (ضمن المهلة)", null));
    }

    // ===== نظرة عامة على مواعيد التقارير ضمن نطاق المستخدم =====
    public async Task<Result<ReportDueOverview>> OverviewAsync(string? weekKey, Guid? departmentId, Guid? teamId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<ReportDueOverview>.Failure("غير مصرّح.", "auth.unauthenticated");

        var key = NormalizeWeekKey(weekKey);
        var scope = await _scope.ResolveAsync(ct);

        var candidates = await ResolveDueCandidatesAsync(scope, departmentId, teamId, ct);
        var evals = await EvaluateAsync(key, candidates, ct);
        var reviews = await ResolvePendingReviewsAsync(key, scope, departmentId, teamId, ct);

        var required = evals.Count;
        var submitted = evals.Count(e => e.HasSubmission);
        var missing = evals.Count(e => !e.HasSubmission);
        var overdueReports = evals.Count(e => !e.HasSubmission && e.Overdue);
        var pendingReviews = reviews.Count;
        var overdueReviews = reviews.Count(r => r.Overdue);

        var rows = BuildReportOverdueRows(key, evals).Concat(BuildReviewOverdueRows(reviews, onlyOverdue: false)).ToList();

        return Result<ReportDueOverview>.Success(new ReportDueOverview(
            key, ReportCalendarPolicy.WeekLabel(key), scope.ScopeType,
            required, submitted, missing, overdueReports, pendingReviews, overdueReviews, rows));
    }

    // ===== قائمة التأخّر (تقارير غير مُسلَّمة + مراجعات متأخّرة) ضمن نطاق المستخدم =====
    public async Task<Result<ReportDueOverdueReport>> OverdueAsync(string? weekKey, Guid? departmentId, Guid? teamId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<ReportDueOverdueReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var key = NormalizeWeekKey(weekKey);
        var scope = await _scope.ResolveAsync(ct);

        var candidates = await ResolveDueCandidatesAsync(scope, departmentId, teamId, ct);
        var evals = await EvaluateAsync(key, candidates, ct);
        var reviews = await ResolvePendingReviewsAsync(key, scope, departmentId, teamId, ct);

        var reportRows = BuildReportOverdueRows(key, evals);
        var reviewRows = BuildReviewOverdueRows(reviews, onlyOverdue: true);
        var rows = reportRows.Concat(reviewRows).ToList();

        return Result<ReportDueOverdueReport>.Success(new ReportDueOverdueReport(
            key, rows.Count, reportRows.Count, reviewRows.Count, rows));
    }

    // ===== المرشّحون المتوقَّع منهم تقرير (بلا حارس CompletionMonitors — متاح لأيّ مستخدم موثَّق ضمن نطاقه) =====
    private async Task<List<DueCandidate>> ResolveDueCandidatesAsync(ScopeContext scope, Guid? departmentId, Guid? teamId, CancellationToken ct)
    {
        var reportingRoleIds = await ReportingRoleIdsAsync(ct);
        if (reportingRoleIds.Count == 0) return new List<DueCandidate>();

        var roleCadence = (await _db.JobRoles.AsNoTracking()
                .Where(j => reportingRoleIds.Contains(j.Id))
                .Select(j => new { j.Id, j.Code })
                .ToListAsync(ct))
            .ToDictionary(j => j.Id, j => ReportCadencePolicy.ExpectedCadence(j.Code));

        var reportingRoleIdSet = roleCadence.Keys.ToHashSet();

        var candQ = _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.JobRoleId != null && reportingRoleIdSet.Contains(u.JobRoleId!.Value));

        // HR استثناء company-wide موثّق (مطابق لمتابعة الالتزام)، وإلا تقييد بنطاق ScopeResolver.
        var companyWide = scope.SeesAll || _currentUser.IsInRole(Roles.Hr);
        if (!companyWide)
        {
            var allowedIds = scope.UserIds;
            candQ = candQ.Where(u => allowedIds.Contains(u.Id));
        }

        if (departmentId is not null) candQ = candQ.Where(u => u.DepartmentId == departmentId);
        if (teamId is not null) candQ = candQ.Where(u => u.TeamId == teamId);

        var raw = await candQ
            .Select(u => new { u.Id, u.FullName, u.DepartmentId, u.TeamId, u.JobRoleId })
            .ToListAsync(ct);
        if (raw.Count == 0) return new List<DueCandidate>();

        var ids = raw.Select(c => c.Id).ToList();
        var deptIds = raw.Where(c => c.DepartmentId is not null).Select(c => c.DepartmentId!.Value).Distinct().ToList();
        var teamIds = raw.Where(c => c.TeamId is not null).Select(c => c.TeamId!.Value).Distinct().ToList();
        var deptNames = await _db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);
        var teamNames = await _db.Teams.AsNoTracking().Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
        var rolesByUser = await UserPrimaryRolesAsync(ids, ct);

        return raw.Select(c => new DueCandidate(
            c.Id, c.FullName, c.DepartmentId, c.TeamId,
            c.DepartmentId is Guid did ? deptNames.GetValueOrDefault(did) : null,
            c.TeamId is Guid tid ? teamNames.GetValueOrDefault(tid) : null,
            RoleAccess.PrimaryRole(rolesByUser.GetValueOrDefault(c.Id, new List<string>())),
            c.JobRoleId is Guid jrc ? roleCadence.GetValueOrDefault(jrc, PeriodType.Weekly) : PeriodType.Weekly)).ToList();
    }

    // ===== تقييم أسبوع واحد (أسبوعي = وحدة لكلّ مرشّح؛ يومي = وحدة لكلّ يوم عمل) — يحاكي EvaluateComplianceWeekAsync =====
    private async Task<List<DueEval>> EvaluateAsync(string key, IReadOnlyList<DueCandidate> candidates, CancellationToken ct)
    {
        if (candidates.Count == 0) return new List<DueEval>();
        var today = ReportCalendarPolicy.RiyadhToday();
        var weekly = candidates.Where(c => c.Cadence != PeriodType.Daily).ToList();
        var daily = candidates.Where(c => c.Cadence == PeriodType.Daily).ToList();
        var evals = new List<DueEval>(candidates.Count);

        if (weekly.Count > 0)
        {
            var weeklyIds = weekly.Select(c => c.UserId).ToList();
            var rows = await _db.ReportSubmissions.AsNoTracking()
                .Where(s => s.PeriodKey == key && s.PeriodType == PeriodType.Weekly
                            && SubmittedStatuses.Contains(s.Status) && weeklyIds.Contains(s.SubmitterId))
                .Select(s => new { s.SubmitterId, s.Id, s.SubmittedAtUtc })
                .ToListAsync(ct);
            var byUser = rows.GroupBy(s => s.SubmitterId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubmittedAtUtc ?? DateTime.MaxValue).First());

            foreach (var c in weekly)
            {
                var due = ReportCalendarPolicy.DueDateForRole(key, c.PrimaryRole);
                if (byUser.TryGetValue(c.UserId, out var s))
                    evals.Add(new DueEval(c, true, due, false, s.Id));
                else
                    evals.Add(new DueEval(c, false, due, today > due, null));
            }
        }

        if (daily.Count > 0)
        {
            var dates = DailyExpectedDates(key, today);
            if (dates.Count > 0)
            {
                var dailyIds = daily.Select(c => c.UserId).ToList();
                var dateKeys = dates.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList();
                var rows = await _db.ReportSubmissions.AsNoTracking()
                    .Where(s => s.PeriodType == PeriodType.Daily && dateKeys.Contains(s.PeriodKey)
                                && SubmittedStatuses.Contains(s.Status) && dailyIds.Contains(s.SubmitterId))
                    .Select(s => new { s.SubmitterId, s.PeriodKey })
                    .ToListAsync(ct);
                var byUserDay = rows.Select(r => (r.SubmitterId, r.PeriodKey)).ToHashSet();

                foreach (var c in daily)
                    foreach (var day in dates)
                    {
                        var dayKey = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        if (byUserDay.Contains((c.UserId, dayKey)))
                            evals.Add(new DueEval(c, true, day, false, null));
                        else
                            evals.Add(new DueEval(c, false, day, today > day, null));
                    }
            }
        }

        return evals;
    }

    // ===== المراجعات العالقة ضمن نطاق المعتمِد (لا تكشف لموظّف مراجعات غيره) =====
    private async Task<List<PendingReview>> ResolvePendingReviewsAsync(string key, ScopeContext scope, Guid? departmentId, Guid? teamId, CancellationToken ct)
    {
        var (start, end) = ReportCalendarPolicy.WeekRange(key);
        var dayKeys = new List<string>();
        for (var d = start; d <= end; d = d.AddDays(1))
            dayKeys.Add(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var q = _db.ReportSubmissions.AsNoTracking()
            .Where(s => PendingApprovalStatuses.Contains(s.Status) && s.CurrentApproverId != null
                        && ((s.PeriodType == PeriodType.Weekly && s.PeriodKey == key)
                            || (s.PeriodType == PeriodType.Daily && dayKeys.Contains(s.PeriodKey))));
        if (departmentId is not null) q = q.Where(s => s.DepartmentId == departmentId);
        if (teamId is not null) q = q.Where(s => s.TeamId == teamId);

        var raw = await q.Select(s => new
        {
            s.Id, s.SubmitterId, s.PeriodType, s.PeriodKey, s.TeamId, s.DepartmentId,
            ApproverId = s.CurrentApproverId!.Value
        }).ToListAsync(ct);
        if (raw.Count == 0) return new List<PendingReview>();

        // المراجع ضمن نطاق المستخدم فقط (المعتمِد نفسه يجب أن يكون ضمن نطاق رؤية الطالب).
        var inScope = raw.Where(s => scope.SeesAll || scope.UserIds.Contains(s.ApproverId)).ToList();
        if (inScope.Count == 0) return new List<PendingReview>();

        var today = ReportCalendarPolicy.RiyadhToday();
        var approverIds = inScope.Select(s => s.ApproverId).Distinct().ToList();
        var names = await UserNamesAsync(approverIds, ct);
        var approverRoles = await UserPrimaryRolesAsync(approverIds, ct);

        var teamIds = inScope.Where(s => s.TeamId is not null).Select(s => s.TeamId!.Value).Distinct().ToList();
        var deptIds = inScope.Where(s => s.DepartmentId is not null).Select(s => s.DepartmentId!.Value).Distinct().ToList();
        var teamNames = teamIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.Teams.AsNoTracking().Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
        var deptNames = deptIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);

        var list = new List<PendingReview>(inScope.Count);
        foreach (var s in inScope)
        {
            var role = RoleAccess.PrimaryRole(approverRoles.GetValueOrDefault(s.ApproverId) ?? new List<string>());
            // مفتاح أسبوع التقرير (اليومي يُشتقّ أسبوعه من يومه) لاحتساب موعد المراجعة بحسب دور المعتمِد.
            var weekKeyOfReport = s.PeriodType == PeriodType.Weekly
                ? s.PeriodKey
                : (DateOnly.TryParse(s.PeriodKey, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dd)
                    ? ReportCalendarPolicy.WeekKeyFor(dd) : key);
            var reviewDue = ReportCalendarPolicy.DueDateForRole(weekKeyOfReport, role);
            var delay = role switch
            {
                Roles.TeamLeader => DelayType.TeamLeaderReviewOverdue,
                Roles.Manager => DelayType.ManagerReviewOverdue,
                _ => DelayType.ExecutiveReviewPending
            };
            list.Add(new PendingReview(
                s.Id, s.ApproverId, names.GetValueOrDefault(s.ApproverId, "—"), role,
                s.DepartmentId, s.DepartmentId is Guid did ? deptNames.GetValueOrDefault(did) : null,
                s.TeamId, s.TeamId is Guid tid ? teamNames.GetValueOrDefault(tid) : null,
                reviewDue, today > reviewDue, delay));
        }
        return list;
    }

    // ===== بناء صفوف التأخّر =====
    private static List<ReportDueOverdueRow> BuildReportOverdueRows(string key, IReadOnlyList<DueEval> evals)
    {
        var today = ReportCalendarPolicy.RiyadhToday();
        var rows = new List<ReportDueOverdueRow>();

        // الأسبوعيون: صفّ واحد لكلّ مرشّح متأخّر.
        foreach (var e in evals.Where(e => e.Candidate.Cadence != PeriodType.Daily && !e.HasSubmission && e.Overdue))
        {
            var c = e.Candidate;
            rows.Add(new ReportDueOverdueRow(
                c.UserId, c.FullName, c.PrimaryRole, Roles.DisplayAr(c.PrimaryRole),
                c.DepartmentId, c.DepartmentName, c.TeamId, c.TeamName,
                DelayType.EmployeeReportNotSubmitted, "تسليم التقرير", e.DueDate,
                Math.Max(0, today.DayNumber - e.DueDate.DayNumber), null));
        }

        // اليوميون: تجميع الأيام المتأخّرة في صفّ واحد لكلّ شخص (الموعد = أبكر يوم متأخّر).
        var dailyMissing = evals.Where(e => e.Candidate.Cadence == PeriodType.Daily && !e.HasSubmission && e.Overdue)
            .GroupBy(e => e.Candidate.UserId);
        foreach (var g in dailyMissing)
        {
            var c = g.First().Candidate;
            var earliest = g.Min(e => e.DueDate);
            rows.Add(new ReportDueOverdueRow(
                c.UserId, c.FullName, c.PrimaryRole, Roles.DisplayAr(c.PrimaryRole),
                c.DepartmentId, c.DepartmentName, c.TeamId, c.TeamName,
                DelayType.EmployeeReportNotSubmitted, $"تسليم {g.Count()} تقرير يومي متأخّر", earliest,
                Math.Max(0, today.DayNumber - earliest.DayNumber), null));
        }

        return rows;
    }

    private static List<ReportDueOverdueRow> BuildReviewOverdueRows(IReadOnlyList<PendingReview> reviews, bool onlyOverdue)
    {
        var today = ReportCalendarPolicy.RiyadhToday();
        var src = onlyOverdue ? reviews.Where(r => r.Overdue) : reviews;
        return src.Select(r => new ReportDueOverdueRow(
            r.ApproverId, r.ApproverName, r.ApproverRole, Roles.DisplayAr(r.ApproverRole),
            r.DepartmentId, r.DepartmentName, r.TeamId, r.TeamName,
            r.DelayType, "مراجعة/اعتماد التقرير", r.ReviewDue,
            Math.Max(0, today.DayNumber - r.ReviewDue.DayNumber), r.SubmissionId)).ToList();
    }

    // ===== مساعدات =====
    private async Task<HashSet<Guid>> ReportingRoleIdsAsync(CancellationToken ct)
    {
        var list = await _db.ReportTemplates.AsNoTracking()
            .Where(t => t.JobRoleId != null && t.IsActive
                        && t.Classification == TemplateClassification.Primary
                        && t.DefaultPeriodType != PeriodType.Monthly
                        && _db.ReportTemplateVersions.Any(v => v.ReportTemplateId == t.Id && v.IsPublished))
            .Select(t => t.JobRoleId!.Value)
            .Distinct()
            .ToListAsync(ct);
        return list.ToHashSet();
    }

    private async Task<string?> JobRoleCodeAsync(Guid jobRoleId, CancellationToken ct) =>
        await _db.JobRoles.AsNoTracking().Where(j => j.Id == jobRoleId).Select(j => j.Code).FirstOrDefaultAsync(ct);

    private static List<DateOnly> DailyExpectedDates(string weekKey, DateOnly today)
    {
        var (start, end) = ReportCalendarPolicy.WeekRange(weekKey);
        var cap = today < end ? today : end;
        var dates = new List<DateOnly>();
        for (var d = start; d <= cap; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday))
                dates.Add(d);
        return dates;
    }

    private static string NormalizeWeekKey(string? weekKey) =>
        ReportCalendarPolicy.IsWeekKey(weekKey) ? weekKey!.Trim() : ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday());

    private async Task<Dictionary<Guid, List<string>>> UserPrimaryRolesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, List<string>>();
        var pairs = await (from ur in _db.UserRoles
                           join r in _db.Roles on ur.RoleId equals r.Id
                           where userIds.Contains(ur.UserId) && r.Name != null
                           select new { ur.UserId, r.Name }).ToListAsync(ct);
        return pairs.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    // ===== أنواع داخلية =====
    private sealed record DueCandidate(
        Guid UserId, string FullName, Guid? DepartmentId, Guid? TeamId,
        string? DepartmentName, string? TeamName, string PrimaryRole, PeriodType Cadence);

    private sealed record DueEval(DueCandidate Candidate, bool HasSubmission, DateOnly DueDate, bool Overdue, Guid? SubmissionId);

    private sealed record PendingReview(
        Guid SubmissionId, Guid ApproverId, string ApproverName, string ApproverRole,
        Guid? DepartmentId, string? DepartmentName, Guid? TeamId, string? TeamName,
        DateOnly ReviewDue, bool Overdue, DelayType DelayType);
}
