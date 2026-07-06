using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Reporting.Application.Notifications;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// مُرسِل بريد عبر SMTP باستخدام MailKit (يدعم STARTTLS لـ Google Workspace).
/// لا يُسجَّل أي سرّ (كلمة مرور التطبيق) في أي مسار.
/// </summary>
public class MailKitEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptions<EmailOptions> options, ILogger<MailKitEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.EffectiveHost) &&
        !string.IsNullOrWhiteSpace(_options.EffectiveFromAddress);

    public async Task<EmailSendResult> SendAsync(string toEmail, string? toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return EmailSendResult.Fail("قناة البريد غير مُهيّأة (Host/From مفقود).");

        if (string.IsNullOrWhiteSpace(toEmail))
            return EmailSendResult.Fail("عنوان المستلِم فارغ.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.EffectiveFromAddress));
        message.To.Add(new MailboxAddress(toName ?? string.Empty, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            var socketOption = _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;
            await client.ConnectAsync(_options.EffectiveHost, _options.EffectivePort, socketOption, ct);

            // اسم المستخدم الفعّال (يقع على عنوان المُرسِل لـ Gmail/Google Workspace إن لم يُضبط صراحةً).
            if (!string.IsNullOrWhiteSpace(_options.EffectiveUsername))
                await client.AuthenticateAsync(_options.EffectiveUsername, _options.Password, ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            // تسجيل آمن: لا يحتوي أي سرّ.
            _logger.LogInformation("Email sent to {ToEmail} via {Host}:{Port} (subject={Subject})",
                toEmail, _options.EffectiveHost, _options.EffectivePort, subject);
            return EmailSendResult.Ok();
        }
        catch (Exception ex)
        {
            var safe = Truncate(ex.Message, 1000);
            _logger.LogWarning("Email send failed to {ToEmail} via {Host}:{Port}: {Error}",
                toEmail, _options.EffectiveHost, _options.EffectivePort, safe);
            return EmailSendResult.Fail(safe);
        }
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
