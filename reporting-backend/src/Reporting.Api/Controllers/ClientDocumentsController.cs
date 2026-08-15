using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Reporting.Application.Documents;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

/// <summary>
/// مستندات العميل (CPW-R1B2 — خدمة المستندات). القراءة محكومة بنطاق رؤية العميل داخل الخدمة،
/// والكتابة Resource-Based (مدير الحساب للعميل أو مدير أساسيّ يراه)، لذا لا سياسة دور على الـController.
/// <para>
/// ثوابت ملزِمة: التنزيل عبر نقطة نهاية مصادَقة ومحكومة فقط — لا روابط موقَّعة ولا Static Files (C-02)؛
/// الحذف Tombstone بسبب إلزاميّ؛ <c>StorageKey</c> لا يظهر في أيّ استجابة؛
/// حدّ حجم الطلب وحدّ معدّل الرفع مفروضان على مسارات الرفع (C-04).
/// </para>
/// </summary>
[Authorize]
[Route("api/clients/{clientId:guid}/documents")]
public class ClientDocumentsController : ApiControllerBase
{
    /// <summary>
    /// سقف نقل صلب يفوق الحدّ المُعَدّ (25MB افتراضًا) بهامش المغلّف متعدّد الأجزاء.
    /// الحدّ الفعليّ الملزِم يبقى <c>FileStorage:MaxUploadSizeBytes</c> داخل الخدمة.
    /// </summary>
    private const long TransportSizeCeiling = 32L * 1024 * 1024;

    private readonly IClientDocumentService _service;

    public ClientDocumentsController(IClientDocumentService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(
        Guid clientId,
        [FromQuery] string? categoryCode,
        [FromQuery] string? confidentialityCode,
        [FromQuery] DocumentLifecycleStatus? lifecycleStatus,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = false,
        CancellationToken ct = default)
        => FromResult(await _service.ListAsync(clientId,
            new ClientDocumentFilter(categoryCode, confidentialityCode, lifecycleStatus, search, includeArchived), ct));

    [HttpGet("storage-usage")]
    public async Task<IActionResult> StorageUsage(Guid clientId, CancellationToken ct)
        => FromResult(await _service.GetStorageUsageAsync(clientId, ct));

    [HttpGet("{documentId:guid}")]
    public async Task<IActionResult> Get(Guid clientId, Guid documentId, CancellationToken ct)
        => FromResult(await _service.GetAsync(clientId, documentId, ct));

    /// <summary>رفع مستند جديد بنسخته الأولى (multipart/form-data).</summary>
    [HttpPost]
    [EnableRateLimiting("document-upload")]
    [RequestSizeLimit(TransportSizeCeiling)]
    public async Task<IActionResult> Create(
        Guid clientId,
        [FromForm] CreateClientDocumentForm form,
        CancellationToken ct)
    {
        var request = new CreateClientDocumentRequest(
            form.Title ?? string.Empty,
            form.CategoryCode ?? string.Empty,
            form.Description,
            form.Tags,
            form.ConfidentialityCode,
            form.ChangeNote,
            form.VisibilityType,
            form.AllowedRoles,
            form.AllowedUserIds);

        return await WithUploadAsync(form.File, upload => _service.CreateAsync(clientId, request, upload, ct));
    }

    /// <summary>إضافة نسخة أحدث لمستند قائم؛ السابقة تصبح <c>Superseded</c> ولا تُحذف.</summary>
    [HttpPost("{documentId:guid}/versions")]
    [EnableRateLimiting("document-upload")]
    [RequestSizeLimit(TransportSizeCeiling)]
    public async Task<IActionResult> AddVersion(
        Guid clientId,
        Guid documentId,
        [FromForm] AddDocumentVersionForm form,
        CancellationToken ct)
    {
        var request = new AddDocumentVersionRequest(form.ChangeNote);
        return await WithUploadAsync(form.File, upload => _service.AddVersionAsync(clientId, documentId, request, upload, ct));
    }

    /// <summary>تعديل البيانات الوصفيّة فقط — لا يمسّ أيّ ملفّ.</summary>
    [HttpPut("{documentId:guid}")]
    public async Task<IActionResult> Update(Guid clientId, Guid documentId, [FromBody] UpdateClientDocumentRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(clientId, documentId, request, ct));

    [HttpPatch("{documentId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid clientId, Guid documentId, [FromBody] ArchiveClientDocumentRequest request, CancellationToken ct)
        => FromResult(await _service.SetArchivedAsync(clientId, documentId, true, request, ct));

    [HttpPatch("{documentId:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid clientId, Guid documentId, [FromBody] ArchiveClientDocumentRequest request, CancellationToken ct)
        => FromResult(await _service.SetArchivedAsync(clientId, documentId, false, request, ct));

    /// <summary>حذف منطقيّ (Tombstone) بسبب إلزاميّ — الصفّ والملفّ يبقيان.</summary>
    [HttpPost("{documentId:guid}/delete")]
    public async Task<IActionResult> Delete(Guid clientId, Guid documentId, [FromBody] DeleteClientDocumentRequest request, CancellationToken ct)
        => FromResult(await _service.DeleteAsync(clientId, documentId, request, ct));

    /// <summary>تنزيل النسخة السارية — نقطة نهاية مصادَقة ومُدقَّقة (C-02).</summary>
    [HttpGet("{documentId:guid}/download")]
    public Task<IActionResult> Download(Guid clientId, Guid documentId, CancellationToken ct)
        => DownloadCoreAsync(clientId, documentId, null, ct);

    /// <summary>تنزيل نسخة محدّدة من تاريخ المستند.</summary>
    [HttpGet("{documentId:guid}/versions/{versionId:guid}/download")]
    public Task<IActionResult> DownloadVersion(Guid clientId, Guid documentId, Guid versionId, CancellationToken ct)
        => DownloadCoreAsync(clientId, documentId, versionId, ct);

    // ===== المساعدات =====

    private async Task<IActionResult> DownloadCoreAsync(Guid clientId, Guid documentId, Guid? versionId, CancellationToken ct)
    {
        var result = await _service.DownloadAsync(clientId, documentId, versionId, ct);
        if (!result.Succeeded) return ToProblem(result);

        var file = result.Value!;
        // ملفّ قابل للتنفيذ في المتصفّح (SVG/HTML/…) أو مجهول الامتداد ⇒ مرفق إجباريّ.
        if (file.ForceAttachment)
            return File(file.Content, file.ContentType, file.FileName);

        Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(file.FileName)}";
        return File(file.Content, file.ContentType);
    }

    /// <summary>يحوّل <c>IFormFile</c> إلى حمولة محايدة ويضمن إغلاق التدفّق بعد انتهاء الخدمة.</summary>
    private async Task<IActionResult> WithUploadAsync<T>(
        IFormFile? file,
        Func<DocumentUploadContent, Task<Reporting.Application.Common.Result<T>>> action)
    {
        if (file is null || file.Length <= 0)
            return Problem(detail: "الملفّ مطلوب.", statusCode: StatusCodes.Status400BadRequest, type: "document.file_required");

        await using var stream = file.OpenReadStream();
        var upload = new DocumentUploadContent(stream, file.FileName, file.ContentType, file.Length);
        return FromResult(await action(upload));
    }
}

/// <summary>نموذج رفع مستند جديد (multipart) — الحقول النصّيّة + الملفّ.</summary>
public class CreateClientDocumentForm
{
    public string? Title { get; set; }
    public string? CategoryCode { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public string? ConfidentialityCode { get; set; }
    public string? ChangeNote { get; set; }

    /// <summary>سياسة الرؤية (CPW-R2). عند غيابها تُطبَّق السياسة الافتراضيّة للتصنيف.</summary>
    public DocumentVisibilityType? VisibilityType { get; set; }

    /// <summary>أسماء الأدوار المصرّح لها — تُستعمل فقط مع <c>CustomRoles</c>.</summary>
    public List<string>? AllowedRoles { get; set; }

    /// <summary>معرّفات المستخدمين المصرّح لهم — تُستعمل فقط مع <c>CustomUsers</c>.</summary>
    public List<Guid>? AllowedUserIds { get; set; }

    public IFormFile? File { get; set; }
}

/// <summary>نموذج إضافة نسخة (multipart).</summary>
public class AddDocumentVersionForm
{
    public string? ChangeNote { get; set; }
    public IFormFile? File { get; set; }
}
