using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api/reports")]
public class ReportsController : ApiControllerBase
{
    private readonly IReportingService _service;
    private readonly IReportDueService _due;

    public ReportsController(IReportingService service, IReportDueService due)
    {
        _service = service;
        _due = due;
    }

    // ===== RPT-DUE1: مواعيد التقارير والتأخّر (قراءة فقط، محسوب عند الطلب، بلا بريد/إشعارات) =====
    // my-status: self-only متاح لأيّ موظّف. overview/overdue: أيّ مستخدم موثَّق وScopeResolver وحده يحدّد ما يظهر.

    /// <summary>حالة تقرير الأسبوع الحالي للمستخدم نفسه (self-only). يعكس اليومي لمندوبي المبيعات.</summary>
    [HttpGet("due/my-status")]
    public async Task<IActionResult> DueMyStatus([FromQuery] string? weekKey, CancellationToken ct)
        => FromResult(await _due.MyStatusAsync(weekKey, ct));

    /// <summary>نظرة عامة على مواعيد التقارير لأسبوع ضمن نطاق المستخدم + فلاتر إدارة/فريق.</summary>
    [HttpGet("due/overview")]
    public async Task<IActionResult> DueOverview([FromQuery] string? weekKey,
        [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _due.OverviewAsync(weekKey, departmentId, teamId, ct));

    /// <summary>قائمة التأخّر (تقارير غير مُسلَّمة + مراجعات متأخّرة) ضمن نطاق المستخدم + فلاتر.</summary>
    [HttpGet("due/overdue")]
    public async Task<IActionResult> DueOverdue([FromQuery] string? weekKey,
        [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _due.OverdueAsync(weekKey, departmentId, teamId, ct));

    [HttpGet("submission-completeness")]
    public async Task<IActionResult> SubmissionCompleteness([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.SubmissionCompletenessAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct));

    /// <summary>
    /// متابعة التزام التسليم (per-person) لأسبوع — Admin/CEO/GM/Manager/TeamLeader/CeoSupport/Viewer/HR.
    /// شاشة متابعة التزام فقط: من سلّم، من تأخّر، الحالة لكلّ موظف متوقَّع. <b>بلا أيّ محتوى للتقرير</b>.
    /// </summary>
    [HttpGet("submission-compliance")]
    [Authorize(Policy = Policies.ReportCompletionView)]
    public async Task<IActionResult> SubmissionCompliance([FromQuery] string? weekKey,
        [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.SubmissionComplianceAsync(weekKey, departmentId, teamId, ct));

    /// <summary>ملخّص التزام أسبوع (أرقام مجمّعة: متوقَّع/مُسلَّم/متأخر/في الموعد + النسب) ضمن نطاق المستخدم.</summary>
    [HttpGet("compliance-summary")]
    [Authorize(Policy = Policies.ReportCompletionView)]
    public async Task<IActionResult> ComplianceSummary([FromQuery] string? weekKey,
        [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.ComplianceSummaryAsync(weekKey, departmentId, teamId, ct));

    /// <summary>اتجاه الالتزام عبر آخر N أسابيع (الأقدم → الأحدث) ضمن نطاق المستخدم.</summary>
    [HttpGet("compliance-trend")]
    [Authorize(Policy = Policies.ReportCompletionView)]
    public async Task<IActionResult> ComplianceTrend([FromQuery] int weeks,
        [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.ComplianceTrendAsync(weeks, departmentId, teamId, ct));

    /// <summary>القوالب/المسمّيات الأكثر تأخّرًا ضمن أسبوع ونطاق المستخدم.</summary>
    [HttpGet("late-by-template")]
    [Authorize(Policy = Policies.ReportCompletionView)]
    public async Task<IActionResult> LateByTemplate([FromQuery] string? weekKey,
        [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.LateByTemplateAsync(weekKey, departmentId, teamId, ct));

    /// <summary>تجميع الالتزام حسب فريق/إدارة ضمن أسبوع ونطاق المستخدم (groupBy = "team" | "department").</summary>
    [HttpGet("compliance-breakdown")]
    [Authorize(Policy = Policies.ReportCompletionView)]
    public async Task<IActionResult> ComplianceBreakdown([FromQuery] string? weekKey,
        [FromQuery] string? groupBy, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.ComplianceBreakdownAsync(weekKey, groupBy, departmentId, teamId, ct));

    /// <summary>
    /// P1-KPI-005 — <b>Deprecated</b>. البديل: <c>GET /api/kpi/performance</c>.
    /// شكل الاستجابة لم يتغيّر (توافق تامّ للمستهلكين القائمين)، لكنّ الحساب يتحوّل داخليًّا
    /// إلى المحرّك الموحّد عند تفعيل <c>Kpi:NewCalculationEngine</c>. خطّة الإزالة في تقرير المرحلة.
    /// </summary>
    [HttpGet("kpi-summary")]
    [Obsolete("P1-KPI-005: استعمل GET /api/kpi/performance.")]
    public async Task<IActionResult> KpiSummary([FromQuery] PeriodType? periodType,
        [FromQuery] string? periodKey, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
    {
        Response.Headers["Deprecation"] = "true";
        Response.Headers["Link"] = "</api/kpi/performance>; rel=\"successor-version\"";
#pragma warning disable CS0618
        return FromResult(await _service.KpiSummaryAsync(new ReportFilter(periodType, periodKey, departmentId, teamId), ct));
#pragma warning restore CS0618
    }

    [HttpGet("governance-summary")]
    public async Task<IActionResult> GovernanceSummary(CancellationToken ct)
        => FromResult(await _service.GovernanceSummaryAsync(ct));

    // ===== RPT-WORKFLOW-BOTTLENECKS-R1: اختناقات مسار الاعتماد (قراءة فقط، ضمن نطاق المستخدم) =====
    // متاحة لأيّ مستخدم مصادَق؛ ScopeResolver وحده يحدّد ما يظهر (الموظف تقاريره العالقة، القائد فريقه،
    // المدير إدارته، الإدارة العليا الكل). لا توسيع صلاحيات — RBAC الخادمي مصدر الحقيقة.

    /// <summary>ملخّص الاختناقات: إجمالي العالق/المتأخر/أقدم عمر/متوسط العمر/أبرز مرحلة ومعتمِد ضمن النطاق.</summary>
    [HttpGet("workflow-bottlenecks/summary")]
    public async Task<IActionResult> WorkflowBottlenecksSummary(
        [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.WorkflowBottlenecksSummaryAsync(departmentId, teamId, ct));

    /// <summary>توزيع الاختناقات حسب المرحلة (قائد فريق/مدير/الإدارة العليا) ضمن النطاق.</summary>
    [HttpGet("workflow-bottlenecks/by-stage")]
    public async Task<IActionResult> WorkflowBottlenecksByStage(
        [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.WorkflowBottlenecksByStageAsync(departmentId, teamId, ct));

    /// <summary>توزيع الاختناقات حسب المعتمِد الحالي ضمن النطاق.</summary>
    [HttpGet("workflow-bottlenecks/by-approver")]
    public async Task<IActionResult> WorkflowBottlenecksByApprover(
        [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, CancellationToken ct)
        => FromResult(await _service.WorkflowBottlenecksByApproverAsync(departmentId, teamId, ct));

    /// <summary>تفاصيل التقارير العالقة ضمن النطاق + فلاتر (stage/teamId/departmentId/approverId/overdueOnly). بلا أيّ محتوى للتقرير.</summary>
    [HttpGet("workflow-bottlenecks/details")]
    public async Task<IActionResult> WorkflowBottlenecksDetails(
        [FromQuery] string? stage, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId,
        [FromQuery] Guid? approverId, [FromQuery] bool overdueOnly, CancellationToken ct)
        => FromResult(await _service.WorkflowBottlenecksDetailsAsync(stage, departmentId, teamId, approverId, overdueOnly, ct));

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
