using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

/// <summary>
/// التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1): إدارة التصعيدات الفردية وخطّها الزمني. كيان مستقلّ تمامًا عن بنود الحوكمة
/// العامة ولا يمسّ سير اعتماد التقارير. كلها محكومة بسياسة GovernanceEscalationAccess، والرؤية/الصلاحيات الدقيقة تُفرَض
/// داخل الخدمة (القراءة غير المصرّح بها تُقنَّع كـ«غير موجود» 404 لا 403).
/// </summary>
[Authorize(Policy = Policies.GovernanceEscalationAccess)]
[Route("api/governance/escalations")]
public class GovernanceEscalationsController : ApiControllerBase
{
    private readonly IGovernanceEscalationService _service;

    public GovernanceEscalationsController(IGovernanceEscalationService service) => _service = service;

    [HttpGet("target-directory")]
    public async Task<IActionResult> TargetDirectory(CancellationToken ct)
        => FromResult(await _service.GetTargetDirectoryAsync(ct));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] GovernanceEscalationStatus? status,
        [FromQuery] EscalationType? type,
        [FromQuery] EscalationSeverity? severity,
        [FromQuery] EscalationTargetType? targetType,
        [FromQuery] Guid? assignedToUserId,
        [FromQuery] Guid? raisedByUserId,
        [FromQuery] Guid? targetUserId,
        [FromQuery] Guid? targetDepartmentId,
        [FromQuery] Guid? targetTeamId,
        [FromQuery] bool openOnly,
        [FromQuery] bool mine,
        [FromQuery] bool assignedToMe,
        CancellationToken ct)
        => FromResult(await _service.ListAsync(
            new GovernanceEscalationFilter(
                status, type, severity, targetType, assignedToUserId, raisedByUserId,
                targetUserId, targetDepartmentId, targetTeamId, openOnly, mine, assignedToMe), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGovernanceEscalationRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGovernanceEscalationRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeGovernanceEscalationStatusRequest request, CancellationToken ct)
        => FromResult(await _service.ChangeStatusAsync(id, request, ct));

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignGovernanceEscalationRequest request, CancellationToken ct)
        => FromResult(await _service.AssignAsync(id, request, ct));

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddGovernanceEscalationCommentRequest request, CancellationToken ct)
        => FromResult(await _service.AddCommentAsync(id, request, ct));

    [HttpPost("{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid id, [FromBody] ReopenGovernanceEscalationRequest request, CancellationToken ct)
        => FromResult(await _service.ReopenAsync(id, request, ct));

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseGovernanceEscalationRequest request, CancellationToken ct)
        => FromResult(await _service.CloseAsync(id, request, ct));

    // TODO (GOV-ACTION-ITEMS-R1): POST /{id:guid}/action-items — بنود الإجراءات التصحيحية المرتبطة بالتصعيد (مرحلة لاحقة).
}
