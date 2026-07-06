using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

/// <summary>
/// طلبات الموارد البشرية العامة (V1.1 — خدمات الموظف). الموظّف ينشئ/يرى/يلغي طلباته؛
/// الإدارة/HR (HrRequestManagement) ترى الكل وتعالج. الفرض النهائي للصلاحية في طبقة الخدمة.
/// </summary>
[Authorize]
[Route("api/employee-service-requests")]
public class EmployeeServiceRequestsController : ApiControllerBase
{
    private readonly IEmployeeServiceRequestService _service;

    public EmployeeServiceRequestsController(IEmployeeServiceRequestService service) => _service = service;

    [HttpGet("my")]
    public async Task<IActionResult> My(CancellationToken ct)
        => FromResult(await _service.GetMyAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeServiceRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(request, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => FromResult(await _service.CancelAsync(id, ct));

    // ===== معالجة الموارد البشرية =====

    [HttpGet]
    [Authorize(Policy = Policies.HrRequestManagement)]
    public async Task<IActionResult> List(
        [FromQuery] EmployeeServiceRequestType? type, [FromQuery] EmployeeServiceRequestStatus? status,
        [FromQuery] string? q, [FromQuery] Guid? userId, CancellationToken ct)
        => FromResult(await _service.ListAsync(type, status, q, userId, ct));

    [HttpPost("{id:guid}/start-review")]
    [Authorize(Policy = Policies.HrRequestManagement)]
    public async Task<IActionResult> StartReview(Guid id, CancellationToken ct)
        => FromResult(await _service.StartReviewAsync(id, ct));

    [HttpPost("{id:guid}/comment")]
    [Authorize(Policy = Policies.HrRequestManagement)]
    public async Task<IActionResult> Comment(Guid id, [FromBody] EmployeeServiceRequestCommentRequest request, CancellationToken ct)
        => FromResult(await _service.CommentAsync(id, request, ct));

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Policies.HrRequestManagement)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] EmployeeServiceRequestCompleteRequest request, CancellationToken ct)
        => FromResult(await _service.CompleteAsync(id, request, ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Policies.HrRequestManagement)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] EmployeeServiceRequestRejectRequest request, CancellationToken ct)
        => FromResult(await _service.RejectAsync(id, request, ct));

    // ===== الملف النهائي (PDF) =====

    /// <summary>رفع الملف النهائي (PDF، ≤ 10MB) — HrRequestManagement. يُخزَّن خارج جذر الويب.</summary>
    [HttpPost("{id:guid}/final-document")]
    [Authorize(Policy = Policies.HrRequestManagement)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> UploadFinalDocument(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null)
            return FromResult(await _service.UploadFinalDocumentAsync(id,
                new FinalDocumentUpload(string.Empty, null, 0, Stream.Null), ct));

        await using var stream = file.OpenReadStream();
        var upload = new FinalDocumentUpload(file.FileName, file.ContentType, file.Length, stream);
        return FromResult(await _service.UploadFinalDocumentAsync(id, upload, ct));
    }

    /// <summary>تنزيل الملف النهائي — لصاحب الطلب أو HrRequestManagement. الفرض في طبقة الخدمة.</summary>
    [HttpGet("{id:guid}/final-document")]
    public async Task<IActionResult> DownloadFinalDocument(Guid id, CancellationToken ct)
    {
        var result = await _service.DownloadFinalDocumentAsync(id, ct);
        if (!result.Succeeded) return ToProblem(result);
        var doc = result.Value!;
        return PhysicalFile(doc.PhysicalPath, doc.ContentType, doc.DownloadFileName);
    }
}
