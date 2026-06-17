using Microsoft.EntityFrameworkCore;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.System;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly INotificationPusher _pusher;

    public NotificationService(AppDbContext db, INotificationPusher pusher)
    {
        _db = db;
        _pusher = pusher;
    }

    public async Task NotifyAsync(Guid recipientId, string type, string title, string? body = null, string? link = null, CancellationToken ct = default)
    {
        if (recipientId == Guid.Empty) return;

        var entity = new Notification
        {
            RecipientId = recipientId,
            Type = type,
            Title = title,
            Body = body,
            Link = link,
            IsRead = false
        };
        _db.Notifications.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dto = Map(entity);
        try { await _pusher.PushAsync(recipientId, dto, ct); }
        catch { /* الدفع اللحظي أفضل-جهد ولا يُعطّل الإجراء */ }
    }

    public async Task NotifyManyAsync(IEnumerable<Guid> recipientIds, string type, string title, string? body = null, string? link = null, CancellationToken ct = default)
    {
        var ids = recipientIds.Where(i => i != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return;

        var entities = ids.Select(id => new Notification
        {
            RecipientId = id, Type = type, Title = title, Body = body, Link = link, IsRead = false
        }).ToList();
        _db.Notifications.AddRange(entities);
        await _db.SaveChangesAsync(ct);

        foreach (var e in entities)
        {
            try { await _pusher.PushAsync(e.RecipientId, Map(e), ct); }
            catch { /* أفضل-جهد */ }
        }
    }

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, CancellationToken ct = default)
        => await _db.Notifications.AsNoTracking()
            .Where(n => n.RecipientId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAtUtc))
            .ToListAsync(ct);

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default)
        => _db.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead, ct);

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.RecipientId == userId, ct);
        if (n is null) return false;
        if (!n.IsRead)
        {
            n.IsRead = true;
            n.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return true;
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        await _db.Notifications.Where(n => n.RecipientId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }

    private static NotificationDto Map(Notification n)
        => new(n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAtUtc);
}
