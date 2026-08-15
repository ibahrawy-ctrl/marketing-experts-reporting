using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Projects360;

namespace Reporting.Api.Controllers;

/// <summary>
/// أهداف المشروع (CPW-R3 · W5 · §9) — الهدف هو **الأب الإلزاميّ** لكلّ مؤشّر (D-02).
///
/// <para>
/// **مستويا الكتابة**: الإنشاء/التعديل/الحذف/التفعيل **بنيويّ** (أدوار الإدارة حصرًا)، بينما
/// <c>PATCH .../status</c> **تشغيليّ** فيُسمح به أيضًا لقائد الفريق ومدير الحساب المسؤولَين عن
/// هذا المشروع بعينه (D-07). التمييز يقع كلّه في طبقة الخدمة لا في سمة على الطريقة.
/// </para>
/// </summary>
[Authorize]
[Route("api/projects/{projectId:guid}/objectives")]
public class ProjectObjectivesController : ApiControllerBase
{
    private readonly IProjectObjectiveService _service;

    public ProjectObjectivesController(IProjectObjectiveService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => FromResult(await _service.ListAsync(projectId, includeInactive, ct));

    [HttpGet("{objectiveId:guid}")]
    public async Task<IActionResult> Get(Guid projectId, Guid objectiveId, CancellationToken ct)
        => FromResult(await _service.GetAsync(projectId, objectiveId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateProjectObjectiveRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(projectId, request, ct));

    [HttpPut("{objectiveId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid objectiveId, [FromBody] UpdateProjectObjectiveRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(projectId, objectiveId, request, ct));

    /// <summary>تحديث الحالة فقط — المستوى التشغيليّ (D-07).</summary>
    [HttpPatch("{objectiveId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid projectId, Guid objectiveId, [FromBody] UpdateProjectObjectiveStatusRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateStatusAsync(projectId, objectiveId, request, ct));

    [HttpPatch("{objectiveId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid projectId, Guid objectiveId, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(projectId, objectiveId, true, ct));

    [HttpPatch("{objectiveId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid projectId, Guid objectiveId, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(projectId, objectiveId, false, ct));

    /// <summary>حذف نهائيّ — يُرفَض بـ409 إن كان للهدف مؤشّرات، فلا يُيتَّم مؤشّر أبدًا.</summary>
    [HttpDelete("{objectiveId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid objectiveId, CancellationToken ct)
        => FromResult(await _service.DeleteAsync(projectId, objectiveId, ct));
}
