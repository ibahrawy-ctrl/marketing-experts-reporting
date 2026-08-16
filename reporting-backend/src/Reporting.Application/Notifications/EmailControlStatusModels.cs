namespace Reporting.Application.Notifications;

/// <summary>
/// EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1 — درجات خطورة تنبيهات لوحة حالة البريد.
/// قيم نصّية ثابتة (لا enum) كي تبقى مستقرّة عبر JSON دون اعتماد على ترتيب رقميّ.
/// </summary>
public static class EmailControlStatusSeverity
{
    /// <summary>خلل يمنع أو يهدّد الإرسال الفعليّ الآن.</summary>
    public const string Critical = "Critical";

    /// <summary>وضع غير مثاليّ لا يمنع الإرسال بذاته.</summary>
    public const string Warning = "Warning";

    /// <summary>معلومة توضيحيّة لمنع سوء القراءة (لا تعني خللًا).</summary>
    public const string Info = "Info";
}

/// <summary>
/// EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1 — أنواع أحداث التذكيرات التي يولّدها
/// <c>ReportReminderService.GenerateAsync</c> (المسار الوحيد الذي يستدعيه المجدول).
///
/// تُستخدَم حصرًا لاشتقاق <c>LastScheduledNotificationCreatedAtUtc</c> = آخر إشعار **مُسجَّل**
/// من فئة مجدوَلة. **ليست** دليلًا على آخر تشغيل للمجدول: المسار اليدويّ
/// (<c>POST /api/report-reminders/generate</c>) يستدعي نفس <c>GenerateAsync</c> وينتج صفوفًا
/// لا تُميَّز عن صفوف المجدول (لا عمود مصدر، ولا تتبّع تشغيل مُخزَّن) — راجع المرحلة 10.
/// </summary>
public static class ScheduledReminderEventTypes
{
    public const string DailyDue = "report-daily-due";
    public const string WeeklyDue = "report-weekly-due";
    public const string Overdue = "report-overdue";
    public const string TeamOverdueSummary = "report-team-overdue-summary";
    public const string DepartmentOverdueSummary = "report-department-overdue-summary";
    public const string ExecutiveOverdueSummary = "report-executive-overdue-summary";
    public const string ReviewOverdueTeamLeader = "report-review-overdue-teamleader";
    public const string ReviewOverdueManager = "report-review-overdue-manager";
    public const string ReviewPendingExecutive = "report-review-pending-executive";

    /// <summary>المجموعة الكاملة (تُطابِق كلّ استدعاءات EnqueueAsync داخل ReportReminderService).</summary>
    public static readonly string[] All =
    {
        DailyDue, WeeklyDue, Overdue,
        TeamOverdueSummary, DepartmentOverdueSummary, ExecutiveOverdueSummary,
        ReviewOverdueTeamLeader, ReviewOverdueManager, ReviewPendingExecutive
    };
}

/// <summary>تنبيه واحد على لوحة الحالة — بلا أيّ سرّ وبلا قيم إعدادات خام.</summary>
/// <param name="Severity">إحدى قيم <see cref="EmailControlStatusSeverity"/>.</param>
/// <param name="Code">رمز ثابت للتنبيه (للاختبارات والواجهة).</param>
/// <param name="Message">نصّ عربيّ للعرض.</param>
public record EmailControlStatusWarningDto(string Severity, string Code, string Message);

/// <summary>
/// EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1 — الحالة التشغيليّة الحيّة لقناة البريد الجديدة.
///
/// **قراءة فقط بالكامل**: لا كتابة على القاعدة، لا اتّصال SMTP، لا استدعاء لأيّ مهمّة مجدوَلة.
///
/// **مصدر الحقيقة للوضع** هو <c>EmailNotifications:Mode</c> (متغيّر البيئة <c>EmailNotifications__Mode</c>)
/// وحده. العلم القديم <c>Email:Enabled</c> يُعرَض للشفافيّة فقط ولا يحكم هذه القناة إطلاقًا
/// (<see cref="LegacyFlagIsAuthoritative"/> = false دائمًا).
///
/// **بلا أسرار**: لا كلمة مرور، لا طولها، لا بصمتها، لا سلسلة اتّصال، لا تفريغ إعدادات.
/// جاهزيّة بيانات الاعتماد تُعرَض كقيمة منطقيّة واحدة فقط (<see cref="CredentialConfigured"/>).
/// </summary>
public record EmailControlCenterStatusDto(
    // ===== 1) الحالة التشغيليّة الحاليّة =====

    /// <summary>الوضع الحاليّ كما يقرؤه التطبيق: Enabled / DryRun / Disabled (أو Invalid إن تعذّر الربط).</summary>
    string Mode,

    /// <summary>هل الإرسال الفعليّ مفعَّل الآن؟ = (Mode == Enabled) حصرًا.</summary>
    bool IsLiveSendingEnabled,

    // ===== 2) جدول التشغيل =====

    /// <summary><c>ReportReminderScheduler:Enabled</c> — هل الخدمة الخلفيّة تُشغّل نبضاتها؟</summary>
    bool SchedulerEnabled,

    /// <summary>فترة النبض بالدقائق كما هي مُعدَّة (الخدمة تفرض حدًّا أدنى 5 عند التشغيل).</summary>
    int PollMinutes,

    /// <summary>ساعة نافذة التقارير اليوميّة بتوقيت الرياض (null = لا نافذة).</summary>
    int? DailyDueHour,
    int? WeeklyDueHour,
    int? OverdueHour,
    int? SummaryHour,
    int? ReviewHour,

    /// <summary>معرّف المنطقة الزمنيّة المرجعيّة للنوافذ.</summary>
    string TimeZoneId,

    /// <summary>تسمية عربيّة للمنطقة الزمنيّة.</summary>
    string TimeZoneLabel,

    /// <summary>اسم بيئة الاستضافة (Production / ReleaseCandidate / Development…).</summary>
    string EnvironmentName,

    // ===== 3) جاهزيّة SMTP (بلا أسرار) =====

    /// <summary>نفس منطق <c>IEmailSender.IsConfigured</c> الحيّ (مضيف + عنوان مُرسِل).</summary>
    bool SmtpConfigured,

    /// <summary>مضيف SMTP الفعّال (غير سرّي). null إن لم يُضبط.</summary>
    string? SmtpHost,

    /// <summary>المنفذ الفعّال. null إن لم يُضبط أيّ مضيف.</summary>
    int? SmtpPort,

    /// <summary>هل يُستخدَم STARTTLS؟ (false = SSL مباشر عند الاتّصال).</summary>
    bool UsesTls,

    /// <summary>عنوان المُرسِل الفعّال (غير سرّي). null إن لم يُضبط.</summary>
    string? SenderAddress,

    /// <summary>هل كلمة مرور التطبيق مضبوطة؟ **قيمة منطقيّة فقط** — لا تُشتقّ منها أيّ خاصيّة للسرّ.</summary>
    bool CredentialConfigured,

    // ===== 4) إعدادات التوافق القديمة =====

    /// <summary><c>Email:Enabled</c> — العلم القديم. يحكم صندوق الصادر القديم وتذكير التسليم القديم فقط.</summary>
    bool LegacyEmailEnabled,

    /// <summary>ثابتة false: العلم القديم ليس مصدر حقيقة لهذه القناة.</summary>
    bool LegacyFlagIsAuthoritative,

    // ===== 5) العدّادات (قراءة فقط، تجميع على مستوى القاعدة) =====

    /// <summary>إجمالي صفوف <c>email_notifications</c>.</summary>
    int TotalNotifications,

    /// <summary>صفوف أُنشئت بوضع محاكاة (عمود <c>Mode</c> = DryRun) — **سجلّ تاريخيّ**، لا يعبّر عن الوضع الحاليّ.</summary>
    int HistoricalDryRunCount,

    /// <summary>صفوف أُنشئت بوضع إرسال فعليّ (عمود <c>Mode</c> = Enabled).</summary>
    int EnabledCount,

    /// <summary>صفوف حالتها Sent (عمود <c>Status</c>).</summary>
    int SentCount,

    /// <summary>Status=Pending ولم تُجرَّب بعد (AttemptCount = 0).</summary>
    int PendingCount,

    /// <summary>Status=Pending وقد جرت محاولة واحدة على الأقلّ (AttemptCount &gt; 0) — لا توجد قيمة Processing في التعداد.</summary>
    int ProcessingCount,

    /// <summary>صفوف حالتها Failed.</summary>
    int FailedCount,

    /// <summary>صفوف <c>email_outbox</c> (القناة القديمة) بحالة Pending.</summary>
    int OutboxCount,

    // ===== 6) آخر نشاط =====

    DateTime? LastNotificationCreatedAtUtc,
    DateTime? LastSentAtUtc,
    DateTime? LastFailureAtUtc,

    /// <summary>
    /// آخر إشعار **مُسجَّل** نوع حدثه ضمن <see cref="ScheduledReminderEventTypes.All"/>.
    /// **ليس** «آخر تشغيل للمجدول» — المسار اليدويّ ينتج صفوفًا مطابقة لا تُميَّز.
    /// </summary>
    DateTime? LastScheduledNotificationCreatedAtUtc,

    // ===== 7) وقت القراءة والتنبيهات =====

    /// <summary>لحظة تنفيذ هذه القراءة (UTC).</summary>
    DateTime CheckedAtUtc,

    IReadOnlyList<EmailControlStatusWarningDto> Warnings);
