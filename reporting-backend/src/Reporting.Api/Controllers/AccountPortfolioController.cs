using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.AccountPortfolio;
using Reporting.Application.Common;

namespace Reporting.Api.Controllers;

// محفظة مدير الحساب (مشاريعي/عملائي) — عرض فقط. النطاق مفروض خادمًا على مشاريع المستخدم الحالي نفسه
// (Project.AccountManagerId == المستخدم). لا إنشاء/تعديل/حذف/اعتماد، لا KPI/تقييمات، لا مسودّات.
[Authorize(Policy = Policies.AccountPortfolioRead)]
[Route("api/account-portfolio")]
public class AccountPortfolioController : ApiControllerBase
{
    private readonly IAccountPortfolioService _service;

    public AccountPortfolioController(IAccountPortfolioService service) => _service = service;

    // مشاريع المستخدم الحالي (AccountManagerId == المستخدم).
    [HttpGet("projects")]
    public async Task<IActionResult> Projects(CancellationToken ct)
        => FromResult(await _service.GetMyProjectsAsync(ct));

    // مشروع واحد للمستخدم — 404 إن غير موجود، 403 إن خارج نطاقه.
    [HttpGet("projects/{id:guid}")]
    public async Task<IActionResult> Project(Guid id, CancellationToken ct)
        => FromResult(await _service.GetMyProjectAsync(id, ct));

    // عملاء المستخدم — مشتقّون حصرًا من مشاريعه المرئية.
    [HttpGet("clients")]
    public async Task<IActionResult> Clients(CancellationToken ct)
        => FromResult(await _service.GetMyClientsAsync(ct));

    // عميل واحد + مشاريع المستخدم المرئية التابعة له — 404/403.
    [HttpGet("clients/{id:guid}")]
    public async Task<IActionResult> Client(Guid id, CancellationToken ct)
        => FromResult(await _service.GetMyClientAsync(id, ct));

    // مخرجات مشروع معتمَدة (تُستثنى المسودّة/المُعادة) — 403 إن خارج النطاق.
    [HttpGet("projects/{id:guid}/outputs")]
    public async Task<IActionResult> Outputs(Guid id, CancellationToken ct)
        => FromResult(await _service.GetProjectOutputsAsync(id, ct));
}
