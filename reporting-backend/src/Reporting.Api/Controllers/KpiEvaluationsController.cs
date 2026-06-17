using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api/kpi-evaluations")]
public class KpiEvaluationsController : ApiControllerBase
{
    private readonly IKpiEvaluationService _service;

    public KpiEvaluationsController(IKpiEvaluationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? subjectUserId, [FromQuery] Guid? evaluatorId, [FromQuery] Guid? teamId,
        [FromQuery] Guid? departmentId, [FromQuery] string? periodKey, [FromQuery] KpiEvaluationStatus? status,
        CancellationToken ct)
        => FromResult(await _service.ListAsync(
            new KpiEvaluationFilter(subjectUserId, evaluatorId, teamId, departmentId, periodKey, status), ct));

    [HttpGet("subject/{subjectUserId:guid}")]
    public async Task<IActionResult> ForSubject(Guid subjectUserId, CancellationToken ct)
        => FromResult(await _service.ListForSubjectAsync(subjectUserId, ct));

    // تجميع KPI الدوري (Phase 5 §8): الأسبوع وحدة الأساس، والمتوسط شهري/ربع سنوي/سنوي/مخصّص.
    // النطاق مفروض خادميًّا داخل الخدمة (لا تصفية من الواجهة فقط).
    [HttpGet("aggregate")]
    public async Task<IActionResult> Aggregate(
        [FromQuery] string granularity, [FromQuery] string? periodKey,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? subjectUserId, [FromQuery] Guid? teamId, [FromQuery] Guid? departmentId,
        CancellationToken ct)
        => FromResult(await _service.GetAggregateAsync(
            new KpiAggregateRequest(granularity, periodKey, from, to, subjectUserId, teamId, departmentId), ct));

    // الموظّفون الذين يحقّ للمستخدم الحالي إنشاء تقييم لهم (مرؤوسوه المباشرون، أو الكل للأدمن).
    [HttpGet("evaluatable-subjects")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> EvaluatableSubjects(CancellationToken ct)
        => FromResult(await _service.GetEvaluatableSubjectsAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => FromResult(await _service.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> CreateOrGet(CreateKpiEvaluationRequest request, CancellationToken ct)
        => FromResult(await _service.CreateOrGetAsync(request, ct));

    [HttpPut("{id:guid}/results")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> SaveResults(Guid id, SaveKpiResultsRequest request, CancellationToken ct)
        => FromResult(await _service.SaveResultsAsync(id, request, ct));

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
        => FromResult(await _service.SubmitAsync(id, ct));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
        => FromResult(await _service.ApproveAsync(id, ct));
}
