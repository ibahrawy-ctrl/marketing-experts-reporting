using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Clients;
using Reporting.Application.Common;

namespace Reporting.Api.Controllers;

/// <summary>
/// مخرَجات خطّة الإنتاج داخل هدف العمل (P2 — منصّة التنفيذ العامة).
/// القراءة والكتابة كلاهما مُنَطَّق بنطاق المشروع داخل الخدمة (IClientProjectAccess + CanManagePlanAsync):
/// الكتابة مسموحة لأدوار الإدارة (ProjectPlanManagers) أو لمدير حسابات المشروع نفسه، لا بالدور وحده،
/// لذا لا تُوضع سياسة ManagementOnly على الكتابة (كانت تحجب مدير الحسابات). لا حذف نهائيّ — تفعيل/تعطيل فقط. **تخطيط فقط بلا تنفيذ.**
/// </summary>
[Authorize]
[Route("api/projects/{projectId:guid}/workstreams/{workstreamId:guid}/deliverables")]
public class WorkstreamDeliverablesController : ApiControllerBase
{
    private readonly IWorkstreamDeliverableService _service;

    public WorkstreamDeliverablesController(IWorkstreamDeliverableService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, Guid workstreamId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => FromResult(await _service.ListAsync(projectId, workstreamId, includeInactive, ct));

    // الكتابة: بلا سياسة دور على الـController — الخدمة تفرض النطاق (CanManagePlanAsync) لتشمل مدير الحسابات.
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, Guid workstreamId, [FromBody] CreateWorkstreamDeliverableRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(projectId, workstreamId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid workstreamId, Guid id, [FromBody] UpdateWorkstreamDeliverableRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(projectId, workstreamId, id, request, ct));

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid projectId, Guid workstreamId, Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(projectId, workstreamId, id, true, ct));

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid projectId, Guid workstreamId, Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(projectId, workstreamId, id, false, ct));
}
