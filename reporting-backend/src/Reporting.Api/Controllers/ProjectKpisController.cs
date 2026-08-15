using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Projects360;

namespace Reporting.Api.Controllers;

/// <summary>
/// مؤشّرات المشروع وقراءاتها اليدويّة (CPW-R3 · W5 · §11 + §13).
///
/// <para>
/// **PROJECT_LEVEL_KPI_CREATE_ROUTE = NONE (D-02)**: كلّ مسارات الإنشاء والتعديل متداخلة تحت
/// الهدف <c>.../objectives/{objectiveId}/kpis</c>. المسار المسطّح <c>.../projects/{id}/kpis</c>
/// موجود **للقراءة فقط** (لوحة المشروع) ولا يقبل <c>POST</c> إطلاقًا — فلا يمكن إنشاء مؤشّر يتيم
/// بلا هدف أب حتّى بالمحاولة المباشرة على المسار.
/// </para>
/// </summary>
[Authorize]
[Route("api/projects/{projectId:guid}")]
public class ProjectKpisController : ApiControllerBase
{
    private readonly IProjectKpiService _service;

    public ProjectKpisController(IProjectKpiService service) => _service = service;

    // ===== قراءة على مستوى المشروع — لا إنشاء بهذا المستوى =====

    /// <summary>كلّ مؤشّرات المشروع للوحة — <b>قراءة فقط</b>، ولا نظير <c>POST</c> لهذا المسار.</summary>
    [HttpGet("kpis")]
    public async Task<IActionResult> ListByProject(Guid projectId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => FromResult(await _service.ListByProjectAsync(projectId, includeInactive, ct));

    // ===== المسارات المتداخلة تحت الهدف =====

    [HttpGet("objectives/{objectiveId:guid}/kpis")]
    public async Task<IActionResult> ListByObjective(Guid projectId, Guid objectiveId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => FromResult(await _service.ListByObjectiveAsync(projectId, objectiveId, includeInactive, ct));

    [HttpGet("objectives/{objectiveId:guid}/kpis/{kpiId:guid}")]
    public async Task<IActionResult> Get(Guid projectId, Guid objectiveId, Guid kpiId, CancellationToken ct)
        => FromResult(await _service.GetAsync(projectId, objectiveId, kpiId, ct));

    [HttpPost("objectives/{objectiveId:guid}/kpis")]
    public async Task<IActionResult> Create(Guid projectId, Guid objectiveId, [FromBody] CreateProjectKpiRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(projectId, objectiveId, request, ct));

    [HttpPut("objectives/{objectiveId:guid}/kpis/{kpiId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid objectiveId, Guid kpiId, [FromBody] UpdateProjectKpiRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(projectId, objectiveId, kpiId, request, ct));

    [HttpPatch("objectives/{objectiveId:guid}/kpis/{kpiId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid projectId, Guid objectiveId, Guid kpiId, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(projectId, objectiveId, kpiId, true, ct));

    [HttpPatch("objectives/{objectiveId:guid}/kpis/{kpiId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid projectId, Guid objectiveId, Guid kpiId, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(projectId, objectiveId, kpiId, false, ct));

    // ===== §13 — القراءات اليدويّة المؤرَّخة =====

    [HttpGet("objectives/{objectiveId:guid}/kpis/{kpiId:guid}/readings")]
    public async Task<IActionResult> ListReadings(Guid projectId, Guid objectiveId, Guid kpiId, CancellationToken ct)
        => FromResult(await _service.ListReadingsAsync(projectId, objectiveId, kpiId, ct));

    /// <summary>
    /// تسجيل قراءة مؤرَّخة ثمّ إعادة كتابة اللقطة السريعة من **أحدث قراءة باقية**.
    /// مستوى **تشغيليّ** (D-07) ⟹ متاح أيضًا لقائد الفريق ومدير الحساب المسؤولَين.
    /// </summary>
    [HttpPost("objectives/{objectiveId:guid}/kpis/{kpiId:guid}/readings")]
    public async Task<IActionResult> AddReading(Guid projectId, Guid objectiveId, Guid kpiId, [FromBody] CreateProjectKpiReadingRequest request, CancellationToken ct)
        => FromResult(await _service.AddReadingAsync(projectId, objectiveId, kpiId, request, ct));

    /// <summary>تصحيح قراءة قائمة — لا تُضاف قراءة ثانية لنفس التاريخ.</summary>
    [HttpPut("objectives/{objectiveId:guid}/kpis/{kpiId:guid}/readings/{readingId:guid}")]
    public async Task<IActionResult> UpdateReading(Guid projectId, Guid objectiveId, Guid kpiId, Guid readingId, [FromBody] UpdateProjectKpiReadingRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateReadingAsync(projectId, objectiveId, kpiId, readingId, request, ct));
}
