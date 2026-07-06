using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Leave;

namespace Reporting.Api.Controllers;

/// <summary>
/// طلبات الإجازة والاستئذان (V1.0.1). الفرض النهائي للصلاحيات والنطاق في طبقة الخدمة؛
/// السياسات هنا طبقة دفاع أولى: المراجعة لمستوى الإدارة، والاعتماد النهائي لقدرة LeaveFinalApproval.
/// </summary>
[Authorize]
[Route("api/leave-requests")]
public class LeaveRequestsController : ApiControllerBase
{
    private readonly ILeaveRequestService _service;

    public LeaveRequestsController(ILeaveRequestService service) => _service = service;

    // ===== الموظّف =====

    [HttpGet("my")]
    public async Task<IActionResult> My(CancellationToken ct)
        => FromResult(await _service.GetMyAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLeaveRequestRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(request, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => FromResult(await _service.CancelAsync(id, ct));

    // ===== المراجعة (الإدارة) =====

    [HttpGet("pending")]
    [Authorize(Policy = Policies.LeaveReview)]
    public async Task<IActionResult> Pending(CancellationToken ct)
        => FromResult(await _service.GetPendingAsync(ct));

    [HttpPost("{id:guid}/team-leader/approve")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> TeamLeaderApprove(Guid id, [FromBody] LeaveApproveRequest request, CancellationToken ct)
        => FromResult(await _service.TeamLeaderApproveAsync(id, request, ct));

    [HttpPost("{id:guid}/team-leader/reject")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> TeamLeaderReject(Guid id, [FromBody] LeaveRejectRequest request, CancellationToken ct)
        => FromResult(await _service.TeamLeaderRejectAsync(id, request, ct));

    [HttpPost("{id:guid}/manager/approve")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> ManagerApprove(Guid id, [FromBody] LeaveApproveRequest request, CancellationToken ct)
        => FromResult(await _service.ManagerApproveAsync(id, request, ct));

    [HttpPost("{id:guid}/manager/reject")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> ManagerReject(Guid id, [FromBody] LeaveRejectRequest request, CancellationToken ct)
        => FromResult(await _service.ManagerRejectAsync(id, request, ct));

    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> Return(Guid id, [FromBody] LeaveReturnRequest request, CancellationToken ct)
        => FromResult(await _service.ReturnAsync(id, request, ct));

    // ===== الاعتماد النهائي (الموارد البشرية / من يقوم مقامها) =====

    [HttpPost("{id:guid}/hr/approve")]
    [Authorize(Policy = Policies.LeaveFinalApproval)]
    public async Task<IActionResult> HrApprove(Guid id, [FromBody] LeaveApproveRequest request, CancellationToken ct)
        => FromResult(await _service.HrApproveAsync(id, request, ct));

    [HttpPost("{id:guid}/hr/reject")]
    [Authorize(Policy = Policies.LeaveFinalApproval)]
    public async Task<IActionResult> HrReject(Guid id, [FromBody] LeaveRejectRequest request, CancellationToken ct)
        => FromResult(await _service.HrRejectAsync(id, request, ct));

    // ===== إبطال إجازة معتمَدة نهائيًّا (V1.1 — مسار محروس للإدارة/HR، يُنشئ عكسًا للخصم الآلي) =====

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = Policies.BalanceManagement)]
    public async Task<IActionResult> RevokeApproved(Guid id, [FromBody] LeaveRevokeRequest request, CancellationToken ct)
        => FromResult(await _service.RevokeApprovedAsync(id, request, ct));

    // ===== معالجة الطلبات العالقة لقادة الفِرق (T-WF1 — Admin فقط، idempotent) =====

    [HttpPost("remediate-team-leader-stuck")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> RemediateTeamLeaderStuck(CancellationToken ct)
        => FromResult(await _service.RemediateTeamLeaderStuckRequestsAsync(ct));
}
