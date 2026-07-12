using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Clients;

namespace Reporting.Api.Controllers;

/// <summary>
/// ملفّ البراند للعميل (Client 360 — CPW-R1B) — علاقة 1:0..1. القراءة محكومة بنطاق رؤية العميل داخل الخدمة.
/// الكتابة (Upsert) Resource-Based داخل الخدمة (مدير أساسيّ ضمن النطاق أو مدير الحساب للعميل)، لذا لا سياسة
/// دور على الـController. GET يُرجِع كائنًا فارغًا (null) عند عدم وجود ملفّ بعد.
/// </summary>
[Authorize]
[Route("api/clients/{clientId:guid}/brand")]
public class ClientBrandController : ApiControllerBase
{
    private readonly IClientBrandService _service;

    public ClientBrandController(IClientBrandService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(Guid clientId, CancellationToken ct)
        => FromResult(await _service.GetAsync(clientId, ct));

    [HttpPut]
    public async Task<IActionResult> Upsert(Guid clientId, [FromBody] UpsertClientBrandProfileRequest request, CancellationToken ct)
        => FromResult(await _service.UpsertAsync(clientId, request, ct));
}
