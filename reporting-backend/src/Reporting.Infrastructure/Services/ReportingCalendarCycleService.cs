using Reporting.Application.Calendar;
using Reporting.Application.Common;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// ROLE-AWARE-REPORTING-CALENDAR — Phase 2.3. خدمة خالصة (Pure) تحسب دورات المستخدم الحاليّ عبر
/// <see cref="ReportingCalendarPolicy"/> (مصدر الحقيقة الوحيد). النافذة السبت→الجمعة موحّدة لكل المستويات،
/// وتاريخ الاستحقاق يختلف بحسب **الدور الأساسيّ الخادميّ** (RoleAccess.PrimaryRole) فقط — لا يُرسَل من الواجهة.
/// قراءة/حساب فقط: لا وصول لقاعدة البيانات، لا تعديل، لا هجرة، لا سير عمل.
/// </summary>
public class ReportingCalendarCycleService : IReportingCalendarCycleService
{
    private readonly ICurrentUser _currentUser;

    // حدود آمنة لعدد الدورات المُعادة (منع طلبات ضخمة).
    private const int DefaultPast = 8;
    private const int DefaultFuture = 1;
    private const int MaxPast = 25;
    private const int MaxFuture = 4;
    // دورة ماضية أقدم من هذا الحدّ تتطلّب سببًا للتسليم المتأخّر (عتبة القِدَم).
    private const int HistoricalReasonThreshold = -2;

    public ReportingCalendarCycleService(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public Task<Result<MyCyclesDto>> GetMyCyclesAsync(
        ReportingCalendarContext context,
        Guid? templateId,
        int? past,
        int? future,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Task.FromResult(Result<MyCyclesDto>.Failure("غير مصرّح.", "auth.unauthenticated"));

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

        var dto = new MyCyclesDto(context, templateId, role, roleLabel, currentKey, today, cycles);
        return Task.FromResult(Result<MyCyclesDto>.Success(dto));
    }

    public Task<Result<ReportingCycleDto>> ResolveAsync(
        string cycleKey,
        ReportingCalendarContext context,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Task.FromResult(Result<ReportingCycleDto>.Failure("غير مصرّح.", "auth.unauthenticated"));

        if (!ReportingCalendarPolicy.IsValidCycleKey(cycleKey))
            return Task.FromResult(Result<ReportingCycleDto>.Failure("مفتاح الدورة غير صالح.", "calendar.cycle_key_invalid"));

        var role = RoleAccess.PrimaryRole(_currentUser.Roles);
        var roleLabel = Roles.DisplayAr(role);
        var today = ReportingCalendarPolicy.RiyadhToday();

        // الإزاحة بعدد الدورات = فرق أسابيع السبت بين الدورة المطلوبة والحالية.
        var currentStart = ReportingCalendarPolicy.CycleStart(today);
        var targetStart = ReportingCalendarPolicy.CycleRange(cycleKey).Start;
        var offset = (targetStart.DayNumber - currentStart.DayNumber) / 7;

        var dto = BuildCycle(cycleKey.Trim(), role, roleLabel, offset, today, context);
        return Task.FromResult(Result<ReportingCycleDto>.Success(dto));
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
