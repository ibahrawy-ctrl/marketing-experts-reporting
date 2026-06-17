using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Calendar;

namespace Reporting.Api.Controllers;

// تقويم التقارير التشغيلي (Phase 5): التقارير الأسبوعية الناقصة (§5)، تأخّر الاعتماد بعد المهلة (§6)،
// والتزام مندوبي المبيعات بالتقارير اليومية وتجميعها أسبوعيًّا (§9).
// النطاق وحدود الإدارة مفروضة خادميًّا داخل الخدمة (ScopeResolver + قواعد المستوى)، لا من الواجهة فقط.
[Authorize]
[Route("api/report-calendar")]
public class ReportCalendarController : ApiControllerBase
{
    private readonly IReportCalendarService _service;

    public ReportCalendarController(IReportCalendarService service) => _service = service;

    // §5 — مَن كان متوقَّعًا منه تقرير أسبوعي ولم يُسلّم (أو تأخّر) لأسبوع معيّن، ضمن النطاق.
    [HttpGet("missing-reports")]
    public async Task<IActionResult> MissingReports([FromQuery] string? weekKey, CancellationToken ct)
        => FromResult(await _service.GetMissingReportsAsync(weekKey, ct));

    // §6 — تقارير مُرسَلة لم تُراجَع بعد انتهاء مهلة المعتمِد الحالي — تُعرض للمستوى الأعلى فقط.
    [HttpGet("approval-delays")]
    public async Task<IActionResult> ApprovalDelays(CancellationToken ct)
        => FromResult(await _service.GetApprovalDelaysAsync(ct));

    // §9 — التزام مندوبي المبيعات بالتقارير اليومية ضمن الأسبوع، وكشف الأسابيع الناقصة (تحتاج مراجعة).
    [HttpGet("sales-daily-compliance")]
    public async Task<IActionResult> SalesDailyCompliance([FromQuery] string? weekKey, CancellationToken ct)
        => FromResult(await _service.GetSalesDailyComplianceAsync(weekKey, ct));
}
