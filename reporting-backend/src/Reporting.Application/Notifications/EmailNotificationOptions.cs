using Reporting.Domain.Enums;

namespace Reporting.Application.Notifications;

/// <summary>
/// إعدادات نظام إشعارات البريد المستقلّ (EMAIL-NOTIFICATIONS-R1) — قسم "EmailNotifications".
/// مستقلّ تمامًا عن قسم "Email" القديم (EmailOptions/EmailOutbox).
/// الافتراضي DryRun (لا إرسال فعليّ). لا أسرار SMTP هنا — بيانات الاعتماد في متغيرات البيئة فقط.
/// </summary>
public class EmailNotificationOptions
{
    public const string SectionName = "EmailNotifications";

    /// <summary>وضع التشغيل: Disabled / DryRun (الافتراضي) / Enabled.</summary>
    public EmailNotificationMode Mode { get; set; } = EmailNotificationMode.DryRun;

    /// <summary>اسم المُرسِل الظاهر.</summary>
    public string FromName { get; set; } = "نظام تقارير خبراء التسويق";

    /// <summary>بريد المُرسِل (يُضبَط عبر البيئة عند الحاجة).</summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>أقصى عدد محاولات إرسال.</summary>
    public int MaxAttempts { get; set; } = 3;
}

/// <summary>
/// إعدادات التطبيق العامّة — قسم "App". يُستخدم BaseUrl لبناء روابط داخل-النظام في رسائل البريد.
/// لا روابط ثابتة (hardcoded) في القوالب.
/// </summary>
public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>العنوان الأساسي للنظام (مثل https://reports.emarketingacademy.net).</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
