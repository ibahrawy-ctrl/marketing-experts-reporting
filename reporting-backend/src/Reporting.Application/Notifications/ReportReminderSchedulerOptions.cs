namespace Reporting.Application.Notifications;

/// <summary>
/// EMAIL-NOTIFICATIONS-FULL-INTERNAL-ACTIVATION-R1 — إعدادات مجدول تذكيرات التقارير (R2).
/// تُقرأ من قسم "ReportReminderScheduler" (متغيرات البيئة ReportReminderScheduler__*).
///
/// EMAIL-NOTIFICATIONS-SPLIT-DELIVERY-WINDOWS-R1 — نوافذ إرسال مفصولة لكلّ فئة.
/// أُلغيت القائمة العامّة <c>RunAtRiyadhHours</c> التي كانت تُشغّل **كلّ** الفئات في **كلّ** ساعة مدرَجة،
/// واستُبدلت بساعة مستقلّة لكلّ فئة بتوقيت الرياض. جدول التشغيل الفعليّ = اتّحاد الساعات المضبوطة،
/// وفي كلّ ساعة تُفعَّل الفئات التي تساوي ساعتها تلك الساعة فقط.
///
/// القرار التشغيليّ المعتمَد: اليوميّ (المبيعات) الساعة 16، وبقيّة الفئات الساعة 9.
/// الفئة التي ساعتها null (أو خارج 0–23) لا تُجدوَل إطلاقًا.
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

    /// <summary>فترة فحص حلول الموعد بالدقائق. الحدّ الأدنى المفروض 5.</summary>
    public int PollMinutes { get; set; } = 15;

    /// <summary>ساعة تذكير التقرير اليوميّ (المبيعات) بتوقيت الرياض. null ⇒ لا يُجدوَل.</summary>
    public int? DailyDueHour { get; set; } = 16;

    /// <summary>ساعة تذكير التقرير الأسبوعيّ بتوقيت الرياض (يُقيَّد بعدها بيوم استحقاق الدور).</summary>
    public int? WeeklyDueHour { get; set; } = 9;

    /// <summary>ساعة تنبيه التأخّر الفرديّ بتوقيت الرياض.</summary>
    public int? OverdueHour { get; set; } = 9;

    /// <summary>ساعة ملخّصات التأخّر (فريق/إدارة/تنفيذيّ) بتوقيت الرياض.</summary>
    public int? SummaryHour { get; set; } = 9;

    /// <summary>ساعة تنبيهات تأخّر/تعليق المراجعة بتوقيت الرياض.</summary>
    public int? ReviewHour { get; set; } = 9;

    /// <summary>الفئات المستحقّة في ساعة رياض معيّنة. فارغة ⇒ لا تشغيل في تلك الساعة.</summary>
    public ReminderCategorySet CategoriesForHour(int riyadhHour) => new(
        WeeklyDue: Normalize(WeeklyDueHour) == riyadhHour,
        DailyDue: Normalize(DailyDueHour) == riyadhHour,
        Overdue: Normalize(OverdueHour) == riyadhHour,
        Summaries: Normalize(SummaryHour) == riyadhHour,
        ReviewOverdue: Normalize(ReviewHour) == riyadhHour);

    /// <summary>كلّ ساعات التشغيل المضبوطة (اتّحاد ساعات الفئات) مرتّبة — للتشخيص والتسجيل.</summary>
    public IReadOnlyList<int> ScheduledHours =>
        new[] { WeeklyDueHour, DailyDueHour, OverdueHour, SummaryHour, ReviewHour }
            .Select(Normalize)
            .Where(h => h is not null)
            .Select(h => h!.Value)
            .Distinct()
            .OrderBy(h => h)
            .ToList();

    /// <summary>الساعة خارج 0–23 تُعامَل كغير مضبوطة (لا تُجدوَل) بدل أن تُطابِق شيئًا.</summary>
    private static int? Normalize(int? hour) => hour is >= 0 and <= 23 ? hour : null;
}

/// <summary>
/// EMAIL-NOTIFICATIONS-SPLIT-DELIVERY-WINDOWS-R1 — مجموعة فئات التذكير المستحقّة في نافذة زمنية واحدة.
/// تُمرَّر كما هي إلى مولّد التذكيرات، فيولّد الفئات المفعَّلة فقط.
/// </summary>
public readonly record struct ReminderCategorySet(
    bool WeeklyDue,
    bool DailyDue,
    bool Overdue,
    bool Summaries,
    bool ReviewOverdue)
{
    public bool IsEmpty => !WeeklyDue && !DailyDue && !Overdue && !Summaries && !ReviewOverdue;

    /// <summary>وصف مختصر للتسجيل، مثل "dailyDue" أو "weeklyDue+overdue+summaries+review".</summary>
    public override string ToString()
    {
        var parts = new List<string>(5);
        if (WeeklyDue) parts.Add("weeklyDue");
        if (DailyDue) parts.Add("dailyDue");
        if (Overdue) parts.Add("overdue");
        if (Summaries) parts.Add("summaries");
        if (ReviewOverdue) parts.Add("review");
        return parts.Count == 0 ? "none" : string.Join("+", parts);
    }
}
