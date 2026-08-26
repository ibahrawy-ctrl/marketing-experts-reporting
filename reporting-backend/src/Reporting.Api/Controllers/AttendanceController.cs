using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Reporting.Application.Attendance;
using Reporting.Application.Security;

namespace Reporting.Api.Controllers;

/// <summary>
/// P2-ATT-006 — سطح وقائع الحضور. مسارات **جديدة بالكامل** لا تمسّ أيّ مسار قائم.
///
/// <para><b>ما لا تفعله هذه الوحدة إطلاقًا:</b> لا تتّخذ أيّ قرار تخويل. كلّ التحقّق —
/// النطاق، والصلاحيّة، وشرعيّة الانتقال، والتزامن — يقع في طبقة الخدمة، وهذه الوحدة
/// تترجم <c>Result</c> إلى HTTP فقط. لذلك لا يوجد هنا أيّ <c>Authorize(Policy=…)</c>
/// يخصّ الحضور: المفاتيح تُفحص داخليًّا كي يبقى «خارج النطاق ⇒ 404» موحّدًا ولا يتحوّل
/// إلى 403 كاشف عند البوّابة.</para>
///
/// <para>مفتاح الميزة <c>Phase2:AttendanceEnabled</c> **ليس تخويلًا**؛ هو مجرّد إخفاء
/// للسطح كلّه (404) قبل التفعيل.</para>
/// </summary>
[Authorize]
public class AttendanceController : ApiControllerBase
{
    private const long MaxUploadBytes = 12 * 1024 * 1024;

    private readonly IAttendanceService _service;
    private readonly Phase2FeatureOptions _flags;

    public AttendanceController(IAttendanceService service, IOptions<Phase2FeatureOptions> flags)
    {
        _service = service;
        _flags = flags.Value;
    }

    private bool Disabled => !_flags.AttendanceEnabled;

    // ═══════════════════════════════ قراءة ═══════════════════════════════

    /// <summary>كتالوج أنواع الحوادث الفعّالة.</summary>
    [HttpGet("types")]
    public async Task<IActionResult> Types(CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.ListTypesAsync(ct));
    }

    /// <summary>قائمة الوقائع — النطاق يُفرَض خادميًّا فوق أيّ مرشِّح يرسله العميل.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AttendanceListFilter filter, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.ListAsync(filter, ct));
    }

    /// <summary>تفاصيل واقعة. غير الموجود وخارج النطاق يعطيان 404 متطابقًا.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.GetAsync(id, ct));
    }

    /// <summary>الخطّ الزمنيّ للواقعة — سجلّ إلحاقيّ لا يُعدَّل ولا يُحذف منه.</summary>
    [HttpGet("{id:guid}/events")]
    public async Task<IActionResult> Events(Guid id, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.ListEventsAsync(id, ct));
    }

    /// <summary>اقتراحات المصالحة مع إجازة/استئذان معتمد — اطّلاع فقط، بلا أيّ تغيير حالة.</summary>
    [HttpGet("{id:guid}/reconciliation-suggestions")]
    public async Task<IActionResult> ReconciliationSuggestions(Guid id, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.SuggestReconciliationAsync(id, ct));
    }

    // ═══════════════════════════════ دورة حياة البلاغ ═══════════════════════════════

    /// <summary>
    /// إنشاء بلاغ. ترويسة <c>Idempotency-Key</c> اختياريّة، وعند إرسالها تمنع ازدواج
    /// البلاغ عند إعادة المحاولة الشبكيّة (الفرض بفهرس فريد مُرشَّح لا بفحص تطبيقيّ فقط).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAttendanceIncidentRequest request, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        var key = Request.Headers.TryGetValue("Idempotency-Key", out var v) ? v.ToString() : null;
        return FromResult(await _service.CreateAsync(request, key, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] UpdateAttendanceDraftRequest request, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.UpdateDraftAsync(id, request, ct));
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] ConcurrencyRequest request, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.SubmitAsync(id, request.ConcurrencyStamp, ct));
    }

    /// <summary>إلغاء مسودّة لم تُرسَل — الحذف الوحيد المسموح في دورة الحياة.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> CancelDraft(Guid id, [FromQuery] int concurrencyStamp, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.CancelDraftAsync(id, concurrencyStamp, ct));
    }

    /// <summary>سحب بلاغ مُرسَل قبل ردّ الموظّف — من مُنشِئه وحده وبسبب موثَّق.</summary>
    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] AttendanceReasonRequest request, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.WithdrawAsync(id, request, ct));
    }

    // ═══════════════════════════════ حقّ الموظّف ═══════════════════════════════

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, [FromBody] EmployeeResponseRequest request, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.AcknowledgeAsync(id, request, ct));
    }

    [HttpPost("{id:guid}/dispute")]
    public async Task<IActionResult> Dispute(Guid id, [FromBody] EmployeeResponseRequest request, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.DisputeAsync(id, request, ct));
    }

    // ═══════════════════════════════ مراجعة الموارد البشريّة ═══════════════════════════════

    /// <summary>تأكيد/رفض/تصحيح/مصالحة/إبطال في نقطة واحدة محكومة بآلة الحالات.</summary>
    [HttpPost("{id:guid}/hr-review")]
    public async Task<IActionResult> HrReview(Guid id, [FromBody] HrReviewRequest request, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.HrReviewAsync(id, request, ct));
    }

    [HttpPost("{id:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid id, [FromBody] AttendanceReasonRequest request, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.EscalateAsync(id, request, ct));
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] AttendanceReasonRequest request, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.CloseAsync(id, request, ct));
    }

    // ═══════════════════════════════ الأدلّة ═══════════════════════════════

    /// <summary>رفع دليل. الحجم والامتداد يُفحصان في الخدمة أيضًا، لا هنا وحدها.</summary>
    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        if (file is null || file.Length <= 0)
            return Problem(detail: "الملفّ مطلوب.", statusCode: StatusCodes.Status400BadRequest,
                type: "attendance.invalid");

        await using var stream = file.OpenReadStream();
        return FromResult(await _service.UploadAttachmentAsync(
            id, file.FileName, file.ContentType, file.Length, stream, ct));
    }

    /// <summary>تنزيل دليل — مرفق إجباريّ دائمًا فلا يُنفَّذ أيّ محتوى في المتصفّح.</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        if (Disabled) return NotFound();

        var result = await _service.DownloadAttachmentAsync(id, attachmentId, ct);
        if (!result.Succeeded) return ToProblem(result);

        var file = result.Value!;
        return File(file.Content, file.ContentType, file.FileName);
    }
}

/// <summary>حمولة انتقال بلا وسائط عدا ختم التزامن.</summary>
public sealed class ConcurrencyRequest
{
    public int ConcurrencyStamp { get; set; }
}
