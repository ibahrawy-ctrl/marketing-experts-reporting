using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

/// <summary>ورشة الحوكمة العامة (GOV-GOVERNANCE-UX1): إدارة بنود الحوكمة وخطها الزمني. كلها محكومة بسياسة GovernanceWorkspaceAccess.</summary>
[Authorize(Policy = Policies.GovernanceWorkspaceAccess)]
[Route("api/governance/items")]
public class GovernanceItemsController : ApiControllerBase
{
    private readonly IGovernanceItemService _service;

    public GovernanceItemsController(IGovernanceItemService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] GovernanceItemStatus? status,
        [FromQuery] GovernanceCategory? category,
        [FromQuery] GovernanceSeverity? severity,
        [FromQuery] Guid? assignedToUserId,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? teamId,
        [FromQuery] bool openOnly,
        CancellationToken ct)
        => FromResult(await _service.ListAsync(
            new GovernanceItemFilter(status, category, severity, assignedToUserId, departmentId, teamId, openOnly), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => FromResult(await _service.GetAsync(id, ct));

    /// <summary>دليل ورشة الحوكمة الموحّد: قوائم اختيار المُسنَد إليه/المتعلَّق ضمن نطاق الملكية (GOV-DIRECTORY-SCOPE-FIX-R1).</summary>
    [HttpGet("directory")]
    public async Task<IActionResult> Directory(CancellationToken ct)
        => FromResult(await _service.GetDirectoryAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGovernanceItemRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGovernanceItemRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeGovernanceItemStatusRequest request, CancellationToken ct)
        => FromResult(await _service.ChangeStatusAsync(id, request, ct));

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddGovernanceItemCommentRequest request, CancellationToken ct)
        => FromResult(await _service.AddCommentAsync(id, request, ct));
}
