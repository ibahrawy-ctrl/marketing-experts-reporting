using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.System;

/// <summary>
/// إشعار بريد (EMAIL-NOTIFICATIONS-R1) — جدول مستقلّ تمامًا عن <c>email_outbox</c> القديم.
/// كل صفّ يمثّل إشعارًا لحدث محدّد لمستلم محدّد، مع مفتاح ترابط فريد لمنع التكرار.
/// لا يُرسَل بريد فعليّ في R1 (الوضع الافتراضي DryRun).
/// </summary>
public class EmailNotification : BaseEntity
{
    /// <summary>نوع الحدث (مثل governance-item-created).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>نوع الكيان المصدر (مثل GovernanceItem).</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>معرّف الكيان المصدر.</summary>
    public Guid EntityId { get; set; }

    /// <summary>معرّف المستخدم المستلم (إن وُجد).</summary>
    public Guid? RecipientUserId { get; set; }

    /// <summary>بريد المستلم.</summary>
    public string? RecipientEmail { get; set; }

    /// <summary>اسم المستلم.</summary>
    public string? RecipientName { get; set; }

    /// <summary>عنوان الرسالة.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>متن الرسالة (HTML).</summary>
    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>متن الرسالة (نصّ صِرف).</summary>
    public string? BodyText { get; set; }

    /// <summary>حالة الإشعار.</summary>
    public EmailNotificationStatus Status { get; set; } = EmailNotificationStatus.Pending;

    /// <summary>الوضع الذي أُنشئ فيه الإشعار.</summary>
    public EmailNotificationMode Mode { get; set; } = EmailNotificationMode.DryRun;

    /// <summary>عدد محاولات الإرسال.</summary>
    public int AttemptCount { get; set; }

    /// <summary>وقت آخر محاولة إرسال.</summary>
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>وقت الإرسال الناجح.</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>وقت الفشل النهائي.</summary>
    public DateTime? FailedAt { get; set; }

    /// <summary>سبب الفشل (بلا أسرار).</summary>
    public string? FailureReason { get; set; }

    /// <summary>معرّف منشئ الإشعار (المستخدم الذي نفّذ العملية المصدر).</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>مفتاح الترابط الفريد لمنع التكرار.</summary>
    public string CorrelationKey { get; set; } = string.Empty;
}
