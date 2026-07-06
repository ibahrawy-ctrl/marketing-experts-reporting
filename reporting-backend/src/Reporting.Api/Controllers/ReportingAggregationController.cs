using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Reports;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

/// <summary>
/// محرّك التجميع الرقمي (ERDS Phase 4) — قراءة فقط. نقطتا اختبار آمنتان تُجمّعان قوالب ERDS المُهيكَلة
/// (B2C حسب الدورة، B2B حسب الخدمة) من جدول TableGrid. النطاق محكوم بـ IScopeResolver داخل الخدمة —
/// لا تفتح بيانات خارج نطاق الدور. لا تغيّر أيّ تسليم/قالب/مسار اعتماد. الفلاتر: PeriodType/PeriodKey/Employee/Team/Department/Item.
/// </summary>
[Authorize]
[Route("api/reporting/aggregation")]
public class ReportingAggregationController : ApiControllerBase
{
    private readonly IReportingAggregationService _service;

    public ReportingAggregationController(IReportingAggregationService service) => _service = service;

    /// <summary>تجميع «تقرير مبيعات B2C حسب الدورة» لكل (أسبوع، موظّف، دورة) ضمن نطاق المستخدم.</summary>
    [HttpGet("b2c")]
    public async Task<IActionResult> B2cByCourse(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? employeeId, [FromQuery] Guid? teamId, [FromQuery] Guid? departmentId,
        [FromQuery] string? course, CancellationToken ct)
        => FromResult(await _service.AggregateB2cByCourseAsync(
            new AggregationFilter(periodType, periodKey, employeeId, teamId, departmentId, course), ct));

    /// <summary>
    /// تجميع «تقرير مبيعات B2C» مجموعةً حسب الدورة (الدورة تظهر مرّة واحدة) مع تفصيل الموظّفين (Drill-down).
    /// العرض الافتراضي للمدير. النطاق محكوم بـ IScopeResolver داخل الخدمة.
    /// </summary>
    [HttpGet("b2c/by-course")]
    public async Task<IActionResult> B2cByCourseGrouped(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? employeeId, [FromQuery] Guid? teamId, [FromQuery] Guid? departmentId,
        [FromQuery] string? course, CancellationToken ct)
        => FromResult(await _service.AggregateB2cByCourseGroupedAsync(
            new AggregationFilter(periodType, periodKey, employeeId, teamId, departmentId, course), ct));

    /// <summary>تجميع «تقرير مبيعات B2B حسب الخدمة» لكل (أسبوع، موظّف، خدمة) ضمن نطاق المستخدم.</summary>
    [HttpGet("b2b")]
    public async Task<IActionResult> B2bByService(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? employeeId, [FromQuery] Guid? teamId, [FromQuery] Guid? departmentId,
        [FromQuery] string? service, CancellationToken ct)
        => FromResult(await _service.AggregateB2bByServiceAsync(
            new AggregationFilter(periodType, periodKey, employeeId, teamId, departmentId, service), ct));

    /// <summary>
    /// تجميع B2C مفصولًا إلى بيانات جديدة New / بيانات CRM قديمة Old (Phase 7). يقرأ القالب القديم (⇒ New)
    /// وجدولَي القالب الجديد (New Leads ⇒ New، Old CRM Data ⇒ Old). النطاق محكوم بـ IScopeResolver داخل الخدمة.
    /// </summary>
    [HttpGet("b2c/new-old")]
    public async Task<IActionResult> B2cNewOld(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? employeeId, [FromQuery] Guid? teamId, [FromQuery] Guid? departmentId,
        [FromQuery] string? course, CancellationToken ct)
        => FromResult(await _service.AggregateB2cNewOldAsync(
            new AggregationFilter(periodType, periodKey, employeeId, teamId, departmentId, course), ct));
}
