using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.System;

/// <summary>
/// رسالة بريد في صندوق الصادر — تُنشأ مع الإشعار وتُرسَل لاحقًا عبر خدمة خلفية مع إعادة محاولة.
/// تضمن عدم ضياع البريد عند فشل SMTP المؤقت، ودون حجب طلب المستخدم.
/// </summary>
public class EmailOutbox : BaseEntity
{
    /// <summary>المستلِم في النظام (للربط/التتبع فقط).</summary>
    public Guid RecipientId { get; set; }

    public string ToEmail { get; set; } = string.Empty;
    public string? ToName { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>نوع الإشعار المصدر (submission.submitted…)، للتتبع.</summary>
    public string Type { get; set; } = string.Empty;

    public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pending;
    public int Attempts { get; set; }

    /// <summary>أوّل وقت تُتاح فيه المحاولة التالية (للتراجع الأُسّي).</summary>
    public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }

    /// <summary>آخر خطأ إرسال (رسالة آمنة بلا أسرار) — للتشخيص.</summary>
    public string? LastError { get; set; }
}
