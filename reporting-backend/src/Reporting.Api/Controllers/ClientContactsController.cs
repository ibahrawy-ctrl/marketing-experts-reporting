using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Clients;

namespace Reporting.Api.Controllers;

/// <summary>
/// جهات اتصال العميل (Client 360 — CPW-R1B). القراءة محكومة بنطاق رؤية العميل داخل الخدمة.
/// الكتابة Resource-Based داخل الخدمة (مدير أساسيّ ضمن النطاق أو مدير الحساب للعميل)، لذا لا سياسة دور
/// على الـController — لتشمل مدير الحساب الذي قد لا يكون في دور إداريّ. التعطيل بدل الحذف.
/// </summary>
[Authorize]
[Route("api/clients/{clientId:guid}/contacts")]
public class ClientContactsController : ApiControllerBase
{
    private readonly IClientContactService _service;

    public ClientContactsController(IClientContactService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(Guid clientId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => FromResult(await _service.ListAsync(clientId, includeInactive, ct));

    [HttpPost]
    public async Task<IActionResult> Create(Guid clientId, [FromBody] CreateClientContactRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(clientId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid clientId, Guid id, [FromBody] UpdateClientContactRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(clientId, id, request, ct));

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid clientId, Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(clientId, id, true, ct));

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid clientId, Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(clientId, id, false, ct));
}
