using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Calendar;
using Reporting.Application.Common;

namespace Reporting.Api.Controllers;

// ROLE-AWARE-REPORTING-CALENDAR — Phase 2.3. تقويم الدورات المُدرِك للأدوار:
// نافذة السبت→الجمعة موحّدة لكل المستويات، وتاريخ الاستحقاق يختلف بحسب دور المستخدم الأساسيّ فقط.
// الدور يُستخرَج خادميًّا (RoleAccess.PrimaryRole) — لا يُرسَل من الواجهة إطلاقًا. قراءة/حساب فقط.
[Authorize]
[Route("api/reporting-calendar")]
public class ReportingCalendarController : ApiControllerBase
{
    private readonly IReportingCalendarCycleService _service;

    public ReportingCalendarController(IReportingCalendarCycleService service) => _service = service;

    // دورات المستخدم الحاليّ (ماضٍ محدود + الحالية + مستقبل محدود) بحسب دوره الأساسيّ.
    // context=report|kpi (تسمية فقط)، templateId اختياريّ (سياق)، past/future مُقيَّدان بحدود آمنة.
    [HttpGet("my-cycles")]
    public async Task<IActionResult> MyCycles(
        [FromQuery] ReportingCalendarContext context = ReportingCalendarContext.Report,
        [FromQuery] Guid? templateId = null,
        [FromQuery] int? past = null,
        [FromQuery] int? future = null,
        CancellationToken ct = default)
        => FromResult(await _service.GetMyCyclesAsync(context, templateId, past, future, ct));

    // تشخيص إداريّ: حلّ دورة واحدة لمفتاح معطى بحسب دور المستخدم الحاليّ (يرفض المفاتيح غير الصالحة).
    [HttpGet("resolve")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Resolve(
        [FromQuery] string cycleKey,
        [FromQuery] ReportingCalendarContext context = ReportingCalendarContext.Report,
        CancellationToken ct = default)
        => FromResult(await _service.ResolveAsync(cycleKey, context, ct));
}
