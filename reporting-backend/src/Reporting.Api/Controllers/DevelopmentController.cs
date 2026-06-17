using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Development;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api")]
public class DevelopmentController : ApiControllerBase
{
    private readonly IDevelopmentService _service;

    public DevelopmentController(IDevelopmentService service) => _service = service;

    // ===== Training Needs =====
    [HttpPost("training-needs")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> CreateNeed([FromBody] CreateTrainingNeedRequest request, CancellationToken ct)
        => FromResult(await _service.CreateTrainingNeedAsync(request, ct));

    [HttpPut("training-needs/{id:guid}")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> UpdateNeed(Guid id, [FromBody] UpdateTrainingNeedRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateTrainingNeedAsync(id, request, ct));

    [HttpGet("training-needs/{id:guid}")]
    public async Task<IActionResult> GetNeed(Guid id, CancellationToken ct)
        => FromResult(await _service.GetTrainingNeedAsync(id, ct));

    [HttpGet("training-needs")]
    public async Task<IActionResult> ListNeeds([FromQuery] Guid? subjectUserId, [FromQuery] TrainingNeedStatus? status, CancellationToken ct)
        => FromResult(await _service.ListTrainingNeedsAsync(new TrainingNeedFilter(subjectUserId, status), ct));

    // ===== Improvement Plans =====
    [HttpPost("improvement-plans")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> CreatePlan([FromBody] CreateImprovementPlanRequest request, CancellationToken ct)
        => FromResult(await _service.CreateImprovementPlanAsync(request, ct));

    [HttpPut("improvement-plans/{id:guid}")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdateImprovementPlanRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateImprovementPlanAsync(id, request, ct));

    [HttpGet("improvement-plans/{id:guid}")]
    public async Task<IActionResult> GetPlan(Guid id, CancellationToken ct)
        => FromResult(await _service.GetImprovementPlanAsync(id, ct));

    [HttpGet("improvement-plans")]
    public async Task<IActionResult> ListPlans([FromQuery] Guid? subjectUserId, [FromQuery] ImprovementPlanStatus? status, CancellationToken ct)
        => FromResult(await _service.ListImprovementPlansAsync(new ImprovementPlanFilter(subjectUserId, status), ct));
}
