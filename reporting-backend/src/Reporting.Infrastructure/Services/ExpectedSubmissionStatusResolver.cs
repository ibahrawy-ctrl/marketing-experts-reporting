using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Common;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// REPORT-EXPECTED-SUBMISSION-STATUS-R1 — التنفيذ الوحيد للحالة المتوقّعة (Users-first LEFT JOIN submissions).
/// يشتقّ لكلّ (مستخدم مطالَب × دورة) أرضيّة الانطباق ثمّ الحالة الموحّدة عبر <see cref="UnifiedCycleStatusPolicy"/>،
/// ويسقطها إلى قاموس القراءة <see cref="ExpectedSubmissionStatus"/>. لا يكرّر منطق تقويم/حالة، ولا يعدّل أيّ بيانات.
/// أرضيّة الانطباق = MAX(إنشاء المستخدم، أوّل نشر للقالب، إسناد المسمّى الموثَّق) — والدورة مطلوبة فقط إن بدأت في/بعدها.
/// </summary>
public sealed class ExpectedSubmissionStatusResolver : IExpectedSubmissionStatusResolver
{
    private readonly AppDbContext _db;
    private readonly ISystemClock _clock;

    private static readonly TimeSpan Riyadh = ReportingCalendarPolicy.RiyadhOffset;

    public ExpectedSubmissionStatusResolver(AppDbContext db, ISystemClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ExpectedCycleResult>> ResolveAsync(ExpectedStatusQuery query, CancellationToken ct = default)
    {
        var keys = (query.CycleKeys ?? Array.Empty<string>())
            .Where(k => ReportingCalendarPolicy.IsValidCycleKey(k))
            .Select(k => k.Trim())
            .Distinct()
            .ToList();
        var userIds = (query.UserIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (keys.Count == 0 || userIds.Count == 0)
            return Array.Empty<ExpectedCycleResult>();

        // ===== (1) المستخدمون (Users-first): الهوية + مكوّن الأرضيّة «تاريخ الإنشاء» + الحالة/التنظيم =====
        var users = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.JobRoleId, u.IsActive, u.CreatedAtUtc, u.DepartmentId, u.TeamId })
            .ToListAsync(ct);
        if (users.Count == 0)
            return Array.Empty<ExpectedCycleResult>();

        var jobRoleIds = users.Where(u => u.JobRoleId is not null).Select(u => u.JobRoleId!.Value).Distinct().ToList();

        // ===== (2) القوالب المطالِبة لكلّ مسمّى (Primary أسبوعيّ منشور) — قالب واحد لكلّ مسمّى =====
        var tplQuery = _db.ReportTemplates.AsNoTracking()
            .Where(t => t.JobRoleId != null && jobRoleIds.Contains(t.JobRoleId!.Value)
                        && t.IsActive
                        && t.Classification == TemplateClassification.Primary
                        && t.DefaultPeriodType != PeriodType.Monthly
                        && _db.ReportTemplateVersions.Any(v => v.ReportTemplateId == t.Id && v.IsPublished));
        if (query.TemplateId is Guid qtid)
            tplQuery = tplQuery.Where(t => t.Id == qtid);

        var tplRows = await tplQuery
            .Select(t => new { t.Id, t.Title, JobRoleId = t.JobRoleId!.Value })
            .ToListAsync(ct);

        // الأخصّ يطغى: قالب واحد لكلّ مسمّى (أوّل عنوان أبجديًّا — مطابق لـ UnifiedReportStatusService).
        var templateByJobRole = tplRows
            .GroupBy(t => t.JobRoleId)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Title).First());

        var templateIds = templateByJobRole.Values.Select(t => t.Id).Distinct().ToList();

        // ===== (3) نسخ القوالب: نسب الإصدارات (لمطابقة التسليمات) + أوّل نشر (مكوّن أرضيّة) =====
        var versionRows = templateIds.Count == 0
            ? new List<VersionRow>()
            : await _db.ReportTemplateVersions.AsNoTracking()
                .Where(v => templateIds.Contains(v.ReportTemplateId))
                .Select(v => new VersionRow(v.Id, v.ReportTemplateId, v.IsPublished, v.PublishedAtUtc))
                .ToListAsync(ct);

        var lineageByTemplate = versionRows
            .GroupBy(v => v.TemplateId)
            .ToDictionary(g => g.Key, g => g.Select(v => v.VersionId).ToHashSet());
        var versionToTemplate = versionRows.ToDictionary(v => v.VersionId, v => v.TemplateId);
        var firstPublishByTemplate = versionRows
            .Where(v => v.IsPublished && v.PublishedAtUtc.HasValue)
            .GroupBy(v => v.TemplateId)
            .ToDictionary(g => g.Key, g => g.Min(v => v.PublishedAtUtc!.Value));

        // ===== (4) إسناد المسمّى الموثَّق (audit_logs user.jobrole.changed) — مكوّن أرضيّة عالي الثقة =====
        var auditRaw = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "user.jobrole.changed" && a.EntityId != null
                        && userIds.Contains(a.EntityId!.Value) && a.DataJson != null)
            .Select(a => new { UserId = a.EntityId!.Value, a.DataJson, a.CreatedAtUtc })
            .ToListAsync(ct);

        // آخر لحظة أُسنِد فيها كلّ (مستخدم → مسمّى) عبر واجهة برمجيّة موثَّقة.
        var auditedAssignedAt = new Dictionary<(Guid User, Guid JobRole), DateTime>();
        foreach (var a in auditRaw)
        {
            var newJobRoleId = ParseNewJobRoleId(a.DataJson);
            if (newJobRoleId is not Guid jr) continue;
            var k = (a.UserId, jr);
            if (!auditedAssignedAt.TryGetValue(k, out var existing) || a.CreatedAtUtc > existing)
                auditedAssignedAt[k] = a.CreatedAtUtc;
        }

        // ===== (5) التسليمات (LEFT JOIN): كلّ تسليمات المستخدمين الأسبوعية لهذه المفاتيح، بكلّ الحالات =====
        var allLineageVersionIds = versionRows.Select(v => v.VersionId).Distinct().ToList();
        var subsByKeyUser = new Dictionary<(Guid User, string Key), SubRow>();
        if (allLineageVersionIds.Count > 0)
        {
            var subs = await _db.ReportSubmissions.AsNoTracking()
                .Where(s => userIds.Contains(s.SubmitterId)
                            && s.PeriodType == PeriodType.Weekly
                            && keys.Contains(s.PeriodKey)
                            && allLineageVersionIds.Contains(s.ReportTemplateVersionId))
                .Select(s => new { s.Id, s.SubmitterId, s.PeriodKey, s.Status, s.SubmittedAtUtc, s.ClosedAtUtc, s.ReportTemplateVersionId })
                .ToListAsync(ct);

            foreach (var g in subs.GroupBy(s => (s.SubmitterId, s.PeriodKey)))
            {
                // التسليم الممثِّل = الأكثر تقدّمًا في سير العمل (الحذف الناعم مُستبعَد بالفلتر العام).
                var rep = g.OrderByDescending(x => StatusProgressRank(x.Status)).First();
                subsByKeyUser[(g.Key.SubmitterId, g.Key.PeriodKey)] = new SubRow(
                    rep.Id, rep.Status, rep.SubmittedAtUtc, rep.ClosedAtUtc, rep.ReportTemplateVersionId);
            }
        }

        // ===== (6) التسميات (تنظيم + الدور الأساسيّ لاشتقاق موعد الاستحقاق بحسب الدور) =====
        var deptIds = users.Where(u => u.DepartmentId is not null).Select(u => u.DepartmentId!.Value).Distinct().ToList();
        var teamIds = users.Where(u => u.TeamId is not null).Select(u => u.TeamId!.Value).Distinct().ToList();
        var deptNames = deptIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);
        var teamNames = teamIds.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.Teams.AsNoTracking().Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
        var rolesByUser = await UserPrimaryRolesAsync(userIds, ct);

        // ===== الاشتقاق =====
        var now = _clock.UtcNow;
        var currentCycleKey = ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhDate(now.UtcDateTime));
        var currentCycleStart = ReportingCalendarPolicy.CycleRange(currentCycleKey).Start;

        var results = new List<ExpectedCycleResult>(users.Count * keys.Count);

        foreach (var u in users)
        {
            if (u.JobRoleId is not Guid userJobRole) continue;
            if (!templateByJobRole.TryGetValue(userJobRole, out var tpl)) continue; // لا قالب مطالبة ⇒ خارج المسار.

            var role = RoleAccess.PrimaryRole(rolesByUser.GetValueOrDefault(u.Id) ?? new List<string>());

            // أرضيّة الانطباق (تُحسَب مرّة لكلّ مستخدم/قالب).
            var userCreated = ReportingCalendarPolicy.RiyadhDate(EnsureUtc(u.CreatedAtUtc));
            DateOnly? templateFirstPublished = firstPublishByTemplate.TryGetValue(tpl.Id, out var fp)
                ? ReportingCalendarPolicy.RiyadhDate(EnsureUtc(fp)) : null;
            DateOnly? auditedAssigned = auditedAssignedAt.TryGetValue((u.Id, userJobRole), out var aa)
                ? ReportingCalendarPolicy.RiyadhDate(EnsureUtc(aa)) : null;

            var floor = ApplicabilityFloorPolicy.Resolve(new ApplicabilityFloorPolicy.FloorInput(
                UserCreatedAt: userCreated,
                TemplateFirstPublishedAt: templateFirstPublished,
                AuditedJobRoleAssignedAt: auditedAssigned));

            var deptName = u.DepartmentId is Guid did ? deptNames.GetValueOrDefault(did) : null;
            var teamName = u.TeamId is Guid tid ? teamNames.GetValueOrDefault(tid) : null;
            var lineage = lineageByTemplate.GetValueOrDefault(tpl.Id) ?? new HashSet<Guid>();

            foreach (var key in keys)
            {
                var (start, end) = ReportingCalendarPolicy.CycleRange(key);
                var dueDate = ReportingCalendarPolicy.RoleDueDate(key, role);
                var cycleStartsAt = new DateTimeOffset(start.Year, start.Month, start.Day, 0, 0, 0, Riyadh);
                var dueAt = new DateTimeOffset(dueDate.Year, dueDate.Month, dueDate.Day, 23, 59, 59, Riyadh);

                var cycleApplicable = ApplicabilityFloorPolicy.IsCycleApplicable(start, floor.Floor);
                var userActive = u.IsActive;

                // التسليم المطابق (يجب أن تنتمي نسخته إلى نسب قالب هذا المستخدم).
                subsByKeyUser.TryGetValue((u.Id, key), out var sub);
                var hasSubmission = sub is not null
                    && versionToTemplate.TryGetValue(sub.VersionId, out var subTpl)
                    && subTpl == tpl.Id
                    && lineage.Contains(sub.VersionId);
                if (!hasSubmission) sub = null;

                SubmissionStatus? status = sub?.Status;
                DateTimeOffset? submittedAt = sub?.SubmittedAtUtc is DateTime sa
                    ? new DateTimeOffset(EnsureUtc(sa)) : null;
                DateTimeOffset? closedAt = sub?.ClosedAtUtc is DateTime ca
                    ? new DateTimeOffset(EnsureUtc(ca)) : null;

                // بوّابة الانطباق تُطبَّق عبر IsRequired: الدورة قبل الأرضيّة ⇒ غير مطلوبة ⇒ NotRequired.
                var input = new CycleStatusInput(
                    IsAssigned: true,
                    IsRequired: userActive && cycleApplicable,
                    CycleStartsAt: cycleStartsAt,
                    DueAt: dueAt,
                    CurrentTime: now,
                    HasSubmission: hasSubmission,
                    SubmissionStatus: status,
                    SubmittedAt: submittedAt,
                    ApprovedAt: null,
                    ClosedAt: closedAt,
                    IsDeleted: false);

                var derived = UnifiedCycleStatusPolicy.Derive(input);

                // إسقاط الحالة الموحّدة → قاموس القراءة المتوقّع، مع بوّابات نقيّة قبل الاشتقاق.
                ExpectedSubmissionStatus expected;
                string? exclusionReason = null;
                // السبب المُصنَّف: يفصل «ما قبل الأرضيّة» عن «الإعفاء/الإجازة» صراحةً.
                // هذا المُشتقّ لا يحسب إعفاءً/إجازة إطلاقًا ⇒ ExemptOrOnLeave لا يُنتَج هنا أبدًا،
                // فالدورة السابقة للأرضيّة تبقى BeforeApplicabilityFloor حصرًا (لا تُعرَض كإعفاء).
                CycleExclusionReason exclusionCode = CycleExclusionReason.None;
                if (!userActive)
                {
                    expected = ExpectedSubmissionStatus.InactiveUser;
                    exclusionReason = "الموظّف غير نشط";
                    exclusionCode = CycleExclusionReason.InactiveUser;
                }
                else if (!cycleApplicable)
                {
                    expected = ExpectedSubmissionStatus.NotApplicable;
                    exclusionReason = "الدورة قبل أرضيّة الانطباق";
                    exclusionCode = CycleExclusionReason.BeforeApplicabilityFloor;
                }
                else
                {
                    expected = MapToExpected(derived.Status, status);
                }

                var isExpectedFlag = userActive && cycleApplicable;
                var hasStarted = hasSubmission;
                var withinDeadline = now <= dueAt;
                var isHistorical = start < currentCycleStart && key != currentCycleKey;
                var (label, severity) = LabelAndSeverity(expected);

                results.Add(new ExpectedCycleResult(
                    UserId: u.Id,
                    UserFullName: u.FullName,
                    TemplateId: tpl.Id,
                    TemplateName: tpl.Title,
                    PeriodKey: key,
                    PeriodStart: start,
                    PeriodEnd: end,
                    DueAt: dueDate,
                    IsExpected: isExpectedFlag,
                    ApplicabilityFloor: floor.Floor,
                    ApplicabilitySource: floor.Source,
                    ApplicabilityConfidence: floor.Confidence,
                    SubmissionId: sub?.Id,
                    SubmissionStatus: status,
                    HasSubmission: hasSubmission,
                    HasStarted: hasStarted,
                    IsWithinDeadline: withinDeadline,
                    IsLate: derived.IsLate,
                    DelayDays: derived.DelayDays,
                    IsActionable: isExpectedFlag && IsActionable(expected),
                    IsHistorical: isHistorical,
                    Status: expected,
                    UnifiedStatus: derived.Status,
                    ActionRequiredRank: ActionRank(expected),
                    ExclusionReason: exclusionReason,
                    StatusLabel: label,
                    Severity: severity)
                {
                    ExclusionReasonCode = exclusionCode,
                    DepartmentId = u.DepartmentId,
                    DepartmentName = deptName,
                    TeamId = u.TeamId,
                    TeamName = teamName,
                    PrimaryRole = role
                });
            }
        }

        return results;
    }

    public async Task<ExpectedSelfStatus> ResolveSelfAsync(
        Guid userId, IReadOnlyList<string> cycleKeys, Guid? templateId = null, CancellationToken ct = default)
    {
        var all = await ResolveAsync(new ExpectedStatusQuery(new[] { userId }, cycleKeys, templateId), ct);

        var now = _clock.UtcNow;
        var currentKey = ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhDate(now.UtcDateTime));

        // الدورة الحاليّة = دورة اليوم فقط (شارة الموظّف). لا تُخفيها بنود تاريخيّة متأخّرة.
        var current = all.FirstOrDefault(r => r.PeriodKey == currentKey && r.IsExpected);

        // بنود العمل التاريخيّة = دورات ماضية تتطلّب إجراءً، الأحدث أوّلًا (DueAt تنازليًّا).
        var historical = all
            .Where(r => r.IsExpected && r.IsHistorical && r.IsActionable)
            .OrderByDescending(r => r.DueAt)
            .ThenBy(r => r.ActionRequiredRank)
            .ToList();

        return new ExpectedSelfStatus(current, historical);
    }

    public async Task<ExpectedManagementProjection> ResolveManagementAsync(
        string cycleKey, IReadOnlyCollection<Guid> userIds, Guid? templateId = null, CancellationToken ct = default)
    {
        var key = ReportingCalendarPolicy.IsValidCycleKey(cycleKey) ? cycleKey.Trim() : cycleKey;
        var all = await ResolveAsync(new ExpectedStatusQuery(userIds, new[] { key }, templateId), ct);

        // العدّادات على المتوقّع فقط (الدورات المنطبقة). NotApplicable/InactiveUser مُستبعَدة.
        var expected = all.Where(r => r.IsExpected).ToList();

        int Count(ExpectedSubmissionStatus s) => expected.Count(r => r.Status == s);
        var submitted = expected.Count(r => r.Status is ExpectedSubmissionStatus.Submitted
            or ExpectedSubmissionStatus.Approved or ExpectedSubmissionStatus.Closed);

        // بنود الإجراء الإداريّ: المتأخّر بلا تسليم/المسودّة المتأخّرة/المُعاد/المُصعَّد (الأكثر إلحاحًا أوّلًا).
        var actionItems = expected
            .Where(r => r.IsActionable)
            .OrderBy(r => r.ActionRequiredRank)
            .ThenBy(r => r.UserFullName)
            .ToList();

        return new ExpectedManagementProjection(
            PeriodKey: key,
            CycleLabel: ReportingCalendarPolicy.CycleLabel(key),
            Expected: expected.Count,
            Submitted: submitted,
            NotStartedWithinDeadline: Count(ExpectedSubmissionStatus.NotStartedWithinDeadline),
            OverdueNotSubmitted: Count(ExpectedSubmissionStatus.OverdueNotSubmitted),
            OverdueDraft: Count(ExpectedSubmissionStatus.OverdueDraft),
            ReturnedActionRequired: Count(ExpectedSubmissionStatus.ReturnedActionRequired),
            EscalatedActionRequired: Count(ExpectedSubmissionStatus.EscalatedActionRequired),
            ActionItems: actionItems);
    }

    // ===== الإسقاط: الحالة الموحّدة (+ الحالة الخام) → قاموس القراءة المتوقّع =====
    private static ExpectedSubmissionStatus MapToExpected(UnifiedCycleStatus unified, SubmissionStatus? raw) => unified switch
    {
        UnifiedCycleStatus.NotDue => ExpectedSubmissionStatus.NotStartedWithinDeadline,
        UnifiedCycleStatus.DueNow => ExpectedSubmissionStatus.NotStartedWithinDeadline,
        UnifiedCycleStatus.Draft => ExpectedSubmissionStatus.DraftWithinDeadline,
        UnifiedCycleStatus.OverdueDraft => ExpectedSubmissionStatus.OverdueDraft,
        UnifiedCycleStatus.OverdueNotSubmitted => ExpectedSubmissionStatus.OverdueNotSubmitted,
        UnifiedCycleStatus.ReturnedForChanges => ExpectedSubmissionStatus.ReturnedActionRequired,
        UnifiedCycleStatus.OverdueReturned => ExpectedSubmissionStatus.ReturnedActionRequired,
        UnifiedCycleStatus.SubmittedOnTime => ExpectedSubmissionStatus.Submitted,
        UnifiedCycleStatus.SubmittedLate => ExpectedSubmissionStatus.Submitted,
        // عالق عند معتمِد: التصعيد يتطلّب إجراء معتمِد، وإلا مُسلَّم (لا إجراء على الموظّف).
        UnifiedCycleStatus.PendingApproval => raw == SubmissionStatus.Escalated
            ? ExpectedSubmissionStatus.EscalatedActionRequired
            : ExpectedSubmissionStatus.Submitted,
        UnifiedCycleStatus.Approved => ExpectedSubmissionStatus.Approved,
        UnifiedCycleStatus.Closed => raw == SubmissionStatus.Closed || raw == SubmissionStatus.Visible
            ? ExpectedSubmissionStatus.Closed
            : ExpectedSubmissionStatus.Approved,
        // NotAssigned/NotRequired لا يصلان هنا (بوّابة الانطباق تسبق الاستدعاء).
        _ => ExpectedSubmissionStatus.HistoricalUnknown
    };

    private static bool IsActionable(ExpectedSubmissionStatus s) => s is
        ExpectedSubmissionStatus.OverdueNotSubmitted or
        ExpectedSubmissionStatus.OverdueDraft or
        ExpectedSubmissionStatus.ReturnedActionRequired or
        ExpectedSubmissionStatus.EscalatedActionRequired;

    // رتبة الإلحاح (أدنى = أعلى إلحاحًا) — تطابق ترتيب أولوية الإجراء الموحّد.
    private static int ActionRank(ExpectedSubmissionStatus s) => s switch
    {
        ExpectedSubmissionStatus.OverdueNotSubmitted => 1,
        ExpectedSubmissionStatus.EscalatedActionRequired => 2,
        ExpectedSubmissionStatus.ReturnedActionRequired => 3,
        ExpectedSubmissionStatus.OverdueDraft => 4,
        ExpectedSubmissionStatus.DraftWithinDeadline => 5,
        ExpectedSubmissionStatus.NotStartedWithinDeadline => 6,
        ExpectedSubmissionStatus.Submitted => 7,
        ExpectedSubmissionStatus.Approved => 8,
        ExpectedSubmissionStatus.Closed => 9,
        _ => 99
    };

    private static (string Label, string Severity) LabelAndSeverity(ExpectedSubmissionStatus s) => s switch
    {
        ExpectedSubmissionStatus.NotApplicable => ("لا ينطبق", "none"),
        ExpectedSubmissionStatus.NotStartedWithinDeadline => ("لم يبدأ — ضمن المهلة", "info"),
        ExpectedSubmissionStatus.DraftWithinDeadline => ("مسودّة — ضمن المهلة", "info"),
        ExpectedSubmissionStatus.OverdueNotSubmitted => ("متأخّر — لم يُسلَّم", "alert"),
        ExpectedSubmissionStatus.OverdueDraft => ("مسودّة متأخّرة", "warn"),
        ExpectedSubmissionStatus.ReturnedActionRequired => ("مُعاد للتعديل", "warn"),
        ExpectedSubmissionStatus.EscalatedActionRequired => ("مُصعَّد", "alert"),
        ExpectedSubmissionStatus.Submitted => ("مُسلَّم", "success"),
        ExpectedSubmissionStatus.Approved => ("معتمَد", "success"),
        ExpectedSubmissionStatus.Closed => ("مُغلَق", "success"),
        ExpectedSubmissionStatus.InactiveUser => ("موظّف غير نشط", "none"),
        _ => ("غير محدَّد", "none")
    };

    // رتبة تقدّم التسليم (لاختيار الممثِّل الأكثر تقدّمًا عند تعدّد الصفوف لنفس الدورة).
    private static int StatusProgressRank(SubmissionStatus s) => s switch
    {
        SubmissionStatus.Draft => 0,
        SubmissionStatus.Returned => 1,
        SubmissionStatus.Submitted => 2,
        SubmissionStatus.ApprovedByDirectManager => 3,
        SubmissionStatus.ApprovedByNextLevel => 4,
        SubmissionStatus.Escalated => 5,
        SubmissionStatus.Closed => 6,
        SubmissionStatus.Visible => 7,
        _ => -1
    };

    // يستخرج newJobRoleId من DataJson لحدث user.jobrole.changed (قد يكون null أو GUID نصّيًّا).
    private static Guid? ParseNewJobRoleId(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            if (!doc.RootElement.TryGetProperty("newJobRoleId", out var prop)) return null;
            if (prop.ValueKind == JsonValueKind.String && Guid.TryParse(prop.GetString(), out var g))
                return g;
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTime EnsureUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private async Task<Dictionary<Guid, List<string>>> UserPrimaryRolesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, List<string>>();
        var pairs = await (from ur in _db.UserRoles
                           join r in _db.Roles on ur.RoleId equals r.Id
                           where userIds.Contains(ur.UserId) && r.Name != null
                           select new { ur.UserId, r.Name }).ToListAsync(ct);
        return pairs.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());
    }

    // ===== أنواع داخلية =====
    private sealed record VersionRow(Guid VersionId, Guid TemplateId, bool IsPublished, DateTime? PublishedAtUtc);

    private sealed record SubRow(Guid Id, SubmissionStatus Status, DateTime? SubmittedAtUtc, DateTime? ClosedAtUtc, Guid VersionId);
}
