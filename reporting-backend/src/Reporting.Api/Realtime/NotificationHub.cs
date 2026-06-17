using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Reporting.Api.Realtime;

/// <summary>محور SignalR للإشعارات اللحظية؛ كل مستخدم يستقبل إشعاراته عبر Clients.User.</summary>
[Authorize]
public class NotificationHub : Hub
{
}
