using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api/report-templates")]
public class ReportTemplatesController : ApiControllerBase
{
    private readonly IReportTemplateService _service;

    public ReportTemplatesController(IReportTemplateService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? jobRoleId, [FromQuery] TemplateStatus? status,
        [FromQuery] bool? isActive, [FromQuery] bool assignedOnly, [FromQuery] Guid? subjectUserId, CancellationToken ct)
        => FromResult(await _service.ListAsync(new TemplateFilter(jobRoleId, status, isActive, assignedOnly, subjectUserId), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => FromResult(await _service.GetAsync(id, ct));

    // معاينة القالب كما يراه الموظّف (قراءة فقط، بلا إنشاء تسليم) — لحوكمة القوالب فقط.
    [HttpGet("{id:guid}/preview")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
        => FromResult(await _service.PreviewAsync(id, ct));

    // تغطية القالب: المرتبطون والمستثنون بأسبابهم — لحوكمة القوالب فقط.
    [HttpGet("{id:guid}/assignments")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Assignments(Guid id, CancellationToken ct)
        => FromResult(await _service.GetAssignmentsAsync(id, ct));

    // إسناد/استثناء صريح للقالب (Employee/JobRole/Team/Department) — لحوكمة القوالب فقط.
    [HttpPost("{id:guid}/assignments")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> AddAssignment(Guid id, CreateAssignmentRequest request, CancellationToken ct)
        => FromResult(await _service.AddAssignmentAsync(id, request, ct));

    // تعطيل/تفعيل إسناد قائم + تعديل الملاحظة — لحوكمة القوالب فقط.
    [HttpPut("{templateId:guid}/assignments/{assignmentId:guid}")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> UpdateAssignment(Guid templateId, Guid assignmentId, UpdateAssignmentRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAssignmentAsync(templateId, assignmentId, request, ct));

    // حذف إسناد صريح — لحوكمة القوالب فقط.
    [HttpDelete("{templateId:guid}/assignments/{assignmentId:guid}")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> RemoveAssignment(Guid templateId, Guid assignmentId, CancellationToken ct)
        => FromResult(await _service.RemoveAssignmentAsync(templateId, assignmentId, ct));

    [HttpPost]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Create(CreateTemplateRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(request, CurrentUserId, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Update(Guid id, UpdateTemplateRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateMetadataAsync(id, request, ct));

    // الأرشفة: تُخفي القالب من إنشاء التقارير الجديدة دون المساس بالتقارير القديمة.
    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
        => FromResult(await _service.ArchiveAsync(id, ct));

    // الحذف النهائي: مسموح فقط لقالب مسودة غير مستخدَم؛ غير ذلك يُرجَع تعارض يوجّه للأرشفة.
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await _service.DeleteAsync(id, ct));

    [HttpPost("versions/{versionId:guid}/fields")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> AddField(Guid versionId, UpsertFieldRequest request, CancellationToken ct)
        => FromResult(await _service.AddFieldAsync(versionId, request, ct));

    [HttpPut("fields/{fieldId:guid}")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> UpdateField(Guid fieldId, UpsertFieldRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateFieldAsync(fieldId, request, ct));

    [HttpDelete("fields/{fieldId:guid}")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> DeleteField(Guid fieldId, CancellationToken ct)
        => FromResult(await _service.DeleteFieldAsync(fieldId, ct));

    [HttpPost("versions/{versionId:guid}/reorder")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Reorder(Guid versionId, [FromBody] IReadOnlyList<Guid> orderedFieldIds, CancellationToken ct)
        => FromResult(await _service.ReorderFieldsAsync(versionId, orderedFieldIds, ct));

    [HttpPost("versions/{versionId:guid}/publish")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Publish(Guid versionId, CancellationToken ct)
        => FromResult(await _service.PublishVersionAsync(versionId, CurrentUserId, ct));

    [HttpPost("{templateId:guid}/versions")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> CreateDraftVersion(Guid templateId, CancellationToken ct)
        => FromResult(await _service.CreateDraftVersionAsync(templateId, ct));

    [HttpDelete("versions/{versionId:guid}")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> DeleteVersion(Guid versionId, CancellationToken ct)
    {
        var result = await _service.DeleteVersionAsync(versionId, ct);
        return result.Succeeded ? NoContent() : ToProblem(result);
    }
}
