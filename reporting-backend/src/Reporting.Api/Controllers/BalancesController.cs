using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;

namespace Reporting.Api.Controllers;

/// <summary>
/// أرصدة الإجازات والأذونات (V1.1 — خدمات الموظف). الرصيد مشتقّ من حركات Ledger.
/// «رصيدي» متاح لكل مستخدم مصادَق (الخدمة تقصره على نفسه)؛ الإدارة/HR (BalanceManagement) تدير الجميع.
/// </summary>
[Authorize]
[Route("api/balances")]
public class BalancesController : ApiControllerBase
{
    private readonly IBalanceService _service;

    public BalancesController(IBalanceService service) => _service = service;

    /// <summary>أرصدة المستخدم الحالي (هو صاحبها) — مقصورة على النفس في الخدمة.</summary>
    [HttpGet("/api/me/balances")]
    public async Task<IActionResult> MyBalances([FromQuery] int? year, CancellationToken ct)
        => FromResult(await _service.GetMyBalancesAsync(year, ct));

    [HttpGet("employees")]
    [Authorize(Policy = Policies.BalanceManagement)]
    public async Task<IActionResult> ListEmployees(
        [FromQuery] string? q, [FromQuery] Guid? departmentId, [FromQuery] Guid? teamId, [FromQuery] int? year, CancellationToken ct)
        => FromResult(await _service.ListEmployeesAsync(q, departmentId, teamId, year, ct));

    [HttpGet("employees/{userId:guid}/ledger")]
    [Authorize(Policy = Policies.BalanceManagement)]
    public async Task<IActionResult> EmployeeLedger(Guid userId, [FromQuery] int? year, CancellationToken ct)
        => FromResult(await _service.GetEmployeeLedgerAsync(userId, year, ct));

    [HttpPost("employees/{userId:guid}/opening")]
    [Authorize(Policy = Policies.BalanceManagement)]
    public async Task<IActionResult> SetOpening(Guid userId, [FromBody] OpeningBalanceRequest request, CancellationToken ct)
        => FromResult(await _service.SetOpeningBalanceAsync(userId, request, ct));

    [HttpPost("employees/{userId:guid}/adjust")]
    [Authorize(Policy = Policies.BalanceManagement)]
    public async Task<IActionResult> Adjust(Guid userId, [FromBody] BalanceAdjustmentRequest request, CancellationToken ct)
        => FromResult(await _service.AdjustAsync(userId, request, ct));
}
