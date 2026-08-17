using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Projects360;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

/// <summary>
/// **جسر التنفيذ الهجين** (P360-WF-R2 §10) — القرار المعتمد `OPTION B`.
///
/// <para>
/// **لا Policy على مستوى المتحكّم**: التخويل **مورديّ** (لهذا المشروع بعينه) لا دوريّ، ويقع كاملًا
/// في الخدمة. سياسة دوريّة هنا كانت ستحجب منفِّذًا مسنَدًا للمشروع أو تفتحه لمن يحمل الدور خارجه —
/// وكلاهما خطأ. <c>[Authorize]</c> وحدها تضمن الهويّة، والباقي يقرّره الحارس المورديّ.
/// </para>
///
/// <para>
/// **مسار مستقلّ عن <c>contract-deliverables</c>**: الكتابة المباشرة على المخرَج تبقى كما هي لمن
/// يملكها؛ وهذا المسار هو الطريق الوحيد لمن **لا** يملكها كي يوصل تنفيذه إلى المشروع.
/// </para>
/// </summary>
[Authorize]
[Route("api/projects/{projectId:guid}/execution-proposals")]
public class ProjectExecutionBridgeController : ApiControllerBase
{
    private readonly IProjectExecutionBridgeService _service;

    public ProjectExecutionBridgeController(IProjectExecutionBridgeService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(
        Guid projectId,
        [FromQuery] ExecutionProposalStatus? status = null,
        [FromQuery] Guid? deliverableId = null,
        CancellationToken ct = default)
        => FromResult(await _service.ListAsync(projectId, status, deliverableId, ct));

    /// <summary>رفع ادّعاء تنفيذ على مخرَج تعاقديّ — متاح لكلّ من يرى المشروع.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        Guid projectId, [FromBody] CreateProjectExecutionProposalRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(projectId, request, ct));

    /// <summary>حسم المقترح — مقصور على من يملك تحديث تقدّم هذا المشروع. متعادل بالحالة.</summary>
    [HttpPatch("{proposalId:guid}/review")]
    public async Task<IActionResult> Review(
        Guid projectId, Guid proposalId, [FromBody] ReviewProjectExecutionProposalRequest request, CancellationToken ct)
        => FromResult(await _service.ReviewAsync(projectId, proposalId, request, ct));
}
