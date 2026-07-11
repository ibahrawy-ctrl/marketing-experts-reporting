using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Reports;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

/// <summary>
/// محرّك التجميع Project-First (RC-4 Task 4، Path A) — قراءة فقط. أربع نقاط تُجمّع قوالب التنفيذ الأربعة
/// (محتوى/تصميم/فيديو/مديرشن) من قسم المشاريع المتكرّر حيث كل الأرقام داخل المشروع، حسب (المشروع/الموظّف/Pod/العميل).
/// النطاق محكوم بـ IScopeResolver داخل الخدمة. لا تغيّر أيّ تسليم/قالب/مسار اعتماد. مستقلّة عن مسار المبيعات (B2C/B2B)
/// وعن المسار القديم (Family B المسطّح). الفلاتر: PeriodType/PeriodKey/Team/Employee/Client/Project (بالمعرّفات الحقيقية).
/// </summary>
[Authorize]
[Route("api/reporting/project-execution")]
public class ProjectFirstExecutionAggregationController : ApiControllerBase
{
    private readonly IProjectFirstExecutionAggregationService _service;

    public ProjectFirstExecutionAggregationController(IProjectFirstExecutionAggregationService service) => _service = service;

    /// <summary>تجميع لكل مشروع ضمن نطاق المستخدم.</summary>
    [HttpGet("projects")]
    public async Task<IActionResult> Projects(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] Guid? clientId, [FromQuery] Guid? projectId, CancellationToken ct)
        => FromResult(await _service.AggregateByProjectAsync(
            new ProjectFirstExecutionFilter(periodType, periodKey, teamId, employeeId, clientId, projectId), ct));

    /// <summary>تجميع لكل (موظّف، مشروع) ضمن نطاق المستخدم.</summary>
    [HttpGet("employees")]
    public async Task<IActionResult> Employees(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] Guid? clientId, [FromQuery] Guid? projectId, CancellationToken ct)
        => FromResult(await _service.AggregateByEmployeeAsync(
            new ProjectFirstExecutionFilter(periodType, periodKey, teamId, employeeId, clientId, projectId), ct));

    /// <summary>تجميع لكل Pod (فريق المُسلِّم) ضمن نطاق المستخدم.</summary>
    [HttpGet("pods")]
    public async Task<IActionResult> Pods(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] Guid? clientId, [FromQuery] Guid? projectId, CancellationToken ct)
        => FromResult(await _service.AggregateByPodAsync(
            new ProjectFirstExecutionFilter(periodType, periodKey, teamId, employeeId, clientId, projectId), ct));

    /// <summary>تجميع لكل عميل ضمن نطاق المستخدم.</summary>
    [HttpGet("clients")]
    public async Task<IActionResult> Clients(
        [FromQuery] PeriodType? periodType, [FromQuery] string? periodKey,
        [FromQuery] Guid? teamId, [FromQuery] Guid? employeeId,
        [FromQuery] Guid? clientId, [FromQuery] Guid? projectId, CancellationToken ct)
        => FromResult(await _service.AggregateByClientAsync(
            new ProjectFirstExecutionFilter(periodType, periodKey, teamId, employeeId, clientId, projectId), ct));
}
