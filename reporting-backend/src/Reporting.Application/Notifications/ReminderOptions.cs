namespace Reporting.Application.Notifications;

/// <summary>
/// إعدادات التذكير بالتقارير (V1) — تُقرأ من قسم "Reminders" (متغيرات البيئة Reminders__*).
/// تذكير واحد خفيف قبل موعد تسليم التقرير الأسبوعي، بلا تكرار مزعج.
/// معطّل افتراضيًا (جاهزية) — لا يُفعَّل إلا بعد اكتمال بيانات الموظفين وموافقة صريحة.
/// </summary>
public class ReminderOptions
{
    public const string SectionName = "Reminders";

    /// <summary>البوابة العامة لخدمة التذكير. معطّلة افتراضيًا — لا تذكير حتى تُفعَّل.</summary>
    public bool Enabled { get; set; }

    /// <summary>فترة فحص الاستحقاق بالدقائق (الخدمة الخلفية).</summary>
    public int PollMinutes { get; set; } = 180;

    /// <summary>عدد الأيام قبل موعد التسليم لإطلاق التذكير (0 = يوم الاستحقاق نفسه).</summary>
    public int LeadDays { get; set; }

    /// <summary>نوع إشعار التذكير — للربط مع قائمة سماح البريد Email__IncludedTypes.</summary>
    public const string ReminderType = "submission.reminder";
}
