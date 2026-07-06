using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Dashboard;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

/// <summary>
/// اللوحة التنفيذية (ERDS Phase 6 — Preview) — قراءة فقط. سبع نقاط عرض تُركّب فوق محرّكات التجميع
/// (Phase 4 مبيعات، Phase 5/5.5 تنفيذ) وتُعيد DTOs مستقلّة جاهزة للوحات/تقارير/AI لاحقة.
/// النطاق محكوم داخل محرّكات التجميع عبر IScopeResolver — لا تفتح بيانات خارج نطاق الدور.
/// لا تغيّر أيّ تسليم/قالب/مسار اعتماد/صلاحية. الفلاتر (best-effort): PeriodType/PeriodKey/Team/Employee/Client/Project.
/// </summary>
[Authorize]
[Route("api/dashboard")]
public class ExecutiveDashboardController : ApiControllerBase
{
    private readonly IExecutiveDashboardService _service;

    public ExecutiveDashboardController(IExecutiveDashboardService service) => _service = service;

    private static ExecutiveDashboardFilter Filter(
        PeriodType? periodType, string? periodKey, Guid? teamId, Guid? employeeId, string? client, string? project)
        => new(periodType, periodKey, teamId, employeeId, client, project);

    /// <summary>إجماليات عامّة على مستوى نطاق المستخدم.</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] string? client, [FromQuery] string? project, CancellationToken ct)
        => FromResult(await _service.GetOverviewAsync(Filter(periodType, periodKey, teamId, employeeId, client, project), ct));

    /// <summary>مبيعات B2C + B2B لكل أسبوع/فريق/موظّف مع المؤشرات الحالية.</summary>
    [HttpGet("sales")]
    public async Task<IActionResult> Sales(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] string? client, [FromQuery] string? project, CancellationToken ct)
        => FromResult(await _service.GetSalesAsync(Filter(periodType, periodKey, teamId, employeeId, client, project), ct));

    /// <summary>مؤشرات كل Pod التنفيذية.</summary>
    [HttpGet("pods")]
    public async Task<IActionResult> Pods(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] string? client, [FromQuery] string? project, CancellationToken ct)
        => FromResult(await _service.GetPodsAsync(Filter(periodType, periodKey, teamId, employeeId, client, project), ct));

    /// <summary>مؤشرات كل عميل المجمّعة عبر مشاريعه.</summary>
    [HttpGet("clients")]
    public async Task<IActionResult> Clients(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] string? client, [FromQuery] string? project, CancellationToken ct)
        => FromResult(await _service.GetClientsAsync(Filter(periodType, periodKey, teamId, employeeId, client, project), ct));

    /// <summary>مؤشرات كل مشروع المجمّعة.</summary>
    [HttpGet("projects")]
    public async Task<IActionResult> Projects(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] string? client, [FromQuery] string? project, CancellationToken ct)
        => FromResult(await _service.GetProjectsAsync(Filter(periodType, periodKey, teamId, employeeId, client, project), ct));

    /// <summary>عبء العمل لكل فريق ولكل موظّف.</summary>
    [HttpGet("workload")]
    public async Task<IActionResult> Workload(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] string? client, [FromQuery] string? project, CancellationToken ct)
        => FromResult(await _service.GetWorkloadAsync(Filter(periodType, periodKey, teamId, employeeId, client, project), ct));

    /// <summary>لوحة المخاطر (أخطر مشاريع/أكثر عملاء تأخّرًا/أكثر Pods ضغطًا/أعلى مهام متوقّفة/أعلى معدّل تأخير).</summary>
    [HttpGet("risks")]
    public async Task<IActionResult> Risks(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] string? client, [FromQuery] string? project, CancellationToken ct)
        => FromResult(await _service.GetRisksAsync(Filter(periodType, periodKey, teamId, employeeId, client, project), ct));
}
