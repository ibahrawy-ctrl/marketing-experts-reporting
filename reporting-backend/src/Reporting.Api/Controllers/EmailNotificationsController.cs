using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Notifications;

namespace Reporting.Api.Controllers;

// سجلّ إشعارات البريد (EMAIL-NOTIFICATIONS-R1) — عرض إداريّ قراءة-فقط بلا متن الرسالة.
// للأدوار Admin / CEO / GM / CeoSupport فقط عبر سياسة EmailNotificationLog.
[Authorize(Policy = Policies.EmailNotificationLog)]
[Route("api/email-notifications")]
public class EmailNotificationsController : ApiControllerBase
{
    private readonly IEmailNotificationService _service;

    public EmailNotificationsController(IEmailNotificationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 200, CancellationToken ct = default)
        => Ok(await _service.ListAsync(take, ct));

    // EMAIL-NOTIFICATIONS-UI-R1 — سجلّ مصفّح بفلاتر (قراءة فقط، لا إرسال/تعديل/حذف).
    [HttpGet("log")]
    public async Task<IActionResult> Log(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        [FromQuery] string? eventType = null,
        [FromQuery] Guid? recipientUserId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken ct = default)
        => Ok(await _service.ListLogAsync(
            new EmailNotificationLogFilter(page, pageSize, status, eventType, recipientUserId, search, dateFrom, dateTo), ct));

    // EMAIL-NOTIFICATIONS-UI-R1 — تفاصيل إشعار بالمتن الكامل (قراءة فقط).
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct = default)
    {
        var detail = await _service.GetLogAsync(id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }
}
