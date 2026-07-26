using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Application.Reports;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// EMAIL-NOTIFICATIONS-FULL-INTERNAL-ACTIVATION-R1 — المجدول الحقيقيّ لتذكيرات التقارير (R2).
///
/// يستدعي <see cref="IReportReminderService.GenerateAsync"/> نفسها المستخدَمة في المسار اليدويّ،
/// بلا أيّ تغيير في منطق التوليد أو حلّ المستلمين أو بناء الرسائل. الدورتان المستهدَفتان تُشتقّان
/// من تقويم الرياض (الدورة السابقة ثمّ الحالية) — لا مفتاح دورة ثابت. سبب الدورتين موثّق في RunOnceAsync.
///
/// التوقيت: توقيت الرياض (UTC+3) عبر <see cref="ReportCalendarPolicy.RiyadhOffset"/> — بلا اعتماد على
/// قاعدة بيانات المناطق الزمنية للنظام، اتّساقًا مع بقية النظام.
///
/// عدم التكرار: طبقتان مستقلّتان —
/// (1) قفل الفتحة الزمنية في الذاكرة: لا يُشغَّل نفس (تاريخ الرياض، الساعة) مرّتين داخل نفس العملية.
/// (2) الضمان الحقيقيّ في القاعدة: منع التكرار عبر CorrelationKey داخل EmailNotificationService.
/// بعد إعادة التشغيل تُفقَد الطبقة (1) فقد يُعاد الاستدعاء، لكنّ الطبقة (2) تُرجِع Duplicate
/// فلا يُنشَأ أيّ صفّ جديد ولا يُرسَل أيّ بريد.
///
/// الإرسال الفعليّ ليس من مسؤولية هذه الخدمة إطلاقًا — يبقى محكومًا حصرًا بـ EmailNotifications__Mode.
/// معطّل افتراضيًّا.
/// </summary>
public class ReportReminderSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReportReminderSchedulerOptions _options;
    private readonly ILogger<ReportReminderSchedulerService> _logger;

    /// <summary>آخر فتحة زمنية شُغِّلت داخل هذه العملية (تاريخ الرياض + الساعة). تُفقَد عند إعادة التشغيل.</summary>
    private (DateOnly Date, int Hour)? _lastRunSlot;

    public ReportReminderSchedulerService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReportReminderSchedulerOptions> options,
        ILogger<ReportReminderSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromMinutes(Math.Max(5, _options.PollMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                // لا نُسقِط الخدمة الخلفية بسبب فشل دورة واحدة.
                _logger.LogError(ex, "ReportReminderScheduler cycle failed");
            }

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// نبضة واحدة: تُقرِّر هل حلّت فتحة تشغيل بتوقيت الرياض، وتُشغِّل المولّد مرّة واحدة لتلك الفتحة.
    /// <paramref name="utcNow"/> صريح ليكون الاختبار حتميًّا بلا اعتماد على ساعة النظام.
    /// البوابة <c>Enabled</c> تُفحَص هنا لتكون مصدرًا واحدًا للحقيقة (الحلقة الخلفية والاختبار سواء).
    /// </summary>
    /// <returns>حصيلة التشغيل إن جرى، وإلّا null.</returns>
    public async Task<ReportReminderRunResult?> TickAsync(DateTime utcNow, CancellationToken ct = default)
    {
        if (!_options.Enabled) return null;

        var hours = _options.ParsedRunAtRiyadhHours;
        if (hours.Count == 0)
        {
            _logger.LogWarning("ReportReminderScheduler enabled but RunAtRiyadhHours is empty; nothing scheduled");
            return null;
        }

        var riyadhNow = (utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc))
            .Add(ReportCalendarPolicy.RiyadhOffset);
        var slot = (Date: DateOnly.FromDateTime(riyadhNow), Hour: riyadhNow.Hour);

        if (!hours.Contains(slot.Hour)) return null;
        if (_lastRunSlot == slot) return null;

        // نُثبّت الفتحة قبل التنفيذ حتى لا تتسبّب دورة فاشلة في إعادة محاولة لا نهائية داخل نفس الساعة.
        _lastRunSlot = slot;

        return await RunOnceAsync(ct);
    }

    /// <summary>
    /// تشغيل المولّد مرّة واحدة لدورتَي التقويم: **الدورة السابقة ثمّ الدورة الحالية**. لا يمرّ بأيّ فحص توقيت.
    ///
    /// EMAIL-NOTIFICATIONS-ROLE-AWARE-SCHEDULE-FIX-R1 — لماذا دورتان لا دورة واحدة؟
    /// إزاحات الاستحقاق حسب الدور (ReportingCalendarPolicy.RoleDueOffset) تتجاوز نافذة الدورة نفسها
    /// (السبت→الجمعة = 0..6): المدير = بداية الدورة + 8 (الأحد التالي)، والمدير العام/الرئيس التنفيذي/
    /// مدير النظام = + 9 (الاثنين التالي). فيوم استحقاق هذين الدورين يقع **داخل نافذة الدورة التالية**.
    /// تشغيل الدورة الحالية وحدها كان يعني أن هذين الدورين لا يبلغان يوم استحقاقهما أبدًا (لا تذكير ولا تأخّر).
    /// تشغيل الدورة السابقة أيضًا يجعل: الأربعاء=موظّفو الدورة الحالية، الخميس=قادة الفرق،
    /// الأحد=مديرو الدورة السابقة، الاثنين=المدير العام/الرئيس التنفيذي/مدير النظام للدورة السابقة.
    ///
    /// عدم التكرار محفوظ: كلّ مفاتيح الترابط تحمل مفتاح الدورة (وتذكير الأسبوعيّ يحمل أيضًا يوم استحقاق الدور)،
    /// فتكرار تشغيل الدورة السابقة يوميًّا يُرجِع Duplicate ولا يُنشئ صفًّا جديدًا.
    /// </summary>
    public async Task<ReportReminderRunResult> RunOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<IReportReminderService>();
        var clock = scope.ServiceProvider.GetRequiredService<ISystemClock>();

        var today = ReportCalendarPolicy.RiyadhDate(clock.UtcNow.UtcDateTime);
        var currentKey = ReportCalendarPolicy.WeekKeyFor(today);
        var previousKey = ReportCalendarPolicy.WeekKeyFor(ReportCalendarPolicy.WeekStart(today).AddDays(-7));

        var previous = await RunForCycleAsync(generator, previousKey, ct);
        var current = await RunForCycleAsync(generator, currentKey, ct);

        return Merge(current, previous);
    }

    private async Task<ReportReminderRunResult> RunForCycleAsync(IReportReminderService generator, string cycleKey, CancellationToken ct)
    {
        var result = await generator.GenerateAsync(new ReportReminderRunOptions(
            WeekKey: cycleKey,
            Date: null,
            IncludeDue: _options.IncludeDue,
            IncludeOverdue: _options.IncludeOverdue,
            IncludeReviewOverdue: _options.IncludeReviewOverdue), ct);

        _logger.LogInformation(
            "ReportReminderScheduler ran for {WeekKey} mode={Mode} wouldGenerate={WouldGenerate} created={Created} duplicate={Duplicate} noEmail={NoEmail} disabled={Disabled}",
            result.WeekKey, result.Mode, result.WouldGenerate, result.Created,
            result.SkippedDuplicate, result.SkippedNoEmail, result.SkippedDisabled);

        return result;
    }

    /// <summary>يدمج حصيلتَي الدورتين: الهويّة (المفتاح/التسمية/الوضع) من الدورة الحالية، والأعداد مجموعة.</summary>
    private static ReportReminderRunResult Merge(ReportReminderRunResult current, ReportReminderRunResult previous)
    {
        var breakdown = current.Breakdown.Concat(previous.Breakdown)
            .GroupBy(r => r.EventType)
            .Select(g => new ReportReminderBreakdownRow(
                g.Key,
                g.Sum(x => x.WouldGenerate),
                g.Sum(x => x.Created),
                g.Sum(x => x.SkippedDuplicate),
                g.Sum(x => x.SkippedNoEmail),
                g.Sum(x => x.SkippedDisabled)))
            .OrderBy(r => r.EventType, StringComparer.Ordinal)
            .ToList();

        return new ReportReminderRunResult(
            current.WeekKey,
            current.WeekLabel,
            current.Mode,
            current.WouldGenerate + previous.WouldGenerate,
            current.Created + previous.Created,
            current.SkippedDuplicate + previous.SkippedDuplicate,
            current.SkippedNoEmail + previous.SkippedNoEmail,
            current.SkippedDisabled + previous.SkippedDisabled,
            breakdown);
    }
}
