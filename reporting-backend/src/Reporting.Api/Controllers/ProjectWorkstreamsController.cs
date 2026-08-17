using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Clients;
using Reporting.Application.Common;

namespace Reporting.Api.Controllers;

/// <summary>
/// تيّارات العمل (أهداف العمل) داخل المشروع (P1 — منصّة التنفيذ العامة).
/// القراءة والكتابة كلاهما مُنَطَّق بنطاق المشروع داخل الخدمة (IClientProjectAccess + CanManagePlanAsync):
/// الكتابة مسموحة لأدوار إدارة الخطّة (ProjectPlanManagers) أو لمالك المشروع أو قائد فريقه أو مدير حسابه
/// المسنَد على هذا المشروع بعينه، لا بالدور وحده، لذا لا تُوضع سياسة ManagementOnly على الكتابة.
/// لا حذف نهائيّ — تفعيل/تعطيل فقط. **تخطيط فقط بلا تنفيذ.**
/// </summary>
[Authorize]
[Route("api/projects/{projectId:guid}/workstreams")]
public class ProjectWorkstreamsController : ApiControllerBase
{
    private readonly IProjectWorkstreamService _service;

    public ProjectWorkstreamsController(IProjectWorkstreamService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => FromResult(await _service.ListAsync(projectId, includeInactive, ct));

    // الكتابة: بلا سياسة دور على الـController — الخدمة تفرض النطاق (CanManagePlanAsync) لتشمل مدير الحسابات.
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateProjectWorkstreamRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(projectId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid id, [FromBody] UpdateProjectWorkstreamRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(projectId, id, request, ct));

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid projectId, Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(projectId, id, true, ct));

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid projectId, Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(projectId, id, false, ct));
}
