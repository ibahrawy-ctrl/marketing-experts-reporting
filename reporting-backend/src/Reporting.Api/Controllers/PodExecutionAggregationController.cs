using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Reports;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

/// <summary>
/// محرّك تجميع التنفيذ (ERDS Phase 5) — قراءة فقط. ثلاث نقاط اختبار آمنة تُجمّع القوالب التنفيذية الرقمية الستة
/// (محتوى/تصميم/فيديو/نشر/ميديا باير/مشاريع) من جدول TableGrid حسب (الفريق/Pod، العميل، المشروع، الموظّف، الفترة).
/// النطاق محكوم بـ IScopeResolver داخل الخدمة — لا تفتح بيانات خارج نطاق الدور. لا تغيّر أيّ تسليم/قالب/مسار اعتماد.
/// مستقلّة تمامًا عن Phase 4 (B2C/B2B). الفلاتر: PeriodType/PeriodKey/Team/Employee/Client/Project.
/// </summary>
[Authorize]
[Route("api/reporting/aggregation")]
public class PodExecutionAggregationController : ApiControllerBase
{
    private readonly IPodExecutionAggregationService _service;

    public PodExecutionAggregationController(IPodExecutionAggregationService service) => _service = service;

    /// <summary>تجميع التنفيذ الموحّد لكل (الفترة، الفريق/Pod، الموظّف، العميل، المشروع) ضمن نطاق المستخدم.</summary>
    [HttpGet("pods")]
    public async Task<IActionResult> Pods(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] string? client, [FromQuery] string? project, CancellationToken ct)
        => FromResult(await _service.AggregateByPodAsync(
            new PodExecutionFilter(periodType, periodKey, teamId, employeeId, client, project), ct));

    /// <summary>تجميع التنفيذ لكل (عميل، مشروع) على مستوى النطاق مع CPL/CPA/ROAS محسوبة من المجاميع.</summary>
    [HttpGet("clients")]
    public async Task<IActionResult> Clients(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] string? client, [FromQuery] string? project, CancellationToken ct)
        => FromResult(await _service.AggregateByClientAsync(
            new PodExecutionFilter(periodType, periodKey, teamId, employeeId, client, project), ct));

    /// <summary>تجميع «تقرير المشاريع حسب العميل/المشروع» فقط لكل (الفترة، الموظّف، العميل، المشروع).</summary>
    [HttpGet("projects")]
    public async Task<IActionResult> Projects(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] string? client, [FromQuery] string? project, CancellationToken ct)
        => FromResult(await _service.AggregateByProjectAsync(
            new PodExecutionFilter(periodType, periodKey, teamId, employeeId, client, project), ct));
}
