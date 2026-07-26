namespace Reporting.Application.Notifications;

/// <summary>
/// EMAIL-NOTIFICATIONS-FULL-INTERNAL-ACTIVATION-R1 — إعدادات مجدول تذكيرات التقارير (R2).
/// تُقرأ من قسم "ReportReminderScheduler" (متغيرات البيئة ReportReminderScheduler__*).
///
/// المجدول يستدعي <c>IReportReminderService.GenerateAsync</c> نفسها المستخدَمة في المسار اليدويّ،
/// بلا أيّ تغيير في منطق التوليد. الإرسال الفعليّ يبقى محكومًا حصرًا بـ EmailNotifications__Mode.
/// معطّل افتراضيًّا — لا يعمل حتى يُفعَّل صراحةً عبر البيئة.
/// </summary>
public class ReportReminderSchedulerOptions
{
    public const string SectionName = "ReportReminderScheduler";

    /// <summary>البوابة العامّة للمجدول. معطّل افتراضيًّا.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// ساعات التشغيل بتوقيت الرياض (UTC+3)، مفصولة بفواصل، بنظام 24 ساعة.
    /// مثال: "8" أو "8,13". القيم خارج 0–23 تُتجاهَل.
    /// </summary>
    public string RunAtRiyadhHours { get; set; } = "8";

    /// <summary>فترة فحص حلول الموعد بالدقائق. الحدّ الأدنى المفروض 5.</summary>
    public int PollMinutes { get; set; } = 15;

    /// <summary>تضمين تذكيرات الاستحقاق.</summary>
    public bool IncludeDue { get; set; } = true;

    /// <summary>تضمين تنبيهات التأخّر.</summary>
    public bool IncludeOverdue { get; set; } = true;

    /// <summary>تضمين تنبيهات تأخّر المراجعة.</summary>
    public bool IncludeReviewOverdue { get; set; } = true;

    /// <summary>ساعات التشغيل مُحلَّلة ومُصفّاة ومرتّبة. فارغة ⇒ لا تشغيل.</summary>
    public IReadOnlyList<int> ParsedRunAtRiyadhHours =>
        (RunAtRiyadhHours ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(h => int.TryParse(h, out var v) ? v : -1)
            .Where(h => h is >= 0 and <= 23)
            .Distinct()
            .OrderBy(h => h)
            .ToList();
}
