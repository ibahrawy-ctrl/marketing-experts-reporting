using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Projects360;

namespace Reporting.Api.Controllers;

/// <summary>
/// مخاطر المشروع وقراراته وملاحظاته الإداريّة (CPW-R3 · W5 · §17-19) — **قراءة فقط**.
///
/// <para>
/// **صفر تغيير على الحوكمة**: لا كتابة ولا سير عمل ولا حالة ولا صلاحيّة جديدة من هنا. مسارات
/// الحوكمة القائمة (<c>api/governance/**</c> و<c>api/management-notes</c>) لم تُمَسّ بحرف،
/// وهذه المسارات ترشيح مشروعيّ فوقها يمرّ ببوّابة رؤية Project 360 نفسها.
/// </para>
/// </summary>
[Authorize]
[Route("api/projects/{projectId:guid}")]
public class ProjectGovernanceReadController : ApiControllerBase
{
    private readonly IProjectGovernanceReadService _service;

    public ProjectGovernanceReadController(IProjectGovernanceReadService service) => _service = service;

    [HttpGet("risks")]
    public async Task<IActionResult> Risks(Guid projectId, [FromQuery] bool openOnly = false, CancellationToken ct = default)
        => FromResult(await _service.ListRisksAsync(projectId, openOnly, ct));

    [HttpGet("decisions")]
    public async Task<IActionResult> Decisions(Guid projectId, [FromQuery] bool pendingOnly = false, CancellationToken ct = default)
        => FromResult(await _service.ListDecisionsAsync(projectId, pendingOnly, ct));

    [HttpGet("notes")]
    public async Task<IActionResult> Notes(Guid projectId, [FromQuery] bool openOnly = false, CancellationToken ct = default)
        => FromResult(await _service.ListNotesAsync(projectId, openOnly, ct));
}
