namespace Reporting.Application.Notifications;

/// <summary>إنشاء الإشعارات وقراءتها؛ يُستدعى من خدمات النطاق عند الأحداث الحسّاسة.</summary>
public interface INotificationService
{
    Task NotifyAsync(Guid recipientId, string type, string title, string? body = null, string? link = null, CancellationToken ct = default);
    Task NotifyManyAsync(IEnumerable<Guid> recipientIds, string type, string title, string? body = null, string? link = null, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>دفع الإشعار لحظيًا (SignalR) — تجريد في طبقة التطبيق، يُنفَّذ في طبقة الـAPI.</summary>
public interface INotificationPusher
{
    Task PushAsync(Guid recipientId, NotificationDto notification, CancellationToken ct = default);
}
