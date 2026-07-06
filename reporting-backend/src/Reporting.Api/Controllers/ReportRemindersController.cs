using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Reports;

namespace Reporting.Api.Controllers;

/// <summary>
/// EMAIL-REPORT-REMINDERS-R1 — نقطة نهاية إدارية لتوليد تذكيرات/تنبيهات التقارير يدويًّا (وضع DryRun افتراضًا).
/// محصورة بالأدوار العليا عبر سياسة EmailNotificationLog (Admin/CEO/GeneralManager/CeoSupport). قراءة فقط على التقارير،
/// وتُدرِج الصفوف عبر القلب الآمن في EmailNotificationService (يحترم الوضع، يمنع التكرار، لا يمسّ email_outbox).
/// لا مُجدوِل هنا (R1 يدويّ)؛ نفس الخدمة تُستدعى لاحقًا من BackgroundService في R2 دون تغيير.
/// </summary>
[Authorize(Policy = Policies.EmailNotificationLog)]
[Route("api/report-reminders")]
public class ReportRemindersController : ApiControllerBase
{
    private readonly IReportReminderService _service;

    public ReportRemindersController(IReportReminderService service) => _service = service;

    /// <summary>يولّد تذكيرات/تنبيهات التقارير على مستوى الشركة ويُدرِجها عبر القلب الآمن (DryRun افتراضًا).</summary>
    [HttpPost("dry-run/generate")]
    public async Task<IActionResult> GenerateDryRun([FromBody] GenerateReportRemindersRequest? request, CancellationToken ct)
    {
        var req = request ?? new GenerateReportRemindersRequest();
        var options = new ReportReminderRunOptions(
            WeekKey: req.WeekKey,
            Date: req.Date,
            IncludeDue: req.IncludeDue,
            IncludeOverdue: req.IncludeOverdue,
            IncludeReviewOverdue: req.IncludeReviewOverdue);

        var result = await _service.GenerateAsync(options, ct);
        return Ok(result);
    }
}

/// <summary>جسم طلب توليد تذكيرات التقارير (كلّها اختيارية بقيم افتراضية آمنة).</summary>
public record GenerateReportRemindersRequest(
    string? WeekKey = null,
    DateOnly? Date = null,
    bool IncludeDue = true,
    bool IncludeOverdue = true,
    bool IncludeReviewOverdue = true);
