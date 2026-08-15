using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Projects360;

namespace Reporting.Api.Controllers;

/// <summary>
/// **المخرَجات التعاقديّة** للمشروع (CPW-R3 · W5 · §15).
///
/// <para>
/// **المصطلح مقصود ولا يُختصَر**: المسار <c>contract-deliverables</c> لا <c>deliverables</c>،
/// لأنّ <c>api/projects/{id}/workstreams/{wid}/deliverables</c> **قائم مسبقًا** لمخرَج تيّار العمل
/// وهو مفهوم مختلف تمامًا. الاسمان المنفصلان يمنعان الالتباس والاصطدام معًا، ومجال الكتالوج
/// <c>contract_deliverable</c> منفصل بدوره عن <c>deliverable</c>.
/// </para>
///
/// <para>
/// **DEC-W4-04 — صفر سكيمة إضافيّة**: لا وزن جديد ولا ربط بالمهامّ ولا محرّك تنفيذ ولا مراحل
/// ولا مهامّ فرعيّة ولا أيّ حقل تقدّم يستلزم هجرة. هذه الواجهة تكشف ما بنته W3/W4 فقط.
/// </para>
/// </summary>
[Authorize]
[Route("api/projects/{projectId:guid}/contract-deliverables")]
public class ProjectContractDeliverablesController : ApiControllerBase
{
    private readonly IProjectContractDeliverableService _service;

    public ProjectContractDeliverablesController(IProjectContractDeliverableService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => FromResult(await _service.ListAsync(projectId, includeInactive, ct));

    /// <summary>أنواع المخرَجات المتاحة من الكتالوج — تبني منها الواجهة قائمتها بلا رموز مكتوبة يدويًّا.</summary>
    [HttpGet("types")]
    public async Task<IActionResult> Types(Guid projectId, CancellationToken ct)
        => FromResult(await _service.ListTypesAsync(projectId, ct));

    [HttpGet("{deliverableId:guid}")]
    public async Task<IActionResult> Get(Guid projectId, Guid deliverableId, CancellationToken ct)
        => FromResult(await _service.GetAsync(projectId, deliverableId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateProjectContractDeliverableRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(projectId, request, ct));

    [HttpPut("{deliverableId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid deliverableId, [FromBody] UpdateProjectContractDeliverableRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(projectId, deliverableId, request, ct));

    /// <summary>تحديث التقدّم/الحالة — المستوى التشغيليّ (D-07).</summary>
    [HttpPatch("{deliverableId:guid}/progress")]
    public async Task<IActionResult> UpdateProgress(Guid projectId, Guid deliverableId, [FromBody] UpdateProjectContractDeliverableProgressRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateProgressAsync(projectId, deliverableId, request, ct));

    [HttpPatch("{deliverableId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid projectId, Guid deliverableId, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(projectId, deliverableId, true, ct));

    [HttpPatch("{deliverableId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid projectId, Guid deliverableId, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(projectId, deliverableId, false, ct));
}
