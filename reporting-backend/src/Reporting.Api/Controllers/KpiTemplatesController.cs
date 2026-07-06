using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api/kpi-templates")]
public class KpiTemplatesController : ApiControllerBase
{
    private readonly IKpiTemplateService _service;

    public KpiTemplatesController(IKpiTemplateService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? jobRoleId, [FromQuery] KpiCadence? cadence,
        [FromQuery] TemplateStatus? status, [FromQuery] bool? isActive, [FromQuery] Guid? subjectUserId, CancellationToken ct)
        => FromResult(await _service.ListAsync(new KpiTemplateFilter(jobRoleId, cadence, status, isActive, subjectUserId), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => FromResult(await _service.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Create(CreateKpiTemplateRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(request, CurrentUserId, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Update(Guid id, UpdateKpiTemplateRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateMetadataAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
        => FromResult(await _service.ArchiveAsync(id, ct));

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
        => FromResult(await _service.ReactivateAsync(id, ct));

    [HttpPost("versions/{versionId:guid}/metrics")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> AddMetric(Guid versionId, UpsertKpiMetricRequest request, CancellationToken ct)
        => FromResult(await _service.AddMetricAsync(versionId, request, ct));

    [HttpPut("metrics/{metricId:guid}")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> UpdateMetric(Guid metricId, UpsertKpiMetricRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateMetricAsync(metricId, request, ct));

    [HttpDelete("metrics/{metricId:guid}")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> DeleteMetric(Guid metricId, CancellationToken ct)
        => FromResult(await _service.DeleteMetricAsync(metricId, ct));

    [HttpPost("versions/{versionId:guid}/publish")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> Publish(Guid versionId, CancellationToken ct)
        => FromResult(await _service.PublishVersionAsync(versionId, CurrentUserId, ct));

    [HttpPost("{templateId:guid}/versions")]
    [Authorize(Policy = Policies.TemplateGovernance)]
    public async Task<IActionResult> CreateDraftVersion(Guid templateId, CancellationToken ct)
        => FromResult(await _service.CreateDraftVersionAsync(templateId, ct));

    // ===== إسناد قوالب KPI (Phase T1) — إدارة الأدمن حصرًا =====
    [HttpGet("{id:guid}/assignments")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> GetAssignments(Guid id, CancellationToken ct)
        => FromResult(await _service.GetAssignmentsAsync(id, ct));

    [HttpPost("{id:guid}/assignments")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> AddAssignment(Guid id, CreateKpiAssignmentRequest request, CancellationToken ct)
        => FromResult(await _service.AddAssignmentAsync(id, request, ct));

    [HttpPut("{templateId:guid}/assignments/{assignmentId:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> UpdateAssignment(Guid templateId, Guid assignmentId, UpdateKpiAssignmentRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAssignmentAsync(templateId, assignmentId, request, ct));

    [HttpDelete("{templateId:guid}/assignments/{assignmentId:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> RemoveAssignment(Guid templateId, Guid assignmentId, CancellationToken ct)
        => FromResult(await _service.RemoveAssignmentAsync(templateId, assignmentId, ct));
}
