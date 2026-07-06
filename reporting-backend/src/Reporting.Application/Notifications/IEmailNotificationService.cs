using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Entities.Leave;

namespace Reporting.Application.Notifications;

/// <summary>
/// خدمة إشعارات البريد المستقلّة (EMAIL-NOTIFICATIONS-R1).
/// تُستدعى من خدمات الحوكمة بعد نجاح العملية الأساسية. لا ترمي استثناءً للمتصل (داخليًا محميّة)،
/// ومع ذلك يجب على المتصل لفّها بـ try/catch كي لا يكسر إخفاقُها العمليةَ الأساسية.
/// تحترم الوضع EmailNotifications:Mode وتمنع التكرار عبر مفتاح الترابط.
/// </summary>
public interface IEmailNotificationService
{
    /// <summary>إشعار إسناد بند حوكمة جديد (عند وجود مُسنَد إليه).</summary>
    Task NotifyGovernanceItemAssignedAsync(GovernanceItem item, CancellationToken ct = default);

    /// <summary>إشعار إسناد إجراء حوكمة (عند الإنشاء بمُسنَد إليه أو عند إعادة الإسناد).</summary>
    Task NotifyGovernanceActionItemAssignedAsync(GovernanceActionItem item, bool isReassign, CancellationToken ct = default);

    /// <summary>إشعار إنشاء تصعيد فردي موجَّه لموظّف (عند TargetType=User وTargetUserId محدّد).</summary>
    Task NotifyGovernanceEscalationCreatedAsync(GovernanceEscalation item, CancellationToken ct = default);

    // ===== EMAIL-OPERATIONAL-NOTIFICATIONS-R1 (أحداث تشغيلية آمنة، DryRun) =====

    /// <summary>
    /// إشعار تحديث/تغيير حالة بند حوكمة مرتبط بالمستلم: المُسنَد إليه + المنشئ (إن اختلف). لا يُرسَل لنطاق التطبيق كاملًا.
    /// changeKey يميّز التحديث لمنع تكرار نفس التحديث دون حجب تحديثات لاحقة مختلفة.
    /// </summary>
    Task NotifyGovernanceItemUpdatedAsync(GovernanceItem item, string changeKey, CancellationToken ct = default);

    /// <summary>إشعار إغلاق/إكمال إجراء حوكمة: المنشئ + المُسنِد (إن اختلفا عن مَن أغلق).</summary>
    Task NotifyGovernanceActionItemCompletedAsync(GovernanceActionItem item, CancellationToken ct = default);

    /// <summary>إشعار إسناد تصعيد للمُسنَد إليه للمراجعة.</summary>
    Task NotifyGovernanceEscalationAssignedAsync(GovernanceEscalation item, CancellationToken ct = default);

    /// <summary>إشعار إغلاق/حلّ تصعيد: الرافع + المُسنَد إليه (إن اختلف)، دون مَن أغلق.</summary>
    Task NotifyGovernanceEscalationClosedAsync(GovernanceEscalation item, CancellationToken ct = default);

    /// <summary>إشعار طلب إجازة جديد بانتظار المراجعة (المُراجِعون/المدير المحدَّدون من الخدمة).</summary>
    Task NotifyLeaveRequestCreatedAsync(LeaveRequest item, IReadOnlyCollection<Guid> reviewerUserIds, CancellationToken ct = default);

    /// <summary>إشعار وصول طلب إجازة لمرحلة الموارد البشرية (مستخدمو HR المحدَّدون من الخدمة).</summary>
    Task NotifyLeaveRequestNeedsHrActionAsync(LeaveRequest item, IReadOnlyCollection<Guid> hrUserIds, CancellationToken ct = default);

    /// <summary>إشعار اعتماد طلب الإجازة نهائيًّا (المستلم = صاحب الطلب).</summary>
    Task NotifyLeaveRequestApprovedAsync(LeaveRequest item, CancellationToken ct = default);

    /// <summary>إشعار رفض طلب الإجازة (المستلم = صاحب الطلب، بصياغة هادئة وسبب إن وُجد).</summary>
    Task NotifyLeaveRequestRejectedAsync(LeaveRequest item, CancellationToken ct = default);

    /// <summary>إشعار طلب موارد بشرية جديد بانتظار المراجعة (مستخدمو HR المحدَّدون من الخدمة).</summary>
    Task NotifyHrRequestCreatedAsync(EmployeeServiceRequest item, IReadOnlyCollection<Guid> hrUserIds, CancellationToken ct = default);

    /// <summary>إشعار إغلاق/إكمال طلب موارد بشرية (المستلم = صاحب الطلب).</summary>
    Task NotifyHrRequestCompletedAsync(EmployeeServiceRequest item, CancellationToken ct = default);

    /// <summary>سجلّ إشعارات البريد (قراءة فقط، أحدثًا أولًا، بحدّ أقصى) — للأدوار العليا.</summary>
    Task<IReadOnlyList<EmailNotificationLogDto>> ListAsync(int take = 200, CancellationToken ct = default);

    // ===== EMAIL-NOTIFICATIONS-UI-R1 (سطح مراجعة مصفّح، قراءة فقط) =====

    /// <summary>سجلّ إشعارات البريد المصفّح مع فلاتر (قراءة فقط، أحدثًا أولًا) + ملخّص عدّاد ثابت على كامل الجدول.</summary>
    Task<EmailNotificationLogPageDto> ListLogAsync(EmailNotificationLogFilter filter, CancellationToken ct = default);

    /// <summary>تفاصيل إشعار بريد واحد بالمتن الكامل (قراءة فقط). يُرجِع null إن لم يوجد.</summary>
    Task<EmailNotificationLogDetailDto?> GetLogAsync(Guid id, CancellationToken ct = default);

    // ===== EMAIL-REPORT-REMINDERS-R1 (تذكيرات/تنبيهات التقارير — تولَّد يدويًّا في وضع DryRun) =====

    /// <summary>
    /// يُدرِج تذكير/تنبيه تقرير واحدًا عبر القلب الآمن نفسه (يحترم الوضع، يمنع التكرار بـ CorrelationKey،
    /// لا يمسّ email_outbox، لا يرمي). يُرجِع نتيجة الإدراج لتجميع العدّادات في مولّد التذكيرات.
    /// </summary>
    Task<ReportReminderOutcome> EnqueueReportReminderAsync(ReportReminderMessage message, CancellationToken ct = default);
}

/// <summary>نتيجة إدراج تذكير تقرير عبر القلب الآمن (EMAIL-REPORT-REMINDERS-R1).</summary>
public enum ReportReminderOutcome
{
    /// <summary>وضع الإشعارات معطّل (Disabled) — لا صفّ ولا إرسال.</summary>
    Disabled,
    /// <summary>سبق إنشاء صفّ بنفس CorrelationKey — تخطٍّ لمنع التكرار.</summary>
    Duplicate,
    /// <summary>لا بريد صالح للمستلم — أُنشئ صفّ بحالة Skipped بلا إرسال.</summary>
    SkippedNoEmail,
    /// <summary>أُنشئ الصفّ (DryRun في R1، أو Pending/Sent/Failed في وضع Enabled).</summary>
    Created,
    /// <summary>وقع استثناء داخليّ حُصِر ولم يُكسَر المولّد.</summary>
    Error
}

/// <summary>رسالة تذكير/تنبيه تقرير جاهزة للإدراج (بلا منطق تقييم — يُبنيها مولّد التذكيرات).</summary>
public record ReportReminderMessage(
    string EventType,
    Guid RecipientUserId,
    string CorrelationKey,
    string Subject,
    string Body,
    string Link,
    Guid EntityId,
    string EntityType = "ReportReminder");

/// <summary>فلاتر سجلّ إشعارات البريد (قراءة فقط — لا تعديل/إرسال).</summary>
public record EmailNotificationLogFilter(
    int Page = 1,
    int PageSize = 25,
    string? Status = null,
    string? EventType = null,
    Guid? RecipientUserId = null,
    string? Search = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null);

/// <summary>صفّ سجلّ إشعار بريد (عرض قائمة — بمعاينة مختصرة للمتن، بلا المتن الكامل).</summary>
public record EmailNotificationRowDto(
    Guid Id,
    DateTime CreatedAtUtc,
    string EventType,
    string Status,
    string Mode,
    Guid? RecipientUserId,
    string? RecipientName,
    string? RecipientEmail,
    string Subject,
    string BodyPreview,
    string CorrelationKey,
    string SourceEntityType,
    Guid SourceEntityId,
    string? ErrorMessage);

/// <summary>ملخّص عدّادات سجلّ إشعارات البريد (على كامل الجدول، مستقلّ عن الفلاتر) — لبطاقات الواجهة.</summary>
public record EmailNotificationLogSummaryDto(
    int Total,
    int DryRun,
    int Skipped,
    int Failed,
    int Sent,
    int Pending,
    int Cancelled,
    DateTime? LastCreatedAtUtc);

/// <summary>نتيجة سجلّ إشعارات البريد المصفّحة.</summary>
public record EmailNotificationLogPageDto(
    IReadOnlyList<EmailNotificationRowDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    EmailNotificationLogSummaryDto Summary);

/// <summary>تفاصيل إشعار بريد بالمتن الكامل (عرض قراءة فقط).</summary>
public record EmailNotificationLogDetailDto(
    Guid Id,
    DateTime CreatedAtUtc,
    string EventType,
    string Status,
    string Mode,
    Guid? RecipientUserId,
    string? RecipientName,
    string? RecipientEmail,
    string Subject,
    string BodyHtml,
    string? BodyText,
    string CorrelationKey,
    string SourceEntityType,
    Guid SourceEntityId,
    int AttemptCount,
    DateTime? LastAttemptAt,
    DateTime? SentAt,
    DateTime? FailedAt,
    string? ErrorMessage,
    Guid? CreatedByUserId);

/// <summary>عنصر سجلّ إشعار بريد (عرض إداري — بلا متن الرسالة).</summary>
public record EmailNotificationLogDto(
    Guid Id,
    DateTime CreatedAtUtc,
    string EventType,
    string EntityType,
    Guid EntityId,
    string? RecipientName,
    string? RecipientEmail,
    string Subject,
    string Status,
    string Mode,
    string? FailureReason,
    string CorrelationKey);
