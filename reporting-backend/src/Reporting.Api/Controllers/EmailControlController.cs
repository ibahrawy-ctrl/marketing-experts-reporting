using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Notifications;

namespace Reporting.Api.Controllers;

// مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1) — إدارة قوالب/قواعد + معاينة مستقبِلين + تذكير يدويّ DryRun.
// كامل المتحكّم للأدمن حصرًا عبر سياسة EmailControlManage. R1: DryRun فقط (لا إرسال فعليّ).
// سجلّ الرسائل يُعرَض عبر متحكّم email-notifications المنفصل (سياسته الخاصة).
[Authorize(Policy = Policies.EmailControlManage)]
[Route("api/email-control")]
public class EmailControlController : ApiControllerBase
{
    private readonly IEmailControlService _service;
    private readonly IEmailControlStatusService _status;

    public EmailControlController(IEmailControlService service, IEmailControlStatusService status)
    {
        _service = service;
        _status = status;
    }

    // ===== الحالة التشغيليّة الحيّة (EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1) =====
    // قراءة فقط: بلا كتابة على القاعدة، بلا اتّصال SMTP، بلا استدعاء أيّ مهمّة مجدوَلة، وبلا أيّ سرّ.
    // الصلاحية موروثة من سياسة المتحكّم EmailControlManage (الأدمن حصرًا) — بلا توسيع أدوار.

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct = default)
        => Ok(await _status.GetStatusAsync(ct));

    // ===== القوالب =====

    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates(CancellationToken ct = default)
        => Ok(await _service.ListTemplatesAsync(ct));

    [HttpGet("templates/{key}")]
    public async Task<IActionResult> GetTemplate(string key, CancellationToken ct = default)
    {
        var dto = await _service.GetTemplateAsync(key, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPut("templates/{key}")]
    public async Task<IActionResult> UpdateTemplate(string key, [FromBody] UpdateEmailTemplateRequest request, CancellationToken ct = default)
        => FromResult(await _service.UpdateTemplateAsync(key, request, CurrentUserId, ct));

    [HttpPost("templates/{key}/preview")]
    public async Task<IActionResult> PreviewTemplate(string key, [FromBody] EmailTemplatePreviewRequest request, CancellationToken ct = default)
        => FromResult(await _service.PreviewTemplateAsync(key, request, ct));

    // ===== القواعد =====

    [HttpGet("rules")]
    public async Task<IActionResult> ListRules(CancellationToken ct = default)
        => Ok(await _service.ListRulesAsync(ct));

    [HttpGet("rules/{id:guid}")]
    public async Task<IActionResult> GetRule(Guid id, CancellationToken ct = default)
    {
        var dto = await _service.GetRuleAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPut("rules/{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateEmailRuleRequest request, CancellationToken ct = default)
        => FromResult(await _service.UpdateRuleAsync(id, request, CurrentUserId, ct));

    // ===== معاينة المستقبِلين =====

    [HttpPost("recipients/preview")]
    public async Task<IActionResult> PreviewRecipients([FromBody] RecipientPreviewRequest request, CancellationToken ct = default)
        => FromResult(await _service.PreviewRecipientsAsync(request, ct));

    // ===== تذكير يدويّ DryRun =====

    [HttpPost("manual-reminders/dry-run")]
    public async Task<IActionResult> ManualReminderDryRun([FromBody] ManualReminderDryRunRequest request, CancellationToken ct = default)
        => FromResult(await _service.ManualReminderDryRunAsync(request, CurrentUserId, ct));
}
