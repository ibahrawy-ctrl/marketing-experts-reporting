using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api")]
public class GovernanceController : ApiControllerBase
{
    private readonly IGovernanceService _service;

    public GovernanceController(IGovernanceService service) => _service = service;

    // ===== Risks =====
    [HttpPost("risks")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> CreateRisk([FromBody] CreateRiskRequest request, CancellationToken ct)
        => FromResult(await _service.CreateRiskAsync(request, ct));

    [HttpPut("risks/{id:guid}")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> UpdateRisk(Guid id, [FromBody] UpdateRiskRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateRiskAsync(id, request, ct));

    [HttpGet("risks/{id:guid}")]
    public async Task<IActionResult> GetRisk(Guid id, CancellationToken ct)
        => FromResult(await _service.GetRiskAsync(id, ct));

    [HttpGet("risks")]
    public async Task<IActionResult> ListRisks([FromQuery] RiskStatus? status, [FromQuery] RiskSeverity? severity,
        [FromQuery] Guid? departmentId, [FromQuery] Guid? ownerId, CancellationToken ct)
        => FromResult(await _service.ListRisksAsync(new RiskFilter(status, severity, departmentId, ownerId), ct));

    // ===== Escalations =====
    [HttpPost("escalations")]
    public async Task<IActionResult> CreateEscalation([FromBody] CreateEscalationRequest request, CancellationToken ct)
        => FromResult(await _service.CreateEscalationAsync(request, ct));

    [HttpPost("escalations/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveEscalation(Guid id, [FromBody] ResolveEscalationRequest request, CancellationToken ct)
        => FromResult(await _service.ResolveEscalationAsync(id, request, ct));

    [HttpGet("escalations/{id:guid}")]
    public async Task<IActionResult> GetEscalation(Guid id, CancellationToken ct)
        => FromResult(await _service.GetEscalationAsync(id, ct));

    [HttpGet("escalations")]
    public async Task<IActionResult> ListEscalations([FromQuery] EscalationStatus? status,
        [FromQuery] Guid? targetUserId, [FromQuery] Guid? raisedById, CancellationToken ct)
        => FromResult(await _service.ListEscalationsAsync(new EscalationFilter(status, targetUserId, raisedById), ct));

    // ===== Decisions =====
    [HttpPost("decisions")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> CreateDecision([FromBody] CreateDecisionRequest request, CancellationToken ct)
        => FromResult(await _service.CreateDecisionAsync(request, ct));

    [HttpPut("decisions/{id:guid}")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> UpdateDecision(Guid id, [FromBody] UpdateDecisionRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateDecisionAsync(id, request, ct));

    [HttpGet("decisions/{id:guid}")]
    public async Task<IActionResult> GetDecision(Guid id, CancellationToken ct)
        => FromResult(await _service.GetDecisionAsync(id, ct));

    [HttpGet("decisions")]
    public async Task<IActionResult> ListDecisions([FromQuery] DecisionStatus? status, CancellationToken ct)
        => FromResult(await _service.ListDecisionsAsync(new DecisionFilter(status), ct));
}
