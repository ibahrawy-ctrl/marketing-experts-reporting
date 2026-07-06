using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Submissions;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api/submissions")]
public class SubmissionsController : ApiControllerBase
{
    private readonly ISubmissionService _service;

    public SubmissionsController(ISubmissionService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] SubmissionStatus? status, [FromQuery] string? periodKey,
        [FromQuery] Guid? submitterId, [FromQuery] Guid? teamId, [FromQuery] Guid? departmentId,
        CancellationToken ct)
        => FromResult(await _service.ListAsync(
            new SubmissionFilter(status, periodKey, submitterId, teamId, departmentId), ct));

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
        => FromResult(await _service.ListMineAsync(ct));

    [HttpGet("pending-approvals")]
    public async Task<IActionResult> PendingApprovals(CancellationToken ct)
        => FromResult(await _service.ListPendingApprovalsAsync(ct));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] SubmissionStatus? status, [FromQuery] string? periodKey,
        [FromQuery] Guid? submitterId, [FromQuery] Guid? teamId, [FromQuery] Guid? departmentId,
        CancellationToken ct)
        => FromResult(await _service.SummaryAsync(
            new SubmissionFilter(status, periodKey, submitterId, teamId, departmentId), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => FromResult(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> CreateOrGetDraft(CreateSubmissionRequest request, CancellationToken ct)
        => FromResult(await _service.CreateOrGetDraftAsync(request, ct));

    [HttpPut("{id:guid}/values")]
    public async Task<IActionResult> SaveValues(Guid id, SaveFieldValuesRequest request, CancellationToken ct)
        => FromResult(await _service.SaveFieldValuesAsync(id, request, ct));

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
        => FromResult(await _service.SubmitAsync(id, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDraft(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteDraftAsync(id, ct);
        return result.Succeeded ? NoContent() : ToProblem(result);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, ApprovalActionRequest request, CancellationToken ct)
        => FromResult(await _service.ApproveAsync(id, request, ct));

    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, ApprovalActionRequest request, CancellationToken ct)
        => FromResult(await _service.ReturnAsync(id, request, ct));

    [HttpPost("{id:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid id, ApprovalActionRequest request, CancellationToken ct)
        => FromResult(await _service.EscalateAsync(id, request, ct));
}
