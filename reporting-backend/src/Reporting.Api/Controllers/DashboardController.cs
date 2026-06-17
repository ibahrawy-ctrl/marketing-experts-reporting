using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Dashboard;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api/dashboard")]
public class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service) => _service = service;

    [HttpGet("me")]
    public async Task<IActionResult> Mine([FromQuery] string? periodKey, CancellationToken ct)
        => FromResult(await _service.GetMineAsync(periodKey, ct));

    /// <summary>اتجاه KPI لشخص داخل النطاق (الافتراضي: المستخدم نفسه).</summary>
    [HttpGet("kpi-trends")]
    public async Task<IActionResult> KpiTrends([FromQuery] Guid? subjectId, CancellationToken ct)
        => FromResult(await _service.GetKpiTrendsAsync(subjectId, ct));

    /// <summary>أداء أعضاء النطاق (فريق/قسم/شركة).</summary>
    [HttpGet("members-performance")]
    public async Task<IActionResult> MembersPerformance(CancellationToken ct)
        => FromResult(await _service.GetMembersPerformanceAsync(ct));

    /// <summary>أحدث التسليمات داخل النطاق.</summary>
    [HttpGet("recent-activity")]
    public async Task<IActionResult> RecentActivity(CancellationToken ct)
        => FromResult(await _service.GetRecentActivityAsync(ct));

    /// <summary>التقارير التي لم تُسلَّم بعد أو تحتاج إجراء داخل النطاق للفترة.</summary>
    [HttpGet("pending-reports")]
    public async Task<IActionResult> PendingReports([FromQuery] string? periodKey, CancellationToken ct)
        => FromResult(await _service.GetPendingReportsAsync(periodKey, ct));

    /// <summary>ملف أداء موظّف موحّد — يُفرض النطاق خادمًا (403 خارج النطاق، 404 لو غير موجود).</summary>
    [HttpGet("employee-profile/{userId:guid}")]
    public async Task<IActionResult> EmployeeProfile([FromRoute] Guid userId, CancellationToken ct)
        => FromResult(await _service.GetEmployeeProfileAsync(userId, ct));
}
