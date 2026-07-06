namespace Reporting.Application.Reports;

/// <summary>
/// EMAIL-REPORT-REMINDERS-R1 — مولّد تذكيرات/تنبيهات التقارير (يدويّ في R1، DryRun فقط).
/// يعيد استخدام تقييم RPT-DUE1 (المواعيد/التأخّر/المراجعات) لكن على مستوى الشركة كاملًا (بلا قيد نطاق)،
/// ثم يُدرِج كلّ تذكير عبر القلب الآمن في EmailNotificationService (يحترم الوضع، يمنع التكرار، لا يمسّ email_outbox).
///
/// المسار المستقبليّ (يُشرَح في تقرير الإغلاق):
/// - R1 (الآن): توليد يدويّ عبر نقطة نهاية إدارية، الوضع DryRun (لا إرسال فعليّ). هذه الخدمة قابلة للاستدعاء مرّة (RunOnce).
/// - R2: تشغيل نفس <see cref="GenerateAsync"/> من خدمة خلفية مجدولة (BackgroundService) بلا أيّ تغيير في المنطق، يبقى DryRun.
/// - R3: تحويل الوضع إلى Enabled + تهيئة SMTP تدريجيًّا؛ لا يتغيّر هذا المولّد إطلاقًا (الإرسال محكوم بالقلب الآمن).
/// </summary>
public interface IReportReminderService
{
    /// <summary>
    /// يولّد تذكيرات/تنبيهات التقارير لأسبوع (وتاريخ يوميّ اختياريّ) على مستوى الشركة، ويُدرِجها عبر القلب الآمن.
    /// قابل للاستدعاء يدويًّا (R1) أو من مجدول لاحقًا (R2) دون تغيير. يحترم الوضع الحاليّ (DryRun افتراضيًّا).
    /// </summary>
    Task<ReportReminderRunResult> GenerateAsync(ReportReminderRunOptions options, CancellationToken ct = default);
}

/// <summary>خيارات توليد تذكيرات التقارير.</summary>
public record ReportReminderRunOptions(
    string? WeekKey = null,
    DateOnly? Date = null,
    bool IncludeDue = true,
    bool IncludeOverdue = true,
    bool IncludeReviewOverdue = true);

/// <summary>حصيلة تشغيل مولّد التذكيرات (عدّادات إجمالية + تفصيل حسب نوع الحدث).</summary>
public record ReportReminderRunResult(
    string WeekKey,
    string WeekLabel,
    string Mode,
    int WouldGenerate,
    int Created,
    int SkippedDuplicate,
    int SkippedNoEmail,
    int SkippedDisabled,
    IReadOnlyList<ReportReminderBreakdownRow> Breakdown);

/// <summary>تفصيل عدّادات نوع حدث واحد.</summary>
public record ReportReminderBreakdownRow(
    string EventType,
    int WouldGenerate,
    int Created,
    int SkippedDuplicate,
    int SkippedNoEmail,
    int SkippedDisabled);
