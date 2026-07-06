using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.System;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly INotificationPusher _pusher;
    private readonly EmailOptions _email;

    public NotificationService(AppDbContext db, INotificationPusher pusher, IOptions<EmailOptions> email)
    {
        _db = db;
        _pusher = pusher;
        _email = email.Value;
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

        await EnqueueEmailsAsync(new[] { recipientId }, type, title, body, link, ct);

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

        await EnqueueEmailsAsync(ids, type, title, body, link, ct);

        await _db.SaveChangesAsync(ct);

        foreach (var e in entities)
        {
            try { await _pusher.PushAsync(e.RecipientId, Map(e), ct); }
            catch { /* أفضل-جهد */ }
        }
    }

    /// <summary>يُضيف رسائل صندوق الصادر للمستلِمين أصحاب البريد — ضمن نفس المعاملة، عند تفعيل القناة.</summary>
    private async Task EnqueueEmailsAsync(IReadOnlyCollection<Guid> recipientIds, string type, string title, string? body, string? link, CancellationToken ct)
    {
        if (!_email.Enabled) return;
        // قائمة السماح (إن وُجدت): لا يُرسَل بريد إلا للأنواع المسموح بها صراحةً — تفعيل محدود أثناء التجربة.
        if (_email.IncludedTypes.Length > 0 &&
            !_email.IncludedTypes.Contains(type, StringComparer.OrdinalIgnoreCase)) return;
        if (_email.ExcludedTypes.Contains(type, StringComparer.OrdinalIgnoreCase)) return;

        var recipients = await _db.Users.AsNoTracking()
            .Where(u => recipientIds.Contains(u.Id) && u.IsActive && u.Email != null && u.Email != "")
            .Select(u => new { u.Id, u.Email, u.FullName })
            .ToListAsync(ct);

        if (recipients.Count == 0) return;

        var subject = title;
        var html = EmailHtml.Build(title, body, link);

        foreach (var r in recipients)
        {
            _db.EmailOutbox.Add(new EmailOutbox
            {
                RecipientId = r.Id,
                ToEmail = r.Email!,
                ToName = r.FullName,
                Subject = subject,
                HtmlBody = html,
                Type = type,
                Status = EmailOutboxStatus.Pending,
                NextAttemptUtc = DateTime.UtcNow
            });
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
