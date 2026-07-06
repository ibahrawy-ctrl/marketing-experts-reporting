using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reporting.Application.Notifications;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// خدمة خلفية ترسل رسائل صندوق الصادر المستحقّة عبر MailKit مع إعادة محاولة وتراجع أُسّي.
/// لا تحجب طلبات المستخدمين، وتضمن عدم ضياع البريد عند فشل SMTP المؤقت.
/// </summary>
public class EmailOutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailOutboxDispatcher> _logger;

    public EmailOutboxDispatcher(IServiceScopeFactory scopeFactory, IOptions<EmailOptions> options, ILogger<EmailOutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(5, _options.PollSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Enabled)
                    await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailOutboxDispatcher cycle failed");
            }

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>تشغيل دورة معالجة واحدة — للاختبار الحتمي بلا انتظار.</summary>
    public Task RunOnceAsync(CancellationToken ct = default) => ProcessBatchAsync(ct);

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var now = DateTime.UtcNow;
        var due = await db.EmailOutbox
            .Where(m => m.Status == EmailOutboxStatus.Pending && m.NextAttemptUtc <= now)
            .OrderBy(m => m.NextAttemptUtc)
            .Take(Math.Max(1, _options.BatchSize))
            .ToListAsync(ct);

        if (due.Count == 0) return;

        foreach (var msg in due)
        {
            var result = await sender.SendAsync(msg.ToEmail, msg.ToName, msg.Subject, msg.HtmlBody, ct);
            msg.Attempts++;
            msg.UpdatedAtUtc = DateTime.UtcNow;

            if (result.Success)
            {
                msg.Status = EmailOutboxStatus.Sent;
                msg.SentAtUtc = DateTime.UtcNow;
                msg.LastError = null;
            }
            else
            {
                msg.LastError = result.Error;
                if (msg.Attempts >= _options.MaxAttempts)
                {
                    msg.Status = EmailOutboxStatus.Failed;
                    _logger.LogWarning("Email permanently failed after {Attempts} attempts to {ToEmail}", msg.Attempts, msg.ToEmail);
                }
                else
                {
                    var backoff = TimeSpan.FromSeconds(_options.BackoffBaseSeconds * msg.Attempts);
                    msg.NextAttemptUtc = DateTime.UtcNow.Add(backoff);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
