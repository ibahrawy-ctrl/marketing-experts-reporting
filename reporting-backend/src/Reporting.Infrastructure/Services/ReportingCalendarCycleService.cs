using Microsoft.EntityFrameworkCore;
using Reporting.Application.Calendar;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// ROLE-AWARE-REPORTING-CALENDAR — Phase 2.3/2.6. تحسب دورات المستخدم الأسبوعية وأيامه اليومية عبر
/// <see cref="ReportingCalendarPolicy"/> (مصدر الحقيقة الوحيد). النافذة الأسبوعية السبت→الجمعة موحّدة لكل المستويات،
/// وتاريخ الاستحقاق يختلف بحسب **الدور الأساسيّ الخادميّ** (RoleAccess.PrimaryRole) فقط — لا يُرسَل من الواجهة.
/// الدورات الأسبوعية حساب خالص بلا قاعدة بيانات؛ الأيام اليومية تقرأ حالة التسليمات من القاعدة (قراءة فقط)
/// لاشتقاق حالة كل يوم. لا تعديل بيانات، لا هجرة، لا سير عمل.
/// </summary>
public class ReportingCalendarCycleService : IReportingCalendarCycleService
{
    private readonly ICurrentUser _currentUser;
    private readonly AppDbContext _db;
    // REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — مصدر الحالة الموحّد (إثراء المسار الأسبوعيّ إضافيًّا).
    private readonly IUnifiedReportStatusService _unified;

    // حدود آمنة لعدد الدورات المُعادة (منع طلبات ضخمة).
    private const int DefaultPast = 8;
    private const int DefaultFuture = 1;
    private const int MaxPast = 25;
    private const int MaxFuture = 4;
    // دورة ماضية أقدم من هذا الحدّ تتطلّب سببًا للتسليم المتأخّر (عتبة القِدَم).
    private const int HistoricalReasonThreshold = -2;

    // حدود آمنة لنافذة الأيام اليومية.
    private const int DefaultPreviousDays = 10;
    private const int DefaultNextDays = 2;
    private const int MaxPreviousDays = 40;
    private const int MaxNextDays = 7;

    // الحالات التي تُعدّ «مُرسَلة» لأغراض عرض التقويم اليوميّ (تسليم رسميّ لا مسودّة/معادة).
    private static readonly SubmissionStatus[] SubmittedStatuses =
    {
        SubmissionStatus.Submitted,
        SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel,
        SubmissionStatus.Escalated,
        SubmissionStatus.Closed,
        SubmissionStatus.Visible
    };

    public ReportingCalendarCycleService(ICurrentUser currentUser, AppDbContext db, IUnifiedReportStatusService unified)
    {
        _currentUser = currentUser;
        _db = db;
        _unified = unified;
    }

    public async Task<Result<MyCyclesDto>> GetMyCyclesAsync(
        ReportingCalendarContext context,
        Guid? templateId,
        int? past,
        int? future,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result<MyCyclesDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var role = RoleAccess.PrimaryRole(_currentUser.Roles);
        var roleLabel = Roles.DisplayAr(role);

        var pastCount = Math.Clamp(past ?? DefaultPast, 0, MaxPast);
        var futureCount = Math.Clamp(future ?? DefaultFuture, 0, MaxFuture);

        var today = ReportingCalendarPolicy.RiyadhToday();
        var currentStart = ReportingCalendarPolicy.CycleStart(today);
        var currentKey = ReportingCalendarPolicy.CycleKeyFor(today);

        var cycles = new List<ReportingCycleDto>(pastCount + futureCount + 1);
        for (var offset = -pastCount; offset <= futureCount; offset++)
        {
            var anchorDate = currentStart.AddDays(offset * 7);
            var key = ReportingCalendarPolicy.CycleKeyFor(anchorDate);
            cycles.Add(BuildCycle(key, role, roleLabel, offset, today, context));
        }

        // REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — إثراء إضافيّ بالحالة الموحّدة:
        // استدعاء دفعيّ واحد للمحرّك (بلا N+1) ثم دمج الحالة في كل صفّ دورة. فشل الإثراء لا يكسر التقويم
        // (الحقول القديمة تبقى؛ Unified يبقى null فتتراجع الواجهة للسلوك القديم — توافق خلفيّ كامل).
        var keys = cycles.Select(c => c.CycleKey).ToList();
        var unifiedResult = await _unified.GetMyWeeklyCycleStatusesAsync(keys, templateId, ct);
        if (unifiedResult.Succeeded && unifiedResult.Value is { Count: > 0 } unifiedList)
        {
            var byKey = unifiedList.ToDictionary(u => u.PeriodKey);
            for (var i = 0; i < cycles.Count; i++)
                if (byKey.TryGetValue(cycles[i].CycleKey, out var u))
                    cycles[i] = cycles[i] with { Unified = u };
        }

        var dto = new MyCyclesDto(context, templateId, role, roleLabel, currentKey, today, cycles);
        return Result<MyCyclesDto>.Success(dto);
    }

    public async Task<Result<ReportingCycleDto>> ResolveAsync(
        string cycleKey,
        ReportingCalendarContext context,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result<ReportingCycleDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        if (!ReportingCalendarPolicy.IsValidCycleKey(cycleKey))
            return Result<ReportingCycleDto>.Failure("مفتاح الدورة غير صالح.", "calendar.cycle_key_invalid");

        var role = RoleAccess.PrimaryRole(_currentUser.Roles);
        var roleLabel = Roles.DisplayAr(role);
        var today = ReportingCalendarPolicy.RiyadhToday();

        // الإزاحة بعدد الدورات = فرق أسابيع السبت بين الدورة المطلوبة والحالية.
        var currentStart = ReportingCalendarPolicy.CycleStart(today);
        var targetStart = ReportingCalendarPolicy.CycleRange(cycleKey).Start;
        var offset = (targetStart.DayNumber - currentStart.DayNumber) / 7;

        var dto = BuildCycle(cycleKey.Trim(), role, roleLabel, offset, today, context);

        // إثراء إضافيّ بالحالة الموحّدة (لدور المستخدم الحاليّ)؛ الفشل لا يكسر التشخيص.
        var unifiedResult = await _unified.GetMyWeeklyCycleStatusAsync(cycleKey.Trim(), null, ct);
        if (unifiedResult.Succeeded && unifiedResult.Value is { } u)
            dto = dto with { Unified = u };

        return Result<ReportingCycleDto>.Success(dto);
    }

    // ===== الوضع اليوميّ (Daily) — نافذة أيام مُدرِكة لحالة تسليمات المستخدم =====
    public async Task<Result<MyDaysDto>> GetMyDaysAsync(
        string? anchorDate,
        int? previousCount,
        int? nextCount,
        Guid? templateId,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid userId)
            return Result<MyDaysDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var role = RoleAccess.PrimaryRole(_currentUser.Roles);
        var roleLabel = Roles.DisplayAr(role);
        var today = ReportingCalendarPolicy.RiyadhToday();

        // SALES-DAILY-SATURDAY-APPLICABILITY-HOTFIX-R1: هل هذا المستخدم من المبيعات (SALES_B2B/B2C)؟
        // يُشتقّ تفعيل السبت من رمز مسمّاه الوظيفيّ خادميًّا (لا من الواجهة). لغير المبيعات = false
        // ⇒ السبت يبقى عطلة أسبوعية في التقويم كما كان (بلا أثر). لمبيعات، السبت ابتداءً من الأرضية 2026-07-25
        // يصبح يوم عمل قابلًا للاختيار/التسليم في التقويم (يطابق المتوقّع/الالتزام).
        var myJobRoleCode = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.JobRoleId != null)
            .Join(_db.JobRoles, u => u.JobRoleId, j => j.Id, (u, j) => j.Code)
            .FirstOrDefaultAsync(ct);
        var saturdayEnabled = ReportingCalendarPolicy.SaturdayEnabledForJobRole(myJobRoleCode);

        // نقطة الارتكاز: تاريخ مُرسَل اختياريّ (للتنقّل)، وإلّا اليوم. لا نثق بمفتاح غير صالح بنيويًّا.
        DateOnly anchor;
        if (string.IsNullOrWhiteSpace(anchorDate))
            anchor = today;
        else if (ReportingCalendarPolicy.IsValidDayKey(anchorDate))
            anchor = ReportingCalendarPolicy.ParseDayKey(anchorDate);
        else
            return Result<MyDaysDto>.Failure("مفتاح اليوم غير صالح.", "calendar.day_key_invalid");

        var prev = Math.Clamp(previousCount ?? DefaultPreviousDays, 0, MaxPreviousDays);
        var next = Math.Clamp(nextCount ?? DefaultNextDays, 0, MaxNextDays);

        // نافذة الأيام حول نقطة الارتكاز (من الأقدم إلى الأحدث).
        var dates = new List<DateOnly>(prev + next + 1);
        for (var d = -prev; d <= next; d++)
            dates.Add(anchor.AddDays(d));

        var windowStart = dates[0];
        var windowEnd = dates[^1];

        // DAILY-…-R1 §3: قراءة حالة كل يوم على اليوم المنطقيّ (CanonicalDay) لا على النصّ الخام؛ نُحمِّل
        // تسليمات المستخدم اليوميّة بلا فلترة نصّية قياسيّة (كي لا تسقط المفاتيح القديمة مثل 6-7-2026)
        // ثم نُطبِّع كلّ مفتاح ونحصره في نافذة الأيام المعروضة قبل التجميع.
        var rawSubs = await _db.ReportSubmissions
            .AsNoTracking()
            .Where(s => s.SubmitterId == userId
                        && s.PeriodType == PeriodType.Daily
                        && s.PeriodKey != null)
            .Select(s => new { s.PeriodKey, s.Status })
            .ToListAsync(ct);

        var subs = rawSubs
            .Select(s => ReportingCalendarPolicy.TryCanonicalDay(s.PeriodKey, out var cd)
                ? new { DayKey = ReportingCalendarPolicy.DayKey(cd), Day = cd, s.Status, Ok = true }
                : new { DayKey = string.Empty, Day = default(DateOnly), s.Status, Ok = false })
            .Where(x => x.Ok && x.Day >= windowStart && x.Day <= windowEnd)
            .ToList();

        // لكل يوم: هل يوجد تسليم رسميّ؟ هل مسودّة؟ هل معاد للتعديل؟
        var byKey = subs
            .GroupBy(s => s.DayKey)
            .ToDictionary(g => g.Key, g => new DayStatusFlags(
                Submitted: g.Any(x => SubmittedStatuses.Contains(x.Status)),
                Draft: g.Any(x => x.Status == SubmissionStatus.Draft),
                Returned: g.Any(x => x.Status == SubmissionStatus.Returned)));

        var days = new List<ReportingDayDto>(dates.Count);
        foreach (var date in dates)
        {
            var key = ReportingCalendarPolicy.DayKey(date);
            byKey.TryGetValue(key, out var flags);
            days.Add(BuildDay(date, today, flags, saturdayEnabled));
        }

        var currentDayKey = ReportingCalendarPolicy.DayKey(today);
        var dto = new MyDaysDto(templateId, role, roleLabel, currentDayKey, today, days);
        return Result<MyDaysDto>.Success(dto);
    }

    private readonly record struct DayStatusFlags(bool Submitted, bool Draft, bool Returned);

    // ===== بناء صفّ يوم واحد (حالة واحدة حصرًا لكل يوم) =====
    // SALES-DAILY-SATURDAY-APPLICABILITY-HOTFIX-R1: <paramref name="saturdayEnabled"/> يُمرَّر من دور
    // المستخدم (مبيعات ⇒ true). حين true يصبح السبت ابتداءً من الأرضية 2026-07-25 يوم عمل (ليس عطلة)،
    // وحين false يبقى السبت عطلة أسبوعية كما كان (غير المبيعات دون تغيير). الجمعة عطلة دائمًا للجميع.
    private static ReportingDayDto BuildDay(DateOnly date, DateOnly today, DayStatusFlags flags, bool saturdayEnabled)
    {
        var isToday = date == today;
        var isPast = date < today;
        var isFuture = date > today;
        var isHoliday = ReportingCalendarPolicy.IsDailyHoliday(date, saturdayEnabled);

        var isSelectable = !isHoliday && !isFuture;
        var isOpenForDraft = isSelectable;
        var isDueToday = isToday && !isHoliday;
        var isOverdue = isPast && !isHoliday && !flags.Submitted;

        // ترتيب حسم الحالة (حالة واحدة حصرًا): عطلة ← مستقبل مقفل ← مُرسَل ← معاد ← ماضٍ متأخّر ← اليوم.
        string status;
        string statusLabel;
        string? lockReason = null;
        if (isHoliday)
        {
            status = "Holiday";
            statusLabel = "عطلة أسبوعية";
            // للمبيعات تكون العطلة اليوميّة = الجمعة فقط (السبت يوم عمل من الأرضية)؛ لغيرهم = الجمعة والسبت.
            lockReason = saturdayEnabled
                ? "لا تقارير يومية في العطلة الأسبوعية (الجمعة)."
                : "لا تقارير يومية في العطلة الأسبوعية (الجمعة/السبت).";
        }
        else if (isFuture)
        {
            status = "FutureLocked";
            statusLabel = "يوم لم يبدأ بعد";
            lockReason = "لا يمكن إنشاء تقرير ليوم لم يبدأ بعد.";
        }
        else if (flags.Submitted)
        {
            status = "Submitted";
            statusLabel = "مُرسَل";
        }
        else if (flags.Returned)
        {
            status = "Returned";
            statusLabel = "مُعاد للتعديل";
        }
        else if (isOverdue)
        {
            status = "Overdue";
            statusLabel = flags.Draft ? "مسودّة غير مُرسَلة — متأخّر" : "متأخّر — لم يُرسَل";
        }
        else // اليوم الحاليّ (يوم عمل)
        {
            status = flags.Draft ? "Draft" : "Available";
            statusLabel = flags.Draft ? "مسودّة غير مُرسَلة" : "متاح للتسليم";
        }

        return new ReportingDayDto(
            DayKey: ReportingCalendarPolicy.DayKey(date),
            Date: date,
            DayNameAr: ReportingCalendarPolicy.ArDayName(date),
            FullDateLabel: ReportingCalendarPolicy.ArFullDateLabel(date),
            IsToday: isToday,
            IsPast: isPast,
            IsFuture: isFuture,
            IsHoliday: isHoliday,
            IsSelectable: isSelectable,
            IsOpenForDraft: isOpenForDraft,
            IsDueToday: isDueToday,
            IsOverdue: isOverdue,
            IsSubmitted: flags.Submitted,
            HasDraft: flags.Draft,
            Status: status,
            StatusLabel: statusLabel,
            LockReason: lockReason,
            PreviousDayKey: ReportingCalendarPolicy.PreviousDayKey(date),
            NextDayKey: ReportingCalendarPolicy.NextDayKey(date));
    }

    // ===== بناء صفّ دورة واحدة (خالص، بلا حالة) =====
    private static ReportingCycleDto BuildCycle(
        string key, string role, string roleLabel, int offset, DateOnly today, ReportingCalendarContext context)
    {
        var (start, end) = ReportingCalendarPolicy.CycleRange(key);
        var tuesdayRef = ReportingCalendarPolicy.TuesdayReference(start);
        var (year, week) = ReportingCalendarPolicy.ParseCycleKey(key);
        var (coverStart, coverEnd) = ReportingCalendarPolicy.DataCoverageWindow(key);

        var roleOffset = ReportingCalendarPolicy.RoleDueOffset(role);
        var roleDue = ReportingCalendarPolicy.RoleDueDate(key, role);
        var roleDueLabel = $"{ReportingCalendarPolicy.ArDayName(roleDue)} {ReportingCalendarPolicy.ArDayMonth(roleDue)}";

        var isCurrent = offset == 0;
        var isPast = offset < 0;
        var isFuture = offset > 0;

        // الدورة المستقبلية مقفلة (لم تبدأ بعد)؛ الحالية والماضية مفتوحتان (يُسمح بالتسليم المتأخّر).
        var isOpen = !isFuture;
        var isLocked = isFuture;
        var lockReason = isFuture ? "الدورة لم تبدأ بعد." : null;

        var status = isCurrent ? "current" : (isPast ? "past" : "locked");
        var isOverdue = !isFuture && today > roleDue;
        var requiresReason = isPast && offset <= HistoricalReasonThreshold;

        return new ReportingCycleDto(
            CycleKey: key,
            CycleNumber: week,
            CycleYear: year,
            CycleStart: start,
            CycleEnd: end,
            TuesdayReference: tuesdayRef,
            CycleLabel: ReportingCalendarPolicy.CycleLabel(key),
            ShortLabel: ReportingCalendarPolicy.ShortCycleLabel(key),
            DataCoverageStart: coverStart,
            DataCoverageEnd: coverEnd,
            Role: role,
            RoleLabel: roleLabel,
            RoleDueOffset: roleOffset,
            RoleDueDate: roleDue,
            RoleDueDateLabel: roleDueLabel,
            Offset: offset,
            IsCurrent: isCurrent,
            IsPast: isPast,
            IsFuture: isFuture,
            Status: status,
            IsOpen: isOpen,
            IsLocked: isLocked,
            LockReason: lockReason,
            IsOverdue: isOverdue,
            RequiresReason: requiresReason,
            Today: today,
            Context: context);
    }
}
