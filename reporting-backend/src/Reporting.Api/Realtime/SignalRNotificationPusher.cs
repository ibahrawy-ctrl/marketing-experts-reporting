using Microsoft.AspNetCore.SignalR;
using Reporting.Application.Notifications;

namespace Reporting.Api.Realtime;

/// <summary>تنفيذ الدفع اللحظي عبر SignalR — يرسل حدث "notification" لمالك الإشعار.</summary>
public class SignalRNotificationPusher : INotificationPusher
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationPusher(IHubContext<NotificationHub> hub) => _hub = hub;

    public Task PushAsync(Guid recipientId, NotificationDto notification, CancellationToken ct = default)
        => _hub.Clients.User(recipientId.ToString()).SendAsync("notification", notification, ct);
}
