using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

/// <summary>
/// إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1): تحويل أي تصعيد/بند حوكمة/ملاحظة يدوية إلى إجراء قابل للتتبّع
/// (مُسنَد إليه + استحقاق + أولوية + حالة + خطّ زمني). كيان مستقلّ تمامًا ولا يمسّ سير اعتماد التقارير. كلها محكومة بسياسة
/// GovernanceActionItemAccess، والرؤية/الصلاحيات الدقيقة تُفرَض داخل الخدمة (القراءة غير المصرّح بها تُقنَّع كـ«غير موجود» 404 لا 403).
/// لا إشعارات/بريد في هذه المرحلة.
/// </summary>
[Authorize(Policy = Policies.GovernanceActionItemAccess)]
[Route("api/governance/action-items")]
public class GovernanceActionItemsController : ApiControllerBase
{
    private readonly IGovernanceActionItemService _service;

    public GovernanceActionItemsController(IGovernanceActionItemService service) => _service = service;

    [HttpGet("assignee-directory")]
    public async Task<IActionResult> AssigneeDirectory(CancellationToken ct)
        => FromResult(await _service.GetAssigneeDirectoryAsync(ct));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ActionItemStatus? status,
        [FromQuery] Guid? assignedToUserId,
        [FromQuery] ActionItemSourceType? sourceType,
        [FromQuery] Guid? sourceId,
        [FromQuery] ActionItemPriority? priority,
        [FromQuery] DateOnly? dueFrom,
        [FromQuery] DateOnly? dueTo,
        [FromQuery] bool overdueOnly,
        [FromQuery] bool mineOnly,
        [FromQuery] bool assignedToMe,
        CancellationToken ct)
        => FromResult(await _service.ListAsync(
            new GovernanceActionItemFilter(
                status, assignedToUserId, sourceType, sourceId, priority,
                dueFrom, dueTo, overdueOnly, mineOnly, assignedToMe), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGovernanceActionItemRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(request, ct));

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeGovernanceActionItemStatusRequest request, CancellationToken ct)
        => FromResult(await _service.ChangeStatusAsync(id, request, ct));

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignGovernanceActionItemRequest request, CancellationToken ct)
        => FromResult(await _service.AssignAsync(id, request, ct));

    [HttpPost("{id:guid}/due-date")]
    public async Task<IActionResult> ChangeDueDate(Guid id, [FromBody] ChangeGovernanceActionItemDueDateRequest request, CancellationToken ct)
        => FromResult(await _service.ChangeDueDateAsync(id, request, ct));

    [HttpPost("{id:guid}/updates")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddGovernanceActionItemCommentRequest request, CancellationToken ct)
        => FromResult(await _service.AddCommentAsync(id, request, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelGovernanceActionItemRequest request, CancellationToken ct)
        => FromResult(await _service.CancelAsync(id, request, ct));
}
