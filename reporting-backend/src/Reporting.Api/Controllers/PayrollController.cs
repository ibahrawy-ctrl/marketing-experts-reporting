using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Payroll;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

/// <summary>
/// «عرض التأثير على الرواتب» (FIN-L1). عرض إعلامي بحت على مستوى الشركة لطلبات الإجازة/الاستئذان المؤثّرة على
/// الراتب + مراجعة مالية (حالة/ملاحظة). القراءة محكومة بسياسة PayrollImpactRead؛ تحديث المراجعة بسياسة
/// PayrollImpactManage. لا يمسّ هذا الموديول الطلب الأصلي ولا حالته ولا يُجري خصمًا آليًّا.
/// </summary>
[Authorize]
[Route("api/payroll/leave-impacts")]
public class PayrollController : ApiControllerBase
{
    private readonly IPayrollImpactService _service;

    public PayrollController(IPayrollImpactService service) => _service = service;

    /// <summary>قائمة الطلبات المؤثّرة + بطاقات تلخيصية. month بصيغة yyyy-MM، وإلا year/month منفصلين.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.PayrollImpactRead)]
    public async Task<IActionResult> List(
        [FromQuery] string? month,
        [FromQuery] int? year,
        [FromQuery] Guid? employeeUserId,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? teamId,
        [FromQuery] LeaveRequestType? type,
        [FromQuery] LeaveRequestStatus? approvalStatus,
        [FromQuery] bool allApprovalStatuses = false,
        [FromQuery] PayrollImpactType? impactType = null,
        [FromQuery] PayrollImpactReviewStatus? reviewStatus = null,
        CancellationToken ct = default)
    {
        int? y = year;
        int? m = null;
        // month=yyyy-MM له الأولوية على year/month المنفصلين إن وُجد.
        if (!string.IsNullOrWhiteSpace(month)
            && DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            y = parsed.Year;
            m = parsed.Month;
        }

        var filter = new PayrollImpactFilter(
            y, m, employeeUserId, departmentId, teamId, type, approvalStatus,
            allApprovalStatuses, impactType, reviewStatus);
        return FromResult(await _service.ListAsync(filter, ct));
    }

    /// <summary>تفاصيل طلب مؤثّر واحد (للوحة الجانبية).</summary>
    [HttpGet("{leaveRequestId:guid}")]
    [Authorize(Policy = Policies.PayrollImpactRead)]
    public async Task<IActionResult> GetById(Guid leaveRequestId, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(leaveRequestId, ct));

    /// <summary>تحديث المراجعة المالية لطلب مؤثّر (Admin/HR) — حالة + ملاحظة فقط، لا يمسّ الطلب الأصلي.</summary>
    [HttpPatch("{leaveRequestId:guid}/review")]
    [Authorize(Policy = Policies.PayrollImpactManage)]
    public async Task<IActionResult> Review(Guid leaveRequestId, [FromBody] PayrollImpactReviewRequest request, CancellationToken ct)
        => FromResult(await _service.ReviewAsync(leaveRequestId, request, ct));
}
