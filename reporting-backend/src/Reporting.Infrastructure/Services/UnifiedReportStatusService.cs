using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Common;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — المصدر الخلفيّ الموحّد لحالة دورات التقارير الأسبوعية.
/// يجلب التسليمات دفعةً واحدة (بلا N+1) لمجموعة مفاتيح الدورات، ويطبّق <see cref="UnifiedCycleStatusPolicy"/>
/// النقيّة لاشتقاق الحالة/الشدّة/التأخّر. قراءة فقط: لا تعديل تسليم/سير عمل، لا هجرة، لا إشعار/بريد.
/// النطاق self-only في هذه المرحلة. المواعيد والدورة عبر <see cref="ReportingCalendarPolicy"/> (السبت مرساة)
/// لمطابقة منتقي الأسبوع تمامًا. تُجلَب كلّ حالات التسليم (لا المُرسَلة فقط) كي تميّز السياسة
/// المسودّة/المُعادة عن غير المُسلَّم.
/// </summary>
public class UnifiedReportStatusService : IUnifiedReportStatusService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ISystemClock _clock;

    // منطقة النظام (الرياض) — نُطبِّقها على حدود الدورة قبل تمريرها للسياسة النقيّة.
    private static readonly TimeSpan Riyadh = ReportingCalendarPolicy.RiyadhOffset;

    public UnifiedReportStatusService(AppDbContext db, ICurrentUser currentUser, ISystemClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<UnifiedReportCycleStatusDto>> GetMyWeeklyCycleStatusAsync(
        string cycleKey, Guid? templateId, CancellationToken ct = default)
    {
        if (!ReportingCalendarPolicy.IsValidCycleKey(cycleKey))
            return Result<UnifiedReportCycleStatusDto>.Failure("مفتاح الدورة غير صالح.", "calendar.cycle_key_invalid");

        var res = await GetMyWeeklyCycleStatusesAsync(new[] { cycleKey.Trim() }, templateId, ct);
        if (!res.Succeeded)
            return Result<UnifiedReportCycleStatusDto>.Failure(res.Error!, res.ErrorCode!);

        var one = res.Value!.FirstOrDefault();
        return one is null
            ? Result<UnifiedReportCycleStatusDto>.Failure("تعذّر اشتقاق حالة الدورة.", "unified_status.derive_failed")
            : Result<UnifiedReportCycleStatusDto>.Success(one);
    }

    public async Task<Result<IReadOnlyList<UnifiedReportCycleStatusDto>>> GetMyWeeklyCycleStatusesAsync(
        IReadOnlyList<string> cycleKeys, Guid? templateId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid uid)
            return Result<IReadOnlyList<UnifiedReportCycleStatusDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        // تطبيع + إزالة التكرار + تجاهل المفاتيح غير الصالحة (بلا رمي).
        var keys = (cycleKeys ?? Array.Empty<string>())
            .Where(k => ReportingCalendarPolicy.IsValidCycleKey(k))
            .Select(k => k.Trim())
            .Distinct()
            .ToList();
        if (keys.Count == 0)
            return Result<IReadOnlyList<UnifiedReportCycleStatusDto>>.Success(Array.Empty<UnifiedReportCycleStatusDto>());

        var role = RoleAccess.PrimaryRole(_currentUser.Roles);

        // هل المستخدم مطالَب بتقرير أسبوعيّ ضمن هذا المسار؟ (مصدر الإسناد = قوالب التقارير المطابقة لمسمّاه).
        var me = await _db.Users.AsNoTracking()
            .Where(u => u.Id == uid)
            .Select(u => new { u.Id, u.FullName, u.JobRoleId, u.IsActive, u.CreatedAtUtc })
            .FirstOrDefaultAsync(ct);
        if (me is null)
            return Result<IReadOnlyList<UnifiedReportCycleStatusDto>>.Failure("المستخدم غير موجود.", "user.not_found");

        // القالب الأساسيّ الأسبوعيّ للمسمّى (أو القالب المحدَّد إن مُرِّر) — لتعبئة اسم/نسخة القالب وتقييد النسب.
        var template = await ResolveTemplateAsync(me.JobRoleId, templateId, ct);
        var isAssigned = template is not null;

        // نسب النسخ لهذا القالب (لمطابقة التسليمات على lineage القالب لا نسخةً بعينها).
        List<Guid>? lineageVersionIds = null;
        if (template is not null)
        {
            lineageVersionIds = await _db.ReportTemplateVersions.AsNoTracking()
                .Where(v => v.ReportTemplateId == template.TemplateId)
                .Select(v => v.Id)
                .ToListAsync(ct);
        }

        // ===== أرضيّة الانطباق (Gate): الدورة قبلها ليست مطلوبة (لا التزام رجعيّ) =====
        // نفس منطق ExpectedSubmissionStatusResolver: MAX(إنشاء المستخدم، أوّل نشر للقالب، إسناد موثَّق).
        DateOnly? templateFirstPublished = null;
        if (template is not null)
        {
            var pubDates = await _db.ReportTemplateVersions.AsNoTracking()
                .Where(v => v.ReportTemplateId == template.TemplateId && v.IsPublished && v.PublishedAtUtc != null)
                .Select(v => v.PublishedAtUtc!.Value)
                .ToListAsync(ct);
            if (pubDates.Count > 0)
                templateFirstPublished = ReportingCalendarPolicy.RiyadhDate(EnsureUtc(pubDates.Min()));
        }

        DateOnly? auditedAssigned = null;
        if (me.JobRoleId is Guid myJobRole)
        {
            var auditRaw = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.Action == "user.jobrole.changed" && a.EntityId == uid && a.DataJson != null)
                .Select(a => new { a.DataJson, a.CreatedAtUtc })
                .ToListAsync(ct);
            DateTime? latest = null;
            foreach (var a in auditRaw)
            {
                if (ParseNewJobRoleId(a.DataJson) == myJobRole && (latest is null || a.CreatedAtUtc > latest))
                    latest = a.CreatedAtUtc;
            }
            if (latest is DateTime la)
                auditedAssigned = ReportingCalendarPolicy.RiyadhDate(EnsureUtc(la));
        }

        var floor = ApplicabilityFloorPolicy.Resolve(new ApplicabilityFloorPolicy.FloorInput(
            UserCreatedAt: ReportingCalendarPolicy.RiyadhDate(EnsureUtc(me.CreatedAtUtc)),
            TemplateFirstPublishedAt: templateFirstPublished,
            AuditedJobRoleAssignedAt: auditedAssigned,
            OrganizationalLaunchFloor: ApplicabilityFloorPolicy.WeeklyReportingLaunchFloor));

        // ===== الجلب الدفعيّ الوحيد (بلا N+1): كلّ تسليمات المستخدم الأسبوعية لهذه المفاتيح، بكلّ الحالات =====
        var subsQuery = _db.ReportSubmissions.AsNoTracking()
            .Where(s => s.SubmitterId == uid && s.PeriodType == PeriodType.Weekly && keys.Contains(s.PeriodKey));
        if (lineageVersionIds is not null && lineageVersionIds.Count > 0)
            subsQuery = subsQuery.Where(s => lineageVersionIds.Contains(s.ReportTemplateVersionId));

        var subs = await subsQuery
            .Select(s => new
            {
                s.Id, s.PeriodKey, s.Status, s.SubmittedAtUtc, s.ClosedAtUtc, s.ReportTemplateVersionId
            })
            .ToListAsync(ct);

        // اختيار التسليم الممثِّل لكلّ دورة = الأكثر تقدّمًا في سير العمل (الحذف الناعم مُستبعَد أصلًا بالفلتر العام).
        var byKey = subs
            .GroupBy(s => s.PeriodKey)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => StatusProgressRank(x.Status)).First());

        var now = _clock.UtcNow;
        var results = new List<Row>(keys.Count);

        foreach (var key in keys)
        {
            var (start, end) = ReportingCalendarPolicy.CycleRange(key);
            var dueDate = ReportingCalendarPolicy.RoleDueDate(key, role);

            // حدود الدورة بمنطقة الرياض: البداية 00:00، الموعد نهاية اليوم 23:59:59 (لمحاكاة مقارنة اليوم today>due).
            var cycleStartsAt = new DateTimeOffset(start.Year, start.Month, start.Day, 0, 0, 0, Riyadh);
            var dueAt = new DateTimeOffset(dueDate.Year, dueDate.Month, dueDate.Day, 23, 59, 59, Riyadh);

            // بوّابة الانطباق: دورة تبدأ قبل الأرضيّة ليست مطلوبة ⇒ NotRequired (لا تظهر كمتأخّرة زائفة).
            var cycleApplicable = ApplicabilityFloorPolicy.IsCycleApplicable(start, floor.Floor);

            byKey.TryGetValue(key, out var rep);
            var hasSubmission = rep is not null;
            SubmissionStatus? status = rep?.Status;
            DateTimeOffset? submittedAt = rep?.SubmittedAtUtc is DateTime sa
                ? new DateTimeOffset(DateTime.SpecifyKind(sa, DateTimeKind.Utc))
                : null;
            DateTimeOffset? closedAt = rep?.ClosedAtUtc is DateTime ca
                ? new DateTimeOffset(DateTime.SpecifyKind(ca, DateTimeKind.Utc))
                : null;

            var input = new CycleStatusInput(
                IsAssigned: isAssigned,
                IsRequired: cycleApplicable,       // بوّابة الانطباق: الدورة قبل الأرضيّة ⇒ غير مطلوبة.
                CycleStartsAt: cycleStartsAt,
                DueAt: dueAt,
                CurrentTime: now,
                HasSubmission: hasSubmission,
                SubmissionStatus: status,
                SubmittedAt: submittedAt,
                ApprovedAt: null,                 // لا عمود ApprovedAtUtc — إعلاميّ فقط.
                ClosedAt: closedAt,
                IsDeleted: false);                // الفلتر العام يستبعد المحذوف ناعمًا أصلًا.

            var derived = UnifiedCycleStatusPolicy.Derive(input);

            results.Add(new Row(
                key, start, end, dueDate,
                rep?.Id, status, rep?.SubmittedAtUtc, closedAt?.UtcDateTime, hasSubmission,
                template, derived));
        }

        // isCurrentPriority: بوّابة الانطباق تُقصي الدورات قبل الأرضيّة (NotRequired ⇒ رتبة 99).
        // نُفضِّل دورة اليوم الحاليّة إن تطلّبت إجراءً كي لا يُخفيها بند تاريخيّ متأخّر، ثمّ الأعلى إلحاحًا.
        var currentKey = ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhDate(now.UtcDateTime));
        var priorityKey = results
            .Where(r => ActionRequiredRank(r.Derived.Status) is int rank && rank <= 6)
            .OrderByDescending(r => r.Key == currentKey)
            .ThenBy(r => ActionRequiredRank(r.Derived.Status))
            .ThenBy(r => r.DueDate)
            .Select(r => (string?)r.Key)
            .FirstOrDefault();

        var dtos = results
            .Select(r => Map(r, priorityKey is not null && r.Key == priorityKey))
            .ToList();

        return Result<IReadOnlyList<UnifiedReportCycleStatusDto>>.Success(dtos);
    }

    // ===== تعيين الصفّ الداخليّ إلى DTO مع الوصف/الشدّة/الإجراءات =====
    private static UnifiedReportCycleStatusDto Map(Row r, bool isCurrentPriority)
    {
        var d = r.Derived;
        return new UnifiedReportCycleStatusDto(
            TemplateId: r.Template?.TemplateId,
            TemplateVersionId: r.Template?.VersionId,
            TemplateName: r.Template?.Title ?? "—",
            PeriodType: PeriodType.Weekly,
            PeriodKey: r.Key,
            CycleLabel: ReportingCalendarPolicy.CycleLabel(r.Key),
            CycleStartDate: r.Start,
            CycleEndDate: r.End,
            DueAt: r.DueDate,
            AssignmentId: null,
            IsAssigned: r.Template is not null,
            SubmissionId: r.SubmissionId,
            SubmissionStatus: r.Status,
            SubmittedAt: r.SubmittedAt,
            ApprovedAt: null,
            ClosedAt: r.ClosedAt,
            HasSubmission: r.HasSubmission,
            IsLate: d.IsLate,
            DelayDays: d.DelayDays,
            UnifiedStatus: d.Status,
            StatusLabel: StatusLabel(d.Status),
            StatusDescription: StatusDescription(d.Status),
            Severity: SeverityString(d.Severity),
            AvailableActions: AvailableActions(d.Status),
            ActionUrl: $"/submissions?period={r.Key}",
            IsCurrentPriority: isCurrentPriority);
    }

    // ===== القالب الأساسيّ الأسبوعيّ للمسمّى (أو المحدَّد) + آخر نسخة منشورة =====
    private async Task<TemplateRef?> ResolveTemplateAsync(Guid? jobRoleId, Guid? templateId, CancellationToken ct)
    {
        var q = _db.ReportTemplates.AsNoTracking()
            .Where(t => t.IsActive
                        && t.Classification == TemplateClassification.Primary
                        && t.DefaultPeriodType != PeriodType.Monthly
                        && _db.ReportTemplateVersions.Any(v => v.ReportTemplateId == t.Id && v.IsPublished));

        if (templateId is Guid tid)
            q = q.Where(t => t.Id == tid);
        else if (jobRoleId is Guid jr)
            q = q.Where(t => t.JobRoleId == jr);
        else
            return null;

        var tpl = await q
            .OrderBy(t => t.Title)
            .Select(t => new { t.Id, t.Title })
            .FirstOrDefaultAsync(ct);
        if (tpl is null) return null;

        var versionId = await _db.ReportTemplateVersions.AsNoTracking()
            .Where(v => v.ReportTemplateId == tpl.Id && v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync(ct);

        return new TemplateRef(tpl.Id, versionId, tpl.Title);
    }

    // رتبة تقدّم التسليم في سير العمل (لاختيار الممثِّل الأكثر تقدّمًا عند تعدّد الصفوف لنفس الدورة).
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

    // رتبة أولوية الإجراء (§4-ب): 1 = الأعلى إلحاحًا. الرتب 1-6 تتطلّب إجراءً من الموظّف.
    private static int ActionRequiredRank(UnifiedCycleStatus s) => s switch
    {
        UnifiedCycleStatus.OverdueNotSubmitted => 1,
        UnifiedCycleStatus.OverdueReturned => 2,
        UnifiedCycleStatus.OverdueDraft => 3,
        UnifiedCycleStatus.ReturnedForChanges => 4,
        UnifiedCycleStatus.DueNow => 5,
        UnifiedCycleStatus.Draft => 6,
        UnifiedCycleStatus.PendingApproval => 7,
        UnifiedCycleStatus.SubmittedLate => 8,
        UnifiedCycleStatus.SubmittedOnTime => 9,
        UnifiedCycleStatus.Closed => 10,
        _ => 99
    };

    private static string SeverityString(CycleStatusSeverity s) => s switch
    {
        CycleStatusSeverity.Info => "info",
        CycleStatusSeverity.Success => "success",
        CycleStatusSeverity.Warn => "warn",
        CycleStatusSeverity.Alert => "alert",
        _ => "none"
    };

    private static string StatusLabel(UnifiedCycleStatus s) => s switch
    {
        UnifiedCycleStatus.NotAssigned => "غير مطالَب",
        UnifiedCycleStatus.NotRequired => "غير مطلوب",
        UnifiedCycleStatus.NotDue => "لم يحن الموعد",
        UnifiedCycleStatus.DueNow => "مستحقّ الآن",
        UnifiedCycleStatus.Draft => "مسودّة",
        UnifiedCycleStatus.OverdueDraft => "مسودّة متأخّرة",
        UnifiedCycleStatus.SubmittedOnTime => "مُسلَّم في الموعد",
        UnifiedCycleStatus.SubmittedLate => "مُسلَّم متأخّرًا",
        UnifiedCycleStatus.PendingApproval => "بانتظار الاعتماد",
        UnifiedCycleStatus.ReturnedForChanges => "معاد للتعديل",
        UnifiedCycleStatus.OverdueReturned => "معاد ومتأخّر",
        UnifiedCycleStatus.Approved => "معتمَد",
        UnifiedCycleStatus.Closed => "مُغلَق",
        UnifiedCycleStatus.OverdueNotSubmitted => "متأخّر — لم يُسلَّم",
        _ => "—"
    };

    private static string StatusDescription(UnifiedCycleStatus s) => s switch
    {
        UnifiedCycleStatus.NotAssigned => "لا يُتوقَّع منك تقرير أسبوعيّ ضمن هذا المسار.",
        UnifiedCycleStatus.NotRequired => "الدورة غير مطلوبة (استثناء/إجازة معتمَدة).",
        UnifiedCycleStatus.NotDue => "لم تبدأ نافذة استحقاق هذه الدورة بعد.",
        UnifiedCycleStatus.DueNow => "نافذة التسليم مفتوحة، وما زلت ضمن المهلة.",
        UnifiedCycleStatus.Draft => "لديك مسودّة لم تُرسَل بعد ضمن المهلة.",
        UnifiedCycleStatus.OverdueDraft => "لديك مسودّة لم تُرسَل وتجاوزت الموعد النهائيّ.",
        UnifiedCycleStatus.SubmittedOnTime => "أُرسِل التقرير في الموعد.",
        UnifiedCycleStatus.SubmittedLate => "أُرسِل التقرير بعد الموعد النهائيّ.",
        UnifiedCycleStatus.PendingApproval => "التقرير قيد المراجعة/الاعتماد — لا إجراء مطلوب منك.",
        UnifiedCycleStatus.ReturnedForChanges => "أُعيد التقرير لإجراء تعديلات ضمن المهلة.",
        UnifiedCycleStatus.OverdueReturned => "أُعيد التقرير لإجراء تعديلات وتجاوز الموعد.",
        UnifiedCycleStatus.Approved => "اكتمل اعتماد التقرير.",
        UnifiedCycleStatus.Closed => "اكتملت دورة التقرير.",
        UnifiedCycleStatus.OverdueNotSubmitted => "تجاوزت الموعد النهائيّ ولم تُسلِّم التقرير.",
        _ => "—"
    };

    // أكواد الإجراءات المتاحة (الواجهة تعرضها؛ لا تحسب حالة). قراءة فقط لهذه المرحلة.
    private static IReadOnlyList<string> AvailableActions(UnifiedCycleStatus s) => s switch
    {
        UnifiedCycleStatus.DueNow => new[] { "create" },
        UnifiedCycleStatus.OverdueNotSubmitted => new[] { "create" },
        UnifiedCycleStatus.Draft => new[] { "edit", "submit" },
        UnifiedCycleStatus.OverdueDraft => new[] { "edit", "submit" },
        UnifiedCycleStatus.ReturnedForChanges => new[] { "edit", "resubmit" },
        UnifiedCycleStatus.OverdueReturned => new[] { "edit", "resubmit" },
        UnifiedCycleStatus.SubmittedOnTime => new[] { "view" },
        UnifiedCycleStatus.SubmittedLate => new[] { "view" },
        UnifiedCycleStatus.PendingApproval => new[] { "view" },
        UnifiedCycleStatus.Closed => new[] { "view" },
        _ => Array.Empty<string>()
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

    // ===== أنواع داخلية =====
    private sealed record TemplateRef(Guid TemplateId, Guid? VersionId, string Title);

    private sealed record Row(
        string Key, DateOnly Start, DateOnly End, DateOnly DueDate,
        Guid? SubmissionId, SubmissionStatus? Status, DateTime? SubmittedAt, DateTime? ClosedAt, bool HasSubmission,
        TemplateRef? Template, CycleStatusResult Derived);
}
