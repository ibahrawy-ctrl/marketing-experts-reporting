using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1 — قراءة الحالة التشغيليّة الحيّة لقناة البريد.
///
/// **قراءة فقط بالكامل**: كلّ استعلام <c>AsNoTracking</c> وتجميعيّ على مستوى القاعدة (COUNT/MAX/MIN)،
/// بلا تحميل جداول كاملة وبلا N+1 وبلا أيّ <c>SaveChanges</c>. لا اتّصال SMTP (تُقرأ الجاهزيّة فقط
/// عبر <see cref="IEmailSender.IsConfigured"/> الذي لا يفتح أيّ اتّصال)، ولا استدعاء لأيّ مهمّة مجدوَلة.
///
/// **بلا أسرار**: <c>Email:Password</c> لا يُقرَأ إلّا لتحويله إلى قيمة منطقيّة واحدة، ولا تُعرَض
/// قيمته ولا طولها ولا بصمتها ولا أيّ جزء منها.
/// </summary>
public class EmailControlStatusService : IEmailControlStatusService
{
    /// <summary>
    /// عتبة اعتبار صفّ Pending/Processing «عالقًا». مشتقّة من تصميم القناة الجديدة:
    /// <c>EmailNotificationService</c> يحسم مصير الصفّ داخل نفس المعاملة (Sent أو Failed أو DryRun)،
    /// فبقاء Pending أكثر من ساعة يعني انقطاعًا فعليًّا لا انتظارًا طبيعيًّا.
    /// </summary>
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromHours(1);

    private const string RiyadhTimeZoneId = "Asia/Riyadh";
    private const string RiyadhTimeZoneLabel = "توقيت الرياض (UTC+3)";

    private readonly AppDbContext _db;
    private readonly IOptions<EmailNotificationOptions> _notificationOptions;
    private readonly IOptions<EmailOptions> _emailOptions;
    private readonly IOptions<ReportReminderSchedulerOptions> _schedulerOptions;
    private readonly IEmailSender _sender;
    private readonly IHostEnvironment _environment;
    private readonly ISystemClock _clock;

    public EmailControlStatusService(
        AppDbContext db,
        IOptions<EmailNotificationOptions> notificationOptions,
        IOptions<EmailOptions> emailOptions,
        IOptions<ReportReminderSchedulerOptions> schedulerOptions,
        IEmailSender sender,
        IHostEnvironment environment,
        ISystemClock clock)
    {
        _db = db;
        _notificationOptions = notificationOptions;
        _emailOptions = emailOptions;
        _schedulerOptions = schedulerOptions;
        _sender = sender;
        _environment = environment;
        _clock = clock;
    }

    public async Task<EmailControlCenterStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var checkedAt = _clock.UtcNow.UtcDateTime;

        // ===== الوضع: مصدر الحقيقة الوحيد =====
        // ربط IOptions كسول: قيمة غير صالحة في EmailNotifications__Mode ترمي عند أوّل قراءة لـ Value،
        // فنحتملها هنا كي تبقى لوحة الحالة قادرة على تشخيص الخلل بدل أن تسقط معه.
        string mode;
        var modeBindingFailed = false;
        try
        {
            mode = _notificationOptions.Value.Mode.ToString();
        }
        catch (Exception)
        {
            mode = "Invalid";
            modeBindingFailed = true;
        }

        var isLive = !modeBindingFailed && mode == nameof(EmailNotificationMode.Enabled);

        var email = _emailOptions.Value;
        var scheduler = _schedulerOptions.Value;

        var host = string.IsNullOrWhiteSpace(email.EffectiveHost) ? null : email.EffectiveHost;
        var sender = string.IsNullOrWhiteSpace(email.EffectiveFromAddress) ? null : email.EffectiveFromAddress;
        var smtpConfigured = _sender.IsConfigured;
        var credentialConfigured = !string.IsNullOrWhiteSpace(email.Password);

        // ===== العدّادات: تجميع واحد على مستوى القاعدة =====
        // Mode و Status عمودان مستقلّان: Mode = الوضع وقت الإنشاء، Status = مصير الصفّ.
        // لذلك لا تُجمَع عدّادات العمودين معًا ولا تُقارَن ببعضها (لا ازدواج في العدّ).
        var stats = await _db.EmailNotifications.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                HistoricalDryRun = g.Count(x => x.Mode == EmailNotificationMode.DryRun),
                Enabled = g.Count(x => x.Mode == EmailNotificationMode.Enabled),
                Sent = g.Count(x => x.Status == EmailNotificationStatus.Sent),
                Pending = g.Count(x => x.Status == EmailNotificationStatus.Pending && x.AttemptCount == 0),
                Processing = g.Count(x => x.Status == EmailNotificationStatus.Pending && x.AttemptCount > 0),
                Failed = g.Count(x => x.Status == EmailNotificationStatus.Failed),
                LastCreated = (DateTime?)g.Max(x => x.CreatedAtUtc),
                LastSent = g.Max(x => x.SentAt),
                LastFailure = g.Max(x => x.FailedAt)
            })
            .FirstOrDefaultAsync(ct);

        var outboxCount = await _db.EmailOutbox.AsNoTracking()
            .CountAsync(x => x.Status == EmailOutboxStatus.Pending, ct);

        // آخر إشعار من فئة مجدوَلة — تعريفه في ScheduledReminderEventTypes: آخر إشعار مُسجَّل،
        // لا آخر تشغيل للمجدول (المسار اليدويّ ينتج صفوفًا مطابقة لا تُميَّز).
        var lastScheduled = await _db.EmailNotifications.AsNoTracking()
            .Where(x => ScheduledReminderEventTypes.All.Contains(x.EventType))
            .MaxAsync(x => (DateTime?)x.CreatedAtUtc, ct);

        var oldestUnsettled = await _db.EmailNotifications.AsNoTracking()
            .Where(x => x.Status == EmailNotificationStatus.Pending)
            .MinAsync(x => (DateTime?)x.CreatedAtUtc, ct);

        var total = stats?.Total ?? 0;
        var historicalDryRun = stats?.HistoricalDryRun ?? 0;
        var enabledCount = stats?.Enabled ?? 0;
        var sentCount = stats?.Sent ?? 0;
        var pendingCount = stats?.Pending ?? 0;
        var processingCount = stats?.Processing ?? 0;
        var failedCount = stats?.Failed ?? 0;

        var warnings = BuildWarnings(
            isLive, modeBindingFailed, smtpConfigured, credentialConfigured,
            scheduler.Enabled, email.Enabled, failedCount, outboxCount, historicalDryRun,
            oldestUnsettled, checkedAt);

        return new EmailControlCenterStatusDto(
            Mode: mode,
            IsLiveSendingEnabled: isLive,
            SchedulerEnabled: scheduler.Enabled,
            PollMinutes: scheduler.PollMinutes,
            DailyDueHour: scheduler.DailyDueHour,
            WeeklyDueHour: scheduler.WeeklyDueHour,
            OverdueHour: scheduler.OverdueHour,
            SummaryHour: scheduler.SummaryHour,
            ReviewHour: scheduler.ReviewHour,
            TimeZoneId: RiyadhTimeZoneId,
            TimeZoneLabel: RiyadhTimeZoneLabel,
            EnvironmentName: _environment.EnvironmentName,
            SmtpConfigured: smtpConfigured,
            SmtpHost: host,
            SmtpPort: host is null ? null : email.EffectivePort,
            UsesTls: email.UseStartTls,
            SenderAddress: sender,
            CredentialConfigured: credentialConfigured,
            LegacyEmailEnabled: email.Enabled,
            LegacyFlagIsAuthoritative: false,
            TotalNotifications: total,
            HistoricalDryRunCount: historicalDryRun,
            EnabledCount: enabledCount,
            SentCount: sentCount,
            PendingCount: pendingCount,
            ProcessingCount: processingCount,
            FailedCount: failedCount,
            OutboxCount: outboxCount,
            LastNotificationCreatedAtUtc: stats?.LastCreated,
            LastSentAtUtc: stats?.LastSent,
            LastFailureAtUtc: stats?.LastFailure,
            LastScheduledNotificationCreatedAtUtc: lastScheduled,
            CheckedAtUtc: checkedAt,
            Warnings: warnings);
    }

    /// <summary>
    /// بناء التنبيهات. مبدأ حاكم: لا تنبيه إلّا على واقعة مُثبَتة من الإعدادات أو من القاعدة.
    ///
    /// ما لا يُنبَّه عليه عمدًا: «لم تُشغَّل نافذة متوقَّعة». تحديد أنّ نافذة كان يجب أن تُنتِج إشعارًا
    /// يتطلّب معرفة ما إذا كان اليوم يحمل مستحقّين أصلًا — ونافذة صحيحة تمامًا قد تُنتِج صفرًا.
    /// فالتنبيه عليه تخمين، وقد استُبعد (المرحلة 6: «فقط إن أمكن إثباتها بلا تخمين»).
    /// </summary>
    private static List<EmailControlStatusWarningDto> BuildWarnings(
        bool isLive, bool modeBindingFailed,
        bool smtpConfigured, bool credentialConfigured,
        bool schedulerEnabled, bool legacyEmailEnabled,
        int failedCount, int outboxCount, int historicalDryRun,
        DateTime? oldestUnsettled, DateTime checkedAt)
    {
        var list = new List<EmailControlStatusWarningDto>();

        if (modeBindingFailed)
            list.Add(new(EmailControlStatusSeverity.Critical, "mode_invalid",
                "قيمة EmailNotifications__Mode غير صالحة — تعذّر تحديد وضع القناة. النظام لا يعمل بوضع معروف."));

        if (isLive && !smtpConfigured)
            list.Add(new(EmailControlStatusSeverity.Critical, "live_without_smtp",
                "الإرسال الفعليّ مفعَّل بينما قناة SMTP غير مُهيّأة (مضيف أو عنوان مُرسِل مفقود) — كلّ محاولة إرسال ستفشل."));

        if (isLive && !credentialConfigured)
            list.Add(new(EmailControlStatusSeverity.Critical, "live_without_credential",
                "الإرسال الفعليّ مفعَّل بينما بيانات اعتماد المُرسِل غير مضبوطة — المصادقة على الخادم لن تتمّ."));

        if (failedCount > 0)
            list.Add(new(EmailControlStatusSeverity.Critical, "failed_notifications",
                $"يوجد {failedCount} إشعار فاشل يحتاج مراجعة."));

        if (oldestUnsettled is { } oldest && checkedAt - oldest > StuckThreshold)
            list.Add(new(EmailControlStatusSeverity.Critical, "stuck_pending",
                "توجد إشعارات لم تُحسَم منذ أكثر من ساعة — القناة الجديدة تحسم مصير كلّ إشعار فورًا، فهذا يدلّ على انقطاع."));

        if (!schedulerEnabled)
            list.Add(new(EmailControlStatusSeverity.Warning, "scheduler_disabled",
                "مجدول التذكيرات معطَّل — لن تُطلَق أيّ نافذة تلقائيًّا."));

        if (outboxCount > 0)
            list.Add(new(EmailControlStatusSeverity.Warning, "outbox_backlog",
                $"صندوق الصادر القديم يحتوي {outboxCount} رسالة معلّقة."));

        if (outboxCount > 0 && !legacyEmailEnabled)
            list.Add(new(EmailControlStatusSeverity.Warning, "legacy_disabled_with_backlog",
                "توجد رسائل معلّقة في صندوق الصادر القديم بينما العلم القديم معطَّل — لن تُرسَل حتى يُفعَّل."));

        if (historicalDryRun > 0)
            list.Add(new(EmailControlStatusSeverity.Info, "historical_dryrun_records",
                $"يوجد {historicalDryRun} سجلّ تاريخيّ أُنشئ في وضع المحاكاة. هذه سجلّات سابقة ولا تعبّر عن الوضع الحاليّ."));

        list.Add(new(EmailControlStatusSeverity.Info, "legacy_flag_not_authoritative",
            "العلم القديم Email__Enabled يخصّ مسارات البريد القديمة فقط، وليس مصدر حقيقة لوضع هذه القناة."));

        return list;
    }
}
