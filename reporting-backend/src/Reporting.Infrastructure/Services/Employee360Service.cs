using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reporting.Application.Common;
using Reporting.Application.Employee360;
using Reporting.Application.Periods;
using Reporting.Application.Security;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// P2-EMP-002 — بانِي عرض الموظّف 360. إسقاط قراءة بحت: لا جدول <c>Employee360</c>،
/// ولا نسخة ثانية من أيّ بيان، ولا كتابة إطلاقًا.
/// كلّ قسم يُبنى داخل حارس مستقلّ فلا يُسقِط فشلُ قسمٍ بقيّةَ الصفحة (§6/P2-EMP-002).
/// </summary>
public class Employee360Service : IEmployee360Service
{
    private const string Purpose = "employee360";

    private readonly AppDbContext _db;
    private readonly IFieldVisibilityPolicy _visibility;
    private readonly IPeriodService _periods;
    private readonly ILogger<Employee360Service> _logger;

    public Employee360Service(
        AppDbContext db,
        IFieldVisibilityPolicy visibility,
        IPeriodService periods,
        ILogger<Employee360Service> logger)
    {
        _db = db;
        _visibility = visibility;
        _periods = periods;
        _logger = logger;
    }

    public async Task<Result<Employee360Dto>> GetProfileAsync(
        Guid subjectUserId, string? sections = null, string? periodKey = null, CancellationToken ct = default)
    {
        var ctx = await _visibility.BuildContextAsync(subjectUserId, Purpose, ct);

        // خارج النطاق أو غير موجود ⟵ نفس الاستجابة تمامًا، كي لا يُستدلّ على وجود موظّف من فرق الرمز.
        if (!ctx.InScope)
            return Result<Employee360Dto>.Failure("الموظّف غير موجود أو خارج نطاقك.", "employee360.not_found");

        var identity = await LoadIdentityAsync(subjectUserId, ct);
        if (identity is null)
            return Result<Employee360Dto>.Failure("الموظّف غير موجود أو خارج نطاقك.", "employee360.not_found");

        var requested = ParseRequestedSections(sections);
        var period = ResolvePeriod(periodKey);

        var result = new Dictionary<string, Employee360SectionDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in Enum.GetValues<Employee360Section>())
        {
            // غير مصرَّح به ⟵ لا يُضاف إلى القاموس أصلًا، فلا يظهر في الـJSON بأيّ صورة.
            if (!_visibility.CanSeeSection(ctx, section)) continue;
            if (requested is not null && !requested.Contains(section)) continue;

            var built = await BuildSectionSafeAsync(section, ctx, identity, period, ct);
            result[SectionKey(section)] = built;
        }

        return Result<Employee360Dto>.Success(new Employee360Dto(
            subjectUserId,
            ctx.IsSelf,
            ctx.Relation.ToString(),
            period?.Key,
            result));
    }

    // ===== البنية العامّة =====

    private async Task<Employee360SectionDto> BuildSectionSafeAsync(
        Employee360Section section, FieldVisibilityContext ctx,
        IdentityRow identity, ResolvedPeriod? period, CancellationToken ct)
    {
        try
        {
            return section switch
            {
                Employee360Section.Identity => BuildIdentity(identity),
                Employee360Section.OperationalSummary => await BuildOperationalSummaryAsync(ctx, ct),
                Employee360Section.Reports => await BuildReportsAsync(ctx, ct),
                Employee360Section.Kpi => await BuildKpiAsync(ctx, period, ct),
                Employee360Section.LeaveAndPermissions => await BuildLeaveAsync(ctx, ct),
                Employee360Section.RequestsAndBalances => await BuildRequestsAndBalancesAsync(ctx, ct),
                Employee360Section.AttendanceAndCompliance => BuildAttendanceUnavailable(),
                Employee360Section.Notes => await BuildNotesAsync(ctx, ct),
                Employee360Section.Governance => await BuildGovernanceAsync(ctx, ct),
                Employee360Section.DevelopmentAndTraining => await BuildDevelopmentAsync(ctx, ct),
                Employee360Section.Timeline => await BuildTimelineAsync(ctx, ct),
                _ => Empty(section, "قسم غير معروف.")
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // لا نُسرّب تفاصيل الاستثناء إلى العميل، ولا نُسقِط بقيّة الأقسام.
            _logger.LogError(ex, "فشل بناء قسم {Section} في عرض الموظّف 360.", section);
            return new Employee360SectionDto(
                SectionKey(section), TitleAr(section),
                Employee360SectionStatus.Error, Employee360DataQuality.Unavailable,
                null, null, null, "تعذّر تحميل هذا القسم. حاول مرّة أخرى.");
        }
    }

    private static HashSet<Employee360Section>? ParseRequestedSections(string? sections)
    {
        if (string.IsNullOrWhiteSpace(sections)) return null;
        var set = new HashSet<Employee360Section>();
        foreach (var raw in sections.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<Employee360Section>(raw, ignoreCase: true, out var parsed))
                set.Add(parsed);
        // طلب أقسام غير معروفة كلّها ⟵ لا نُرجِع كلّ شيء صامتًا، بل مجموعة فارغة صريحة.
        return set;
    }

    private ResolvedPeriod? ResolvePeriod(string? periodKey)
    {
        if (string.IsNullOrWhiteSpace(periodKey)) return _periods.LastCompletedWeek();
        var resolved = _periods.Resolve(new PeriodRequest(PeriodKinds.Week, periodKey));
        return resolved.Succeeded ? resolved.Value : _periods.LastCompletedWeek();
    }

    private static string SectionKey(Employee360Section section) =>
        char.ToLowerInvariant(section.ToString()[0]) + section.ToString()[1..];

    private static string TitleAr(Employee360Section section) => section switch
    {
        Employee360Section.Identity => "الهويّة وحالة التوظيف",
        Employee360Section.OperationalSummary => "الملخّص التشغيليّ",
        Employee360Section.Reports => "التقارير",
        Employee360Section.Kpi => "مؤشّرات الأداء",
        Employee360Section.LeaveAndPermissions => "الإجازات والاستئذانات",
        Employee360Section.RequestsAndBalances => "الطلبات والأرصدة",
        Employee360Section.AttendanceAndCompliance => "الحضور والالتزام",
        Employee360Section.Notes => "الملاحظات الإداريّة",
        Employee360Section.Governance => "الحوكمة",
        Employee360Section.DevelopmentAndTraining => "التطوير والتدريب",
        Employee360Section.Timeline => "الخطّ الزمنيّ الموحّد",
        _ => section.ToString()
    };

    private static Employee360SectionDto Empty(Employee360Section section, string reason) =>
        new(SectionKey(section), TitleAr(section),
            Employee360SectionStatus.NoData, Employee360DataQuality.Complete,
            null, null, Array.Empty<object>(), reason);

    private static Employee360SectionDto Ready(
        Employee360Section section, object? summary, IReadOnlyList<object> items, DateTime? lastUpdated) =>
        new(SectionKey(section), TitleAr(section),
            items.Count == 0 && summary is null ? Employee360SectionStatus.NoData : Employee360SectionStatus.Ready,
            Employee360DataQuality.Complete, lastUpdated, summary, items);

    // ===== (1) الهويّة =====

    private sealed record IdentityRow(
        Guid UserId, string FullName, string? Email, string? JobRoleName,
        string? TeamName, string? DepartmentName, string? DirectManagerName,
        bool IsActive, DateTime CreatedAtUtc);

    private async Task<IdentityRow?> LoadIdentityAsync(Guid userId, CancellationToken ct) =>
        await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new IdentityRow(
                u.Id, u.FullName, u.Email,
                _db.JobRoles.Where(r => r.Id == u.JobRoleId).Select(r => r.NameAr).FirstOrDefault(),
                _db.Teams.Where(t => t.Id == u.TeamId).Select(t => t.NameAr).FirstOrDefault(),
                _db.Departments.Where(d => d.Id == u.DepartmentId).Select(d => d.NameAr).FirstOrDefault(),
                _db.Users.Where(m => m.Id == u.ManagerId).Select(m => m.FullName).FirstOrDefault(),
                u.IsActive, u.CreatedAtUtc))
            .FirstOrDefaultAsync(ct);

    private static Employee360SectionDto BuildIdentity(IdentityRow row) =>
        Ready(Employee360Section.Identity,
            new Employee360IdentityDto(
                row.UserId, row.FullName, row.Email, row.JobRoleName,
                row.TeamName, row.DepartmentName, row.DirectManagerName,
                row.IsActive, row.CreatedAtUtc),
            Array.Empty<object>(), row.CreatedAtUtc);

    // ===== (2) الملخّص التشغيليّ =====

    private async Task<Employee360SectionDto> BuildOperationalSummaryAsync(
        FieldVisibilityContext ctx, CancellationToken ct)
    {
        var subject = ctx.SubjectUserId;

        var submissionCounts = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => s.SubmitterId == subject)
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var submitted = submissionCounts
            .Where(c => c.Status != SubmissionStatus.Draft)
            .Sum(c => c.Count);
        var returned = submissionCounts
            .Where(c => c.Status == SubmissionStatus.Returned)
            .Sum(c => c.Count);
        var needsAction = submissionCounts
            .Where(c => c.Status is SubmissionStatus.Draft or SubmissionStatus.Returned)
            .Sum(c => c.Count);

        var lastKpi = await _db.KpiEvaluations.AsNoTracking()
            .Where(e => e.SubjectUserId == subject && e.Status == KpiEvaluationStatus.Approved)
            .OrderByDescending(e => e.PeriodKey)
            .Select(e => new { e.TotalScore, e.PeriodKey })
            .FirstOrDefaultAsync(ct);

        var kpiCount = await _db.KpiEvaluations.AsNoTracking()
            .CountAsync(e => e.SubjectUserId == subject && e.Status == KpiEvaluationStatus.Approved, ct);

        var openLeave = await _db.LeaveRequests.AsNoTracking()
            .CountAsync(l => l.RequesterUserId == subject
                && l.Status != LeaveRequestStatus.HrApproved
                && l.Status != LeaveRequestStatus.HrRejected
                && l.Status != LeaveRequestStatus.Cancelled, ct);

        var openServiceRequests = await _db.EmployeeServiceRequests.AsNoTracking()
            .CountAsync(r => r.RequesterUserId == subject
                && r.Status != EmployeeServiceRequestStatus.Completed
                && r.Status != EmployeeServiceRequestStatus.Rejected
                && r.Status != EmployeeServiceRequestStatus.Cancelled, ct);

        // الملاحظات المفتوحة تُعدّ **بعد** ترشيح الحسّاسيّة، وإلّا سرّب العدّاد وجود ملاحظة محجوبة.
        var visibleNoteSensitivities = await _db.ManagementNotes.AsNoTracking()
            .Where(n => n.EntityType == ManagementNoteEntityType.User
                && n.EntityId == subject
                && n.RequiresAction
                && n.Status == ManagementNoteStatus.Open)
            .Select(n => n.Sensitivity)
            .ToListAsync(ct);
        var openNotes = visibleNoteSensitivities
            .Count(s => _visibility.CanSee(ctx, NoteSensitivity.Effective(s)));

        var openGovernance = await _db.GovernanceItems.AsNoTracking()
            .CountAsync(g => (g.RelatedUserId == subject || g.AssignedToUserId == subject)
                && g.ClosedAtUtc == null, ct);

        var summary = new Employee360OperationalSummaryDto(
            submitted, returned, needsAction,
            kpiCount, lastKpi?.TotalScore, lastKpi?.PeriodKey,
            openLeave, openServiceRequests, openNotes, openGovernance);

        return Ready(Employee360Section.OperationalSummary, summary, Array.Empty<object>(), null);
    }

    // ===== (3) التقارير =====

    private async Task<Employee360SectionDto> BuildReportsAsync(FieldVisibilityContext ctx, CancellationToken ct)
    {
        var items = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => s.SubmitterId == ctx.SubjectUserId)
            .OrderByDescending(s => s.SubmittedAtUtc ?? s.CreatedAtUtc)
            .Take(50)
            .Select(s => new Employee360ReportDto(
                s.Id,
                _db.ReportTemplateVersions
                    .Where(v => v.Id == s.ReportTemplateVersionId)
                    .Select(v => v.ReportTemplate!.Title)
                    .FirstOrDefault() ?? "—",
                s.PeriodKey,
                s.PeriodType.ToString(),
                s.Status.ToString(),
                s.SubmittedAtUtc,
                s.ClosedAtUtc))
            .ToListAsync(ct);

        return items.Count == 0
            ? Empty(Employee360Section.Reports, "لا توجد تقارير مسجّلة لهذا الموظّف.")
            : Ready(Employee360Section.Reports, null, items, items.Max(i => i.SubmittedAtUtc));
    }

    // ===== (4) مؤشّرات الأداء =====

    private async Task<Employee360SectionDto> BuildKpiAsync(
        FieldVisibilityContext ctx, ResolvedPeriod? period, CancellationToken ct)
    {
        var subject = ctx.SubjectUserId;

        var approved = await _db.KpiEvaluations.AsNoTracking()
            .Where(e => e.SubjectUserId == subject && e.Status == KpiEvaluationStatus.Approved)
            .OrderByDescending(e => e.PeriodKey)
            .Take(200)
            .Select(e => new Employee360KpiEvaluationDto(
                e.Id,
                _db.KpiTemplateVersions
                    .Where(v => v.Id == e.KpiTemplateVersionId)
                    .Select(v => v.KpiTemplate!.Title)
                    .FirstOrDefault() ?? "—",
                e.PeriodType.ToString(),
                e.PeriodKey,
                e.TotalScore,
                e.Status.ToString(),
                e.Trend.ToString(),
                e.SubmittedAtUtc))
            .ToListAsync(ct);

        if (approved.Count == 0)
            return Empty(Employee360Section.Kpi, "لا توجد تقييمات معتمدة لهذا الموظّف بعد.");

        var weekly = approved.Where(e => e.PeriodType == nameof(PeriodType.Weekly)).ToList();
        var lastCompleted = period ?? _periods.LastCompletedWeek();
        var previous = _periods.PreviousComparable(lastCompleted);

        var summary = new Employee360KpiSummaryDto(
            Window("LastCompletedWeek", nameof(PeriodType.Weekly), lastCompleted.Key, weekly, new[] { lastCompleted.Key }),
            Window("PreviousWeek", nameof(PeriodType.Weekly), previous.Key, weekly, new[] { previous.Key }),
            Window("LastFourWeeks", nameof(PeriodType.Weekly), null, weekly, LastNWeekKeys(lastCompleted, 4)),
            AggregateWindow("Month", nameof(PeriodType.Monthly), PeriodKinds.Month, lastCompleted, weekly),
            AggregateWindow("Quarter", nameof(PeriodType.Quarterly), PeriodKinds.Quarter, lastCompleted, weekly),
            AggregateWindow("Year", nameof(PeriodType.Yearly), PeriodKinds.Year, lastCompleted, weekly),
            approved[0].Trend,
            ExtremeWeek(weekly, best: true),
            ExtremeWeek(weekly, best: false));

        return Ready(Employee360Section.Kpi, summary, approved.Cast<object>().ToList(),
            approved.Max(e => e.SubmittedAtUtc));
    }

    private static Employee360KpiWindowDto Window(
        string windowKey, string periodType, string? periodKey,
        IReadOnlyList<Employee360KpiEvaluationDto> weekly, IReadOnlyCollection<string> keys)
    {
        var inWindow = weekly.Where(e => keys.Contains(e.PeriodKey) && e.TotalScore.HasValue).ToList();
        var coverage = keys.Count == 0 ? 0m : Math.Round((decimal)inWindow.Count / keys.Count, 4);
        return new Employee360KpiWindowDto(
            windowKey, periodType, periodKey,
            inWindow.Count == 0 ? null : Math.Round(inWindow.Average(e => e.TotalScore!.Value), 2),
            inWindow.Count, keys.Count, coverage);
    }

    private Employee360KpiWindowDto? AggregateWindow(
        string windowKey, string periodType, string periodKind,
        ResolvedPeriod anchor, IReadOnlyList<Employee360KpiEvaluationDto> weekly)
    {
        var key = periodKind switch
        {
            PeriodKinds.Month => $"{anchor.End.Year:D4}-{anchor.End.Month:D2}",
            PeriodKinds.Quarter => $"{anchor.End.Year:D4}-Q{(anchor.End.Month - 1) / 3 + 1}",
            PeriodKinds.Year => $"{anchor.End.Year:D4}",
            _ => null
        };
        if (key is null) return null;

        var resolved = _periods.Resolve(new PeriodRequest(periodKind, key));
        if (!resolved.Succeeded || resolved.Value is null) return null;

        var keys = _periods.WeekKeysWithin(resolved.Value);
        return Window(windowKey, periodType, key, weekly, keys);
    }

    private IReadOnlyCollection<string> LastNWeekKeys(ResolvedPeriod lastCompleted, int count)
    {
        var keys = new List<string>();
        var cursor = lastCompleted;
        for (var i = 0; i < count; i++)
        {
            keys.Add(cursor.Key);
            cursor = _periods.PreviousComparable(cursor);
        }
        return keys;
    }

    private static Employee360KpiWindowDto? ExtremeWeek(
        IReadOnlyList<Employee360KpiEvaluationDto> weekly, bool best)
    {
        var scored = weekly.Where(e => e.TotalScore.HasValue).ToList();
        if (scored.Count == 0) return null;
        var pick = best
            ? scored.OrderByDescending(e => e.TotalScore).First()
            : scored.OrderBy(e => e.TotalScore).First();
        return new Employee360KpiWindowDto(
            best ? "BestWeek" : "WorstWeek", nameof(PeriodType.Weekly), pick.PeriodKey,
            pick.TotalScore, 1, 1, 1m);
    }

    // ===== (5) الإجازات والاستئذانات =====

    private async Task<Employee360SectionDto> BuildLeaveAsync(FieldVisibilityContext ctx, CancellationToken ct)
    {
        // سبب الإجازة تصنيفه HrOnly ⟹ لا يُسلسَل لمن لا يملك الإذن الصريح.
        var canSeeReason = await _visibility.CanSeeAsync(
            ctx, FieldSensitivity.HrOnly, "leaveRequest.reason", ct);

        var rows = await _db.LeaveRequests.AsNoTracking()
            .Where(l => l.RequesterUserId == ctx.SubjectUserId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(50)
            .Select(l => new
            {
                l.Id, l.Type, l.StartDate, l.EndDate, l.StartTime, l.EndTime,
                l.Status, l.CurrentStep, l.Reason, l.CreatedAtUtc
            })
            .ToListAsync(ct);

        var items = rows.Select(l => new Employee360LeaveDto(
            l.Id, l.Type.ToString(), l.StartDate, l.EndDate, l.StartTime, l.EndTime,
            l.Status.ToString(), l.CurrentStep.ToString(),
            canSeeReason ? l.Reason : null,
            l.CreatedAtUtc)).ToList();

        return items.Count == 0
            ? Empty(Employee360Section.LeaveAndPermissions, "لا توجد إجازات أو استئذانات مسجّلة.")
            : Ready(Employee360Section.LeaveAndPermissions, null, items, items.Max(i => i.CreatedAtUtc));
    }

    // ===== (6) الطلبات والأرصدة =====

    private async Task<Employee360SectionDto> BuildRequestsAndBalancesAsync(
        FieldVisibilityContext ctx, CancellationToken ct)
    {
        var subject = ctx.SubjectUserId;

        var requests = await _db.EmployeeServiceRequests.AsNoTracking()
            .Where(r => r.RequesterUserId == subject)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(50)
            .Select(r => new Employee360ServiceRequestDto(
                r.Id, r.RequestType.ToString(), r.Title, r.Status.ToString(),
                r.CreatedAtUtc, r.CompletedAtUtc))
            .ToListAsync(ct);

        // الرصيد يُحسب من الحركات (لا حذف من الدفتر) ولا يُقرأ من حقل مخزَّن.
        var ledger = await _db.EmployeeBalanceLedger.AsNoTracking()
            .Where(e => e.EmployeeId == subject)
            .GroupBy(e => new { e.BalanceType, e.Year, e.Direction })
            .Select(g => new
            {
                g.Key.BalanceType, g.Key.Year, g.Key.Direction,
                Total = g.Sum(x => x.Amount)
            })
            .ToListAsync(ct);

        var balances = ledger
            .GroupBy(x => new { x.BalanceType, x.Year })
            .Select(g =>
            {
                var credited = g.Where(x => x.Direction == BalanceDirection.Credit).Sum(x => x.Total);
                var debited = g.Where(x => x.Direction == BalanceDirection.Debit).Sum(x => x.Total);
                return new Employee360BalanceDto(
                    g.Key.BalanceType.ToString(), g.Key.Year, credited, debited, credited - debited);
            })
            .OrderByDescending(b => b.Year)
            .ThenBy(b => b.BalanceType)
            .ToList();

        if (requests.Count == 0 && balances.Count == 0)
            return Empty(Employee360Section.RequestsAndBalances, "لا توجد طلبات ولا حركات رصيد مسجّلة.");

        return Ready(Employee360Section.RequestsAndBalances,
            new { balances },
            requests.Cast<object>().ToList(),
            requests.Count == 0 ? null : requests.Max(r => r.CreatedAtUtc));
    }

    // ===== (7) الحضور والالتزام =====

    /// <summary>
    /// وحدة الحضور تُبنى في P2-ATT-005/006. حتّى ذلك الحين يُعلن القسم صراحةً أنّه غير متاح
    /// — ولا يُختلَق له جدول موازٍ ولا بيانات وهميّة (§3).
    /// </summary>
    private static Employee360SectionDto BuildAttendanceUnavailable() =>
        new(SectionKey(Employee360Section.AttendanceAndCompliance),
            TitleAr(Employee360Section.AttendanceAndCompliance),
            Employee360SectionStatus.NoData, Employee360DataQuality.Unavailable,
            null, null, Array.Empty<object>(),
            "وحدة الحضور غير مفعّلة في هذا الإصدار.");

    // ===== (8) الملاحظات =====

    private async Task<Employee360SectionDto> BuildNotesAsync(FieldVisibilityContext ctx, CancellationToken ct)
    {
        var rows = await _db.ManagementNotes.AsNoTracking()
            .Where(n => n.EntityType == ManagementNoteEntityType.User && n.EntityId == ctx.SubjectUserId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(100)
            .Select(n => new
            {
                n.Id, n.NoteType, n.Status, n.Sensitivity, n.Body, n.RequiresAction, n.CreatedAtUtc
            })
            .ToListAsync(ct);

        var items = new List<object>();
        foreach (var n in rows)
        {
            var effective = NoteSensitivity.Effective(n.Sensitivity);
            if (!await _visibility.CanSeeAsync(ctx, effective, "managementNote.body", ct)) continue;
            items.Add(new Employee360NoteDto(
                n.Id, n.NoteType.ToString(), n.Status.ToString(), effective.ToString(),
                n.Body, n.RequiresAction, n.CreatedAtUtc));
        }

        return items.Count == 0
            ? Empty(Employee360Section.Notes, "لا توجد ملاحظات ظاهرة لك على هذا الموظّف.")
            : Ready(Employee360Section.Notes, null, items,
                items.Cast<Employee360NoteDto>().Max(i => i.CreatedAtUtc));
    }

    // ===== (9) الحوكمة =====

    private async Task<Employee360SectionDto> BuildGovernanceAsync(FieldVisibilityContext ctx, CancellationToken ct)
    {
        var subject = ctx.SubjectUserId;

        var items = await _db.GovernanceItems.AsNoTracking()
            .Where(g => g.RelatedUserId == subject || g.AssignedToUserId == subject)
            .OrderByDescending(g => g.CreatedAtUtc)
            .Take(50)
            .Select(g => new Employee360GovernanceDto(
                "Item", g.Id, g.Title, g.Status.ToString(), g.CreatedAtUtc))
            .ToListAsync(ct);

        var escalations = await _db.GovernanceEscalations.AsNoTracking()
            .Where(e => e.TargetUserId == subject || e.AssignedToUserId == subject)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(50)
            .Select(e => new Employee360GovernanceDto(
                "Escalation", e.Id, e.Title, e.Status.ToString(), e.CreatedAtUtc))
            .ToListAsync(ct);

        // بنود الإجراء المعلَّمة حسّاسة تحتاج إذنًا صريحًا، ولا يفتحها دور الحوكمة وحده.
        var canSeeSensitiveActions = await _visibility.CanSeeAsync(
            ctx, FieldSensitivity.ManagementConfidential, "governanceActionItem.sensitive", ct);

        var actions = await _db.GovernanceActionItems.AsNoTracking()
            .Where(a => a.AssignedToUserId == subject && (!a.IsSensitive || canSeeSensitiveActions))
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(50)
            .Select(a => new Employee360GovernanceDto(
                "ActionItem", a.Id, a.Title, a.Status.ToString(), a.CreatedAtUtc))
            .ToListAsync(ct);

        var all = items.Concat(escalations).Concat(actions)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();

        return all.Count == 0
            ? Empty(Employee360Section.Governance, "لا توجد عناصر حوكمة مرتبطة بهذا الموظّف.")
            : Ready(Employee360Section.Governance, null, all.Cast<object>().ToList(),
                all.Max(x => x.CreatedAtUtc));
    }

    // ===== (10) التطوير والتدريب =====

    private async Task<Employee360SectionDto> BuildDevelopmentAsync(FieldVisibilityContext ctx, CancellationToken ct)
    {
        var subject = ctx.SubjectUserId;

        var needs = await _db.TrainingNeeds.AsNoTracking()
            .Where(t => t.SubjectUserId == subject)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(50)
            .Select(t => new Employee360DevelopmentDto(
                "TrainingNeed", t.Id, t.Title, t.Status.ToString(), null, t.CreatedAtUtc))
            .ToListAsync(ct);

        var plans = await _db.ImprovementPlans.AsNoTracking()
            .Where(p => p.SubjectUserId == subject)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(50)
            .Select(p => new Employee360DevelopmentDto(
                "ImprovementPlan", p.Id, p.Title, p.Status.ToString(), p.DueDateUtc, p.CreatedAtUtc))
            .ToListAsync(ct);

        var all = needs.Concat(plans).OrderByDescending(x => x.CreatedAtUtc).ToList();

        return all.Count == 0
            ? Empty(Employee360Section.DevelopmentAndTraining, "لا توجد خطط تطوير أو احتياجات تدريب.")
            : Ready(Employee360Section.DevelopmentAndTraining, null, all.Cast<object>().ToList(),
                all.Max(x => x.CreatedAtUtc));
    }

    // ===== (11) الخطّ الزمنيّ الموحّد =====

    private async Task<Employee360SectionDto> BuildTimelineAsync(FieldVisibilityContext ctx, CancellationToken ct)
    {
        var subject = ctx.SubjectUserId;
        var viewer = ctx.ViewerUserId;

        var submissions = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => s.SubmitterId == subject && s.SubmittedAtUtc != null)
            .OrderByDescending(s => s.SubmittedAtUtc)
            .Take(30)
            .Select(s => new Employee360TimelineEventDto(
                "ReportSubmitted", "Submissions", s.Id,
                s.PeriodKey, s.SubmittedAtUtc!.Value,
                s.CurrentApproverId == viewer))
            .ToListAsync(ct);

        var evaluations = await _db.KpiEvaluations.AsNoTracking()
            .Where(e => e.SubjectUserId == subject && e.SubmittedAtUtc != null)
            .OrderByDescending(e => e.SubmittedAtUtc)
            .Take(30)
            .Select(e => new Employee360TimelineEventDto(
                "KpiEvaluated", "KpiEvaluations", e.Id,
                e.PeriodKey, e.SubmittedAtUtc!.Value,
                e.ReviewerId == viewer && e.Status == KpiEvaluationStatus.Submitted))
            .ToListAsync(ct);

        var leaves = await _db.LeaveRequests.AsNoTracking()
            .Where(l => l.RequesterUserId == subject)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(30)
            .Select(l => new Employee360TimelineEventDto(
                "LeaveRequested", "LeaveRequests", l.Id,
                l.Type.ToString(), l.CreatedAtUtc,
                l.TeamLeaderReviewerId == viewer || l.ManagerReviewerId == viewer || l.HrReviewerId == viewer))
            .ToListAsync(ct);

        var all = submissions.Concat(evaluations).Concat(leaves)
            .OrderByDescending(e => e.AtUtc)
            .Take(60)
            .ToList();

        return all.Count == 0
            ? Empty(Employee360Section.Timeline, "لا توجد أحداث مسجّلة بعد.")
            : Ready(Employee360Section.Timeline, null, all.Cast<object>().ToList(), all[0].AtUtc);
    }
}
