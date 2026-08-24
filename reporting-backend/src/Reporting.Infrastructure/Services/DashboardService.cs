using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reporting.Application.Common;
using Reporting.Application.Dashboard;
using Reporting.Application.Kpi;
using Reporting.Application.Reports;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// يحدد نوع الداشبورد من الدور، ويحسب نطاق الرؤية من التسلسل الإداري (ManagerId)،
/// ثم يجمّع الأرقام الرسمية خادمًا فقط ضمن النطاق والفترة — لا يرى المستخدم أي بيانات خارج نطاقه.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;
    private readonly IExpectedSubmissionStatusResolver _expected;
    private readonly KpiFeatureOptions _kpiOptions;

    public DashboardService(
        AppDbContext db,
        ICurrentUser currentUser,
        IScopeResolver scope,
        IExpectedSubmissionStatusResolver expected,
        IOptions<KpiFeatureOptions> kpiOptions)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
        _expected = expected;
        _kpiOptions = kpiOptions.Value;
    }

    private static readonly SubmissionStatus[] CompletedStatuses =
    {
        SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel,
        SubmissionStatus.Closed,
        SubmissionStatus.Visible
    };

    public async Task<Result<DashboardDto>> GetMineAsync(string? periodKey, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<DashboardDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var me = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid, ct);
        if (me is null) return Result<DashboardDto>.Failure("المستخدم غير موجود.", "auth.not_found");

        var role = PrimaryRole(_currentUser.Roles);
        var dashboardType = DashboardTypeFor(role);
        var scopeType = ScopeTypeFor(role);
        var scopeIds = await ResolveScopeIdsAsync(uid, scopeType, ct);

        var key = string.IsNullOrWhiteSpace(periodKey) ? CurrentWeekKey() : periodKey.Trim();

        // REPORT-EXPECTED-SUBMISSION-STATUS-R1 — عدّادات البطاقات Users-first (مطالَبون LEFT JOIN تسليمات):
        // «المطلوبة» = الدورات المنطبقة الفعليّة، و«تحتاج إجراء» تشمل «من لم يبدأ ومتأخّر» (non-starter).
        // مصدر واحد للحقيقة مع قائمة «pending-reports» ⇒ الأرقام متطابقة عبر الشاشات.
        var expected = (await _expected.ResolveAsync(
                new ExpectedStatusQuery(scopeIds, new[] { key }, null), ct))
            .Where(r => r.IsExpected)
            .ToList();

        var total = expected.Count;
        var completed = expected.Count(r => r.Status is ExpectedSubmissionStatus.Approved
            or ExpectedSubmissionStatus.Closed);
        var pending = expected.Count(r => r.Status == ExpectedSubmissionStatus.Submitted);
        // «تحتاج إجراء» = متأخّر بلا تسليم + مسودّة متأخّرة + مُعاد + مُصعَّد (موحّد مع قائمة التقارير المتأخّرة).
        var needsAction = expected.Count(r => r.IsActionable);

        // تفصيل حالة التسليمات القائمة (ودجة الدونات) — عرض خام مستقلّ لِما سُلِّم فعلًا.
        var subs = await _db.ReportSubmissions
            .Where(s => scopeIds.Contains(s.SubmitterId) && s.PeriodKey == key)
            .Select(s => s.Status)
            .ToListAsync(ct);

        // ADMIN-GOVERNANCE-R1: لا يدخل التقييم النتائج النهائية إلا إذا كان معتمَدًا (Approved).
        // المحذوف إداريًّا (IsDeleted) مستبعَد تلقائيًّا عبر الفلتر العالميّ.
        // B-2 — توسيط ذو مرحلتين: متوسّط كلّ موظّف أوّلًا ثمّ متوسّط الموظّفين، فلا يزن من قُيّم
        // عشر مرّات أكثر ممّن قُيّم مرّة. كان هنا متوسّطًا خامًا لكلّ التقييمات وهو ما يشوّه رقم المجموعة.
        var kpiRows = await _db.KpiEvaluations
            .Where(e => scopeIds.Contains(e.SubjectUserId) && e.PeriodKey == key
                        && e.TotalScore != null && e.Status == KpiEvaluationStatus.Approved)
            .Select(e => new { e.SubjectUserId, Score = e.TotalScore!.Value })
            .ToListAsync(ct);
        decimal? kpiAverage = KpiScorePolicy.Round(
            KpiScorePolicy.GroupScore(kpiRows
                .GroupBy(r => r.SubjectUserId)
                .Select(g => KpiScorePolicy.EmployeePeriodScore(g.Sum(x => x.Score), g.Count())!.Value)));

        var cards = new List<SummaryCardDto>
        {
            new("totalReports", "التقارير المطلوبة", total, "neutral", "report-status"),
            new("completedReports", "التقارير المكتملة", completed, completed >= total && total > 0 ? "green" : "neutral", "report-status"),
            new("pendingApproval", "في انتظار الاعتماد", pending, pending == 0 ? "green" : "amber", "report-status"),
            new("needsAction", "تحتاج إجراء", needsAction, needsAction == 0 ? "green" : "red", "report-status"),
            new("kpiAverage", "متوسط KPI", kpiAverage, KpiStatus(kpiAverage, _kpiOptions.DefaultBelowTargetThreshold), "kpi-summary"),
        };

        // ودجة اتجاه KPI: متوسط الدرجة لكل فترة (آخر 8 فترات) ضمن النطاق.
        // B-2 هنا أيضًا: نقطة كلّ فترة = متوسّط متوسّطات الموظّفين فيها لا متوسّط تقييماتها الخام.
        var trendRows = await _db.KpiEvaluations
            .Where(e => scopeIds.Contains(e.SubjectUserId) && e.TotalScore != null
                        && e.Status == KpiEvaluationStatus.Approved)
            .Select(e => new { e.PeriodKey, e.SubjectUserId, Score = e.TotalScore!.Value })
            .ToListAsync(ct);
        var trendData = trendRows
            .GroupBy(r => r.PeriodKey)
            .OrderByDescending(g => g.Key)
            .Take(8)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                periodKey = g.Key,
                value = KpiScorePolicy.Round(KpiScorePolicy.GroupScore(g
                    .GroupBy(x => x.SubjectUserId)
                    .Select(u => KpiScorePolicy.EmployeePeriodScore(u.Sum(x => x.Score), u.Count())!.Value))),
            })
            .ToList();

        var statusBreakdown = subs
            .GroupBy(s => s)
            .Select(g => new { status = g.Key.ToString(), count = g.Count() })
            .ToList();

        var widgets = new List<DashboardWidgetDto>
        {
            new("kpiTrend", "lineChart", "تقدم KPI", trendData),
            new("reportStatus", "donut", "حالة التقارير", statusBreakdown),
        };

        var permissions = PermissionsFor(_currentUser.Roles);
        var actions = ActionsFor(permissions);

        var dto = new DashboardDto(
            dashboardType,
            new DashboardPeriodDto(key, PeriodLabel(key)),
            new DashboardUserDto(uid, me.FullName, role),
            new DashboardScopeDto(scopeType, scopeIds),
            permissions,
            cards,
            widgets,
            actions);

        return Result<DashboardDto>.Success(dto);
    }

    // ===== نقاط الـDrill-down (كلها محصورة بنطاق المستخدم) =====

    public async Task<Result<KpiTrendDto>> GetKpiTrendsAsync(Guid? subjectId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<KpiTrendDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var role = PrimaryRole(_currentUser.Roles);
        var scopeType = ScopeTypeFor(role);
        var scopeIds = await ResolveScopeIdsAsync(uid, scopeType, ct);

        var target = subjectId ?? uid;
        // منع رؤية اتجاه شخص خارج النطاق (IDOR/BOLA).
        if (!scopeIds.Contains(target))
            return Result<KpiTrendDto>.Failure("لا تملك صلاحية رؤية بيانات هذا المستخدم.", "auth.forbidden");

        var subject = await _db.Users.FirstOrDefaultAsync(u => u.Id == target, ct);
        if (subject is null) return Result<KpiTrendDto>.Failure("المستخدم غير موجود.", "user.not_found");

        var points = await _db.KpiEvaluations
            .Where(e => e.SubjectUserId == target && e.TotalScore != null
                        && e.Status == KpiEvaluationStatus.Approved)
            .GroupBy(e => e.PeriodKey)
            .Select(g => new { PeriodKey = g.Key, Avg = g.Average(x => x.TotalScore!.Value) })
            .OrderByDescending(x => x.PeriodKey)
            .Take(12)
            .ToListAsync(ct);

        var ordered = points
            .OrderBy(x => x.PeriodKey)
            .Select(x => new KpiTrendPointDto(x.PeriodKey, Math.Round(x.Avg, 1)))
            .ToList();

        return Result<KpiTrendDto>.Success(new KpiTrendDto(target, subject.FullName, ordered));
    }

    public async Task<Result<IReadOnlyList<MemberPerformanceDto>>> GetMembersPerformanceAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<MemberPerformanceDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var role = PrimaryRole(_currentUser.Roles);
        var scopeType = ScopeTypeFor(role);
        var scopeIds = await ResolveScopeIdsAsync(uid, scopeType, ct);
        var key = CurrentWeekKey();

        var members = await _db.Users
            .Where(u => scopeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(ct);

        // متوسط KPI لكل عضو (كل الفترات) + اتجاه آخر فترتين. معتمَد فقط (ADMIN-GOVERNANCE-R1).
        var kpis = await _db.KpiEvaluations
            .Where(e => scopeIds.Contains(e.SubjectUserId) && e.TotalScore != null
                        && e.Status == KpiEvaluationStatus.Approved)
            .Select(e => new { e.SubjectUserId, e.PeriodKey, Score = e.TotalScore!.Value })
            .ToListAsync(ct);

        // تقارير الفترة الحالية لكل عضو.
        var subs = await _db.ReportSubmissions
            .Where(s => scopeIds.Contains(s.SubmitterId) && s.PeriodKey == key)
            .Select(s => new { s.SubmitterId, s.Status })
            .ToListAsync(ct);

        var result = new List<MemberPerformanceDto>();
        foreach (var m in members)
        {
            var memberKpis = kpis.Where(k => k.SubjectUserId == m.Id).ToList();
            decimal? avg = memberKpis.Count > 0 ? Math.Round(memberKpis.Average(k => k.Score), 1) : null;

            var byPeriod = memberKpis
                .GroupBy(k => k.PeriodKey)
                .Select(g => new { g.Key, Avg = g.Average(x => x.Score) })
                .OrderByDescending(x => x.Key)
                .Take(2)
                .ToList();
            var trend = byPeriod.Count < 2
                ? "Unknown"
                : byPeriod[0].Avg > byPeriod[1].Avg ? "Up"
                : byPeriod[0].Avg < byPeriod[1].Avg ? "Down"
                : "Flat";

            var memberSubs = subs.Where(s => s.SubmitterId == m.Id).ToList();
            var reportsTotal = memberSubs.Count;
            var reportsCompleted = memberSubs.Count(s => CompletedStatuses.Contains(s.Status));

            // B-6 — الحكم «دون المستهدف» يقع هنا بالعتبة المركزيّة، لا في الواجهة بثابت 60.
            // `null` تبقى `null`: غياب التقييم ليس ضعف أداء.
            var threshold = _kpiOptions.DefaultBelowTargetThreshold;
            bool? isBelowTarget = avg is null ? null : avg.Value < threshold;

            result.Add(new MemberPerformanceDto(
                m.Id, m.FullName, avg, trend, reportsTotal, reportsCompleted, isBelowTarget, threshold));
        }

        var ordered = result
            .OrderByDescending(r => r.KpiAverage ?? -1)
            .ToList();

        return Result<IReadOnlyList<MemberPerformanceDto>>.Success(ordered);
    }

    public async Task<Result<IReadOnlyList<ActivityItemDto>>> GetRecentActivityAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<ActivityItemDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var role = PrimaryRole(_currentUser.Roles);
        var scopeType = ScopeTypeFor(role);
        var scopeIds = await ResolveScopeIdsAsync(uid, scopeType, ct);

        var recent = await (
            from s in _db.ReportSubmissions
            where scopeIds.Contains(s.SubmitterId) && s.SubmittedAtUtc != null
            join v in _db.ReportTemplateVersions on s.ReportTemplateVersionId equals v.Id
            join t in _db.ReportTemplates on v.ReportTemplateId equals t.Id
            orderby s.SubmittedAtUtc descending
            select new
            {
                s.Id,
                s.SubmitterId,
                Title = t.Title,
                s.Status,
                s.PeriodKey,
                s.SubmittedAtUtc
            })
            .Take(15)
            .ToListAsync(ct);

        var names = await _db.Users
            .Where(u => scopeIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var items = recent
            .Select(s => new ActivityItemDto(
                s.Id,
                names.TryGetValue(s.SubmitterId, out var n) ? n : "—",
                s.Title,
                s.Status.ToString(),
                s.PeriodKey,
                s.SubmittedAtUtc))
            .ToList();

        return Result<IReadOnlyList<ActivityItemDto>>.Success(items);
    }

    public async Task<Result<IReadOnlyList<PendingReportDto>>> GetPendingReportsAsync(string? periodKey, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<PendingReportDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var role = PrimaryRole(_currentUser.Roles);
        var scopeType = ScopeTypeFor(role);
        var scopeIds = await ResolveScopeIdsAsync(uid, scopeType, ct);
        var key = string.IsNullOrWhiteSpace(periodKey) ? CurrentWeekKey() : periodKey.Trim();

        // REPORT-EXPECTED-SUBMISSION-STATUS-R1 — Users-first: كل مطالَب في النطاق LEFT JOIN تسليماته.
        // بنود الإجراء تشمل «من لم يبدأ ومتأخّر» (non-starter، SubmissionId=null) لا المسودّات/المُعادة فقط.
        var projection = await _expected.ResolveManagementAsync(key, scopeIds, null, ct);

        var items = projection.ActionItems
            .Select(r => new PendingReportDto(
                r.SubmissionId,
                r.UserId,
                r.UserFullName,
                r.TemplateName,
                r.Status.ToString(),
                r.PeriodKey)
            {
                StatusLabel = r.StatusLabel,
                Severity = r.Severity,
                HasSubmission = r.HasSubmission
            })
            .ToList();

        return Result<IReadOnlyList<PendingReportDto>>.Success(items);
    }

    // ===== ملف أداء الموظف (Phase 3) — محصور بنطاق المستخدم =====

    public async Task<Result<EmployeeProfileDto>> GetEmployeeProfileAsync(Guid userId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<EmployeeProfileDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var role = PrimaryRole(_currentUser.Roles);
        var scopeType = ScopeTypeFor(role);
        var scopeIds = await ResolveScopeIdsAsync(uid, scopeType, ct);

        // منع فتح ملف موظّف خارج النطاق (IDOR/BOLA) — يُفرض خادمًا لا واجهةً فقط.
        if (!scopeIds.Contains(userId))
            return Result<EmployeeProfileDto>.Failure("لا تملك صلاحية رؤية ملف هذا الموظّف.", "auth.forbidden");

        var subject = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (subject is null) return Result<EmployeeProfileDto>.Failure("الموظّف غير موجود.", "user.not_found");

        // ----- الرأس: بيانات أساسية من الجداول المرجعية -----
        var jobRoleName = subject.JobRoleId is Guid jrid
            ? await _db.JobRoles.Where(j => j.Id == jrid).Select(j => (string?)j.NameAr).FirstOrDefaultAsync(ct)
            : null;
        var teamName = subject.TeamId is Guid tid
            ? await _db.Teams.Where(t => t.Id == tid).Select(t => (string?)t.NameAr).FirstOrDefaultAsync(ct)
            : null;
        var departmentName = subject.DepartmentId is Guid did
            ? await _db.Departments.Where(d => d.Id == did).Select(d => (string?)d.NameAr).FirstOrDefaultAsync(ct)
            : null;
        var managerName = subject.ManagerId is Guid mid
            ? await _db.Users.Where(u => u.Id == mid).Select(u => (string?)u.FullName).FirstOrDefaultAsync(ct)
            : null;

        // ----- KPI: التقييمات المُسلَّمة ذات درجة (لا تجميع دوري جديد) -----
        var kpiRows = await (
            from e in _db.KpiEvaluations
            where e.SubjectUserId == userId
            join v in _db.KpiTemplateVersions on e.KpiTemplateVersionId equals v.Id
            join t in _db.KpiTemplates on v.KpiTemplateId equals t.Id
            orderby e.PeriodKey descending
            select new
            {
                e.Id,
                Title = t.Title,
                e.PeriodKey,
                e.TotalScore,
                e.Status,
                e.Trend,
                e.SubmittedAtUtc
            })
            .ToListAsync(ct);

        // النتائج النهائية (آخر درجة/المتوسط) من المعتمَد فقط (ADMIN-GOVERNANCE-R1)؛ قائمة التقييمات أدناه تعرض كل الحالات مع شارتها.
        var scored = kpiRows.Where(k => k.TotalScore != null && k.Status == KpiEvaluationStatus.Approved).ToList();
        decimal? lastKpiScore = scored.Count > 0 ? scored[0].TotalScore : null;
        string? lastKpiPeriod = scored.Count > 0 ? scored[0].PeriodKey : null;
        string lastKpiTrend = scored.Count > 0 ? scored[0].Trend.ToString() : "Unknown";
        decimal? averageKpi = scored.Count > 0 ? Math.Round(scored.Average(k => k.TotalScore!.Value), 1) : null;

        var kpiEvaluations = kpiRows
            .Take(10)
            .Select(k => new EmployeeProfileKpiDto(
                k.Id, k.Title, k.PeriodKey, k.TotalScore, k.Status.ToString(), k.Trend.ToString()))
            .ToList();

        // ----- التقارير: أحدث التسليمات مع عنوان القالب والمعتمِد الحالي -----
        var subRows = await (
            from s in _db.ReportSubmissions
            where s.SubmitterId == userId
            join v in _db.ReportTemplateVersions on s.ReportTemplateVersionId equals v.Id
            join t in _db.ReportTemplates on v.ReportTemplateId equals t.Id
            orderby s.SubmittedAtUtc descending, s.CreatedAtUtc descending
            select new
            {
                s.Id,
                Title = t.Title,
                s.PeriodKey,
                s.Status,
                s.SubmittedAtUtc,
                s.CurrentApproverId,
                s.CreatedAtUtc
            })
            .ToListAsync(ct);

        var approverIds = subRows.Where(s => s.CurrentApproverId != null).Select(s => s.CurrentApproverId!.Value).Distinct().ToList();
        var approverNames = await _db.Users
            .Where(u => approverIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var reports = subRows
            .Take(10)
            .Select(s => new EmployeeProfileReportDto(
                s.Id, s.Title, s.PeriodKey, s.Status.ToString(), s.SubmittedAtUtc,
                s.CurrentApproverId is Guid aid && approverNames.TryGetValue(aid, out var an) ? an : null))
            .ToList();

        var reportsSubmitted = subRows.Count(s => s.Status != SubmissionStatus.Draft);
        var reportsReturned = subRows.Count(s => s.Status == SubmissionStatus.Returned);
        var reportsNeedsAction = subRows.Count(s =>
            s.Status == SubmissionStatus.Draft || s.Status == SubmissionStatus.Returned || s.Status == SubmissionStatus.Escalated);

        // ----- الملاحظات الإدارية المفتوحة التي تتطلّب إجراءً على هذا الموظّف -----
        var openNotesRequiringAction = await _db.ManagementNotes
            .CountAsync(n => n.EntityType == ManagementNoteEntityType.User && n.EntityId == userId
                && n.Status == ManagementNoteStatus.Open && n.RequiresAction, ct);

        // ----- بنود الحوكمة المرتبطة: مخاطرة بصفته موضوعًا، أو تصعيد موجَّه إليه -----
        var riskItems = await _db.Risks
            .Where(r => r.SubjectUserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new EmployeeProfileGovernanceDto("Risk", r.Id, r.Title, r.Status.ToString(), r.CreatedAtUtc))
            .ToListAsync(ct);

        var escItems = await _db.Escalations
            .Where(e => e.TargetUserId == userId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Select(e => new EmployeeProfileGovernanceDto("Escalation", e.Id, e.Reason, e.Status.ToString(), e.CreatedAtUtc))
            .ToListAsync(ct);

        var governanceItems = riskItems.Concat(escItems)
            .OrderByDescending(g => g.CreatedAtUtc)
            .Take(10)
            .ToList();

        // ----- حالة مختصرة للرأس -----
        var hasData = scored.Count > 0 || subRows.Count > 0;
        string statusKey, statusLabel;
        if (!hasData)
        {
            statusKey = "insufficient";
            statusLabel = "لا توجد بيانات كافية";
        }
        else if (reportsNeedsAction > 0)
        {
            statusKey = "late";
            statusLabel = "متأخر";
        }
        else if ((lastKpiScore != null && lastKpiScore < 60) || reportsReturned > 0 || openNotesRequiringAction > 0)
        {
            statusKey = "watch";
            statusLabel = "يحتاج متابعة";
        }
        else
        {
            statusKey = "good";
            statusLabel = "جيد";
        }

        var header = new EmployeeProfileHeaderDto(
            subject.Id, subject.FullName, subject.Email, jobRoleName,
            teamName, departmentName, managerName,
            subject.IsActive, statusKey, statusLabel);

        var summary = new EmployeeProfileSummaryDto(
            lastKpiScore, lastKpiPeriod, lastKpiTrend,
            averageKpi, scored.Count,
            reportsSubmitted, reportsReturned, reportsNeedsAction,
            openNotesRequiringAction);

        // ----- خط زمني مبسّط من البيانات الحالية -----
        var timeline = new List<EmployeeProfileTimelineDto>();
        foreach (var s in subRows.Where(s => s.SubmittedAtUtc != null).Take(10))
            timeline.Add(new EmployeeProfileTimelineDto("Submission", $"سلّم تقرير «{s.Title}» ({s.PeriodKey})", s.SubmittedAtUtc!.Value));
        foreach (var k in scored.Where(k => k.SubmittedAtUtc != null).Take(10))
            timeline.Add(new EmployeeProfileTimelineDto("Kpi", $"تقييم «{k.Title}» ({k.PeriodKey}) — {k.TotalScore}", k.SubmittedAtUtc!.Value));
        foreach (var g in governanceItems)
            timeline.Add(new EmployeeProfileTimelineDto(g.Kind, g.Kind == "Risk" ? $"مخاطرة: {g.Title}" : $"تصعيد: {g.Title}", g.CreatedAtUtc));

        var orderedTimeline = timeline
            .OrderByDescending(t => t.AtUtc)
            .Take(15)
            .ToList();

        // ----- الإجازات والاستئذانات الأخيرة (V1.0.1) — عرض فقط -----
        var leaveRows = await _db.LeaveRequests.AsNoTracking()
            .Where(r => r.RequesterUserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(10)
            .ToListAsync(ct);
        var hrIds = leaveRows.Where(r => r.HrReviewerId != null).Select(r => r.HrReviewerId!.Value).Distinct().ToList();
        var hrNames = hrIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.Where(u => hrIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var leaveRequests = leaveRows.Select(r => new EmployeeProfileLeaveDto(
            r.Id, r.Type.ToString(), r.StartDate, r.EndDate, r.StartTime, r.EndTime, r.Status.ToString(),
            r.HrReviewerId is Guid hid && hrNames.TryGetValue(hid, out var hn) ? hn : null,
            r.Status == LeaveRequestStatus.HrApproved, r.CreatedAtUtc)).ToList();

        var dto = new EmployeeProfileDto(header, summary, reports, kpiEvaluations, governanceItems, orderedTimeline, leaveRequests);
        return Result<EmployeeProfileDto>.Success(dto);
    }

    // ===== الدور ونوع الداشبورد والنطاق =====

    private static string PrimaryRole(IReadOnlyCollection<string> roles) => RoleAccess.PrimaryRole(roles);

    private static string DashboardTypeFor(string role) => role switch
    {
        Roles.Admin => "AdminGovernance",
        Roles.CeoSupport => "Governance",
        Roles.Ceo => "CEO",
        Roles.GeneralManager => "GM",
        Roles.Manager => "Manager",
        Roles.TeamLeader => "TeamLeader",
        _ => "Employee"
    };

    private static string ScopeTypeFor(string role) => RoleAccess.ScopeTypeFor(role);

    /// <summary>
    /// يحسب معرّفات المستخدمين داخل نطاق رؤية المستخدم الحالي عبر المصدر الموحّد <see cref="IScopeResolver"/>،
    /// فيشمل نطاق الدور (شجرة ManagerId) مُوحَّدًا مع نطاقات المناصب المرنة (رؤية فقط).
    /// المعامل scopeType يطابق ما يحسبه المُحلِّل من الأدوار ويُحتفظ به لاتّساق التوقيع مع مواضع الاستدعاء.
    /// </summary>
    private async Task<List<Guid>> ResolveScopeIdsAsync(Guid uid, string scopeType, CancellationToken ct)
    {
        var scope = await _scope.ResolveForAsync(uid, _currentUser.Roles, ct);
        return scope.UserIds.ToList();
    }

    // ===== الصلاحيات والأفعال =====

    private static IReadOnlyList<string> PermissionsFor(IReadOnlyCollection<string> roles) => RoleAccess.PermissionsFor(roles);

    private static IReadOnlyList<DashboardActionDto> ActionsFor(IReadOnlyList<string> permissions)
    {
        var actions = new List<DashboardActionDto>();
        if (permissions.Contains("ApproveReports"))
            actions.Add(new DashboardActionDto("approve", "اعتماد التقارير", "ApproveReports"));
        if (permissions.Contains("ExportReports"))
            actions.Add(new DashboardActionDto("export", "تصدير", "ExportReports"));
        if (permissions.Contains("ManageTemplates"))
            actions.Add(new DashboardActionDto("manageTemplates", "قوالب التقارير", "ManageTemplates"));
        return actions;
    }

    // ===== الفترة =====

    private static string CurrentWeekKey()
    {
        var now = DateTime.UtcNow;
        var week = ISOWeek.GetWeekOfYear(now);
        var year = ISOWeek.GetYear(now);
        return $"{year}-W{week:00}";
    }

    private static string PeriodLabel(string periodKey) =>
        periodKey == CurrentWeekKey() ? "الأسبوع الحالي" : periodKey;

    /// <summary>
    /// B-6 — نبرة البطاقة تُقاس على العتبة المركزيّة القابلة للضبط، لا على 85/70 مكتوبَين هنا.
    /// <c>null</c> يبقى محايدًا: لا تقييم ≠ أداء ضعيف.
    /// </summary>
    private static string KpiStatus(decimal? avg, decimal threshold) => avg switch
    {
        null => "neutral",
        var v when v >= threshold => "green",
        var v when v >= threshold * 0.75m => "amber",
        _ => "red"
    };
}
