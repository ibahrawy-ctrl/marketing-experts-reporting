using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Archive;
using Reporting.Application.Common;

namespace Reporting.Api.Controllers;

/// <summary>
/// الأرشيف الإداريّ (RESTORE-ARCHIVE-GOVERNANCE-R1 — Phase 12): قراءة العناصر المحذوفة إداريًّا
/// ناعمًا (تقارير + تقييمات KPI) واسترجاعها وفق دلالات Hybrid. محكوم بسياسة ArchiveGovernanceAccess
/// (Admin/CEO/GM فقط). لا حذف نهائيّ ولا جدولة ولا إشعارات/بريد.
/// </summary>
[Authorize(Policy = Policies.ArchiveGovernanceAccess)]
[Route("api/admin/archive")]
public class AdminArchiveController : ApiControllerBase
{
    private readonly IArchiveService _service;

    public AdminArchiveController(IArchiveService service) => _service = service;

    /// <summary>قائمة العناصر المؤرشفة (مصفّحة + مرشّحة) مع حساب قابلية الاسترجاع لكلّ عنصر.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ArchiveItemType? itemType, [FromQuery] string? periodKey, [FromQuery] Guid? employeeId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.ListAsync(new ArchiveFilter(itemType, periodKey, employeeId, page, pageSize), ct));

    /// <summary>تفاصيل تقرير مؤرشف مع لقطة سير العمل والأثر التدقيقيّ واستراتيجية الاسترجاع.</summary>
    [HttpGet("report/{id:guid}")]
    public async Task<IActionResult> ReportDetails(Guid id, CancellationToken ct)
        => FromResult(await _service.GetDetailsAsync(ArchiveItemType.Report, id, ct));

    /// <summary>تفاصيل تقييم KPI مؤرشف.</summary>
    [HttpGet("kpi/{id:guid}")]
    public async Task<IActionResult> KpiDetails(Guid id, CancellationToken ct)
        => FromResult(await _service.GetDetailsAsync(ArchiveItemType.KpiEvaluation, id, ct));

    /// <summary>استرجاع تقرير مُسلَّم محذوف إداريًّا وفق دلالات Hybrid (سبب إلزاميّ 10–500 محرفًا).</summary>
    [HttpPost("report/{id:guid}/restore")]
    public async Task<IActionResult> RestoreReport(Guid id, RestoreRequest request, CancellationToken ct)
        => FromResult(await _service.RestoreReportAsync(id, request, ct));

    /// <summary>استرجاع تقييم KPI محذوف إداريًّا (سبب إلزاميّ 10–500 محرفًا).</summary>
    [HttpPost("kpi/{id:guid}/restore")]
    public async Task<IActionResult> RestoreKpi(Guid id, RestoreRequest request, CancellationToken ct)
        => FromResult(await _service.RestoreKpiAsync(id, request, ct));
}
