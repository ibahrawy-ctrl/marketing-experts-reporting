using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Reports;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api/reports")]
public class ReportsController : ApiControllerBase
{
    private readonly IReportingService _service;

    public ReportsController(IReportingService service) => _service = service;

    [HttpGet("submission-completeness")]
    public async Task<IActionResult> SubmissionCompleteness([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.SubmissionCompletenessAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct));

    [HttpGet("kpi-summary")]
    public async Task<IActionResult> KpiSummary([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.KpiSummaryAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct));

    [HttpGet("governance-summary")]
    public async Task<IActionResult> GovernanceSummary(CancellationToken ct)
        => FromResult(await _service.GovernanceSummaryAsync(ct));

    /// <summary>تجميع أرقام مبيعات B2C ضمن نطاق رؤية المستخدم — الموظف أرقامه، القائد فريقه… إلخ.</summary>
    [HttpGet("b2c-rollup")]
    public async Task<IActionResult> B2cRollup([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.B2cSalesRollupAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct));

    /// <summary>تجميع أرقام أداء الإعلانات (Media Buyer) ضمن نطاق رؤية المستخدم — المشتري أرقامه، مدير الأداء فريقه، الإدارة العليا ملخّص فقط. Business-1B.</summary>
    [HttpGet("media-buyer-rollup")]
    public async Task<IActionResult> MediaBuyerRollup([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.MediaBuyerRollupAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct));

    /// <summary>تجميع أرقام أداء SEO (كلمات/مهام/مشاكل/مقالات) ضمن نطاق رؤية المستخدم — الأخصائي أرقامه، قائد SEO فريقه، الإدارة العليا ملخّص فقط. Business-1C.</summary>
    [HttpGet("seo-rollup")]
    public async Task<IActionResult> SeoRollup([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.SeoRollupAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct));

    /// <summary>تجميع أرقام أداء كاتب المحتوى (المطلوبة/المسلَّمة/المعتمدة من أول مرة/المتأخرة) ضمن نطاق رؤية المستخدم — الكاتب أرقامه، قائد السوشيال فريقه، الإدارة العليا ملخّص فقط. Business-1D-1.</summary>
    [HttpGet("content-writer-rollup")]
    public async Task<IActionResult> ContentWriterRollup([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.ContentWriterRollupAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct));

    /// <summary>تجميع أرقام أداء فريق التصميم (المطلوبة/المسلَّمة/المعتمدة من أول مرة/المتأخرة/المعادة) ضمن نطاق رؤية المستخدم — المصمّم أرقامه، قائد السوشيال فريقه، الإدارة العليا ملخّص فقط. Business-1D-2.</summary>
    [HttpGet("designer-rollup")]
    public async Task<IActionResult> DesignerRollup([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.DesignerRollupAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct));

    /// <summary>تجميع أرقام أداء فريق الفيديو (المطلوبة/المسلَّمة/المعتمدة من أول مرة/المتأخرة/المعادة) ضمن نطاق رؤية المستخدم — عضو الفيديو أرقامه، قائد السوشيال فريقه، الإدارة العليا ملخّص فقط. Business-1D-3.</summary>
    [HttpGet("video-rollup")]
    public async Task<IActionResult> VideoRollup([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.VideoRollupAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct));

    /// <summary>تجميع أداء المودريشن (الواردة/المُجاب عليها/نسبة الرد/سرعة الرد/المصعّدة/الشكاوى) ضمن نطاق رؤية المستخدم — المودريتر أرقامه، قائد السوشيال فريقه، الإدارة العليا ملخّص فقط. Business-1D-4.</summary>
    [HttpGet("moderation-rollup")]
    public async Task<IActionResult> ModerationRollup([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.ModerationRollupAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct));

    /// <summary>ملخّص تشغيل السوشيال ميديا الموحّد — يجمع المحتوى/التصميم/الفيديو/المودريشن حسب نطاق رؤية المستخدم. Business-1D-5.</summary>
    [HttpGet("social-ops-rollup")]
    public async Task<IActionResult> SocialOpsRollup([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.SocialOpsRollupAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct));

    [HttpGet("submissions/export")]
    public async Task<IActionResult> ExportSubmissions([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
    {
        var result = await _service.ExportSubmissionsCsvAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct);
        if (!result.Succeeded) return ToProblem(result);
        return File(result.Value!, "text/csv", $"submissions-{periodKey ?? "all"}.csv");
    }

    [HttpGet("submission-completeness/export-pdf")]
    public async Task<IActionResult> ExportCompletenessPdf([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
    {
        var result = await _service.ExportCompletenessPdfAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct);
        if (!result.Succeeded) return ToProblem(result);
        return File(result.Value!, "application/pdf", $"completeness-{periodKey ?? "all"}.pdf");
    }

    [HttpGet("kpi-summary/export-pdf")]
    public async Task<IActionResult> ExportKpiSummaryPdf([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
    {
        var result = await _service.ExportKpiSummaryPdfAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct);
        if (!result.Succeeded) return ToProblem(result);
        return File(result.Value!, "application/pdf", $"kpi-summary-{periodKey ?? "all"}.pdf");
    }

    [HttpGet("executive-summary/export-pdf")]
    public async Task<IActionResult> ExportExecutiveSummaryPdf([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
    {
        var result = await _service.ExportExecutiveSummaryPdfAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct);
        if (!result.Succeeded) return ToProblem(result);
        return File(result.Value!, "application/pdf", $"executive-summary-{periodKey ?? "all"}.pdf");
    }
}
