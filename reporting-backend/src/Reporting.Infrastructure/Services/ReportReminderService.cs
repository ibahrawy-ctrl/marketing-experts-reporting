using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Application.Reports;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// EMAIL-REPORT-REMINDERS-R1 — مولّد تذكيرات/تنبيهات التقارير (توليد يدويّ في R1، وضع DryRun افتراضًا).
/// يعيد استخدام منطق تقييم RPT-DUE1 (المرشّحون/المواعيد/التأخّر/المراجعات العالقة) لكن على مستوى الشركة كاملًا
/// (بلا أيّ قيد نطاق ScopeResolver — لا يحقن ICurrentUser/IScopeResolver)، ثم يُدرِج كلّ تذكير عبر القلب الآمن في
/// <see cref="IEmailNotificationService.EnqueueReportReminderAsync"/> الذي يحترم الوضع، يمنع التكرار عبر CorrelationKey،
/// لا يمسّ email_outbox، ولا يرمي.
///
/// المسار المستقبليّ (R1→R2→R3):
/// - R1 (الآن): يُستدعى <see cref="GenerateAsync"/> يدويًّا عبر نقطة نهاية إدارية، الوضع DryRun (لا إرسال فعليّ).
/// - R2: تلفّ خدمةٌ خلفية مجدولة (BackgroundService) نفس <see cref="GenerateAsync"/> بلا تغيير في المنطق، يبقى DryRun.
/// - R3: يُحوَّل EmailNotifications:Mode إلى Enabled وتُهيّأ SMTP؛ لا يتغيّر هذا المولّد إطلاقًا (الإرسال محكوم بالقلب الآمن).
///
/// قراءة فقط على بيانات التقارير: لا يعدّل أيّ تسليم/سير عمل/تقييم؛ يكتب فقط صفوف email_notifications عبر القلب الآمن.
/// </summary>
public class ReportReminderService : IReportReminderService
{
    private readonly AppDbContext _db;
    private readonly IEmailNotificationService _email;
    private readonly EmailNotificationOptions _options;
    private readonly AppOptions _app;
    private readonly ILogger<ReportReminderService> _logger;

    // حالات «أُرسِل التقرير» — مطابقة لـ RPT-DUE1/ReportingService.
    private static readonly SubmissionStatus[] SubmittedStatuses =
    {
        SubmissionStatus.Submitted, SubmissionStatus.Returned, SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel, SubmissionStatus.Escalated, SubmissionStatus.Closed,
        SubmissionStatus.Visible
    };

    // حالات «بانتظار اعتماد» — مطابقة لـ RPT-DUE1/ReportingService.
    private static readonly SubmissionStatus[] PendingApprovalStatuses =
    {
        SubmissionStatus.Submitted, SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel, SubmissionStatus.Escalated
    };

    public ReportReminderService(
        AppDbContext db,
        IEmailNotificationService email,
        IOptions<EmailNotificationOptions> options,
        IOptions<AppOptions> app,
        ILogger<ReportReminderService> logger)
    {
        _db = db;
        _email = email;
        _options = options.Value;
        _app = app.Value;
        _logger = logger;
    }

    public async Task<ReportReminderRunResult> GenerateAsync(ReportReminderRunOptions options, CancellationToken ct = default)
    {
        var key = NormalizeWeekKey(options.WeekKey);
        var weekLabel = ReportCalendarPolicy.WeekLabel(key);
        var mode = _options.Mode.ToString();

        var acc = new Accumulator();

        var candidates = await ResolveDueCandidatesAsync(ct);
        var evals = await EvaluateAsync(key, candidates, options.Date, ct);
        var reviews = await ResolvePendingReviewsAsync(key, ct);

        // ===== تذكيرات الاستحقاق (لم يحن التأخّر بعد) — النوعان 1 و2 =====
        if (options.IncludeDue)
        {
            await EmitDueRemindersAsync(key, weekLabel, evals, acc, ct);
        }

        // ===== تنبيهات التأخّر — الأنواع 3 و4 و5 و6 =====
        if (options.IncludeOverdue)
        {
            await EmitOverdueRemindersAsync(key, weekLabel, evals, acc, ct);
        }

        // ===== تنبيهات تأخّر/تعليق المراجعة — الأنواع 7 و8 و9 =====
        if (options.IncludeReviewOverdue)
        {
            await EmitReviewRemindersAsync(key, weekLabel, reviews, acc, ct);
        }

        return acc.ToResult(key, weekLabel, mode);
    }

    // ===== النوعان 1 (أسبوعي مستحق) و2 (يومي مستحق) — غير متأخّرة =====
    private async Task EmitDueRemindersAsync(string key, string weekLabel, IReadOnlyList<DueEval> evals, Accumulator acc, CancellationToken ct)
    {
        var today = ReportCalendarPolicy.RiyadhToday();
        var link = BuildLink("/app/submissions");

        foreach (var e in evals.Where(e => !e.HasSubmission))
        {
            var c = e.Candidate;
            if (c.Cadence == PeriodType.Daily)
            {
                // يوميّ مستحقّ اليوم (لم يتأخّر بعد).
                if (e.DueDate != today) continue;
                var dateKey = e.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var subject = "تذكير بتقرير المبيعات اليومي";
                var body = string.Join("\n", new[]
                {
                    $"مرحبًا {c.FullName}،",
                    $"هذا تذكير لطيف بتقرير المبيعات اليومي ليوم {dateKey}، لمساعدتك على توثيق نشاطك اليومي بسهولة.",
                    "يمكنك تسجيل تقريرك عبر الرابط أدناه — نشكر لك التزامك واجتهادك."
                });
                await EnqueueAsync(acc, "report-daily-due", c.UserId,
                    $"report-daily-due:{dateKey}:{c.UserId}", subject, body, link, ct);
            }
            else
            {
                // أسبوعيّ مستحقّ ولم يتأخّر بعد (ضمن المهلة).
                if (e.Overdue) continue;
                var subject = "تذكير بتقريرك الأسبوعي";
                var body = string.Join("\n", new[]
                {
                    $"مرحبًا {c.FullName}،",
                    $"هذا تذكير لطيف بتقريرك الأسبوعي ({weekLabel})، حتى يتم توثيق مجهودك وإنجازاتك بشكل واضح داخل النظام.",
                    $"الموعد المتوقّع للتسليم: {e.DueDate:yyyy-MM-dd}.",
                    "يمكنك تسجيل تقريرك عبر الرابط أدناه — نقدّر لك حرصك."
                });
                await EnqueueAsync(acc, "report-weekly-due", c.UserId,
                    $"report-weekly-due:{key}:{c.UserId}", subject, body, link, ct);
            }
        }
    }

    // ===== النوع 3 (تأخّر تقرير الموظّف) + ملخّصات التأخّر 4/5/6 =====
    private async Task EmitOverdueRemindersAsync(string key, string weekLabel, IReadOnlyList<DueEval> evals, Accumulator acc, CancellationToken ct)
    {
        var submissionsLink = BuildLink("/app/submissions");
        var complianceLink = BuildLink("/app/compliance");

        // --- النوع 3: تنبيه فرديّ لكل موظّف متأخّر (أسبوعيّ = صفّ واحد؛ يوميّ = صفّ لكل يوم متأخّر) ---
        foreach (var e in evals.Where(e => !e.HasSubmission && e.Overdue))
        {
            var c = e.Candidate;
            var subject = "تنبيه بتأخر تقريرك";
            if (c.Cadence == PeriodType.Daily)
            {
                var dateKey = e.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var body = string.Join("\n", new[]
                {
                    $"مرحبًا {c.FullName}،",
                    "نذكّرك بلطف أن تقريرك لم يتم تسجيله بعد، وتسجيله يساعد في حفظ مجهودك وتسهيل متابعة إنجازاتك.",
                    $"تقرير المبيعات اليومي ليوم {dateKey}."
                });
                await EnqueueAsync(acc, "report-overdue", c.UserId,
                    $"report-overdue:{dateKey}:{c.UserId}:{DelayType.EmployeeReportNotSubmitted}",
                    subject, body, submissionsLink, ct);
            }
            else
            {
                var body = string.Join("\n", new[]
                {
                    $"مرحبًا {c.FullName}،",
                    "نذكّرك بلطف أن تقريرك لم يتم تسجيله بعد، وتسجيله يساعد في حفظ مجهودك وتسهيل متابعة إنجازاتك.",
                    $"الفترة: {weekLabel}، والموعد المتوقّع: {e.DueDate:yyyy-MM-dd}."
                });
                await EnqueueAsync(acc, "report-overdue", c.UserId,
                    $"report-overdue:{key}:{c.UserId}:{DelayType.EmployeeReportNotSubmitted}",
                    subject, body, submissionsLink, ct);
            }
        }

        // --- المرشّحون المتأخّرون المميَّزون (لأي فترة) لبناء الملخّصات ---
        var overdueByUser = evals
            .Where(e => !e.HasSubmission && e.Overdue)
            .GroupBy(e => e.Candidate.UserId)
            .Select(g => g.First().Candidate)
            .ToList();
        if (overdueByUser.Count == 0) return;

        // أسماء المستلمين المحتملين (قادة الفرق/المديرون/الإداريون) تُحلّ لاحقًا حسب الحاجة.

        // --- النوع 4: ملخّص لقائد الفريق (تجميع حسب الفريق ثم قائده الفعليّ) ---
        var teamOverdueCounts = overdueByUser
            .Where(c => c.TeamId is not null)
            .GroupBy(c => c.TeamId!.Value)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Name: g.First().TeamName));
        if (teamOverdueCounts.Count > 0)
        {
            var teamIds = teamOverdueCounts.Keys.ToList();
            var teamLeaders = await _db.Teams.AsNoTracking()
                .Where(t => teamIds.Contains(t.Id) && t.TeamLeaderId != null)
                .Select(t => new { t.Id, LeaderId = t.TeamLeaderId!.Value })
                .ToListAsync(ct);
            // تجميع العدّاد لكلّ قائد فريق (قد يقود أكثر من فريق).
            var byLeader = new Dictionary<Guid, int>();
            foreach (var t in teamLeaders)
                byLeader[t.LeaderId] = byLeader.GetValueOrDefault(t.LeaderId) + teamOverdueCounts[t.Id].Count;
            var leaderNames = await UserNamesAsync(byLeader.Keys, ct);
            foreach (var (leaderId, count) in byLeader)
            {
                var subject = "تنبيه بوجود تقارير متأخرة داخل فريقك";
                var body = string.Join("\n", new[]
                {
                    $"مرحبًا {leaderNames.GetValueOrDefault(leaderId, "—")}،",
                    $"نودّ تنبيهك بلطف إلى وجود {count} تقرير متأخّر داخل فريقك لهذا الأسبوع ({weekLabel}).",
                    "متابعتك تساعد فريقك على إبراز إنجازاته في الوقت المناسب."
                });
                await EnqueueAsync(acc, "report-team-overdue-summary", leaderId,
                    $"report-team-overdue-summary:{key}:{leaderId}", subject, body, complianceLink, ct);
            }
        }

        // --- النوع 5: ملخّص للمدير (تجميع حسب الإدارة ثم مديرها الفعليّ) ---
        var deptOverdueCounts = overdueByUser
            .Where(c => c.DepartmentId is not null)
            .GroupBy(c => c.DepartmentId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        if (deptOverdueCounts.Count > 0)
        {
            var deptIds = deptOverdueCounts.Keys.ToList();
            var deptManagers = await _db.Departments.AsNoTracking()
                .Where(d => deptIds.Contains(d.Id) && d.ManagerId != null)
                .Select(d => new { d.Id, ManagerId = d.ManagerId!.Value })
                .ToListAsync(ct);
            var byManager = new Dictionary<Guid, int>();
            foreach (var d in deptManagers)
                byManager[d.ManagerId] = byManager.GetValueOrDefault(d.ManagerId) + deptOverdueCounts[d.Id];
            var managerNames = await UserNamesAsync(byManager.Keys, ct);
            foreach (var (managerId, count) in byManager)
            {
                var subject = "تنبيه بوجود تقارير متأخرة داخل إدارتك";
                var body = string.Join("\n", new[]
                {
                    $"مرحبًا {managerNames.GetValueOrDefault(managerId, "—")}،",
                    $"نودّ تنبيهك بلطف إلى وجود {count} تقرير متأخّر داخل إدارتك لهذا الأسبوع ({weekLabel}).",
                    "متابعتك تدعم فرق إدارتك على إبراز إنجازاتها في وقتها."
                });
                await EnqueueAsync(acc, "report-department-overdue-summary", managerId,
                    $"report-department-overdue-summary:{key}:{managerId}", subject, body, complianceLink, ct);
            }
        }

        // --- النوع 6: ملخّص تنفيذيّ (CEO/GM/CeoSupport) على مستوى الشركة ---
        var totalOverdue = overdueByUser.Count;
        if (totalOverdue > 0)
        {
            var execIds = await ExecutiveRecipientIdsAsync(ct);
            if (execIds.Count > 0)
            {
                var execNames = await UserNamesAsync(execIds, ct);
                foreach (var execId in execIds)
                {
                    var subject = "ملخص متابعة التقارير المتأخرة";
                    var body = string.Join("\n", new[]
                    {
                        $"مرحبًا {execNames.GetValueOrDefault(execId, "—")}،",
                        $"ملخّص لطيف لمتابعة التقارير: يوجد {totalOverdue} موظّف لديه تقرير متأخّر على مستوى الشركة لهذا الأسبوع ({weekLabel}).",
                        "يمكنك الاطّلاع على التفاصيل عبر الرابط أدناه لدعم فرق العمل في متابعتها."
                    });
                    await EnqueueAsync(acc, "report-executive-overdue-summary", execId,
                        $"report-executive-overdue-summary:{key}:{execId}", subject, body, complianceLink, ct);
                }
            }
        }
    }

    // ===== الأنواع 7 (قائد الفريق) و8 (المدير) و9 (تنفيذيّ) — مراجعات متأخّرة/معلّقة =====
    private async Task EmitReviewRemindersAsync(string key, string weekLabel, IReadOnlyList<PendingReview> reviews, Accumulator acc, CancellationToken ct)
    {
        var complianceLink = BuildLink("/app/compliance");
        var overdue = reviews.Where(r => r.Overdue).ToList();
        if (overdue.Count == 0) return;

        // تجميع عدد المراجعات المتأخّرة لكلّ معتمِد ونوع تأخّره.
        var byApprover = overdue
            .GroupBy(r => (r.ApproverId, r.DelayType))
            .Select(g => new { g.Key.ApproverId, g.Key.DelayType, Count = g.Count(), Name = g.First().ApproverName })
            .ToList();

        foreach (var g in byApprover)
        {
            switch (g.DelayType)
            {
                case DelayType.TeamLeaderReviewOverdue:
                {
                    var subject = "تنبيه بتأخر مراجعة تقارير الفريق";
                    var body = string.Join("\n", new[]
                    {
                        $"مرحبًا {g.Name}،",
                        $"نذكّرك بلطف بوجود {g.Count} تقرير بانتظار مراجعتك داخل فريقك لهذا الأسبوع ({weekLabel})،",
                        "ومراجعتها تساعد على استمرار سير العمل بسلاسة وإبراز جهود الفريق."
                    });
                    await EnqueueAsync(acc, "report-review-overdue-teamleader", g.ApproverId,
                        $"report-review-overdue-teamleader:{key}:{g.ApproverId}", subject, body, complianceLink, ct);
                    break;
                }
                case DelayType.ManagerReviewOverdue:
                {
                    var subject = "تنبيه بتأخر مراجعة تقارير الإدارة";
                    var body = string.Join("\n", new[]
                    {
                        $"مرحبًا {g.Name}،",
                        $"نذكّرك بلطف بوجود {g.Count} تقرير بانتظار مراجعتك داخل إدارتك لهذا الأسبوع ({weekLabel})،",
                        "ومراجعتها تساعد على استمرار سير العمل بسلاسة ودعم فرق الإدارة."
                    });
                    await EnqueueAsync(acc, "report-review-overdue-manager", g.ApproverId,
                        $"report-review-overdue-manager:{key}:{g.ApproverId}", subject, body, complianceLink, ct);
                    break;
                }
                default: // ExecutiveReviewPending
                {
                    var subject = "تنبيه بوجود مراجعات تقارير معلقة";
                    var body = string.Join("\n", new[]
                    {
                        $"مرحبًا {g.Name}،",
                        $"نودّ تنبيهك بلطف إلى وجود {g.Count} تقرير بانتظار المراجعة النهائية لهذا الأسبوع ({weekLabel}).",
                        "متابعتك تساعد على إغلاق دورة التقارير في وقتها."
                    });
                    await EnqueueAsync(acc, "report-review-pending-executive", g.ApproverId,
                        $"report-review-pending-executive:{key}:{g.ApproverId}", subject, body, complianceLink, ct);
                    break;
                }
            }
        }
    }

    // ===== إدراج تذكير واحد عبر القلب الآمن + تجميع النتيجة =====
    private async Task EnqueueAsync(Accumulator acc, string eventType, Guid recipientUserId,
        string correlationKey, string subject, string body, string link, CancellationToken ct)
    {
        var outcome = await _email.EnqueueReportReminderAsync(new ReportReminderMessage(
            EventType: eventType,
            RecipientUserId: recipientUserId,
            CorrelationKey: correlationKey,
            Subject: subject,
            Body: body,
            Link: link,
            EntityId: recipientUserId,
            EntityType: "ReportReminder"), ct);
        acc.Record(eventType, outcome);
    }

    // ===== المرشّحون المتوقَّع منهم تقرير على مستوى الشركة (بلا قيد نطاق) =====
    private async Task<List<DueCandidate>> ResolveDueCandidatesAsync(CancellationToken ct)
    {
        var reportingRoleIds = await ReportingRoleIdsAsync(ct);
        if (reportingRoleIds.Count == 0) return new List<DueCandidate>();

        var roleCadence = (await _db.JobRoles.AsNoTracking()
                .Where(j => reportingRoleIds.Contains(j.Id))
                .Select(j => new { j.Id, j.Code })
                .ToListAsync(ct))
            .ToDictionary(j => j.Id, j => ReportCadencePolicy.ExpectedCadence(j.Code));

        var reportingRoleIdSet = roleCadence.Keys.ToHashSet();

        // مستوى الشركة كاملًا: كلّ موظّف نشط مسمّاه ضمن أدوار التقارير — بلا قيد ScopeResolver.
        var raw = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.JobRoleId != null && reportingRoleIdSet.Contains(u.JobRoleId!.Value))
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

    // ===== تقييم أسبوع واحد (أسبوعي = وحدة لكل مرشّح؛ يومي = وحدة لكل يوم عمل) — يحاكي RPT-DUE1 =====
    private async Task<List<DueEval>> EvaluateAsync(string key, IReadOnlyList<DueCandidate> candidates, DateOnly? restrictDate, CancellationToken ct)
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
            if (restrictDate is DateOnly rd) dates = dates.Where(d => d == rd).ToList();
            if (dates.Count > 0)
            {
                var dailyIds = daily.Select(c => c.UserId).ToList();
                var (winStart, winEnd) = ReportingCalendarPolicy.CycleRange(key); // نافذة الدورة اليوميّة Sat→Fri
                // DAILY-…-R1 §3: تحميل عريض بلا فلترة نصّية قياسيّة (كي لا تسقط المفاتيح القديمة)
                // ثم تطبيع كلّ مفتاح إلى اليوم المنطقيّ داخل نافذة الأسبوع، والمطابقة على (المستخدم، اليوم المنطقيّ).
                var rows = await _db.ReportSubmissions.AsNoTracking()
                    .Where(s => s.PeriodType == PeriodType.Daily && s.PeriodKey != null
                                && SubmittedStatuses.Contains(s.Status) && dailyIds.Contains(s.SubmitterId))
                    .Select(s => new { s.SubmitterId, s.PeriodKey })
                    .ToListAsync(ct);
                var byUserDay = new HashSet<(Guid, string)>();
                foreach (var r in rows)
                    if (ReportingCalendarPolicy.TryCanonicalDay(r.PeriodKey, out var cd)
                        && cd >= winStart && cd <= winEnd)
                        byUserDay.Add((r.SubmitterId, ReportingCalendarPolicy.DayKey(cd)));

                foreach (var c in daily)
                    foreach (var day in dates)
                    {
                        var dayKey = ReportingCalendarPolicy.DayKey(day);
                        if (byUserDay.Contains((c.UserId, dayKey)))
                            evals.Add(new DueEval(c, true, day, false, null));
                        else
                            evals.Add(new DueEval(c, false, day, today > day, null));
                    }
            }
        }

        return evals;
    }

    // ===== المراجعات العالقة على مستوى الشركة (بلا قيد نطاق) =====
    private async Task<List<PendingReview>> ResolvePendingReviewsAsync(string key, CancellationToken ct)
    {
        // نافذة تصفية اليوميّة العالقة = نافذة الدورة Sat→Fri (لا WeekRange الخميس→الأربعاء).
        var (start, end) = ReportingCalendarPolicy.CycleRange(key);

        // DAILY-…-R1 §3: لا نفلتر اليوميّة العالقة بقائمة مفاتيح قياسيّة عند قاعدة البيانات (كي لا تسقط
        // المفاتيح القديمة)؛ نُحمِّل كلّ اليوميّة العالقة ثم نُطبِّق نافذة الأسبوع المنطقيّة داخليًّا بعد التطبيع.
        var raw = await _db.ReportSubmissions.AsNoTracking()
            .Where(s => PendingApprovalStatuses.Contains(s.Status) && s.CurrentApproverId != null
                        && ((s.PeriodType == PeriodType.Weekly && s.PeriodKey == key)
                            || s.PeriodType == PeriodType.Daily))
            .Select(s => new
            {
                s.Id, s.PeriodType, s.PeriodKey, s.TeamId, s.DepartmentId,
                ApproverId = s.CurrentApproverId!.Value
            }).ToListAsync(ct);
        if (raw.Count == 0) return new List<PendingReview>();

        // استبعاد اليوميّة خارج نافذة الأسبوع المنطقيّة (بعد التطبيع)؛ غير القابلة للتفسير تُستبعَد.
        raw = raw.Where(s => s.PeriodType != PeriodType.Daily
                             || (ReportingCalendarPolicy.TryCanonicalDay(s.PeriodKey, out var cd)
                                 && cd >= start && cd <= end)).ToList();
        if (raw.Count == 0) return new List<PendingReview>();

        var today = ReportCalendarPolicy.RiyadhToday();
        var approverIds = raw.Select(s => s.ApproverId).Distinct().ToList();
        var names = await UserNamesAsync(approverIds, ct);
        var approverRoles = await UserPrimaryRolesAsync(approverIds, ct);

        var list = new List<PendingReview>(raw.Count);
        foreach (var s in raw)
        {
            var role = RoleAccess.PrimaryRole(approverRoles.GetValueOrDefault(s.ApproverId) ?? new List<string>());
            var weekKeyOfReport = s.PeriodType == PeriodType.Weekly
                ? s.PeriodKey
                : (ReportingCalendarPolicy.TryCanonicalDay(s.PeriodKey, out var dd)
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
                reviewDue, today > reviewDue, delay));
        }
        return list;
    }

    // ===== مساعدات =====
    private async Task<List<Guid>> ExecutiveRecipientIdsAsync(CancellationToken ct)
    {
        var execRoleNames = new[] { Roles.Ceo, Roles.GeneralManager, Roles.CeoSupport };
        return await (from ur in _db.UserRoles
                      join r in _db.Roles on ur.RoleId equals r.Id
                      join u in _db.Users on ur.UserId equals u.Id
                      where u.IsActive && r.Name != null && execRoleNames.Contains(r.Name)
                      select u.Id).Distinct().ToListAsync(ct);
    }

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

    // DAILY-BUSINESS-DAY-COMPLIANCE-R1 §4 + SALES-DAILY-SATURDAY-APPLICABILITY-HOTFIX-R1:
    // يُفوَّض بالكامل إلى مصدر الحقيقة المركزيّ (ReportingCalendarPolicy.DailyExpectedDates = نافذة الدورة
    // Sat→Fri + أرضيّة الإطلاق + الأحد→الخميس). التذكير اليوميّ **مبيعات حصريًّا** (Daily ⟺ SALES_B2B/B2C)
    // ⇒ saturdayEnabled:true فيُذكَّر بالسبت المتوقَّع ابتداءً من الأرضية 2026-07-25 (الجمعة تبقى محجوبة).
    private static List<DateOnly> DailyExpectedDates(string cycleKey, DateOnly today) =>
        ReportingCalendarPolicy.DailyExpectedDates(cycleKey, today, saturdayEnabled: true);

    private static string NormalizeWeekKey(string? weekKey) =>
        ReportCalendarPolicy.IsWeekKey(weekKey) ? weekKey!.Trim() : ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.RiyadhToday());

    private string BuildLink(string path)
    {
        var baseUrl = (_app.BaseUrl ?? string.Empty).TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? path : baseUrl + path;
    }

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
        return await _db.Users.AsNoTracking().Where(u => distinct.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    // ===== أنواع داخلية =====
    private sealed record DueCandidate(
        Guid UserId, string FullName, Guid? DepartmentId, Guid? TeamId,
        string? DepartmentName, string? TeamName, string PrimaryRole, PeriodType Cadence);

    private sealed record DueEval(DueCandidate Candidate, bool HasSubmission, DateOnly DueDate, bool Overdue, Guid? SubmissionId);

    private sealed record PendingReview(
        Guid SubmissionId, Guid ApproverId, string ApproverName, string ApproverRole,
        DateOnly ReviewDue, bool Overdue, DelayType DelayType);

    /// <summary>مُجمِّع عدّادات النتيجة حسب نوع الحدث (WouldGenerate/Created/Duplicate/NoEmail/Disabled).</summary>
    private sealed class Accumulator
    {
        private readonly Dictionary<string, int[]> _rows = new();
        // فهارس العدّادات: 0=WouldGenerate 1=Created 2=SkippedDuplicate 3=SkippedNoEmail 4=SkippedDisabled

        public void Record(string eventType, ReportReminderOutcome outcome)
        {
            if (!_rows.TryGetValue(eventType, out var counts))
            {
                counts = new int[5];
                _rows[eventType] = counts;
            }
            counts[0]++; // كلّ محاولة إدراج تُعدّ «قابلة للتوليد»
            switch (outcome)
            {
                case ReportReminderOutcome.Created: counts[1]++; break;
                case ReportReminderOutcome.Duplicate: counts[2]++; break;
                case ReportReminderOutcome.SkippedNoEmail: counts[3]++; break;
                case ReportReminderOutcome.Disabled: counts[4]++; break;
                case ReportReminderOutcome.Error: break; // حُصِر داخليًّا؛ لا يُنسب لعدّاد نجاح
            }
        }

        public ReportReminderRunResult ToResult(string weekKey, string weekLabel, string mode)
        {
            var breakdown = _rows
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new ReportReminderBreakdownRow(
                    kv.Key, kv.Value[0], kv.Value[1], kv.Value[2], kv.Value[3], kv.Value[4]))
                .ToList();

            return new ReportReminderRunResult(
                weekKey, weekLabel, mode,
                WouldGenerate: breakdown.Sum(b => b.WouldGenerate),
                Created: breakdown.Sum(b => b.Created),
                SkippedDuplicate: breakdown.Sum(b => b.SkippedDuplicate),
                SkippedNoEmail: breakdown.Sum(b => b.SkippedNoEmail),
                SkippedDisabled: breakdown.Sum(b => b.SkippedDisabled),
                Breakdown: breakdown);
        }
    }
}
