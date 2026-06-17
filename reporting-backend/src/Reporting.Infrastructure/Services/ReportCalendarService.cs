using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Calendar;
using Reporting.Application.Common;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// تقويم التقارير التشغيلي (Phase 5): كشف التقارير الأسبوعية الناقصة (§5)، تأخّر الاعتماد بعد المهلة (§6)،
/// والتزام مندوبي المبيعات بالتقارير اليومية وتجميعها أسبوعيًّا (§9). كل النواتج مقيَّدة خادميًّا بـ ScopeResolver.
/// المتوقَّع منهم تقريرٌ يُحدَّد بالمسمّى الوظيفي المربوط بقالب تقرير منشور أساسي فعّال — لا بأسماء أشخاص.
/// </summary>
public class ReportCalendarService : IReportCalendarService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;

    public ReportCalendarService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
    }

    // حالات «أُرسِل التقرير» (غادر المسودّة) — تُعدّ تسليمًا قائمًا.
    private static readonly SubmissionStatus[] SubmittedStatuses =
    {
        SubmissionStatus.Submitted, SubmissionStatus.Returned, SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel, SubmissionStatus.Escalated, SubmissionStatus.Closed,
        SubmissionStatus.Visible
    };

    // حالات «بانتظار الاعتماد» — مُرسَل لكنه لم يُغلَق بعد (يخضع لمهلة المراجعة).
    private static readonly SubmissionStatus[] PendingApprovalStatuses =
    {
        SubmissionStatus.Submitted, SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel, SubmissionStatus.Escalated
    };

    public async Task<Result<MissingReportsReport>> GetMissingReportsAsync(string? weekKey, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid)
            return Result<MissingReportsReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var key = NormalizeWeekKey(weekKey);
        if (!ReportCalendarPolicy.IsWeekKey(key))
            return Result<MissingReportsReport>.Failure(
                "صيغة الأسبوع غير صحيحة؛ استخدم YYYY-Www مثل 2026-W25.", "report_calendar.week_format_invalid");

        var (weekStart, weekEnd) = ReportCalendarPolicy.WeekRange(key);
        var scope = await _scope.ResolveAsync(ct);

        // المتوقَّع منهم تقرير أسبوعي = مستخدمون نشطون ضمن النطاق مسمّاهم الوظيفي تقريرُه أسبوعي.
        var candidates = await ExpectedReportersAsync(PeriodType.Weekly, scope, ct);

        var candidateIds = candidates.Select(c => c.Id).ToList();
        var subs = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => s.PeriodKey == key && s.PeriodType == PeriodType.Weekly
                        && SubmittedStatuses.Contains(s.Status)
                        && candidateIds.Contains(s.SubmitterId))
            .Select(s => new { s.SubmitterId, s.SubmittedAtUtc })
            .ToListAsync(ct);
        var submittedByUser = subs.GroupBy(s => s.SubmitterId)
            .ToDictionary(g => g.Key, g => g.Min(x => x.SubmittedAtUtc));

        var rolesByUser = await UserRolesAsync(candidateIds, ct);
        var teamNames = await TeamNamesAsync(candidates, ct);

        // إجازات معتمدة تغطّي الأسبوع كاملًا (V1.0.1) — تُستثنى من «التقارير الناقصة».
        var fullWeekLeaveUsers = await ApprovedFullWeekLeaveUsersAsync(candidateIds, weekStart, weekEnd, ct);

        var rows = new List<ExpectedReporterRow>();
        foreach (var c in candidates)
        {
            var role = RoleAccess.PrimaryRole(rolesByUser.GetValueOrDefault(c.Id, new List<string>()));
            var due = ReportCalendarPolicy.DueDateForRole(key, role);
            string status;
            DateTime? submittedAt = null;
            if (submittedByUser.TryGetValue(c.Id, out var at))
            {
                submittedAt = at;
                var submittedDate = DateOnly.FromDateTime(at ?? DateTime.UtcNow);
                status = submittedDate > due ? "late" : "submitted";
            }
            else if (fullWeekLeaveUsers.Contains(c.Id))
            {
                // في إجازة معتمدة طوال الأسبوع — لا يُحتسب التقرير ناقصًا.
                status = "leave";
            }
            else
            {
                status = "missing";
            }

            rows.Add(new ExpectedReporterRow(
                c.Id, c.FullName, Roles.DisplayAr(role), PeriodType.Weekly,
                c.TeamId, c.TeamId is Guid tid ? teamNames.GetValueOrDefault(tid) : null,
                status, due, submittedAt));
        }

        var teamShortfalls = rows.GroupBy(r => r.TeamId)
            .Select(g => new TeamShortfallRow(
                g.Key, g.Select(x => x.TeamName).FirstOrDefault(n => n != null) ?? "بدون فريق",
                g.Count(), g.Count(r => r.Status == "missing"), g.Count(r => r.Status == "late")))
            .Where(t => t.Missing > 0 || t.Late > 0)
            .OrderByDescending(t => t.Missing + t.Late)
            .ToList();

        var ordered = rows
            .OrderBy(r => r.Status == "missing" ? 0 : r.Status == "late" ? 1 : 2)
            .ThenBy(r => r.FullName)
            .ToList();

        var report = new MissingReportsReport(
            key, ReportCalendarPolicy.WeekLabel(key), weekStart, weekEnd,
            ordered.Count,
            ordered.Count(r => r.Status == "submitted"),
            ordered.Count(r => r.Status == "late"),
            ordered.Count(r => r.Status == "missing"),
            scope.ScopeType, true, ordered, teamShortfalls,
            ordered.Count(r => r.Status == "leave"));

        return Result<MissingReportsReport>.Success(report);
    }

    public async Task<Result<ApprovalDelaysReport>> GetApprovalDelaysAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<ApprovalDelaysReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);

        // تأخّر الاعتماد إشارة للمستوى الأعلى فقط — المستخدم العادي (غير الإداري) لا يراها.
        if (!_currentUser.IsInAnyRole(Roles.Management))
            return Result<ApprovalDelaysReport>.Success(
                new ApprovalDelaysReport(scope.ScopeType, 0, new List<ApprovalDelayRow>()));

        var q = _db.ReportSubmissions.AsNoTracking()
            .Where(s => s.CurrentApproverId != null && PendingApprovalStatuses.Contains(s.Status));
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            q = q.Where(s => ids.Contains(s.SubmitterId));
        }

        var subs = await q.Select(s => new
        {
            s.Id,
            s.SubmitterId,
            s.PeriodKey,
            s.Status,
            s.SubmittedAtUtc,
            ApproverId = s.CurrentApproverId!.Value,
            Title = _db.ReportTemplateVersions.Where(v => v.Id == s.ReportTemplateVersionId)
                .Select(v => v.ReportTemplate!.Title).FirstOrDefault()
        }).ToListAsync(ct);

        // المستوى الأعلى فقط: استبعد ما المستخدم الحالي هو معتمِده الحالي (يُصعَّد لمن فوقه).
        subs = subs.Where(s => s.ApproverId != uid).ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var approverIds = subs.Select(s => s.ApproverId).Distinct().ToList();
        var submitterIds = subs.Select(s => s.SubmitterId).Distinct().ToList();
        var rolesByApprover = await UserRolesAsync(approverIds, ct);
        var names = await _db.Users.AsNoTracking()
            .Where(u => approverIds.Contains(u.Id) || submitterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var rows = new List<ApprovalDelayRow>();
        foreach (var s in subs)
        {
            if (!ReportCalendarPolicy.IsWeekKey(s.PeriodKey)) continue; // المهلة الدورية للتقارير الأسبوعية
            var approverRole = RoleAccess.PrimaryRole(rolesByApprover.GetValueOrDefault(s.ApproverId, new List<string>()));
            var due = ReportCalendarPolicy.DueDateForRole(s.PeriodKey, approverRole);
            if (today <= due) continue; // ضمن المهلة
            rows.Add(new ApprovalDelayRow(
                s.Id, s.SubmitterId, names.GetValueOrDefault(s.SubmitterId, string.Empty),
                s.Title ?? string.Empty, s.PeriodKey, s.Status,
                s.ApproverId, names.GetValueOrDefault(s.ApproverId, string.Empty),
                Roles.DisplayAr(approverRole), due, today.DayNumber - due.DayNumber, s.SubmittedAtUtc));
        }

        rows = rows.OrderByDescending(r => r.DaysOverdue).ToList();
        return Result<ApprovalDelaysReport>.Success(new ApprovalDelaysReport(scope.ScopeType, rows.Count, rows));
    }

    public async Task<Result<SalesDailyComplianceReport>> GetSalesDailyComplianceAsync(string? weekKey, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid)
            return Result<SalesDailyComplianceReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var key = NormalizeWeekKey(weekKey);
        if (!ReportCalendarPolicy.IsWeekKey(key))
            return Result<SalesDailyComplianceReport>.Failure(
                "صيغة الأسبوع غير صحيحة؛ استخدم YYYY-Www مثل 2026-W25.", "report_calendar.week_format_invalid");

        var (weekStart, weekEnd) = ReportCalendarPolicy.WeekRange(key);
        var scope = await _scope.ResolveAsync(ct);

        var candidates = await ExpectedReportersAsync(PeriodType.Daily, scope, ct);

        // الأيام المتوقَّعة = الأيام المنقضية من الأسبوع حتى تاريخه (لا نفترض أيام العطلات — تقويم أيام العمل مؤجَّل).
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var elapsedEnd = today < weekEnd ? today : weekEnd;
        var expectedDays = elapsedEnd >= weekStart ? elapsedEnd.DayNumber - weekStart.DayNumber + 1 : 0;

        var candidateIds = candidates.Select(c => c.Id).ToList();
        var dailySubs = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => s.PeriodType == PeriodType.Daily
                        && SubmittedStatuses.Contains(s.Status)
                        && candidateIds.Contains(s.SubmitterId))
            .Select(s => new { s.SubmitterId, s.PeriodKey, s.SubmittedAtUtc })
            .ToListAsync(ct);

        // عدّ الأيام المميّزة المُسلَّمة داخل الأسبوع لكل مندوب (تجميع اليومي → أسبوعي).
        var daysByUser = new Dictionary<Guid, HashSet<DateOnly>>();
        foreach (var s in dailySubs)
        {
            if (!DateOnly.TryParse(s.PeriodKey, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                if (s.SubmittedAtUtc is DateTime at) day = DateOnly.FromDateTime(at);
                else continue;
            }
            if (day < weekStart || day > weekEnd) continue;
            if (!daysByUser.TryGetValue(s.SubmitterId, out var set))
            {
                set = new HashSet<DateOnly>();
                daysByUser[s.SubmitterId] = set;
            }
            set.Add(day);
        }

        var teamNames = await TeamNamesAsync(candidates, ct);

        // أيام الإجازة المعتمدة لكل مندوب ضمن النافذة المنقضية (V1.0.1) — تُخصم من الأيام المتوقَّعة
        // حتى لا يُحتسب يوم الإجازة المعتمدة تقريرًا يوميًّا مفقودًا ولا يخفض الالتزام.
        var leaveDaysByUser = expectedDays > 0
            ? await ApprovedLeaveDayCountsAsync(candidateIds, weekStart, elapsedEnd, ct)
            : new Dictionary<Guid, int>();

        var rows = candidates.Select(c =>
        {
            var submitted = daysByUser.TryGetValue(c.Id, out var set) ? set.Count : 0;
            var leaveDays = leaveDaysByUser.TryGetValue(c.Id, out var ld) ? Math.Min(ld, expectedDays) : 0;
            var userExpected = Math.Max(0, expectedDays - leaveDays);
            var missing = Math.Max(0, userExpected - submitted);
            var complete = userExpected > 0 && submitted >= userExpected;
            var needsReview = userExpected > 0 && submitted < userExpected;
            return new SalesDailyComplianceRow(
                c.Id, c.FullName, c.TeamId,
                c.TeamId is Guid tid ? teamNames.GetValueOrDefault(tid) : null,
                userExpected, submitted, missing, complete, needsReview, leaveDays);
        })
        .OrderByDescending(r => r.MissingDays)
        .ThenBy(r => r.FullName)
        .ToList();

        var report = new SalesDailyComplianceReport(
            key, ReportCalendarPolicy.WeekLabel(key), weekStart, weekEnd,
            rows.Count, rows.Count(r => r.IsComplete), rows.Count(r => !r.IsComplete),
            scope.ScopeType, true, rows);

        return Result<SalesDailyComplianceReport>.Success(report);
    }

    // ===== مساعدات داخلية =====

    private static string NormalizeWeekKey(string? weekKey) =>
        string.IsNullOrWhiteSpace(weekKey)
            ? ReportCalendarPolicy.WeekKeyFor(DateOnly.FromDateTime(DateTime.UtcNow))
            : weekKey.Trim();

    /// <summary>
    /// المستخدمون النشطون المتوقَّع منهم تقرير بدورية معيّنة، ضمن نطاق المستخدم الحالي.
    /// «المتوقَّع» = مسمّاه الوظيفي مربوط بقالب تقرير منشور أساسي فعّال، ودوريّته المحسوبة == cadence المطلوبة.
    /// </summary>
    private async Task<List<ReporterRow>> ExpectedReportersAsync(PeriodType cadence, ScopeContext scope, CancellationToken ct)
    {
        var reportingRoleIds = await _db.ReportTemplates.AsNoTracking()
            .Where(t => t.JobRoleId != null && t.IsActive
                        && t.Classification == TemplateClassification.Primary
                        && _db.ReportTemplateVersions.Any(v => v.ReportTemplateId == t.Id && v.IsPublished))
            .Select(t => t.JobRoleId!.Value)
            .Distinct()
            .ToListAsync(ct);

        if (reportingRoleIds.Count == 0) return new List<ReporterRow>();

        var codes = await _db.JobRoles.AsNoTracking()
            .Where(j => reportingRoleIds.Contains(j.Id))
            .Select(j => new { j.Id, j.Code })
            .ToListAsync(ct);

        var matchingRoleIds = codes
            .Where(c => ReportCadencePolicy.ExpectedCadence(c.Code) == cadence)
            .Select(c => c.Id)
            .ToHashSet();
        if (matchingRoleIds.Count == 0) return new List<ReporterRow>();

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.JobRoleId != null && matchingRoleIds.Contains(u.JobRoleId!.Value))
            .Select(u => new ReporterRow(u.Id, u.FullName, u.TeamId))
            .ToListAsync(ct);

        return scope.SeesAll ? users : users.Where(u => scope.Contains(u.Id)).ToList();
    }

    private async Task<Dictionary<Guid, List<string>>> UserRolesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, List<string>>();
        var pairs = await (from ur in _db.UserRoles
                           join r in _db.Roles on ur.RoleId equals r.Id
                           where userIds.Contains(ur.UserId) && r.Name != null
                           select new { ur.UserId, r.Name }).ToListAsync(ct);
        return pairs.GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());
    }

    private async Task<Dictionary<Guid, string>> TeamNamesAsync(IEnumerable<ReporterRow> reporters, CancellationToken ct)
    {
        var teamIds = reporters.Where(r => r.TeamId != null).Select(r => r.TeamId!.Value).Distinct().ToList();
        if (teamIds.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Teams.AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
    }

    private readonly record struct ReporterRow(Guid Id, string FullName, Guid? TeamId);

    // ===== تكامل الإجازات المعتمدة (V1.0.1) =====

    /// <summary>المستخدمون الذين تغطّي إجازتُهم المعتمدة (HrApproved، نوع Leave) الأسبوع كاملًا.</summary>
    private async Task<HashSet<Guid>> ApprovedFullWeekLeaveUsersAsync(
        IReadOnlyCollection<Guid> userIds, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct)
    {
        if (userIds.Count == 0) return new HashSet<Guid>();
        var ids = await _db.LeaveRequests.AsNoTracking()
            .Where(r => r.Type == LeaveRequestType.Leave
                        && r.Status == LeaveRequestStatus.HrApproved
                        && userIds.Contains(r.RequesterUserId)
                        && r.StartDate <= weekStart && r.EndDate >= weekEnd)
            .Select(r => r.RequesterUserId)
            .Distinct()
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    /// <summary>عدد أيام الإجازة المعتمدة (Leave/HrApproved) لكل مستخدم ضمن [from, to] شاملًا.</summary>
    private async Task<Dictionary<Guid, int>> ApprovedLeaveDayCountsAsync(
        IReadOnlyCollection<Guid> userIds, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (userIds.Count == 0 || to < from) return new Dictionary<Guid, int>();

        var leaves = await _db.LeaveRequests.AsNoTracking()
            .Where(r => r.Type == LeaveRequestType.Leave
                        && r.Status == LeaveRequestStatus.HrApproved
                        && userIds.Contains(r.RequesterUserId)
                        && r.StartDate <= to && r.EndDate >= from)
            .Select(r => new { r.RequesterUserId, r.StartDate, r.EndDate })
            .ToListAsync(ct);

        // عدّ الأيام المميّزة المغطّاة داخل النافذة (طلبات متداخلة لا تُضاعِف العدّ).
        var daysByUser = new Dictionary<Guid, HashSet<DateOnly>>();
        foreach (var l in leaves)
        {
            var s = l.StartDate < from ? from : l.StartDate;
            var e = l.EndDate > to ? to : l.EndDate;
            if (!daysByUser.TryGetValue(l.RequesterUserId, out var set))
            {
                set = new HashSet<DateOnly>();
                daysByUser[l.RequesterUserId] = set;
            }
            for (var d = s; d <= e; d = d.AddDays(1)) set.Add(d);
        }
        return daysByUser.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
    }
}
