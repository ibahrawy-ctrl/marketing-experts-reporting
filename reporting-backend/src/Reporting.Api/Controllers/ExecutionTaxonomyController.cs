using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.ExecutionTaxonomy;

namespace Reporting.Api.Controllers;

/// <summary>
/// إدارة كتالوج تصنيفات التنفيذ (RC-4 Task 4D2) — الأدمن/CEO/GM عبر سياسة حوكمة القوالب.
/// قراءة/إنشاء/تعديل/تفعيل/تعطيل فقط — لا حذف نهائيّ.
/// </summary>
[Authorize(Policy = Policies.TemplateGovernance)]
[Route("api/execution-taxonomy")]
public class ExecutionTaxonomyController : ApiControllerBase
{
    private readonly IExecutionTaxonomyService _service;
    public ExecutionTaxonomyController(IExecutionTaxonomyService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? domain, [FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await _service.ListAsync(domain, includeInactive, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => FromResult(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExecutionTaxonomyRequest req, CancellationToken ct)
        => FromResult(await _service.CreateAsync(req, CurrentUserId, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExecutionTaxonomyRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(id, req, CurrentUserId, ct));

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(id, true, CurrentUserId, ct));

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(id, false, CurrentUserId, ct));
}
