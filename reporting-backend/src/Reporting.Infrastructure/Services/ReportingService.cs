using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class ReportingService : IReportingService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;

    /// <summary>الأدوار المسموح لها بعرض تقارير المراقبة (إداريون + دعم الرئيس + مُطّلِع).</summary>
    private static readonly string[] MonitoringRoles =
        Roles.Management.Concat(new[] { Roles.CeoSupport, Roles.Viewer }).ToArray();

    // حالات «أُرسِل التقرير» — تُعدّ تسليمًا قائمًا في متابعة الالتزام (أيّ حالة بعد المسودّة).
    private static readonly SubmissionStatus[] SubmittedStatuses =
    {
        SubmissionStatus.Submitted, SubmissionStatus.Returned, SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel, SubmissionStatus.Escalated, SubmissionStatus.Closed,
        SubmissionStatus.Visible
    };

    private const decimal AlertThreshold = 60m;

    public ReportingService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
    }

    public async Task<Result<SubmissionCompletenessReport>> SubmissionCompletenessAsync(ReportFilter filter, CancellationToken ct = default)
    {
        if (Guard() is { } err) return Result<SubmissionCompletenessReport>.Failure(err.Error!, err.ErrorCode);

        var scope = await _scope.ResolveAsync(ct);
        var q = ApplyFilter(_db.ReportSubmissions.AsNoTracking(), filter);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            q = q.Where(s => ids.Contains(s.SubmitterId));
        }
        var rows = await q.Select(s => new { s.Status, s.DepartmentId }).ToListAsync(ct);

        var total = rows.Count;
        var closed = rows.Count(r => IsComplete(r.Status));
        var pending = total - closed;

        var byStatus = rows.GroupBy(r => r.Status)
            .Select(g => new SubmissionStatusCount(g.Key, g.Count()))
            .OrderBy(x => x.Status)
            .ToList();

        var deptIds = rows.Where(r => r.DepartmentId is not null).Select(r => r.DepartmentId!.Value).Distinct().ToList();
        var deptNames = await _db.Departments.Where(d => deptIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);

        var byDept = rows.GroupBy(r => r.DepartmentId)
            .Select(g =>
            {
                var t = g.Count();
                var c = g.Count(x => IsComplete(x.Status));
                var name = g.Key is Guid id ? deptNames.GetValueOrDefault(id, "غير محدّد") : "غير محدّد";
                return new DepartmentCompletenessRow(g.Key, name, t, c, t - c, Rate(c, t));
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        return Result<SubmissionCompletenessReport>.Success(new SubmissionCompletenessReport(
            filter.PeriodKey, total, closed, pending, Rate(closed, total), byStatus, byDept));
    }

    public async Task<Result<SubmissionComplianceReport>> SubmissionComplianceAsync(string? weekKey, Guid? departmentId, Guid? teamId, CancellationToken ct = default)
    {
        var candRes = await ResolveComplianceCandidatesAsync(departmentId, teamId, ct);
        if (!candRes.Succeeded) return Result<SubmissionComplianceReport>.Failure(candRes.Error!, candRes.ErrorCode);

        var key = NormalizeWeekKey(weekKey);
        var candidates = candRes.Value!;
        if (candidates.Count == 0)
            return Result<SubmissionComplianceReport>.Success(new SubmissionComplianceReport(
                key, ReportCalendarPolicy.ShortWeekLabel(key), 0, 0, 0, 0, 0m, new List<SubmissionComplianceRow>()));

        var evals = await EvaluateComplianceWeekAsync(key, candidates, ct);

        // صفّ واحد لكلّ شخص: الأسبوعي وحدة واحدة؛ اليومي يُلخَّص («سلّم X من Y يوم») لتفادي صفوف متعدّدة.
        var rows = evals
            .GroupBy(e => e.Candidate.UserId)
            .Select(g => BuildPersonRow(key, g.ToList()))
            .OrderBy(r => r.Submitted)        // غير المُسلِّمين أولًا.
            .ThenByDescending(r => r.Late)
            .ThenBy(r => r.FullName, StringComparer.Ordinal)
            .ToList();

        var agg = Aggregate(evals);
        return Result<SubmissionComplianceReport>.Success(new SubmissionComplianceReport(
            key, ReportCalendarPolicy.ShortWeekLabel(key), agg.Expected, agg.Submitted, agg.Missing, agg.Late,
            agg.CompliancePercent, rows, agg.LateSubmitted, agg.MissingOverdue, agg.OnTime, agg.OnTimePercent));
    }

    public async Task<Result<ComplianceSummaryReport>> ComplianceSummaryAsync(string? weekKey, Guid? departmentId, Guid? teamId, CancellationToken ct = default)
    {
        var candRes = await ResolveComplianceCandidatesAsync(departmentId, teamId, ct);
        if (!candRes.Succeeded) return Result<ComplianceSummaryReport>.Failure(candRes.Error!, candRes.ErrorCode);

        var key = NormalizeWeekKey(weekKey);
        var evals = await EvaluateComplianceWeekAsync(key, candRes.Value!, ct);
        var a = Aggregate(evals);
        return Result<ComplianceSummaryReport>.Success(new ComplianceSummaryReport(
            key, ReportCalendarPolicy.ShortWeekLabel(key), a.Expected, a.Submitted, a.Missing, a.Late,
            a.LateSubmitted, a.MissingOverdue, a.OnTime, a.CompliancePercent, a.OnTimePercent));
    }

    public async Task<Result<ComplianceTrendReport>> ComplianceTrendAsync(int weeks, Guid? departmentId, Guid? teamId, CancellationToken ct = default)
    {
        var candRes = await ResolveComplianceCandidatesAsync(departmentId, teamId, ct);
        if (!candRes.Succeeded) return Result<ComplianceTrendReport>.Failure(candRes.Error!, candRes.ErrorCode);

        var n = weeks <= 0 ? 8 : Math.Min(weeks, 26);
        var candidates = candRes.Value!;
        var today = ReportCalendarPolicy.RiyadhToday();

        // مفاتيح آخر n أسابيع (الأقدم → الأحدث)، بلا تكرار مع الحفاظ على الترتيب.
        var keys = new List<string>(n);
        for (var i = n - 1; i >= 0; i--)
        {
            var wk = ReportCalendarPolicy.WeekKeyFor(today.AddDays(-7 * i));
            if (!keys.Contains(wk)) keys.Add(wk);
        }

        var points = new List<ComplianceTrendPoint>(keys.Count);
        foreach (var key in keys)
        {
            var evals = await EvaluateComplianceWeekAsync(key, candidates, ct);
            var a = Aggregate(evals);
            points.Add(new ComplianceTrendPoint(
                key, ReportCalendarPolicy.ShortWeekLabel(key), a.Expected, a.Submitted, a.Late, a.CompliancePercent, a.OnTimePercent));
        }

        return Result<ComplianceTrendReport>.Success(new ComplianceTrendReport(keys.Count, points));
    }

    public async Task<Result<LateByTemplateReport>> LateByTemplateAsync(string? weekKey, Guid? departmentId, Guid? teamId, CancellationToken ct = default)
    {
        var candRes = await ResolveComplianceCandidatesAsync(departmentId, teamId, ct);
        if (!candRes.Succeeded) return Result<LateByTemplateReport>.Failure(candRes.Error!, candRes.ErrorCode);

        var key = NormalizeWeekKey(weekKey);
        var candidates = candRes.Value!;
        var evals = await EvaluateComplianceWeekAsync(key, candidates, ct);

        // عنوان القالب الأساسي المنشور لكلّ مسمّى وظيفي ضمن المرشّحين (المصدر المتوقَّع للتقرير).
        var jobRoleIds = candidates.Where(c => c.JobRoleId is not null).Select(c => c.JobRoleId!.Value).Distinct().ToList();
        var titleByRole = (await _db.ReportTemplates.AsNoTracking()
                .Where(t => t.JobRoleId != null && jobRoleIds.Contains(t.JobRoleId!.Value) && t.IsActive
                            && t.Classification == TemplateClassification.Primary
                            && _db.ReportTemplateVersions.Any(v => v.ReportTemplateId == t.Id && v.IsPublished))
                .Select(t => new { RoleId = t.JobRoleId!.Value, t.Title })
                .ToListAsync(ct))
            .GroupBy(x => x.RoleId)
            .ToDictionary(g => g.Key, g => g.First().Title);

        var rows = evals
            .Where(e => e.Candidate.JobRoleId is not null)
            .GroupBy(e => e.Candidate.JobRoleId!.Value)
            .Select(g =>
            {
                var expected = g.Count();
                var late = g.Count(e => e.Late);
                var missing = g.Count(e => !e.HasSubmission);
                var roleName = g.First().Candidate.JobRoleName ?? "—";
                return new LateByTemplateRow(
                    g.Key, titleByRole.GetValueOrDefault(g.Key, roleName), roleName,
                    expected, late, missing, Rate(late, expected));
            })
            .Where(r => r.Expected > 0)
            .OrderByDescending(r => r.LatePercent)
            .ThenByDescending(r => r.Late)
            .ThenBy(r => r.TemplateTitle, StringComparer.Ordinal)
            .ToList();

        return Result<LateByTemplateReport>.Success(new LateByTemplateReport(
            key, ReportCalendarPolicy.ShortWeekLabel(key), rows));
    }

    public async Task<Result<ComplianceBreakdownReport>> ComplianceBreakdownAsync(string? weekKey, string? groupBy, Guid? departmentId, Guid? teamId, CancellationToken ct = default)
    {
        var candRes = await ResolveComplianceCandidatesAsync(departmentId, teamId, ct);
        if (!candRes.Succeeded) return Result<ComplianceBreakdownReport>.Failure(candRes.Error!, candRes.ErrorCode);

        var byDept = string.Equals(groupBy?.Trim(), "department", StringComparison.OrdinalIgnoreCase);
        var dim = byDept ? "department" : "team";
        var key = NormalizeWeekKey(weekKey);
        var evals = await EvaluateComplianceWeekAsync(key, candRes.Value!, ct);

        var rows = evals
            .GroupBy(e => byDept ? e.Candidate.DepartmentId : e.Candidate.TeamId)
            .Select(g =>
            {
                var a = Aggregate(g.ToList());
                var name = byDept
                    ? (g.First().Candidate.DepartmentName ?? "بدون إدارة")
                    : (g.First().Candidate.TeamName ?? "بدون فريق");
                return new ComplianceBreakdownRow(
                    g.Key, name, a.Expected, a.Submitted, a.Late, a.Missing, a.CompliancePercent, a.OnTimePercent);
            })
            .OrderBy(r => r.CompliancePercent)        // الأضعف التزامًا أولًا.
            .ThenBy(r => r.GroupName, StringComparer.Ordinal)
            .ToList();

        return Result<ComplianceBreakdownReport>.Success(new ComplianceBreakdownReport(
            key, ReportCalendarPolicy.ShortWeekLabel(key), dim, rows));
    }

    // ===== RPT-WORKFLOW-BOTTLENECKS-R1: اختناقات مسار الاعتماد (قراءة فقط، نطاق ScopeResolver) =====

    // حالات «بانتظار اعتماد» — تقرير قد يكون عالقًا عند معتمِد. (Draft/Returned/Closed/Visible مستبعَدة:
    // المسودّة لم تُرسَل، المُعادة بلا معتمِد حالي، المُغلق/الظاهر انتهى مساره.)
    private static readonly SubmissionStatus[] PendingApprovalStatuses =
    {
        SubmissionStatus.Submitted, SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel, SubmissionStatus.Escalated
    };

    public async Task<Result<WorkflowBottlenecksSummaryReport>> WorkflowBottlenecksSummaryAsync(Guid? departmentId, Guid? teamId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<WorkflowBottlenecksSummaryReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var items = await LoadBottlenecksAsync(null, departmentId, teamId, null, false, ct);
        if (items.Count == 0)
            return Result<WorkflowBottlenecksSummaryReport>.Success(new WorkflowBottlenecksSummaryReport(
                0, 0, 0, 0, null, null, 0, null, null, 0));

        var byStage = items.GroupBy(i => i.StageKey)
            .Select(g => new { g.Key, Label = g.First().StageLabel, Count = g.Count() })
            .OrderByDescending(x => x.Count).First();
        var byApprover = items.GroupBy(i => i.ApproverId)
            .Select(g => new { g.Key, Name = g.First().ApproverName, Count = g.Count() })
            .OrderByDescending(x => x.Count).First();

        return Result<WorkflowBottlenecksSummaryReport>.Success(new WorkflowBottlenecksSummaryReport(
            items.Count,
            items.Count(i => i.IsOverdue),
            Math.Round(items.Max(i => i.AgeHours), 1),
            Math.Round(items.Average(i => i.AgeHours), 1),
            byStage.Key, byStage.Label, byStage.Count,
            byApprover.Key, byApprover.Name, byApprover.Count));
    }

    public async Task<Result<WorkflowBottlenecksByStageReport>> WorkflowBottlenecksByStageAsync(Guid? departmentId, Guid? teamId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<WorkflowBottlenecksByStageReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var items = await LoadBottlenecksAsync(null, departmentId, teamId, null, false, ct);
        var rows = items.GroupBy(i => i.StageKey)
            .Select(g => new WorkflowBottleneckStageRow(
                g.Key, g.First().StageLabel, g.Count(), g.Count(i => i.IsOverdue),
                Math.Round(g.Average(i => i.AgeHours), 1), Math.Round(g.Max(i => i.AgeHours), 1),
                g.First().SlaHours))
            .OrderByDescending(r => r.OverdueCount)
            .ThenByDescending(r => r.PendingCount)
            .ToList();
        return Result<WorkflowBottlenecksByStageReport>.Success(new WorkflowBottlenecksByStageReport(rows));
    }

    public async Task<Result<WorkflowBottlenecksByApproverReport>> WorkflowBottlenecksByApproverAsync(Guid? departmentId, Guid? teamId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<WorkflowBottlenecksByApproverReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var items = await LoadBottlenecksAsync(null, departmentId, teamId, null, false, ct);
        var rows = items.GroupBy(i => i.ApproverId)
            .Select(g =>
            {
                var f = g.First();
                return new WorkflowBottleneckApproverRow(
                    g.Key, f.ApproverName, f.ApproverRole, Roles.DisplayAr(f.ApproverRole),
                    f.StageKey, f.StageLabel, g.Count(), g.Count(i => i.IsOverdue),
                    Math.Round(g.Average(i => i.AgeHours), 1), Math.Round(g.Max(i => i.AgeHours), 1));
            })
            .OrderByDescending(r => r.OverdueCount)
            .ThenByDescending(r => r.PendingCount)
            .ThenBy(r => r.ApproverName, StringComparer.Ordinal)
            .ToList();
        return Result<WorkflowBottlenecksByApproverReport>.Success(new WorkflowBottlenecksByApproverReport(rows));
    }

    public async Task<Result<WorkflowBottlenecksDetailsReport>> WorkflowBottlenecksDetailsAsync(string? stage, Guid? departmentId, Guid? teamId, Guid? approverId, bool overdueOnly, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<WorkflowBottlenecksDetailsReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var stageKey = string.IsNullOrWhiteSpace(stage) ? null : stage!.Trim().ToLowerInvariant();
        var items = await LoadBottlenecksAsync(stageKey, departmentId, teamId, approverId, overdueOnly, ct);

        var rows = items
            .OrderByDescending(i => i.IsOverdue)
            .ThenByDescending(i => i.AgeHours)
            .Select(i => new WorkflowBottleneckDetailRow(
                i.SubmissionId, i.TemplateTitle, i.SubmitterName, i.TeamName, i.DepartmentName,
                i.ApproverId, i.ApproverName, Roles.DisplayAr(i.ApproverRole),
                i.StageKey, i.StageLabel, i.Status, StatusLabelAr(i.Status),
                i.SubmittedAtUtc, i.StageEnteredAtUtc, i.AgeHours, i.SlaHours, i.IsOverdue))
            .ToList();

        return Result<WorkflowBottlenecksDetailsReport>.Success(new WorkflowBottlenecksDetailsReport(
            rows.Count, rows.Count(r => r.IsOverdue), rows));
    }

    private sealed record BottleneckItem(
        Guid SubmissionId, string TemplateTitle, Guid SubmitterId, string SubmitterName,
        Guid? TeamId, string? TeamName, Guid? DepartmentId, string? DepartmentName,
        Guid ApproverId, string ApproverName, string ApproverRole,
        string StageKey, string StageLabel, SubmissionStatus Status,
        DateTime? SubmittedAtUtc, DateTime StageEnteredAtUtc, double AgeHours, int SlaHours, bool IsOverdue);

    // تصنيف المرحلة + SLA من دور المعتمِد الحالي. قائد فريق=24h، مدير=48h، الإدارة العليا(GM/CEO/Admin/CeoSupport)=72h.
    private static (string Key, string Label, int Sla) StageForRole(string primaryRole) => primaryRole switch
    {
        Roles.TeamLeader => ("team_leader", "خطوة قائد الفريق", 24),
        Roles.Manager => ("manager", "خطوة المدير", 48),
        Roles.GeneralManager or Roles.Ceo or Roles.Admin or Roles.CeoSupport => ("senior_management", "الإدارة العليا", 72),
        _ => ("other", "مرحلة أخرى", 72)
    };

    /// <summary>
    /// المصدر الوحيد لقائمة التقارير العالقة ضمن نطاق ScopeResolver + الفلاتر. تقرير عالق = حالته انتظار اعتماد
    /// و CurrentApproverId محدَّد و توجد خطوة Pending. عمر المرحلة = الآن − أحدث (أعلى Level) خطوة Pending.CreatedAtUtc.
    /// </summary>
    private async Task<List<BottleneckItem>> LoadBottlenecksAsync(
        string? stageKey, Guid? departmentId, Guid? teamId, Guid? approverId, bool overdueOnly, CancellationToken ct)
    {
        var scope = await _scope.ResolveAsync(ct);

        var q = _db.ReportSubmissions.AsNoTracking()
            .Where(s => PendingApprovalStatuses.Contains(s.Status) && s.CurrentApproverId != null);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            q = q.Where(s => ids.Contains(s.SubmitterId));
        }
        if (departmentId is not null) q = q.Where(s => s.DepartmentId == departmentId);
        if (teamId is not null) q = q.Where(s => s.TeamId == teamId);
        if (approverId is not null) q = q.Where(s => s.CurrentApproverId == approverId);

        var raw = await q.Select(s => new
        {
            s.Id, s.ReportTemplateVersionId, s.SubmitterId, s.TeamId, s.DepartmentId,
            s.Status, s.SubmittedAtUtc, ApproverId = s.CurrentApproverId!.Value,
            PendingSteps = s.ApprovalSteps
                .Where(a => a.Status == ApprovalStatus.Pending)
                .Select(a => new { a.Level, a.CreatedAtUtc })
                .ToList()
        }).ToListAsync(ct);

        // يجب وجود خطوة Pending قائمة (تعريف العالق). عمر المرحلة من أحدث خطوة Pending (أعلى Level).
        var withStep = raw
            .Select(s => new { s, Step = s.PendingSteps.OrderByDescending(p => p.Level).FirstOrDefault() })
            .Where(x => x.Step != null)
            .ToList();
        if (withStep.Count == 0) return new List<BottleneckItem>();

        var versionIds = withStep.Select(x => x.s.ReportTemplateVersionId).Distinct().ToList();
        var titleByVersion = await (from v in _db.ReportTemplateVersions.AsNoTracking()
                                    join t in _db.ReportTemplates on v.ReportTemplateId equals t.Id
                                    where versionIds.Contains(v.Id)
                                    select new { v.Id, t.Title })
            .ToDictionaryAsync(x => x.Id, x => x.Title, ct);

        var submitterIds = withStep.Select(x => x.s.SubmitterId).Distinct().ToList();
        var approverIds = withStep.Select(x => x.s.ApproverId).Distinct().ToList();
        var names = await UserNamesAsync(submitterIds.Concat(approverIds), ct);
        var approverRoles = await UserPrimaryRolesAsync(approverIds, ct);

        var teamIds = withStep.Where(x => x.s.TeamId is not null).Select(x => x.s.TeamId!.Value).Distinct().ToList();
        var teamNames = teamIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Teams.AsNoTracking().Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
        var deptIds = withStep.Where(x => x.s.DepartmentId is not null).Select(x => x.s.DepartmentId!.Value).Distinct().ToList();
        var deptNames = deptIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);

        var now = DateTime.UtcNow;
        var items = new List<BottleneckItem>(withStep.Count);
        foreach (var x in withStep)
        {
            var s = x.s;
            var enteredAt = x.Step!.CreatedAtUtc;
            var role = RoleAccess.PrimaryRole(approverRoles.GetValueOrDefault(s.ApproverId) ?? new List<string>());
            var (key, label, sla) = StageForRole(role);
            var ageHours = Math.Round(Math.Max(0d, (now - enteredAt).TotalHours), 1);
            var overdue = ageHours > sla;

            items.Add(new BottleneckItem(
                s.Id, titleByVersion.GetValueOrDefault(s.ReportTemplateVersionId, "تقرير"),
                s.SubmitterId, names.GetValueOrDefault(s.SubmitterId, "—"),
                s.TeamId, s.TeamId is Guid tid ? teamNames.GetValueOrDefault(tid) : null,
                s.DepartmentId, s.DepartmentId is Guid did ? deptNames.GetValueOrDefault(did) : null,
                s.ApproverId, names.GetValueOrDefault(s.ApproverId, "—"), role,
                key, label, s.Status, s.SubmittedAtUtc, enteredAt, ageHours, sla, overdue));
        }

        if (stageKey is not null) items = items.Where(i => i.StageKey == stageKey).ToList();
        if (overdueOnly) items = items.Where(i => i.IsOverdue).ToList();
        return items;
    }

    // ===== البنية التحتية المشتركة لمتابعة الالتزام (مصدر واحد للمتوقَّع + التصنيف الموحّد للتأخّر) =====
    // تعريف «متأخر» الموحّد عبر الخدمتين: (سلّم بعد موعد دوره) أو (لم يسلّم وانقضى موعد دوره).
    // Compliance% = Submitted/Expected (المتأخر-المُسلَّم يُحتسب التزامًا)؛ OnTime% = OnTime/Expected.
    // كلّ حسابات «اليوم»/الاستحقاق/التأخّر بتوقيت الرياض (ReportCalendarPolicy.RiyadhToday/RiyadhDate).

    private enum ComplianceState { OnTime, LateSubmitted, MissingOverdue, MissingNotOverdue }

    private sealed record ComplianceCandidate(
        Guid UserId, string FullName,
        Guid? DepartmentId, Guid? TeamId, Guid? JobRoleId,
        string? DepartmentName, string? TeamName, string? JobRoleName,
        string PrimaryRole, PeriodType Cadence);

    private sealed record ComplianceEval(
        ComplianceCandidate Candidate, bool HasSubmission, SubmissionStatus? Status,
        DateTime? SubmittedAtUtc, ComplianceState State)
    {
        public bool LateSubmitted => State == ComplianceState.LateSubmitted;
        public bool MissingOverdue => State == ComplianceState.MissingOverdue;
        public bool Late => LateSubmitted || MissingOverdue;     // التعريف الموحّد.
        public bool OnTime => State == ComplianceState.OnTime;
    }

    private readonly record struct ComplianceAggregate(
        int Expected, int Submitted, int Missing, int Late, int LateSubmitted, int MissingOverdue,
        int OnTime, decimal CompliancePercent, decimal OnTimePercent);

    private static string NormalizeWeekKey(string? weekKey) =>
        string.IsNullOrWhiteSpace(weekKey) ? ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday()) : weekKey!.Trim();

    private static ComplianceAggregate Aggregate(IReadOnlyCollection<ComplianceEval> evals)
    {
        var expected = evals.Count;
        var submitted = evals.Count(e => e.HasSubmission);
        var lateSubmitted = evals.Count(e => e.LateSubmitted);
        var missingOverdue = evals.Count(e => e.MissingOverdue);
        var onTime = evals.Count(e => e.OnTime);
        return new ComplianceAggregate(
            expected, submitted, expected - submitted, lateSubmitted + missingOverdue,
            lateSubmitted, missingOverdue, onTime, Rate(submitted, expected), Rate(onTime, expected));
    }

    /// <summary>
    /// المصدر الوحيد للمرشّحين المتوقَّع منهم تقرير (أسبوعي أو يومي) ضمن نطاق المستخدم + الفلاتر الاختيارية.
    /// نفس منطق التذكير: مستخدم نشط بمسمّى له قالب أساسي منشور فعّال دوريّته غير شهرية. الدورية الفعلية للمرشّح
    /// تُشتقّ من ReportCadencePolicy.ExpectedCadence (مبيعات B2C/B2B = يومي، البقية = أسبوعي). (Archived/Draft/
    /// Supplementary و Monthly مستبعَدة — الشهري Known Limitation؛ اليومي مشمول.) RPT-SCOPE1: تقييد بنطاق
    /// ScopeResolver (HR استثناء company-wide موثّق)، ثم الفلاتر.
    /// </summary>
    private async Task<Result<List<ComplianceCandidate>>> ResolveComplianceCandidatesAsync(Guid? departmentId, Guid? teamId, CancellationToken ct)
    {
        // حارس مخصّص يشمل HR (متابعة التزام فقط) — منفصل عن حارس تقارير المراقبة (MonitoringRoles) الذي يستثني HR.
        if (!_currentUser.IsAuthenticated)
            return Result<List<ComplianceCandidate>>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (!_currentUser.IsInAnyRole(Roles.CompletionMonitors))
            return Result<List<ComplianceCandidate>>.Failure("لا تملك صلاحية عرض متابعة الالتزام.", "auth.forbidden");

        // المسمّيات المتوقَّع منها تقرير = لها قالب أساسي منشور فعّال دوريّته غير شهرية (أسبوعي أو يومي).
        // قيد PeriodType != Monthly يستبعد القوالب الشهرية فقط (Known Limitation)، ويُبقي الأسبوعي واليومي.
        var reportingRoleIds = await _db.ReportTemplates.AsNoTracking()
            .Where(t => t.JobRoleId != null && t.IsActive
                        && t.Classification == TemplateClassification.Primary
                        && t.DefaultPeriodType != PeriodType.Monthly
                        && _db.ReportTemplateVersions.Any(v => v.ReportTemplateId == t.Id && v.IsPublished))
            .Select(t => t.JobRoleId!.Value)
            .Distinct()
            .ToListAsync(ct);

        // الدورية الفعلية لكلّ مسمّى من ReportCadencePolicy (تُرجِع Daily أو Weekly فقط — الشهري مُستبعَد سلفًا بقيد القالب).
        var roleCadence = (await _db.JobRoles.AsNoTracking()
                .Where(j => reportingRoleIds.Contains(j.Id))
                .Select(j => new { j.Id, j.Code })
                .ToListAsync(ct))
            .ToDictionary(j => j.Id, j => ReportCadencePolicy.ExpectedCadence(j.Code));

        var reportingRoleIdSet = roleCadence.Keys.ToHashSet();

        var candQ = _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.JobRoleId != null && reportingRoleIdSet.Contains(u.JobRoleId!.Value));

        var scope = await _scope.ResolveAsync(ct);
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

        if (raw.Count == 0) return Result<List<ComplianceCandidate>>.Success(new List<ComplianceCandidate>());

        var ids = raw.Select(c => c.Id).ToList();
        var deptIds = raw.Where(c => c.DepartmentId is not null).Select(c => c.DepartmentId!.Value).Distinct().ToList();
        var teamIds = raw.Where(c => c.TeamId is not null).Select(c => c.TeamId!.Value).Distinct().ToList();
        var jobRoleIds = raw.Where(c => c.JobRoleId is not null).Select(c => c.JobRoleId!.Value).Distinct().ToList();
        var deptNames = await _db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);
        var teamNames = await _db.Teams.AsNoTracking().Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
        var jobRoleNames = await _db.JobRoles.AsNoTracking().Where(j => jobRoleIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, j => j.NameAr, ct);
        var rolesByUser = await UserPrimaryRolesAsync(ids, ct);

        var list = raw.Select(c => new ComplianceCandidate(
            c.Id, c.FullName, c.DepartmentId, c.TeamId, c.JobRoleId,
            c.DepartmentId is Guid did ? deptNames.GetValueOrDefault(did) : null,
            c.TeamId is Guid tid ? teamNames.GetValueOrDefault(tid) : null,
            c.JobRoleId is Guid jid ? jobRoleNames.GetValueOrDefault(jid) : null,
            RoleAccess.PrimaryRole(rolesByUser.GetValueOrDefault(c.Id, new List<string>())),
            c.JobRoleId is Guid jrc ? roleCadence.GetValueOrDefault(jrc, PeriodType.Weekly) : PeriodType.Weekly)).ToList();

        return Result<List<ComplianceCandidate>>.Success(list);
    }

    /// <summary>
    /// أيام التسليم اليومي المتوقَّعة داخل الأسبوع التشغيلي (الخميس → الأربعاء): أيام العمل فقط (تُستبعَد
    /// الجمعة والسبت عطلة نهاية الأسبوع السعودية)، حتى «اليوم» بتوقيت الرياض كحدّ أقصى (لا تُحتسب أيام مستقبلية).
    /// وتُستبعَد أيضًا أيّ أيام قبل أرضيّة الإطلاق المنظّميّة (4 يوليو 2026) — لا توقّع يوميّ قبل الأرضيّة.
    /// </summary>
    // DAILY-BUSINESS-DAY-COMPLIANCE-R1 §4: يُفوَّض بالكامل إلى مصدر الحقيقة المركزيّ
    // (ReportingCalendarPolicy.DailyExpectedDates = نافذة الدورة Sat→Fri + أرضيّة الإطلاق + الأحد→الخميس).
    private static List<DateOnly> DailyExpectedDates(string cycleKey, DateOnly today) =>
        ReportingCalendarPolicy.DailyExpectedDates(cycleKey, today);

    /// <summary>
    /// تقييم التزام أسبوع تشغيلي واحد لمجموعة مرشّحين. الأسبوعي = وحدة متوقَّعة واحدة لكلّ مرشّح وفق موعد دوره
    /// (DueDateForRole). اليومي (مبيعات B2C/B2B) = وحدة متوقَّعة لكلّ يوم عمل ضمن الأسبوع (DailyExpectedDates)،
    /// موعد كلّ يوم نهايته نفسه. «اليوم» والتأخّر بتوقيت الرياض. لا يكشف أيّ محتوى — حالة + وقت التسليم فقط.
    /// </summary>
    private async Task<List<ComplianceEval>> EvaluateComplianceWeekAsync(string key, IReadOnlyList<ComplianceCandidate> candidates, CancellationToken ct)
    {
        if (candidates.Count == 0) return new List<ComplianceEval>();

        var today = ReportCalendarPolicy.RiyadhToday();

        // أرضيّة الانطباق الأسبوعيّة المركزيّة (WEEKLY-REPORTING-APPLICABILITY-FLOOR-R1): دورة أسبوعيّة تبدأ
        // قبل أرضيّة الإطلاق المعتمَدة على مستوى المنظّمة (4 يوليو 2026 = بداية 2026-W28) غير مطلوبة إطلاقًا،
        // فلا تُولِّد أيّ وحدة أسبوعيّة متوقَّعة (لا Expected/Missing/MissingOverdue ولا مقام التزام ولا تخفيض نسبة).
        // مصدر واحد مركزيّ = ApplicabilityFloorPolicy.WeeklyReportingLaunchFloor (نفس ثابت المستهلكات الأخرى).
        // يُطبَّق على المرشّحين الأسبوعيّين هنا؛ واليوميّ (مبيعات) مبوَّب بنفس الأرضيّة المنظّميّة داخل
        // DailyExpectedDates (DAILY-REPORTING-APPLICABILITY-R1) — لا يوم يوميّ متوقَّع قبل 4 يوليو 2026.
        var weekStart = ReportCalendarPolicy.WeekRange(key).Start;
        var weeklyApplicable = ApplicabilityFloorPolicy.IsCycleApplicable(
            weekStart, ApplicabilityFloorPolicy.WeeklyReportingLaunchFloor);

        var weeklyCandidates = weeklyApplicable
            ? candidates.Where(c => c.Cadence != PeriodType.Daily).ToList()
            : new List<ComplianceCandidate>();
        var dailyCandidates = candidates.Where(c => c.Cadence == PeriodType.Daily).ToList();
        var evals = new List<ComplianceEval>(candidates.Count);

        // ===== الأسبوعيون: وحدة واحدة لكلّ مرشّح =====
        if (weeklyCandidates.Count > 0)
        {
            var weeklyIds = weeklyCandidates.Select(c => c.UserId).ToList();
            var submittedRows = await _db.ReportSubmissions.AsNoTracking()
                .Where(s => s.PeriodKey == key && s.PeriodType == PeriodType.Weekly
                            && SubmittedStatuses.Contains(s.Status)
                            && weeklyIds.Contains(s.SubmitterId))
                .Select(s => new { s.SubmitterId, s.Status, s.SubmittedAtUtc })
                .ToListAsync(ct);

            var submittedByUser = submittedRows
                .GroupBy(s => s.SubmitterId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubmittedAtUtc ?? DateTime.MaxValue).First());

            foreach (var c in weeklyCandidates)
            {
                var due = ReportCalendarPolicy.DueDateForRole(key, c.PrimaryRole);
                if (submittedByUser.TryGetValue(c.UserId, out var sub))
                {
                    var submittedDate = sub.SubmittedAtUtc is DateTime at ? ReportCalendarPolicy.RiyadhDate(at) : today;
                    var state = submittedDate > due ? ComplianceState.LateSubmitted : ComplianceState.OnTime;
                    evals.Add(new ComplianceEval(c, true, sub.Status, sub.SubmittedAtUtc, state));
                }
                else
                {
                    var state = today > due ? ComplianceState.MissingOverdue : ComplianceState.MissingNotOverdue;
                    evals.Add(new ComplianceEval(c, false, null, null, state));
                }
            }
        }

        // ===== اليوميون (مبيعات): وحدة لكلّ يوم عمل متوقَّع ضمن الأسبوع =====
        if (dailyCandidates.Count > 0)
        {
            var dates = DailyExpectedDates(key, today);
            if (dates.Count > 0)
            {
                var dailyIds = dailyCandidates.Select(c => c.UserId).ToList();
                var (winStart, winEnd) = ReportingCalendarPolicy.CycleRange(key); // نافذة الدورة اليوميّة Sat→Fri

                // DAILY-…-R1 §3: تحميل عريض بلا فلترة نصّية قياسيّة (كي لا تسقط المفاتيح القديمة مثل 6-7-2026)
                // ثم تطبيع كلّ مفتاح إلى اليوم المنطقيّ داخل نافذة الأسبوع، والمطابقة على (المستخدم، اليوم المنطقيّ).
                var dailyRows = await _db.ReportSubmissions.AsNoTracking()
                    .Where(s => s.PeriodType == PeriodType.Daily && s.PeriodKey != null
                                && SubmittedStatuses.Contains(s.Status)
                                && dailyIds.Contains(s.SubmitterId))
                    .Select(s => new { s.SubmitterId, s.PeriodKey, s.Status, s.SubmittedAtUtc })
                    .ToListAsync(ct);

                var dailyByUserDay = dailyRows
                    .Select(s => ReportingCalendarPolicy.TryCanonicalDay(s.PeriodKey, out var cd)
                        ? new { s.SubmitterId, Day = cd, s.Status, s.SubmittedAtUtc, Ok = true }
                        : new { s.SubmitterId, Day = default(DateOnly), s.Status, s.SubmittedAtUtc, Ok = false })
                    .Where(x => x.Ok && x.Day >= winStart && x.Day <= winEnd)
                    .GroupBy(s => (s.SubmitterId, DayKey: ReportingCalendarPolicy.DayKey(s.Day)))
                    .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubmittedAtUtc ?? DateTime.MaxValue).First());

                foreach (var c in dailyCandidates)
                {
                    foreach (var day in dates)
                    {
                        var dayKey = ReportingCalendarPolicy.DayKey(day);
                        // موعد التقرير اليومي = نهاية يومه؛ متأخّر إن سُلّم بعد ذلك اليوم.
                        if (dailyByUserDay.TryGetValue((c.UserId, dayKey), out var sub))
                        {
                            var submittedDate = sub.SubmittedAtUtc is DateTime at ? ReportCalendarPolicy.RiyadhDate(at) : day;
                            var state = submittedDate > day ? ComplianceState.LateSubmitted : ComplianceState.OnTime;
                            evals.Add(new ComplianceEval(c, true, sub.Status, sub.SubmittedAtUtc, state));
                        }
                        else
                        {
                            var state = today > day ? ComplianceState.MissingOverdue : ComplianceState.MissingNotOverdue;
                            evals.Add(new ComplianceEval(c, false, null, null, state));
                        }
                    }
                }
            }
        }

        return evals;
    }

    /// <summary>
    /// يبني صفّ متابعة واحدًا لشخص من تقييماته في الأسبوع. الأسبوعي = تقييم واحد (حالته كما هي).
    /// اليومي = تلخيص أيام العمل المتوقَّعة («سلّم X من Y يوم» + «(متأخر)» إن وُجد تأخّر): Submitted حين سلّم كلّ
    /// الأيام المتوقَّعة، Late حين تأخّر أو تجاوز يومًا واحدًا على الأقل، وقت التسليم = أحدث تسليم.
    /// </summary>
    private static SubmissionComplianceRow BuildPersonRow(string key, IReadOnlyList<ComplianceEval> userEvals)
    {
        var c = userEvals[0].Candidate;
        if (c.Cadence != PeriodType.Daily)
        {
            var e = userEvals[0];
            return new SubmissionComplianceRow(
                c.UserId, c.FullName, c.DepartmentName, c.TeamName, c.JobRoleName,
                e.HasSubmission,
                e.HasSubmission ? StatusLabelAr(e.Status!.Value) : "لم يسلّم",
                e.Late, e.SubmittedAtUtc, key, e.LateSubmitted);
        }

        var expected = userEvals.Count;
        var submitted = userEvals.Count(x => x.HasSubmission);
        var lateSubmitted = userEvals.Count(x => x.LateSubmitted);
        var late = userEvals.Any(x => x.Late);
        var lastSubmittedAt = userEvals.Where(x => x.SubmittedAtUtc is not null).Max(x => x.SubmittedAtUtc);
        var label = $"سلّم {submitted} من {expected} يوم" + (lateSubmitted > 0 ? " (متأخر)" : "");
        return new SubmissionComplianceRow(
            c.UserId, c.FullName, c.DepartmentName, c.TeamName, c.JobRoleName,
            submitted == expected && expected > 0, label, late, lastSubmittedAt, key, lateSubmitted > 0);
    }

    public async Task<Result<KpiSummaryReport>> KpiSummaryAsync(ReportFilter filter, CancellationToken ct = default)
    {
        if (Guard() is { } err) return Result<KpiSummaryReport>.Failure(err.Error!, err.ErrorCode);

        var scope = await _scope.ResolveAsync(ct);
        var q = _db.KpiEvaluations.AsNoTracking().Where(e => e.TotalScore != null);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            q = q.Where(e => ids.Contains(e.SubjectUserId));
        }
        if (filter.PeriodType is not null) q = q.Where(e => e.PeriodType == filter.PeriodType);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) q = q.Where(e => e.PeriodKey == filter.PeriodKey);
        if (filter.DepartmentId is not null) q = q.Where(e => e.DepartmentId == filter.DepartmentId);
        if (filter.TeamId is not null) q = q.Where(e => e.TeamId == filter.TeamId);

        var rows = await q.Select(e => new
        {
            e.SubjectUserId, e.TotalScore, e.Trend, e.PeriodKey
        }).ToListAsync(ct);

        var names = await UserNamesAsync(rows.Select(r => r.SubjectUserId), ct);
        var summaryRows = rows.Select(r => new KpiSummaryRow(
                r.SubjectUserId, names.GetValueOrDefault(r.SubjectUserId, string.Empty),
                r.TotalScore, r.Trend, r.TotalScore < AlertThreshold, r.PeriodKey))
            .OrderBy(r => r.TotalScore)
            .ToList();

        var evaluated = rows.Count;
        decimal? avg = evaluated > 0 ? Math.Round(rows.Average(r => r.TotalScore!.Value), 2) : null;
        var below = rows.Count(r => r.TotalScore < AlertThreshold);

        return Result<KpiSummaryReport>.Success(new KpiSummaryReport(filter.PeriodKey, evaluated, avg, below, summaryRows));
    }

    public async Task<Result<B2cRollupReport>> B2cSalesRollupAsync(ReportFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<B2cRollupReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);

        // تقليل البيانات خادميًا: مستوى الرؤية يُشتق من دور الطالب لا من الواجهة.
        // own/team/department → يرى صفوف المندوبين التفصيلية؛ company/governance → ملخّص فقط (بلا صفوف ولا أفضل/أضعف).
        var scopeType = RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(_currentUser.Roles));
        var canViewRows = scopeType is "own" or "team" or "department";
        var viewLevel = scopeType switch
        {
            "own" => "self",
            "team" => "team",
            "department" => "department",
            _ => "summary"
        };

        // إيجاد قالب «تقرير مندوب مبيعات B2C الفردي» وحقوله (لكل الإصدارات) — مصدر التجميع.
        var template = await _db.ReportTemplates.AsNoTracking()
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .FirstOrDefaultAsync(t => t.Title == B2cReportSchema.TemplateTitle, ct);

        if (template is null)
            return Result<B2cRollupReport>.Success(EmptyRollup(filter.PeriodKey, viewLevel, canViewRows));

        var versionIds = template.Versions.Select(v => v.Id).ToList();
        var fields = template.Versions.SelectMany(v => v.Fields).ToList();

        Guid? FieldId(string label) =>
            fields.Where(f => f.Label == label).Select(f => (Guid?)f.Id).FirstOrDefault();

        var numericFieldMap = new Dictionary<string, Guid?>
        {
            [B2cReportSchema.Leads] = FieldId(B2cReportSchema.Leads),
            [B2cReportSchema.Calls] = FieldId(B2cReportSchema.Calls),
            [B2cReportSchema.FollowUps] = FieldId(B2cReportSchema.FollowUps),
            [B2cReportSchema.Registrations] = FieldId(B2cReportSchema.Registrations),
            [B2cReportSchema.ClosedDeals] = FieldId(B2cReportSchema.ClosedDeals),
            [B2cReportSchema.TargetRegistrations] = FieldId(B2cReportSchema.TargetRegistrations),
        };
        var lostReasonsFieldId = FieldId(B2cReportSchema.LostReasons);

        // التسليمات (غير المسودّات) لهذا القالب ضمن النطاق والفترة.
        var subsQ = _db.ReportSubmissions.AsNoTracking()
            .Where(s => versionIds.Contains(s.ReportTemplateVersionId) && s.Status != SubmissionStatus.Draft);
        if (filter.PeriodType is not null) subsQ = subsQ.Where(s => s.PeriodType == filter.PeriodType);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) subsQ = subsQ.Where(s => s.PeriodKey == filter.PeriodKey);
        if (filter.TeamId is not null) subsQ = subsQ.Where(s => s.TeamId == filter.TeamId);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            subsQ = subsQ.Where(s => ids.Contains(s.SubmitterId));
        }

        var subs = await subsQ.Select(s => new { s.Id, s.SubmitterId }).ToListAsync(ct);
        if (subs.Count == 0)
            return Result<B2cRollupReport>.Success(EmptyRollup(filter.PeriodKey, viewLevel, canViewRows));

        var subIds = subs.Select(s => s.Id).ToList();
        var values = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => subIds.Contains(v.ReportSubmissionId))
            .Select(v => new { v.ReportSubmissionId, v.TemplateFieldId, v.ValueNumber, v.ValueText })
            .ToListAsync(ct);

        var subToSubmitter = subs.ToDictionary(s => s.Id, s => s.SubmitterId);
        var names = await UserNamesAsync(subs.Select(s => s.SubmitterId), ct);

        decimal Sum(IEnumerable<Guid> submissionIds, string label)
        {
            var fid = numericFieldMap[label];
            if (fid is null) return 0m;
            return values.Where(v => submissionIds.Contains(v.ReportSubmissionId) && v.TemplateFieldId == fid)
                .Sum(v => v.ValueNumber ?? 0m);
        }

        // تجميع لكل مندوب (قد يكون له أكثر من فترة عند غياب مرشّح الفترة).
        var bySubmitter = subs.GroupBy(s => s.SubmitterId).ToList();
        var rows = new List<B2cRollupRow>();
        foreach (var g in bySubmitter)
        {
            var sIds = g.Select(x => x.Id).ToList();
            var leads = Sum(sIds, B2cReportSchema.Leads);
            var calls = Sum(sIds, B2cReportSchema.Calls);
            var followUps = Sum(sIds, B2cReportSchema.FollowUps);
            var registrations = Sum(sIds, B2cReportSchema.Registrations);
            var closed = Sum(sIds, B2cReportSchema.ClosedDeals);
            var target = Sum(sIds, B2cReportSchema.TargetRegistrations);
            var conversion = leads > 0 ? Math.Round(registrations / leads * 100m, 1) : 0m;
            var achievement = target > 0 ? Math.Round(registrations / target * 100m, 1) : 0m;
            rows.Add(new B2cRollupRow(g.Key, names.GetValueOrDefault(g.Key, string.Empty),
                leads, calls, followUps, registrations, closed, target, conversion, achievement, false));
        }

        var totalLeads = rows.Sum(r => r.Leads);
        var totalRegistrations = rows.Sum(r => r.Registrations);
        var totalTarget = rows.Sum(r => r.TargetRegistrations);
        var overallConversion = totalLeads > 0 ? Math.Round(totalRegistrations / totalLeads * 100m, 1) : 0m;
        var overallAchievement = totalTarget > 0 ? Math.Round(totalRegistrations / totalTarget * 100m, 1) : 0m;

        // «يحتاج متابعة» = معدل تحويله أقل من المتوسط العام (وله ليدز).
        var finalRows = rows
            .Select(r => r with { NeedsFollowUp = r.Leads > 0 && r.ConversionRate < overallConversion })
            .OrderByDescending(r => r.Registrations)
            .ToList();

        var best = finalRows.OrderByDescending(r => r.Registrations).ThenByDescending(r => r.ConversionRate).FirstOrDefault();
        var worst = finalRows.Where(r => r.Leads > 0).OrderBy(r => r.ConversionRate).ThenBy(r => r.Registrations).FirstOrDefault();

        var lostReasons = lostReasonsFieldId is null
            ? new List<string>()
            : values.Where(v => v.TemplateFieldId == lostReasonsFieldId && !string.IsNullOrWhiteSpace(v.ValueText))
                .Select(v => v.ValueText!.Trim())
                .Distinct()
                .Take(5)
                .ToList();

        // تقليل البيانات: الأدوار المؤسسية (company/governance) تحصل على الإجماليات فقط —
        // تُحذف صفوف المندوبين والأفضل/الأضعف لأنها تكشف هوية الأفراد. التجميعات تبقى.
        var shapedRows = canViewRows ? (IReadOnlyList<B2cRollupRow>)finalRows : new List<B2cRollupRow>();
        var shapedBest = canViewRows ? best : null;
        var shapedWorst = canViewRows ? worst : null;

        return Result<B2cRollupReport>.Success(new B2cRollupReport(
            filter.PeriodKey,
            finalRows.Count,
            totalLeads,
            rows.Sum(r => r.Calls),
            rows.Sum(r => r.FollowUps),
            totalRegistrations,
            rows.Sum(r => r.ClosedDeals),
            totalTarget,
            overallConversion,
            overallAchievement,
            shapedBest, shapedWorst, shapedRows, lostReasons,
            viewLevel, canViewRows));
    }

    private static B2cRollupReport EmptyRollup(string? periodKey, string viewLevel, bool canViewRows) =>
        new(periodKey, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null,
            new List<B2cRollupRow>(), new List<string>(), viewLevel, canViewRows);

    public async Task<Result<MediaBuyerRollupReport>> MediaBuyerRollupAsync(ReportFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<MediaBuyerRollupReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);

        // تقليل البيانات خادميًا (نفس درس Business-1A): مستوى الرؤية يُشتق من دور الطالب لا من الواجهة.
        // own/team/department → يرى صفوف المشترين التفصيلية؛ company/governance → ملخّص فقط (بلا صفوف ولا أفضل/أضعف).
        var scopeType = RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(_currentUser.Roles));
        var canViewRows = scopeType is "own" or "team" or "department";
        var viewLevel = scopeType switch
        {
            "own" => "self",
            "team" => "team",
            "department" => "department",
            _ => "summary"
        };

        // إيجاد قالب «تقرير النمو والأداء — Media Buyer» وحقوله (لكل الإصدارات) — مصدر التجميع. لا قالب جديد.
        var template = await _db.ReportTemplates.AsNoTracking()
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .FirstOrDefaultAsync(t => t.Title == MediaBuyerReportSchema.TemplateTitle, ct);

        if (template is null)
            return Result<MediaBuyerRollupReport>.Success(EmptyMediaBuyerRollup(filter.PeriodKey, viewLevel, canViewRows));

        var versionIds = template.Versions.Select(v => v.Id).ToList();
        var fields = template.Versions.SelectMany(v => v.Fields).ToList();

        Guid? FieldId(string label) =>
            fields.Where(f => f.Label == label).Select(f => (Guid?)f.Id).FirstOrDefault();

        var spendFid = FieldId(MediaBuyerReportSchema.Spend);
        var leadsFid = FieldId(MediaBuyerReportSchema.Leads);
        var ctrFid = FieldId(MediaBuyerReportSchema.Ctr);
        var conversionFid = FieldId(MediaBuyerReportSchema.ConversionRate);
        var issueCauseFid = FieldId(MediaBuyerReportSchema.IssueCause);
        var decisionsFid = FieldId(MediaBuyerReportSchema.DecisionsNeeded);

        var subsQ = _db.ReportSubmissions.AsNoTracking()
            .Where(s => versionIds.Contains(s.ReportTemplateVersionId) && s.Status != SubmissionStatus.Draft);
        if (filter.PeriodType is not null) subsQ = subsQ.Where(s => s.PeriodType == filter.PeriodType);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) subsQ = subsQ.Where(s => s.PeriodKey == filter.PeriodKey);
        if (filter.TeamId is not null) subsQ = subsQ.Where(s => s.TeamId == filter.TeamId);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            subsQ = subsQ.Where(s => ids.Contains(s.SubmitterId));
        }

        var subs = await subsQ.Select(s => new { s.Id, s.SubmitterId }).ToListAsync(ct);
        if (subs.Count == 0)
            return Result<MediaBuyerRollupReport>.Success(EmptyMediaBuyerRollup(filter.PeriodKey, viewLevel, canViewRows));

        var subIds = subs.Select(s => s.Id).ToList();
        var values = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => subIds.Contains(v.ReportSubmissionId))
            .Select(v => new { v.ReportSubmissionId, v.TemplateFieldId, v.ValueNumber, v.ValueText })
            .ToListAsync(ct);

        // v5: المؤشرات مضمَّنة داخل المشاريع (PRS) — تُضاف فوق مجموع top-level (v1–v4). لا ازدواج (مفاتيح v5 غائبة عن v4).
        var prsFieldIds = fields.Where(f => f.FieldType == FieldType.ProjectRepeatableSection).Select(f => f.Id).ToList();
        var prs = await LoadPrsAsync(prsFieldIds, subIds, ct);

        var names = await UserNamesAsync(subs.Select(s => s.SubmitterId), ct);

        decimal Sum(IEnumerable<Guid> ids, Guid? fid, string prsKey)
        {
            var idList = ids as IList<Guid> ?? ids.ToList();
            var top = fid is null ? 0m
                : values.Where(v => idList.Contains(v.ReportSubmissionId) && v.TemplateFieldId == fid).Sum(v => v.ValueNumber ?? 0m);
            return top + prs.Sum(idList, prsKey);
        }

        decimal Avg(IEnumerable<Guid> ids, Guid? fid, string prsKey)
        {
            var idList = ids as IList<Guid> ?? ids.ToList();
            var nums = fid is null ? new List<decimal>()
                : values.Where(v => idList.Contains(v.ReportSubmissionId) && v.TemplateFieldId == fid && v.ValueNumber is not null)
                    .Select(v => v.ValueNumber!.Value).ToList();
            nums.AddRange(prs.Values(idList, prsKey));
            return nums.Count > 0 ? Math.Round(nums.Average(), 1) : 0m;
        }

        var bySubmitter = subs.GroupBy(s => s.SubmitterId).ToList();
        var rows = new List<MediaBuyerRollupRow>();
        foreach (var g in bySubmitter)
        {
            var sIds = g.Select(x => x.Id).ToList();
            var spend = Sum(sIds, spendFid, MediaBuyerReportSchema.Prs.Spend);
            var leads = Sum(sIds, leadsFid, MediaBuyerReportSchema.Prs.Leads);
            // CPL يُحتسب آليًا = الإنفاق/الليدز (Auto) — أدق من القيمة المُبلَّغة.
            var cpl = leads > 0 ? Math.Round(spend / leads, 2) : 0m;
            var ctr = Avg(sIds, ctrFid, MediaBuyerReportSchema.Prs.Ctr);
            var conversion = Avg(sIds, conversionFid, MediaBuyerReportSchema.Prs.ConversionRate);
            rows.Add(new MediaBuyerRollupRow(g.Key, names.GetValueOrDefault(g.Key, string.Empty),
                spend, leads, cpl, ctr, conversion, false));
        }

        var totalSpend = rows.Sum(r => r.Spend);
        var totalLeads = rows.Sum(r => r.Leads);
        var overallCpl = totalLeads > 0 ? Math.Round(totalSpend / totalLeads, 2) : 0m;
        var avgCtr = rows.Count > 0 ? Math.Round(rows.Average(r => r.Ctr), 1) : 0m;
        var avgConversion = rows.Count > 0 ? Math.Round(rows.Average(r => r.ConversionRate), 1) : 0m;

        // «يحتاج تدخل» = CPL أعلى من المتوسط العام (وله ليدز) → كفاءة إنفاق أضعف.
        var finalRows = rows
            .Select(r => r with { NeedsIntervention = r.Leads > 0 && r.Cpl > overallCpl })
            .OrderBy(r => r.Cpl == 0m ? decimal.MaxValue : r.Cpl)
            .ToList();

        // الأفضل = أقل CPL (وله ليدز) → أعلى كفاءة؛ الأضعف = أعلى CPL → يحتاج تدخل.
        var withLeads = finalRows.Where(r => r.Leads > 0).ToList();
        var best = withLeads.OrderBy(r => r.Cpl).ThenByDescending(r => r.Leads).FirstOrDefault();
        var worst = withLeads.OrderByDescending(r => r.Cpl).ThenBy(r => r.Leads).FirstOrDefault();

        List<string> TextList(Guid? fid, string prsKey) =>
            (fid is null
                ? Enumerable.Empty<string>()
                : values.Where(v => v.TemplateFieldId == fid && !string.IsNullOrWhiteSpace(v.ValueText)).Select(v => v.ValueText!.Trim()))
                .Concat(prs.Texts(prsKey))
                .Distinct().Take(5).ToList();

        var issueCauses = TextList(issueCauseFid, MediaBuyerReportSchema.Prs.IssueCause);
        var decisions = TextList(decisionsFid, MediaBuyerReportSchema.Prs.DecisionsNeeded);

        // تقليل البيانات: الأدوار المؤسسية (company/governance) تحصل على الإجماليات فقط — تُحذف الصفوف والأفضل/الأضعف.
        var shapedRows = canViewRows ? (IReadOnlyList<MediaBuyerRollupRow>)finalRows : new List<MediaBuyerRollupRow>();
        var shapedBest = canViewRows ? best : null;
        var shapedWorst = canViewRows ? worst : null;

        return Result<MediaBuyerRollupReport>.Success(new MediaBuyerRollupReport(
            filter.PeriodKey,
            finalRows.Count,
            totalSpend,
            totalLeads,
            overallCpl,
            avgCtr,
            avgConversion,
            shapedBest, shapedWorst, shapedRows, issueCauses, decisions,
            viewLevel, canViewRows));
    }

    private static MediaBuyerRollupReport EmptyMediaBuyerRollup(string? periodKey, string viewLevel, bool canViewRows) =>
        new(periodKey, 0, 0, 0, 0, 0, 0, null, null,
            new List<MediaBuyerRollupRow>(), new List<string>(), new List<string>(), viewLevel, canViewRows);

    public async Task<Result<SeoRollupReport>> SeoRollupAsync(ReportFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<SeoRollupReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);

        // تقليل البيانات خادميًا (نفس درس Business-1A/1B): مستوى الرؤية يُشتق من دور الطالب لا من الواجهة.
        // own/team/department → يرى صفوف الأعضاء التفصيلية؛ company/governance → ملخّص فقط (بلا صفوف ولا أفضل/أحوج).
        var scopeType = RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(_currentUser.Roles));
        var canViewRows = scopeType is "own" or "team" or "department";
        var viewLevel = scopeType switch
        {
            "own" => "self",
            "team" => "team",
            "department" => "department",
            _ => "summary"
        };

        // يدمج قالبين موجودين مسبقًا (لا قالب جديد): «🔍 تقرير فريق SEO» (كلمات/مهام/مشاكل/فهرسة/Organic)
        // و«تقرير متابعة مقالات SEO الأسبوعي» (أعداد المقالات). نقرأ القيم بالاسم لتجنّب تباعد الأسماء عن الـ Seeder.
        var titles = new[] { SeoReportSchema.TeamTemplateTitle, SeoReportSchema.ArticlesTemplateTitle };
        var templates = await _db.ReportTemplates.AsNoTracking()
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .Where(t => titles.Contains(t.Title))
            .ToListAsync(ct);

        if (templates.Count == 0)
            return Result<SeoRollupReport>.Success(EmptySeoRollup(filter.PeriodKey, viewLevel, canViewRows));

        var versionIds = templates.SelectMany(t => t.Versions.Select(v => v.Id)).ToList();
        var fields = templates.SelectMany(t => t.Versions).SelectMany(v => v.Fields).ToList();

        // أسماء حقول الفريق والمقالات لا تتقاطع → بحث بالاسم عبر القالبين معًا آمن.
        var fieldIdsByLabel = fields.GroupBy(f => f.Label)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Id).ToHashSet());

        HashSet<Guid> Fids(string label) =>
            fieldIdsByLabel.TryGetValue(label, out var set) ? set : new HashSet<Guid>();

        var improvedFids = Fids(SeoReportSchema.ImprovedKeywords);
        var declinedFids = Fids(SeoReportSchema.DeclinedKeywords);
        var organicFids = Fids(SeoReportSchema.OrganicTraffic);
        var indexedFids = Fids(SeoReportSchema.IndexedPages);
        var tasksFids = Fids(SeoReportSchema.TasksDone);
        var techIssuesFids = Fids(SeoReportSchema.TechnicalIssues);
        var plannedFids = Fids(SeoReportSchema.ArticlesPlanned);
        var publishedFids = Fids(SeoReportSchema.ArticlesPublished);
        var lateFids = Fids(SeoReportSchema.ArticlesLate);
        var decisionsFids = Fids(SeoReportSchema.DecisionsNeeded);
        var recommendationsFids = Fids(SeoReportSchema.Recommendations);

        var subsQ = _db.ReportSubmissions.AsNoTracking()
            .Where(s => versionIds.Contains(s.ReportTemplateVersionId) && s.Status != SubmissionStatus.Draft);
        if (filter.PeriodType is not null) subsQ = subsQ.Where(s => s.PeriodType == filter.PeriodType);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) subsQ = subsQ.Where(s => s.PeriodKey == filter.PeriodKey);
        if (filter.TeamId is not null) subsQ = subsQ.Where(s => s.TeamId == filter.TeamId);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            subsQ = subsQ.Where(s => ids.Contains(s.SubmitterId));
        }

        var subs = await subsQ.Select(s => new { s.Id, s.SubmitterId }).ToListAsync(ct);
        if (subs.Count == 0)
            return Result<SeoRollupReport>.Success(EmptySeoRollup(filter.PeriodKey, viewLevel, canViewRows));

        var subIds = subs.Select(s => s.Id).ToList();
        var values = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => subIds.Contains(v.ReportSubmissionId))
            .Select(v => new { v.ReportSubmissionId, v.TemplateFieldId, v.ValueNumber, v.ValueText })
            .ToListAsync(ct);

        // v5: المؤشرات مضمَّنة داخل المشاريع (PRS) — تُضاف فوق مجموع top-level (v1–v4). لا ازدواج (مفاتيح v5 غائبة عن v4).
        var prsFieldIds = fields.Where(f => f.FieldType == FieldType.ProjectRepeatableSection).Select(f => f.Id).ToList();
        var prs = await LoadPrsAsync(prsFieldIds, subIds, ct);

        var names = await UserNamesAsync(subs.Select(s => s.SubmitterId), ct);

        decimal Sum(IEnumerable<Guid> ids, HashSet<Guid> fids, string prsKey)
        {
            var idList = ids as IList<Guid> ?? ids.ToList();
            var top = fids.Count == 0 ? 0m
                : values.Where(v => idList.Contains(v.ReportSubmissionId) && fids.Contains(v.TemplateFieldId)).Sum(v => v.ValueNumber ?? 0m);
            return top + prs.Sum(idList, prsKey);
        }

        var bySubmitter = subs.GroupBy(s => s.SubmitterId).ToList();
        var rows = new List<SeoRollupRow>();
        foreach (var g in bySubmitter)
        {
            var sIds = g.Select(x => x.Id).ToList();
            var improved = Sum(sIds, improvedFids, SeoReportSchema.Prs.ImprovedKeywords);
            var declined = Sum(sIds, declinedFids, SeoReportSchema.Prs.DeclinedKeywords);
            // صافي تحسّن الكلمات يُحتسب آليًا = تحسّنت − تراجعت (Auto من حقول يدوية).
            var net = improved - declined;
            var planned = Sum(sIds, plannedFids, SeoReportSchema.Prs.ArticlesPlanned);
            var published = Sum(sIds, publishedFids, SeoReportSchema.Prs.ArticlesPublished);
            // معدّل إنجاز المحتوى لهذا العضو = منشورة/مخطّط لها (٪).
            var contentRate = planned > 0 ? Math.Round(published / planned * 100m, 1) : 0m;
            rows.Add(new SeoRollupRow(
                g.Key, names.GetValueOrDefault(g.Key, string.Empty),
                improved, declined, net,
                Sum(sIds, tasksFids, SeoReportSchema.Prs.TasksDone), Sum(sIds, techIssuesFids, SeoReportSchema.Prs.TechnicalIssues),
                Sum(sIds, indexedFids, SeoReportSchema.Prs.IndexedPages), Sum(sIds, organicFids, SeoReportSchema.Prs.OrganicTraffic),
                planned, published, Sum(sIds, lateFids, SeoReportSchema.Prs.ArticlesLate),
                contentRate, false));
        }

        var totalImproved = rows.Sum(r => r.ImprovedKeywords);
        var totalDeclined = rows.Sum(r => r.DeclinedKeywords);
        // صافي حركة الكلمات إجمالًا = إجمالي تحسّنت − إجمالي تراجعت.
        var netMovement = totalImproved - totalDeclined;
        var totalPlanned = rows.Sum(r => r.ArticlesPlanned);
        var totalPublished = rows.Sum(r => r.ArticlesPublished);
        var contentDelivery = totalPlanned > 0 ? Math.Round(totalPublished / totalPlanned * 100m, 1) : 0m;

        // «يحتاج متابعة» = صافي الكلمات سالب (تراجَع أكثر من التحسّن).
        var finalRows = rows
            .Select(r => r with { NeedsFollowup = r.NetKeywords < 0 })
            .OrderByDescending(r => r.NetKeywords)
            .ToList();

        // الأفضل = أعلى صافي كلمات؛ الأحوج للمتابعة = أدنى صافي كلمات.
        var best = finalRows.OrderByDescending(r => r.NetKeywords).FirstOrDefault();
        var worst = finalRows.OrderBy(r => r.NetKeywords).FirstOrDefault();

        List<string> TextList(HashSet<Guid> fids, string prsKey) =>
            values.Where(v => fids.Contains(v.TemplateFieldId) && !string.IsNullOrWhiteSpace(v.ValueText))
                .Select(v => v.ValueText!.Trim())
                .Concat(prs.Texts(prsKey))
                .Distinct().Take(5).ToList();

        var decisions = TextList(decisionsFids, SeoReportSchema.Prs.DecisionsNeeded);
        var recommendations = TextList(recommendationsFids, SeoReportSchema.Prs.Recommendations);

        // تقليل البيانات: الأدوار المؤسسية (company/governance) تحصل على الإجماليات فقط — تُحذف الصفوف والأفضل/الأحوج.
        var shapedRows = canViewRows ? (IReadOnlyList<SeoRollupRow>)finalRows : new List<SeoRollupRow>();
        var shapedBest = canViewRows ? best : null;
        var shapedWorst = canViewRows ? worst : null;

        return Result<SeoRollupReport>.Success(new SeoRollupReport(
            filter.PeriodKey,
            finalRows.Count,
            totalImproved,
            totalDeclined,
            netMovement,
            rows.Sum(r => r.TasksDone),
            rows.Sum(r => r.TechnicalIssues),
            rows.Sum(r => r.IndexedPages),
            rows.Sum(r => r.OrganicTraffic),
            totalPlanned,
            totalPublished,
            rows.Sum(r => r.ArticlesLate),
            contentDelivery,
            shapedBest, shapedWorst, shapedRows, decisions, recommendations,
            viewLevel, canViewRows));
    }

    private static SeoRollupReport EmptySeoRollup(string? periodKey, string viewLevel, bool canViewRows) =>
        new(periodKey, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null,
            new List<SeoRollupRow>(), new List<string>(), new List<string>(), viewLevel, canViewRows);

    public async Task<Result<ContentWriterRollupReport>> ContentWriterRollupAsync(ReportFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<ContentWriterRollupReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);

        // تقليل البيانات خادميًا (نفس درس Business-1A/1B/1C): مستوى الرؤية يُشتق من دور الطالب لا من الواجهة.
        var scopeType = RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(_currentUser.Roles));
        var canViewRows = scopeType is "own" or "team" or "department";
        var viewLevel = scopeType switch
        {
            "own" => "self",
            "team" => "team",
            "department" => "department",
            _ => "summary"
        };

        // يعتمد على قالب واحد موجود مسبقًا (لا قالب جديد): «تقرير كاتب المحتوى الأسبوعي».
        var template = await _db.ReportTemplates.AsNoTracking()
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .Where(t => t.Title == ContentWriterReportSchema.TemplateTitle)
            .FirstOrDefaultAsync(ct);

        if (template is null)
            return Result<ContentWriterRollupReport>.Success(EmptyContentWriterRollup(filter.PeriodKey, viewLevel, canViewRows));

        var versionIds = template.Versions.Select(v => v.Id).ToList();
        var fields = template.Versions.SelectMany(v => v.Fields).ToList();

        var fieldIdsByLabel = fields.GroupBy(f => f.Label)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Id).ToHashSet());

        HashSet<Guid> Fids(string label) =>
            fieldIdsByLabel.TryGetValue(label, out var set) ? set : new HashSet<Guid>();

        var requiredFids = Fids(ContentWriterReportSchema.RequiredPieces);
        var deliveredFids = Fids(ContentWriterReportSchema.DeliveredPieces);
        var approvedFids = Fids(ContentWriterReportSchema.ApprovedFirstTime);
        var lateFids = Fids(ContentWriterReportSchema.LatePieces);
        var achievementFids = Fids(ContentWriterReportSchema.OutputAchievement);
        var delayFids = Fids(ContentWriterReportSchema.DelayReasons);
        var challengesFids = Fids(ContentWriterReportSchema.Challenges);

        var subsQ = _db.ReportSubmissions.AsNoTracking()
            .Where(s => versionIds.Contains(s.ReportTemplateVersionId) && s.Status != SubmissionStatus.Draft);
        if (filter.PeriodType is not null) subsQ = subsQ.Where(s => s.PeriodType == filter.PeriodType);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) subsQ = subsQ.Where(s => s.PeriodKey == filter.PeriodKey);
        if (filter.TeamId is not null) subsQ = subsQ.Where(s => s.TeamId == filter.TeamId);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            subsQ = subsQ.Where(s => ids.Contains(s.SubmitterId));
        }

        var subs = await subsQ.Select(s => new { s.Id, s.SubmitterId }).ToListAsync(ct);
        if (subs.Count == 0)
            return Result<ContentWriterRollupReport>.Success(EmptyContentWriterRollup(filter.PeriodKey, viewLevel, canViewRows));

        var subIds = subs.Select(s => s.Id).ToList();
        var values = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => subIds.Contains(v.ReportSubmissionId))
            .Select(v => new { v.ReportSubmissionId, v.TemplateFieldId, v.ValueNumber, v.ValueText })
            .ToListAsync(ct);

        // v5: المؤشرات مضمَّنة داخل المشاريع (PRS) — تُضاف فوق مجموع top-level (v1–v4). لا ازدواج (مفاتيح v5 غائبة عن v4).
        var prsFieldIds = fields.Where(f => f.FieldType == FieldType.ProjectRepeatableSection).Select(f => f.Id).ToList();
        var prs = await LoadPrsAsync(prsFieldIds, subIds, ct);

        var names = await UserNamesAsync(subs.Select(s => s.SubmitterId), ct);

        decimal Sum(IEnumerable<Guid> ids, HashSet<Guid> fids, string prsKey)
        {
            var idList = ids as IList<Guid> ?? ids.ToList();
            var top = fids.Count == 0 ? 0m
                : values.Where(v => idList.Contains(v.ReportSubmissionId) && fids.Contains(v.TemplateFieldId)).Sum(v => v.ValueNumber ?? 0m);
            return top + prs.Sum(idList, prsKey);
        }

        decimal Avg(IEnumerable<Guid> ids, HashSet<Guid> fids, string prsKey)
        {
            var idsList = ids as ICollection<Guid> ?? ids.ToList();
            var nums = fids.Count == 0 ? new List<decimal>()
                : values
                    .Where(v => idsList.Contains(v.ReportSubmissionId) && fids.Contains(v.TemplateFieldId) && v.ValueNumber.HasValue)
                    .Select(v => v.ValueNumber!.Value)
                    .ToList();
            nums.AddRange(prs.Values(idsList, prsKey));
            return nums.Count == 0 ? 0m : Math.Round(nums.Average(), 1);
        }

        var bySubmitter = subs.GroupBy(s => s.SubmitterId).ToList();
        var rows = new List<ContentWriterRollupRow>();
        foreach (var g in bySubmitter)
        {
            var sIds = g.Select(x => x.Id).ToList();
            var required = Sum(sIds, requiredFids, ContentWriterReportSchema.Prs.RequiredPieces);
            var delivered = Sum(sIds, deliveredFids, ContentWriterReportSchema.Prs.DeliveredPieces);
            var approved = Sum(sIds, approvedFids, ContentWriterReportSchema.Prs.ApprovedFirstTime);
            var late = Sum(sIds, lateFids, ContentWriterReportSchema.Prs.LatePieces);
            // المعادة للتعديل تُحتسب آليًا = المسلَّمة − المعتمدة من أول مرة (لا تقل عن صفر).
            var revised = Math.Max(0m, delivered - approved);
            var firstApproval = delivered > 0 ? Math.Round(approved / delivered * 100m, 1) : 0m;
            var revisionRate = delivered > 0 ? Math.Round(revised / delivered * 100m, 1) : 0m;
            var planAdherence = Avg(sIds, achievementFids, ContentWriterReportSchema.Prs.OutputAchievement);
            rows.Add(new ContentWriterRollupRow(
                g.Key, names.GetValueOrDefault(g.Key, string.Empty),
                required, delivered, approved, late, revised,
                firstApproval, revisionRate, planAdherence, false));
        }

        var totalRequired = rows.Sum(r => r.RequiredPieces);
        var totalDelivered = rows.Sum(r => r.DeliveredPieces);
        var totalApproved = rows.Sum(r => r.ApprovedFirstTime);
        var totalLate = rows.Sum(r => r.LatePieces);
        var totalRevised = Math.Max(0m, totalDelivered - totalApproved);
        var contentDelivery = totalRequired > 0 ? Math.Round(totalDelivered / totalRequired * 100m, 1) : 0m;
        var firstApprovalRate = totalDelivered > 0 ? Math.Round(totalApproved / totalDelivered * 100m, 1) : 0m;
        var revisionRateTotal = totalDelivered > 0 ? Math.Round(totalRevised / totalDelivered * 100m, 1) : 0m;
        var avgPlanAdherence = rows.Count > 0 ? Math.Round(rows.Average(r => r.PlanAdherence), 1) : 0m;

        // «يحتاج متابعة» = نسبة الاعتماد من أول مرة < 70٪ أو وجود محتوى متأخر.
        var finalRows = rows
            .Select(r => r with { NeedsFollowup = r.FirstApprovalRate < 70m || r.LatePieces > 0 })
            .OrderByDescending(r => r.FirstApprovalRate)
            .ToList();

        // الأفضل = أعلى نسبة اعتماد من أول مرة؛ الأحوج للمتابعة = أدناها.
        var best = finalRows.OrderByDescending(r => r.FirstApprovalRate).FirstOrDefault();
        var worst = finalRows.OrderBy(r => r.FirstApprovalRate).FirstOrDefault();

        List<string> TextList(HashSet<Guid> fids, string prsKey) =>
            values.Where(v => fids.Contains(v.TemplateFieldId) && !string.IsNullOrWhiteSpace(v.ValueText))
                .Select(v => v.ValueText!.Trim())
                .Concat(prs.Texts(prsKey))
                .Distinct().Take(5).ToList();

        var delayReasons = TextList(delayFids, ContentWriterReportSchema.Prs.DelayReasons);
        var decisions = TextList(challengesFids, ContentWriterReportSchema.Prs.Challenges);

        // تقليل البيانات: الأدوار المؤسسية (company/governance) تحصل على الإجماليات فقط — تُحذف الصفوف والأفضل/الأحوج.
        var shapedRows = canViewRows ? (IReadOnlyList<ContentWriterRollupRow>)finalRows : new List<ContentWriterRollupRow>();
        var shapedBest = canViewRows ? best : null;
        var shapedWorst = canViewRows ? worst : null;

        return Result<ContentWriterRollupReport>.Success(new ContentWriterRollupReport(
            filter.PeriodKey,
            finalRows.Count,
            totalRequired,
            totalDelivered,
            totalApproved,
            totalLate,
            totalRevised,
            contentDelivery,
            firstApprovalRate,
            revisionRateTotal,
            avgPlanAdherence,
            shapedBest, shapedWorst, shapedRows, delayReasons, decisions,
            viewLevel, canViewRows));
    }

    private static ContentWriterRollupReport EmptyContentWriterRollup(string? periodKey, string viewLevel, bool canViewRows) =>
        new(periodKey, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null,
            new List<ContentWriterRollupRow>(), new List<string>(), new List<string>(), viewLevel, canViewRows);

    public async Task<Result<DesignerRollupReport>> DesignerRollupAsync(ReportFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<DesignerRollupReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);

        // تقليل البيانات خادميًا (نفس درس Business-1A/1B/1C/1D-1): مستوى الرؤية يُشتق من دور الطالب لا من الواجهة.
        var scopeType = RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(_currentUser.Roles));
        var canViewRows = scopeType is "own" or "team" or "department";
        var viewLevel = scopeType switch
        {
            "own" => "self",
            "team" => "team",
            "department" => "department",
            _ => "summary"
        };

        // يعتمد على قالب واحد موجود مسبقًا (لا قالب جديد): «تقرير فريق التصميم».
        var template = await _db.ReportTemplates.AsNoTracking()
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .Where(t => t.Title == DesignerReportSchema.TemplateTitle)
            .FirstOrDefaultAsync(ct);

        if (template is null)
            return Result<DesignerRollupReport>.Success(EmptyDesignerRollup(filter.PeriodKey, viewLevel, canViewRows));

        var versionIds = template.Versions.Select(v => v.Id).ToList();
        var fields = template.Versions.SelectMany(v => v.Fields).ToList();

        var fieldIdsByLabel = fields.GroupBy(f => f.Label)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Id).ToHashSet());

        HashSet<Guid> Fids(string label) =>
            fieldIdsByLabel.TryGetValue(label, out var set) ? set : new HashSet<Guid>();

        var requestedFids = Fids(DesignerReportSchema.RequestedDesigns);
        var deliveredFids = Fids(DesignerReportSchema.DeliveredDesigns);
        var approvedFids = Fids(DesignerReportSchema.ApprovedFirstTime);
        var lateFids = Fids(DesignerReportSchema.LateDesigns);
        var pendingFids = Fids(DesignerReportSchema.PendingReview);
        var revisedFids = Fids(DesignerReportSchema.RevisedDesigns);
        var achievementFids = Fids(DesignerReportSchema.OutputAchievement);
        var delayFids = Fids(DesignerReportSchema.DelayReasons);
        var challengesFids = Fids(DesignerReportSchema.Challenges);

        var subsQ = _db.ReportSubmissions.AsNoTracking()
            .Where(s => versionIds.Contains(s.ReportTemplateVersionId) && s.Status != SubmissionStatus.Draft);
        if (filter.PeriodType is not null) subsQ = subsQ.Where(s => s.PeriodType == filter.PeriodType);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) subsQ = subsQ.Where(s => s.PeriodKey == filter.PeriodKey);
        if (filter.TeamId is not null) subsQ = subsQ.Where(s => s.TeamId == filter.TeamId);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            subsQ = subsQ.Where(s => ids.Contains(s.SubmitterId));
        }

        var subs = await subsQ.Select(s => new { s.Id, s.SubmitterId }).ToListAsync(ct);
        if (subs.Count == 0)
            return Result<DesignerRollupReport>.Success(EmptyDesignerRollup(filter.PeriodKey, viewLevel, canViewRows));

        var subIds = subs.Select(s => s.Id).ToList();
        var values = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => subIds.Contains(v.ReportSubmissionId))
            .Select(v => new { v.ReportSubmissionId, v.TemplateFieldId, v.ValueNumber, v.ValueText })
            .ToListAsync(ct);

        // v5: المؤشرات مضمَّنة داخل المشاريع (PRS) — تُضاف فوق مجموع top-level (v1–v4). لا ازدواج (مفاتيح v5 غائبة عن v4).
        var prsFieldIds = fields.Where(f => f.FieldType == FieldType.ProjectRepeatableSection).Select(f => f.Id).ToList();
        var prs = await LoadPrsAsync(prsFieldIds, subIds, ct);

        var names = await UserNamesAsync(subs.Select(s => s.SubmitterId), ct);

        decimal Sum(IEnumerable<Guid> ids, HashSet<Guid> fids, string prsKey)
        {
            var idList = ids as IList<Guid> ?? ids.ToList();
            var top = fids.Count == 0 ? 0m
                : values.Where(v => idList.Contains(v.ReportSubmissionId) && fids.Contains(v.TemplateFieldId)).Sum(v => v.ValueNumber ?? 0m);
            return top + prs.Sum(idList, prsKey);
        }

        decimal Avg(IEnumerable<Guid> ids, HashSet<Guid> fids, string prsKey)
        {
            var idsList = ids as ICollection<Guid> ?? ids.ToList();
            var nums = fids.Count == 0 ? new List<decimal>()
                : values
                    .Where(v => idsList.Contains(v.ReportSubmissionId) && fids.Contains(v.TemplateFieldId) && v.ValueNumber.HasValue)
                    .Select(v => v.ValueNumber!.Value)
                    .ToList();
            nums.AddRange(prs.Values(idsList, prsKey));
            return nums.Count == 0 ? 0m : Math.Round(nums.Average(), 1);
        }

        var bySubmitter = subs.GroupBy(s => s.SubmitterId).ToList();
        var rows = new List<DesignerRollupRow>();
        foreach (var g in bySubmitter)
        {
            var sIds = g.Select(x => x.Id).ToList();
            var requested = Sum(sIds, requestedFids, DesignerReportSchema.Prs.RequestedDesigns);
            var delivered = Sum(sIds, deliveredFids, DesignerReportSchema.Prs.DeliveredDesigns);
            var approved = Sum(sIds, approvedFids, DesignerReportSchema.Prs.ApprovedFirstTime);
            var late = Sum(sIds, lateFids, DesignerReportSchema.Prs.LateDesigns);
            var pending = Sum(sIds, pendingFids, DesignerReportSchema.Prs.PendingReview);
            // المعادة للتعديل تُقرأ مباشرة من حقل «أعيدت للتعديل» (القالب أغنى من كاتب المحتوى).
            var revised = Sum(sIds, revisedFids, DesignerReportSchema.Prs.RevisedDesigns);
            var firstApproval = delivered > 0 ? Math.Round(approved / delivered * 100m, 1) : 0m;
            var revisionRate = delivered > 0 ? Math.Round(revised / delivered * 100m, 1) : 0m;
            // نسبة الالتزام بالمواعيد = (المسلَّمة − المتأخرة) / المسلَّمة (٪).
            var onTime = delivered > 0 ? Math.Round(Math.Max(0m, delivered - late) / delivered * 100m, 1) : 0m;
            var planAdherence = Avg(sIds, achievementFids, DesignerReportSchema.Prs.OutputAchievement);
            rows.Add(new DesignerRollupRow(
                g.Key, names.GetValueOrDefault(g.Key, string.Empty),
                requested, delivered, approved, late, pending, revised,
                firstApproval, revisionRate, onTime, planAdherence, false));
        }

        var totalRequested = rows.Sum(r => r.RequestedDesigns);
        var totalDelivered = rows.Sum(r => r.DeliveredDesigns);
        var totalApproved = rows.Sum(r => r.ApprovedFirstTime);
        var totalLate = rows.Sum(r => r.LateDesigns);
        var totalPending = rows.Sum(r => r.PendingReview);
        var totalRevised = rows.Sum(r => r.RevisedDesigns);
        var deliveryRate = totalRequested > 0 ? Math.Round(totalDelivered / totalRequested * 100m, 1) : 0m;
        var firstApprovalRate = totalDelivered > 0 ? Math.Round(totalApproved / totalDelivered * 100m, 1) : 0m;
        var revisionRateTotal = totalDelivered > 0 ? Math.Round(totalRevised / totalDelivered * 100m, 1) : 0m;
        var onTimeRateTotal = totalDelivered > 0 ? Math.Round(Math.Max(0m, totalDelivered - totalLate) / totalDelivered * 100m, 1) : 0m;
        var avgPlanAdherence = rows.Count > 0 ? Math.Round(rows.Average(r => r.PlanAdherence), 1) : 0m;

        // «يحتاج متابعة» = نسبة الاعتماد من أول مرة < 70٪ أو وجود تصاميم متأخرة.
        var finalRows = rows
            .Select(r => r with { NeedsFollowup = r.FirstApprovalRate < 70m || r.LateDesigns > 0 })
            .OrderByDescending(r => r.FirstApprovalRate)
            .ToList();

        // الأفضل = أعلى نسبة اعتماد من أول مرة؛ الأحوج للمتابعة = أدناها.
        var best = finalRows.OrderByDescending(r => r.FirstApprovalRate).FirstOrDefault();
        var worst = finalRows.OrderBy(r => r.FirstApprovalRate).FirstOrDefault();

        List<string> TextList(HashSet<Guid> fids, string prsKey) =>
            values.Where(v => fids.Contains(v.TemplateFieldId) && !string.IsNullOrWhiteSpace(v.ValueText))
                .Select(v => v.ValueText!.Trim())
                .Concat(prs.Texts(prsKey))
                .Distinct().Take(5).ToList();

        var delayReasons = TextList(delayFids, DesignerReportSchema.Prs.DelayReasons);
        var decisions = TextList(challengesFids, DesignerReportSchema.Prs.Challenges);

        // تقليل البيانات: الأدوار المؤسسية (company/governance) تحصل على الإجماليات فقط — تُحذف الصفوف والأفضل/الأحوج.
        var shapedRows = canViewRows ? (IReadOnlyList<DesignerRollupRow>)finalRows : new List<DesignerRollupRow>();
        var shapedBest = canViewRows ? best : null;
        var shapedWorst = canViewRows ? worst : null;

        return Result<DesignerRollupReport>.Success(new DesignerRollupReport(
            filter.PeriodKey,
            finalRows.Count,
            totalRequested,
            totalDelivered,
            totalApproved,
            totalLate,
            totalPending,
            totalRevised,
            deliveryRate,
            firstApprovalRate,
            revisionRateTotal,
            onTimeRateTotal,
            avgPlanAdherence,
            shapedBest, shapedWorst, shapedRows, delayReasons, decisions,
            viewLevel, canViewRows));
    }

    private static DesignerRollupReport EmptyDesignerRollup(string? periodKey, string viewLevel, bool canViewRows) =>
        new(periodKey, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null,
            new List<DesignerRollupRow>(), new List<string>(), new List<string>(), viewLevel, canViewRows);

    public async Task<Result<VideoRollupReport>> VideoRollupAsync(ReportFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<VideoRollupReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);

        // تقليل البيانات خادميًا (نفس درس Business-1A/1B/1C/1D-1/1D-2): مستوى الرؤية يُشتق من دور الطالب لا من الواجهة.
        var scopeType = RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(_currentUser.Roles));
        var canViewRows = scopeType is "own" or "team" or "department";
        var viewLevel = scopeType switch
        {
            "own" => "self",
            "team" => "team",
            "department" => "department",
            _ => "summary"
        };

        // يعتمد على قالب واحد موجود مسبقًا (لا قالب جديد): «تقرير فريق الفيديو».
        var template = await _db.ReportTemplates.AsNoTracking()
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .Where(t => t.Title == VideoReportSchema.TemplateTitle)
            .FirstOrDefaultAsync(ct);

        if (template is null)
            return Result<VideoRollupReport>.Success(EmptyVideoRollup(filter.PeriodKey, viewLevel, canViewRows));

        var versionIds = template.Versions.Select(v => v.Id).ToList();
        var fields = template.Versions.SelectMany(v => v.Fields).ToList();

        var fieldIdsByLabel = fields.GroupBy(f => f.Label)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Id).ToHashSet());

        HashSet<Guid> Fids(string label) =>
            fieldIdsByLabel.TryGetValue(label, out var set) ? set : new HashSet<Guid>();

        var requestedFids = Fids(VideoReportSchema.RequestedVideos);
        var deliveredFids = Fids(VideoReportSchema.DeliveredVideos);
        var approvedFids = Fids(VideoReportSchema.ApprovedFirstTime);
        var lateFids = Fids(VideoReportSchema.LateVideos);
        var pendingFids = Fids(VideoReportSchema.PendingReview);
        var revisedFids = Fids(VideoReportSchema.RevisedVideos);
        var achievementFids = Fids(VideoReportSchema.OutputAchievement);
        var delayFids = Fids(VideoReportSchema.DelayReasons);
        var challengesFids = Fids(VideoReportSchema.Challenges);

        var subsQ = _db.ReportSubmissions.AsNoTracking()
            .Where(s => versionIds.Contains(s.ReportTemplateVersionId) && s.Status != SubmissionStatus.Draft);
        if (filter.PeriodType is not null) subsQ = subsQ.Where(s => s.PeriodType == filter.PeriodType);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) subsQ = subsQ.Where(s => s.PeriodKey == filter.PeriodKey);
        if (filter.TeamId is not null) subsQ = subsQ.Where(s => s.TeamId == filter.TeamId);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            subsQ = subsQ.Where(s => ids.Contains(s.SubmitterId));
        }

        var subs = await subsQ.Select(s => new { s.Id, s.SubmitterId }).ToListAsync(ct);
        if (subs.Count == 0)
            return Result<VideoRollupReport>.Success(EmptyVideoRollup(filter.PeriodKey, viewLevel, canViewRows));

        var subIds = subs.Select(s => s.Id).ToList();
        var values = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => subIds.Contains(v.ReportSubmissionId))
            .Select(v => new { v.ReportSubmissionId, v.TemplateFieldId, v.ValueNumber, v.ValueText })
            .ToListAsync(ct);

        // v5: المؤشرات مضمَّنة داخل المشاريع (PRS) — تُضاف فوق مجموع top-level (v1–v4). لا ازدواج (مفاتيح v5 غائبة عن v4).
        var prsFieldIds = fields.Where(f => f.FieldType == FieldType.ProjectRepeatableSection).Select(f => f.Id).ToList();
        var prs = await LoadPrsAsync(prsFieldIds, subIds, ct);

        var names = await UserNamesAsync(subs.Select(s => s.SubmitterId), ct);

        decimal Sum(IEnumerable<Guid> ids, HashSet<Guid> fids, string prsKey)
        {
            var idList = ids as IList<Guid> ?? ids.ToList();
            var top = fids.Count == 0 ? 0m
                : values.Where(v => idList.Contains(v.ReportSubmissionId) && fids.Contains(v.TemplateFieldId)).Sum(v => v.ValueNumber ?? 0m);
            return top + prs.Sum(idList, prsKey);
        }

        decimal Avg(IEnumerable<Guid> ids, HashSet<Guid> fids, string prsKey)
        {
            var idsList = ids as ICollection<Guid> ?? ids.ToList();
            var nums = fids.Count == 0 ? new List<decimal>()
                : values
                    .Where(v => idsList.Contains(v.ReportSubmissionId) && fids.Contains(v.TemplateFieldId) && v.ValueNumber.HasValue)
                    .Select(v => v.ValueNumber!.Value)
                    .ToList();
            nums.AddRange(prs.Values(idsList, prsKey));
            return nums.Count == 0 ? 0m : Math.Round(nums.Average(), 1);
        }

        var bySubmitter = subs.GroupBy(s => s.SubmitterId).ToList();
        var rows = new List<VideoRollupRow>();
        foreach (var g in bySubmitter)
        {
            var sIds = g.Select(x => x.Id).ToList();
            var requested = Sum(sIds, requestedFids, VideoReportSchema.Prs.RequestedVideos);
            var delivered = Sum(sIds, deliveredFids, VideoReportSchema.Prs.DeliveredVideos);
            var approved = Sum(sIds, approvedFids, VideoReportSchema.Prs.ApprovedFirstTime);
            var late = Sum(sIds, lateFids, VideoReportSchema.Prs.LateVideos);
            var pending = Sum(sIds, pendingFids, VideoReportSchema.Prs.PendingReview);
            // المعادة للتعديل تُقرأ مباشرة من حقل «أعيدت للتعديل» (القالب أغنى من كاتب المحتوى).
            var revised = Sum(sIds, revisedFids, VideoReportSchema.Prs.RevisedVideos);
            var firstApproval = delivered > 0 ? Math.Round(approved / delivered * 100m, 1) : 0m;
            var revisionRate = delivered > 0 ? Math.Round(revised / delivered * 100m, 1) : 0m;
            // نسبة الالتزام بالمواعيد = (المسلَّمة − المتأخرة) / المسلَّمة (٪).
            var onTime = delivered > 0 ? Math.Round(Math.Max(0m, delivered - late) / delivered * 100m, 1) : 0m;
            var planAdherence = Avg(sIds, achievementFids, VideoReportSchema.Prs.OutputAchievement);
            rows.Add(new VideoRollupRow(
                g.Key, names.GetValueOrDefault(g.Key, string.Empty),
                requested, delivered, approved, late, pending, revised,
                firstApproval, revisionRate, onTime, planAdherence, false));
        }

        var totalRequested = rows.Sum(r => r.RequestedVideos);
        var totalDelivered = rows.Sum(r => r.DeliveredVideos);
        var totalApproved = rows.Sum(r => r.ApprovedFirstTime);
        var totalLate = rows.Sum(r => r.LateVideos);
        var totalPending = rows.Sum(r => r.PendingReview);
        var totalRevised = rows.Sum(r => r.RevisedVideos);
        var deliveryRate = totalRequested > 0 ? Math.Round(totalDelivered / totalRequested * 100m, 1) : 0m;
        var firstApprovalRate = totalDelivered > 0 ? Math.Round(totalApproved / totalDelivered * 100m, 1) : 0m;
        var revisionRateTotal = totalDelivered > 0 ? Math.Round(totalRevised / totalDelivered * 100m, 1) : 0m;
        var onTimeRateTotal = totalDelivered > 0 ? Math.Round(Math.Max(0m, totalDelivered - totalLate) / totalDelivered * 100m, 1) : 0m;
        var avgPlanAdherence = rows.Count > 0 ? Math.Round(rows.Average(r => r.PlanAdherence), 1) : 0m;

        // «يحتاج متابعة» = نسبة الاعتماد من أول مرة < 70٪ أو وجود فيديوهات متأخرة.
        var finalRows = rows
            .Select(r => r with { NeedsFollowup = r.FirstApprovalRate < 70m || r.LateVideos > 0 })
            .OrderByDescending(r => r.FirstApprovalRate)
            .ToList();

        // الأفضل = أعلى نسبة اعتماد من أول مرة؛ الأحوج للمتابعة = أدناها.
        var best = finalRows.OrderByDescending(r => r.FirstApprovalRate).FirstOrDefault();
        var worst = finalRows.OrderBy(r => r.FirstApprovalRate).FirstOrDefault();

        List<string> TextList(HashSet<Guid> fids, string prsKey) =>
            values.Where(v => fids.Contains(v.TemplateFieldId) && !string.IsNullOrWhiteSpace(v.ValueText))
                .Select(v => v.ValueText!.Trim())
                .Concat(prs.Texts(prsKey))
                .Distinct().Take(5).ToList();

        var delayReasons = TextList(delayFids, VideoReportSchema.Prs.DelayReasons);
        var decisions = TextList(challengesFids, VideoReportSchema.Prs.Challenges);

        // تقليل البيانات: الأدوار المؤسسية (company/governance) تحصل على الإجماليات فقط — تُحذف الصفوف والأفضل/الأحوج.
        var shapedRows = canViewRows ? (IReadOnlyList<VideoRollupRow>)finalRows : new List<VideoRollupRow>();
        var shapedBest = canViewRows ? best : null;
        var shapedWorst = canViewRows ? worst : null;

        return Result<VideoRollupReport>.Success(new VideoRollupReport(
            filter.PeriodKey,
            finalRows.Count,
            totalRequested,
            totalDelivered,
            totalApproved,
            totalLate,
            totalPending,
            totalRevised,
            deliveryRate,
            firstApprovalRate,
            revisionRateTotal,
            onTimeRateTotal,
            avgPlanAdherence,
            shapedBest, shapedWorst, shapedRows, delayReasons, decisions,
            viewLevel, canViewRows));
    }

    private static VideoRollupReport EmptyVideoRollup(string? periodKey, string viewLevel, bool canViewRows) =>
        new(periodKey, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null,
            new List<VideoRollupRow>(), new List<string>(), new List<string>(), viewLevel, canViewRows);

    public async Task<Result<ModerationRollupReport>> ModerationRollupAsync(ReportFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<ModerationRollupReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        var scope = await _scope.ResolveAsync(ct);

        // تقليل البيانات خادميًا (نفس درس Business-1A..1D-3): مستوى الرؤية يُشتق من دور الطالب لا من الواجهة.
        var scopeType = RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(_currentUser.Roles));
        var canViewRows = scopeType is "own" or "team" or "department";
        var viewLevel = scopeType switch
        {
            "own" => "self",
            "team" => "team",
            "department" => "department",
            _ => "summary"
        };

        // يعتمد على قالب واحد موجود مسبقًا (لا قالب جديد): «تقرير المديرشن الأسبوعي».
        var template = await _db.ReportTemplates.AsNoTracking()
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .Where(t => t.Title == ModerationReportSchema.TemplateTitle)
            .FirstOrDefaultAsync(ct);

        if (template is null)
            return Result<ModerationRollupReport>.Success(EmptyModerationRollup(filter.PeriodKey, viewLevel, canViewRows));

        var versionIds = template.Versions.Select(v => v.Id).ToList();
        var fields = template.Versions.SelectMany(v => v.Fields).ToList();

        var fieldIdsByLabel = fields.GroupBy(f => f.Label)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Id).ToHashSet());

        HashSet<Guid> Fids(string label) =>
            fieldIdsByLabel.TryGetValue(label, out var set) ? set : new HashSet<Guid>();

        var incomingFids = Fids(ModerationReportSchema.IncomingMessages);
        var answeredFids = Fids(ModerationReportSchema.AnsweredMessages);
        var responseMinFids = Fids(ModerationReportSchema.AvgResponseMinutes);
        var problematicFids = Fids(ModerationReportSchema.ProblematicComments);
        var escalationsFids = Fids(ModerationReportSchema.Escalations);
        var complaintsFids = Fids(ModerationReportSchema.Complaints);
        var convertedFids = Fids(ModerationReportSchema.ConvertedOpportunities);
        var recurringFids = Fids(ModerationReportSchema.RecurringQuestions);
        var recommendationsFids = Fids(ModerationReportSchema.Recommendations);

        var subsQ = _db.ReportSubmissions.AsNoTracking()
            .Where(s => versionIds.Contains(s.ReportTemplateVersionId) && s.Status != SubmissionStatus.Draft);
        if (filter.PeriodType is not null) subsQ = subsQ.Where(s => s.PeriodType == filter.PeriodType);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) subsQ = subsQ.Where(s => s.PeriodKey == filter.PeriodKey);
        if (filter.TeamId is not null) subsQ = subsQ.Where(s => s.TeamId == filter.TeamId);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            subsQ = subsQ.Where(s => ids.Contains(s.SubmitterId));
        }

        var subs = await subsQ.Select(s => new { s.Id, s.SubmitterId }).ToListAsync(ct);
        if (subs.Count == 0)
            return Result<ModerationRollupReport>.Success(EmptyModerationRollup(filter.PeriodKey, viewLevel, canViewRows));

        var subIds = subs.Select(s => s.Id).ToList();
        var values = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => subIds.Contains(v.ReportSubmissionId))
            .Select(v => new { v.ReportSubmissionId, v.TemplateFieldId, v.ValueNumber, v.ValueText })
            .ToListAsync(ct);

        // v5: المؤشرات مضمَّنة داخل المشاريع (PRS) — تُضاف فوق مجموع top-level (v1–v4). لا ازدواج (مفاتيح v5 غائبة عن v4).
        var prsFieldIds = fields.Where(f => f.FieldType == FieldType.ProjectRepeatableSection).Select(f => f.Id).ToList();
        var prs = await LoadPrsAsync(prsFieldIds, subIds, ct);

        var names = await UserNamesAsync(subs.Select(s => s.SubmitterId), ct);

        decimal Sum(IEnumerable<Guid> ids, HashSet<Guid> fids, string prsKey)
        {
            var idList = ids as IList<Guid> ?? ids.ToList();
            var top = fids.Count == 0 ? 0m
                : values.Where(v => idList.Contains(v.ReportSubmissionId) && fids.Contains(v.TemplateFieldId)).Sum(v => v.ValueNumber ?? 0m);
            return top + prs.Sum(idList, prsKey);
        }

        decimal Avg(IEnumerable<Guid> ids, HashSet<Guid> fids, string prsKey)
        {
            var idsList = ids as ICollection<Guid> ?? ids.ToList();
            var nums = fids.Count == 0 ? new List<decimal>()
                : values
                    .Where(v => idsList.Contains(v.ReportSubmissionId) && fids.Contains(v.TemplateFieldId) && v.ValueNumber.HasValue)
                    .Select(v => v.ValueNumber!.Value)
                    .ToList();
            nums.AddRange(prs.Values(idsList, prsKey));
            return nums.Count == 0 ? 0m : Math.Round(nums.Average(), 1);
        }

        var bySubmitter = subs.GroupBy(s => s.SubmitterId).ToList();
        var rows = new List<ModerationRollupRow>();
        foreach (var g in bySubmitter)
        {
            var sIds = g.Select(x => x.Id).ToList();
            var incoming = Sum(sIds, incomingFids, ModerationReportSchema.Prs.IncomingMessages);
            var answered = Sum(sIds, answeredFids, ModerationReportSchema.Prs.AnsweredMessages);
            var unhandled = Math.Max(0m, incoming - answered);
            var avgResponse = Avg(sIds, responseMinFids, ModerationReportSchema.Prs.AvgResponseMinutes);
            var problematic = Sum(sIds, problematicFids, ModerationReportSchema.Prs.ProblematicComments);
            var escalations = Sum(sIds, escalationsFids, ModerationReportSchema.Prs.Escalations);
            var complaints = Sum(sIds, complaintsFids, ModerationReportSchema.Prs.Complaints);
            var converted = Sum(sIds, convertedFids, ModerationReportSchema.Prs.ConvertedOpportunities);
            // نسبة الرد = المُجاب عليها / الواردة (٪).
            var responseRate = incoming > 0 ? Math.Round(answered / incoming * 100m, 1) : 0m;
            rows.Add(new ModerationRollupRow(
                g.Key, names.GetValueOrDefault(g.Key, string.Empty),
                incoming, answered, unhandled, avgResponse, problematic,
                escalations, complaints, converted, responseRate, false));
        }

        var totalIncoming = rows.Sum(r => r.IncomingMessages);
        var totalAnswered = rows.Sum(r => r.AnsweredMessages);
        var totalUnhandled = rows.Sum(r => r.UnhandledMessages);
        var totalProblematic = rows.Sum(r => r.ProblematicComments);
        var totalEscalations = rows.Sum(r => r.Escalations);
        var totalComplaints = rows.Sum(r => r.Complaints);
        var totalConverted = rows.Sum(r => r.ConvertedOpportunities);
        var responseRateTotal = totalIncoming > 0 ? Math.Round(totalAnswered / totalIncoming * 100m, 1) : 0m;
        var avgResponseTotal = rows.Count > 0 ? Math.Round(rows.Average(r => r.AvgResponseMinutes), 1) : 0m;

        // «يحتاج متابعة» = نسبة الرد < 90٪ أو وجود شكاوى.
        var finalRows = rows
            .Select(r => r with { NeedsFollowup = r.ResponseRate < 90m || r.Complaints > 0 })
            .OrderByDescending(r => r.ResponseRate)
            .ToList();

        // الأفضل = أعلى نسبة رد؛ الأحوج للمتابعة = أدناها.
        var best = finalRows.OrderByDescending(r => r.ResponseRate).FirstOrDefault();
        var worst = finalRows.OrderBy(r => r.ResponseRate).FirstOrDefault();

        List<string> TextList(HashSet<Guid> fids, string prsKey) =>
            values.Where(v => fids.Contains(v.TemplateFieldId) && !string.IsNullOrWhiteSpace(v.ValueText))
                .Select(v => v.ValueText!.Trim())
                .Concat(prs.Texts(prsKey))
                .Distinct().Take(5).ToList();

        var recurringIssues = TextList(recurringFids, ModerationReportSchema.Prs.RecurringQuestions);
        var decisions = TextList(recommendationsFids, ModerationReportSchema.Prs.Recommendations);

        // تقليل البيانات: الأدوار المؤسسية (company/governance) تحصل على الإجماليات فقط — تُحذف الصفوف والأفضل/الأحوج.
        var shapedRows = canViewRows ? (IReadOnlyList<ModerationRollupRow>)finalRows : new List<ModerationRollupRow>();
        var shapedBest = canViewRows ? best : null;
        var shapedWorst = canViewRows ? worst : null;

        return Result<ModerationRollupReport>.Success(new ModerationRollupReport(
            filter.PeriodKey,
            finalRows.Count,
            totalIncoming,
            totalAnswered,
            totalUnhandled,
            totalProblematic,
            totalEscalations,
            totalComplaints,
            totalConverted,
            responseRateTotal,
            avgResponseTotal,
            shapedBest, shapedWorst, shapedRows, recurringIssues, decisions,
            viewLevel, canViewRows));
    }

    private static ModerationRollupReport EmptyModerationRollup(string? periodKey, string viewLevel, bool canViewRows) =>
        new(periodKey, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null,
            new List<ModerationRollupRow>(), new List<string>(), new List<string>(), viewLevel, canViewRows);

    // أسماء المسارات (تُستخدم في مؤشرات الخطر والتوصية).
    private const string TrackContent = "المحتوى";
    private const string TrackDesign = "التصميم";
    private const string TrackVideo = "الفيديو";
    private const string TrackModeration = "المودريشن";
    private const string TrackNone = "لا يوجد";

    public async Task<Result<SocialOpsRollupReport>> SocialOpsRollupAsync(ReportFilter filter, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<SocialOpsRollupReport>.Failure("غير مصرّح.", "auth.unauthenticated");

        // إعادة استخدام التجميعات الأربعة القائمة — لا تكرار للحسابات ولا كسر للعقود.
        // كلٌّ منها يطبّق تقليل البيانات خادميًا حسب دور المستخدم نفسه (الصفوف فارغة للـGM/CEO).
        var contentR = await ContentWriterRollupAsync(filter, ct);
        var designR = await DesignerRollupAsync(filter, ct);
        var videoR = await VideoRollupAsync(filter, ct);
        var modR = await ModerationRollupAsync(filter, ct);

        // فشل أيٍّ منها (مثل auth) — مُستبعَد عمليًا لأنها تتشارك نفس فحص المصادقة أعلاه؛ لكن نحترس.
        if (!contentR.Succeeded) return Result<SocialOpsRollupReport>.Failure(contentR.Error!, contentR.ErrorCode!);
        if (!designR.Succeeded) return Result<SocialOpsRollupReport>.Failure(designR.Error!, designR.ErrorCode!);
        if (!videoR.Succeeded) return Result<SocialOpsRollupReport>.Failure(videoR.Error!, videoR.ErrorCode!);
        if (!modR.Succeeded) return Result<SocialOpsRollupReport>.Failure(modR.Error!, modR.ErrorCode!);

        var content = contentR.Value!;
        var design = designR.Value!;
        var video = videoR.Value!;
        var mod = modR.Value!;

        var scopeType = RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(_currentUser.Roles));
        var canViewRows = scopeType is "own" or "team" or "department";
        var viewLevel = content.ViewLevel; // نفس مستوى الرؤية المشتق من الدور.

        var contentSummary = new SocialContentSummary(
            content.Reporters, content.TotalRequired, content.TotalDelivered,
            content.FirstApprovalRate, content.Rows.Count(r => r.NeedsFollowup));
        var designSummary = new SocialDesignSummary(
            design.Reporters, design.TotalRequested, design.TotalDelivered,
            design.FirstApprovalRate, design.TotalLate, design.Rows.Count(r => r.NeedsFollowup));
        var videoSummary = new SocialVideoSummary(
            video.Reporters, video.TotalRequested, video.TotalDelivered,
            video.FirstApprovalRate, video.TotalLate, video.Rows.Count(r => r.NeedsFollowup));
        var modSummary = new SocialModerationSummary(
            mod.Reporters, mod.TotalIncoming, mod.TotalAnswered, mod.ResponseRate,
            mod.AvgResponseMinutes, mod.TotalComplaints, mod.TotalEscalations);

        var totalReporters = content.Reporters + design.Reporters + video.Reporters + mod.Reporters;

        static decimal Clamp(decimal v) => v < 0m ? 0m : v > 100m ? 100m : v;

        // صحة كل مسار 0–100 (الأعلى أفضل) — من الإجماليات فقط لتعمل لكل الأدوار.
        var contentHealth = content.Reporters > 0
            ? Math.Round((content.ContentDeliveryRate + content.FirstApprovalRate) / 2m, 1) : 0m;
        var designHealth = design.Reporters > 0
            ? Math.Round((design.DeliveryRate + design.FirstApprovalRate + design.OnTimeRate) / 3m, 1) : 0m;
        var videoHealth = video.Reporters > 0
            ? Math.Round((video.DeliveryRate + video.FirstApprovalRate + video.OnTimeRate) / 3m, 1) : 0m;
        var modHealth = mod.Reporters > 0
            ? Clamp(Math.Round(mod.ResponseRate - mod.TotalComplaints * 2m - mod.TotalEscalations * 1m, 1)) : 0m;

        var tracks = new[]
        {
            (Label: TrackContent, Health: contentHealth, Delay: content.TotalLate, Revision: content.RevisionRate, Active: content.Reporters > 0),
            (Label: TrackDesign, Health: designHealth, Delay: design.TotalLate, Revision: design.RevisionRate, Active: design.Reporters > 0),
            (Label: TrackVideo, Health: videoHealth, Delay: video.TotalLate, Revision: video.RevisionRate, Active: video.Reporters > 0),
            // المودريشن مسار استجابة لا إنتاج — لا يدخل في «أكثر تأخّرًا» (تأخير التسليمات) ولا «أكثر إعادةً»؛ مخاطره عبر الشكاوى/التصعيد.
            (Label: TrackModeration, Health: modHealth, Delay: 0m, Revision: 0m, Active: mod.Reporters > 0),
        };
        var active = tracks.Where(t => t.Active).ToList();

        var healthScore = active.Count > 0 ? Math.Round(active.Average(t => t.Health), 1) : 0m;
        var healthLabel = healthScore >= 85m ? "ممتاز"
            : healthScore >= 70m ? "جيد"
            : healthScore >= 55m ? "مقبول"
            : active.Count == 0 ? "لا توجد بيانات"
            : "يحتاج تدخّل";

        var mostNeedsFollowup = active.Count > 0 ? active.OrderBy(t => t.Health).First().Label : TrackNone;
        var mostDelayed = active.Where(t => t.Delay > 0m).OrderByDescending(t => t.Delay).Select(t => t.Label).FirstOrDefault() ?? TrackNone;
        var mostRevised = active.Where(t => t.Revision > 0m).OrderByDescending(t => t.Revision).Select(t => t.Label).FirstOrDefault() ?? TrackNone;
        var mostComplaints = (mod.Reporters > 0 && (mod.TotalComplaints + mod.TotalEscalations) > 0m) ? TrackModeration : TrackNone;

        string topRisk;
        if (active.Count == 0)
            topRisk = TrackNone;
        else if (mod.Reporters > 0 && (mod.TotalComplaints > 0m || mod.TotalEscalations > 0m))
            topRisk = $"شكاوى/تصعيد في المودريشن — شكاوى {mod.TotalComplaints:0} وتصعيدات {mod.TotalEscalations:0}";
        else
        {
            var worst = active.OrderBy(t => t.Health).First();
            topRisk = $"ضعف الأداء في مسار {worst.Label} (مؤشر الصحة {worst.Health:0.#}٪)";
        }

        // توصية تشغيلية مختصرة — تُبنى من أبرز إشارتين سلبيّتين.
        var recParts = new List<string>();
        if (mostRevised != TrackNone) recParts.Add($"تقليل تعديلات {mostRevised}");
        if (mod.Reporters > 0 && mod.AvgResponseMinutes > 15m) recParts.Add("تحسين سرعة رد المودريشن");
        if (mostDelayed != TrackNone) recParts.Add($"معالجة تأخير {mostDelayed}");
        if (recParts.Count == 0)
            recParts.Add(active.Count == 0 ? "لا توجد بيانات تشغيلية لهذه الفترة" : $"الحفاظ على مستوى الأداء الحالي ({healthLabel})");
        var recommendation = active.Count == 0
            ? recParts[0]
            : "الأولوية هذا الأسبوع: " + string.Join(" و", recParts.Take(2)) + ".";

        // قرار مطلوب — أول توصية/قرار نصّي مجمّع عبر المسارات (متاح لكل الأدوار، ليس صفًّا تفصيليًا).
        var decisionNeeded = content.DecisionsNeeded
            .Concat(design.DecisionsNeeded).Concat(video.DecisionsNeeded).Concat(mod.DecisionsNeeded)
            .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().FirstOrDefault();

        return Result<SocialOpsRollupReport>.Success(new SocialOpsRollupReport(
            filter.PeriodKey,
            totalReporters,
            contentSummary, designSummary, videoSummary, modSummary,
            healthScore, healthLabel,
            topRisk, mostNeedsFollowup, mostDelayed, mostRevised, mostComplaints,
            recommendation, decisionNeeded,
            viewLevel, canViewRows));
    }

    public async Task<Result<GovernanceSummaryReport>> GovernanceSummaryAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<GovernanceSummaryReport>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (!RoleAccess.CanViewGovernance(_currentUser.Roles))
            return Result<GovernanceSummaryReport>.Failure("لا تملك صلاحية عرض ملخص الحوكمة.", "auth.forbidden");

        var openRisks = await _db.Risks.AsNoTracking()
            .Where(r => r.Status != RiskStatus.Closed)
            .Select(r => r.Severity).ToListAsync(ct);

        var bySeverity = openRisks.GroupBy(s => s)
            .Select(g => new SeverityCount(g.Key, g.Count()))
            .OrderByDescending(x => x.Severity)
            .ToList();

        var openEscalations = await _db.Escalations.AsNoTracking()
            .CountAsync(e => e.Status == EscalationStatus.Open || e.Status == EscalationStatus.Acknowledged, ct);

        var openTraining = await _db.TrainingNeeds.AsNoTracking()
            .CountAsync(t => t.Status != TrainingNeedStatus.Completed && t.Status != TrainingNeedStatus.Cancelled, ct);

        var openPlans = await _db.ImprovementPlans.AsNoTracking()
            .CountAsync(p => p.Status != ImprovementPlanStatus.Completed && p.Status != ImprovementPlanStatus.Cancelled, ct);

        var openDecisions = await _db.Decisions.AsNoTracking()
            .CountAsync(d => d.Status == DecisionStatus.Proposed, ct);

        return Result<GovernanceSummaryReport>.Success(new GovernanceSummaryReport(
            openRisks.Count, bySeverity, openEscalations, openTraining, openPlans, openDecisions));
    }

    public async Task<Result<byte[]>> ExportSubmissionsCsvAsync(ReportFilter filter, CancellationToken ct = default)
    {
        if (Guard() is { } err) return Result<byte[]>.Failure(err.Error!, err.ErrorCode);

        var scope = await _scope.ResolveAsync(ct);
        var q = ApplyFilter(_db.ReportSubmissions.AsNoTracking(), filter);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            q = q.Where(s => ids.Contains(s.SubmitterId));
        }
        var rows = await q.OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new { s.SubmitterId, s.DepartmentId, s.TeamId, s.PeriodType, s.PeriodKey, s.Status, s.SubmittedAtUtc })
            .ToListAsync(ct);

        var userNames = await UserNamesAsync(rows.Select(r => r.SubmitterId), ct);
        var deptIds = rows.Where(r => r.DepartmentId is not null).Select(r => r.DepartmentId!.Value).Distinct().ToList();
        var deptNames = await _db.Departments.Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);
        var teamIds = rows.Where(r => r.TeamId is not null).Select(r => r.TeamId!.Value).Distinct().ToList();
        var teamNames = await _db.Teams.Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);

        var sb = new StringBuilder();
        sb.AppendLine("الموظف,الإدارة,الفريق,نوع الفترة,مفتاح الفترة,الحالة,تاريخ الإرسال");
        foreach (var r in rows)
        {
            var name = userNames.GetValueOrDefault(r.SubmitterId, string.Empty);
            var dept = r.DepartmentId is Guid d ? deptNames.GetValueOrDefault(d, string.Empty) : string.Empty;
            var team = r.TeamId is Guid t ? teamNames.GetValueOrDefault(t, string.Empty) : string.Empty;
            var submitted = r.SubmittedAtUtc?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
            sb.Append(Csv(name)).Append(',')
              .Append(Csv(dept)).Append(',')
              .Append(Csv(team)).Append(',')
              .Append(Csv(r.PeriodType.ToString())).Append(',')
              .Append(Csv(r.PeriodKey)).Append(',')
              .Append(Csv(r.Status.ToString())).Append(',')
              .Append(Csv(submitted)).Append('\n');
        }

        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        return Result<byte[]>.Success(bom.Concat(body).ToArray());
    }

    public async Task<Result<byte[]>> ExportCompletenessPdfAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var report = await SubmissionCompletenessAsync(filter, ct);
        if (!report.Succeeded) return Result<byte[]>.Failure(report.Error!, report.ErrorCode);
        return Result<byte[]>.Success(PdfReportBuilder.Completeness(report.Value!));
    }

    public async Task<Result<byte[]>> ExportKpiSummaryPdfAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var report = await KpiSummaryAsync(filter, ct);
        if (!report.Succeeded) return Result<byte[]>.Failure(report.Error!, report.ErrorCode);
        return Result<byte[]>.Success(PdfReportBuilder.KpiSummary(report.Value!));
    }

    public async Task<Result<byte[]>> ExportExecutiveSummaryPdfAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var completeness = await SubmissionCompletenessAsync(filter, ct);
        if (!completeness.Succeeded) return Result<byte[]>.Failure(completeness.Error!, completeness.ErrorCode);

        var kpi = await KpiSummaryAsync(filter, ct);
        if (!kpi.Succeeded) return Result<byte[]>.Failure(kpi.Error!, kpi.ErrorCode);

        // الحوكمة مقصورة على من يملك صلاحية عرضها — تُضمَّن فقط إن سُمح بها.
        GovernanceSummaryReport? governance = null;
        if (RoleAccess.CanViewGovernance(_currentUser.Roles))
        {
            var gov = await GovernanceSummaryAsync(ct);
            if (gov.Succeeded) governance = gov.Value;
        }

        var pdf = PdfReportBuilder.ExecutiveSummary(completeness.Value!, kpi.Value!, governance, filter.PeriodKey);
        return Result<byte[]>.Success(pdf);
    }

    private (string? Error, string? ErrorCode)? Guard()
    {
        if (!_currentUser.IsAuthenticated) return ("غير مصرّح.", "auth.unauthenticated");
        if (!_currentUser.IsInAnyRole(MonitoringRoles)) return ("لا تملك صلاحية عرض تقارير المراقبة.", "auth.forbidden");
        return null;
    }

    private static IQueryable<ReportSubmission> ApplyFilter(IQueryable<ReportSubmission> q, ReportFilter filter)
    {
        if (filter.PeriodType is not null) q = q.Where(s => s.PeriodType == filter.PeriodType);
        if (!string.IsNullOrWhiteSpace(filter.PeriodKey)) q = q.Where(s => s.PeriodKey == filter.PeriodKey);
        if (filter.DepartmentId is not null) q = q.Where(s => s.DepartmentId == filter.DepartmentId);
        if (filter.TeamId is not null) q = q.Where(s => s.TeamId == filter.TeamId);
        return q;
    }

    private static bool IsComplete(SubmissionStatus s) =>
        s is SubmissionStatus.Closed or SubmissionStatus.Visible;

    // وصف حالة الالتزام نصًّا (لا يكشف محتوى — وصف موضع التسليم في دورة الحياة فقط).
    private static string StatusLabelAr(SubmissionStatus s) => s switch
    {
        SubmissionStatus.Submitted => "مُرسَل (قيد المراجعة)",
        SubmissionStatus.Returned => "مُعاد للتعديل",
        SubmissionStatus.ApprovedByDirectManager => "معتمد من المدير المباشر",
        SubmissionStatus.ApprovedByNextLevel => "معتمد من المستوى الأعلى",
        SubmissionStatus.Escalated => "مُصعَّد",
        SubmissionStatus.Closed => "مُغلق",
        SubmissionStatus.Visible => "ظاهر",
        _ => "لم يسلّم"
    };

    // الأدوار النظامية (Identity) لكل مستخدم — لتحديد موعد التسليم الخاص بدوره (حساب التأخّر فقط).
    private async Task<Dictionary<Guid, List<string>>> UserPrimaryRolesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, List<string>>();
        var pairs = await (from ur in _db.UserRoles
                           join r in _db.Roles on ur.RoleId equals r.Id
                           where userIds.Contains(ur.UserId) && r.Name != null
                           select new { ur.UserId, r.Name }).ToListAsync(ct);
        return pairs.GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());
    }

    private static decimal Rate(int part, int total) =>
        total == 0 ? 0m : Math.Round((decimal)part / total * 100m, 1);

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    // ======================= قراءة أقسام المشاريع المتكررة (PRS) للـ Rollups (v5 backward-compat) =======================
    // v1–v4: مؤشرات الأداء مخزَّنة top-level في SubmissionFieldValue.ValueNumber (يجمعها كل rollup كما كان).
    // v5: المؤشرات مضمَّنة داخل كل مشروع في ValueJson للحقل من نوع ProjectRepeatableSection.
    // هذا المحمِّل يقرأ PRS ويجمّعه لكل تسليم حسب مفتاح الحقل الفرعي، ليضيفه كل rollup فوق مجموع top-level.
    // لا ازدواج حساب: كل تسليم يتبع نسخة واحدة؛ تسليمات v1–v4 لا تحمل مفاتيح v5 الفرعية (قيمتها 0)،
    // وتسليمات v5 لا تحمل قيمًا top-level للمؤشرات (قيمتها 0). فالمجموع (top-level + PRS) صحيح للطرفين.

    private sealed class PrsEntryDto
    {
        public Guid? ProjectId { get; set; }
        public Dictionary<string, JsonElement>? Answers { get; set; }
    }

    /// <summary>تجميع قيم PRS لكل تسليم حسب المفتاح الفرعي: قائمة قيمة لكل مشروع (تسمح بـ Sum أو Average).</summary>
    private sealed class PrsAggregate
    {
        private readonly Dictionary<Guid, Dictionary<string, List<decimal>>> _numeric = new();
        private readonly Dictionary<string, List<string>> _text = new(StringComparer.OrdinalIgnoreCase);

        public void AddNumeric(Guid subId, string key, decimal value)
        {
            if (!_numeric.TryGetValue(subId, out var byKey))
                _numeric[subId] = byKey = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);
            if (!byKey.TryGetValue(key, out var list)) byKey[key] = list = new List<decimal>();
            list.Add(value);
        }

        public void AddText(string key, string value)
        {
            if (!_text.TryGetValue(key, out var list)) _text[key] = list = new List<string>();
            list.Add(value);
        }

        /// <summary>مجموع كل قيم المفتاح عبر التسليمات المحدَّدة (كل المشاريع).</summary>
        public decimal Sum(IEnumerable<Guid> ids, string key)
        {
            decimal total = 0m;
            foreach (var id in ids)
                if (_numeric.TryGetValue(id, out var byKey) && byKey.TryGetValue(key, out var list))
                    total += list.Sum();
            return total;
        }

        /// <summary>كل القيم الرقمية (قيمة لكل مشروع) للمفتاح عبر التسليمات — للمتوسّطات.</summary>
        public List<decimal> Values(IEnumerable<Guid> ids, string key)
        {
            var res = new List<decimal>();
            foreach (var id in ids)
                if (_numeric.TryGetValue(id, out var byKey) && byKey.TryGetValue(key, out var list))
                    res.AddRange(list);
            return res;
        }

        /// <summary>القيم النصّية المميّزة للمفتاح عبر كل التسليمات (لقوائم القرارات/التوصيات).</summary>
        public IEnumerable<string> Texts(string key) =>
            _text.TryGetValue(key, out var list) ? list : Enumerable.Empty<string>();
    }

    private static readonly JsonSerializerOptions PrsJsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>يحمّل ويجمّع قيم حقول PRS ضمن الإصدارات/التسليمات المحدَّدة. آمن على الأنواع القديمة (لا PRS ⇒ فارغ).</summary>
    private async Task<PrsAggregate> LoadPrsAsync(
        IReadOnlyCollection<Guid> prsFieldIds, IReadOnlyCollection<Guid> subIds, CancellationToken ct)
    {
        var agg = new PrsAggregate();
        if (prsFieldIds.Count == 0 || subIds.Count == 0) return agg;

        var raw = await _db.SubmissionFieldValues.AsNoTracking()
            .Where(v => subIds.Contains(v.ReportSubmissionId)
                && prsFieldIds.Contains(v.TemplateFieldId)
                && v.ValueJson != null)
            .Select(v => new { v.ReportSubmissionId, v.ValueJson })
            .ToListAsync(ct);

        foreach (var r in raw)
        {
            List<PrsEntryDto>? entries;
            try { entries = JsonSerializer.Deserialize<List<PrsEntryDto>>(r.ValueJson!, PrsJsonOptions); }
            catch { continue; }
            if (entries is null) continue;

            foreach (var e in entries)
            {
                if (e.Answers is null) continue;
                foreach (var (key, el) in e.Answers)
                {
                    if (TryReadPrsNumber(el, out var num)) agg.AddNumeric(r.ReportSubmissionId, key, num);
                    else if (TryReadPrsText(el, out var txt)) agg.AddText(key, txt);
                }
            }
        }
        return agg;
    }

    private static bool TryReadPrsNumber(JsonElement el, out decimal value)
    {
        value = 0m;
        switch (el.ValueKind)
        {
            case JsonValueKind.Number:
                return el.TryGetDecimal(out value);
            case JsonValueKind.String:
                // يمرّ عبر NumericNormalizer (RC-3 Task 2B) لتطبيع الخانات العربية-الهندية/الفارسية قبل التحويل.
                return NumericNormalizer.TryParseDecimal(el.GetString(), out value);
            default:
                return false;
        }
    }

    private static bool TryReadPrsText(JsonElement el, out string value)
    {
        value = string.Empty;
        if (el.ValueKind != JsonValueKind.String) return false;
        var s = el.GetString();
        if (string.IsNullOrWhiteSpace(s)) return false;
        value = s.Trim();
        return true;
    }
}
