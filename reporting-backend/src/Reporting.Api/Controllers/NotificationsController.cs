using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Notifications;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api/notifications")]
public class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _service.ListAsync(CurrentUserId, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
        => Ok(new { count = await _service.UnreadCountAsync(CurrentUserId, ct) });

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        => await _service.MarkReadAsync(CurrentUserId, id, ct) ? Ok() : NotFound();

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _service.MarkAllReadAsync(CurrentUserId, ct);
        return Ok();
    }

    // إرسال رسالة اختبار واحدة مباشرةً للتحقّق من تهيئة SMTP — لا يمرّ بصندوق الصادر
    // ولا يتأثّر بمفتاح Email:Enabled، ولا يُرسل لأي أحد سوى العنوان المحدّد. للمدير النظامي فقط.
    [HttpPost("test-email")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> SendTestEmail([FromBody] TestEmailRequest request, [FromServices] IEmailSender sender, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ToEmail))
            return BadRequest(new { ok = false, error = "ToEmail مطلوب." });

        if (!sender.IsConfigured)
            return StatusCode(503, new { ok = false, error = "قناة البريد غير مُهيّأة (Host/From مفقود)." });

        var subject = "اختبار بريد — نظام تقارير خبراء التسويق";
        var body = "هذه رسالة اختبار للتحقّق من تهيئة قناة البريد (SMTP). إن وصلتك فالقناة تعمل بنجاح.";
        var html = EmailHtml.Build(subject, body, null);

        var result = await sender.SendAsync(request.ToEmail.Trim(), null, subject, html, ct);
        return result.Success
            ? Ok(new { ok = true })
            : StatusCode(502, new { ok = false, error = result.Error });
    }
}

public record TestEmailRequest(string? ToEmail);
