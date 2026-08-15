using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Documents;

namespace Reporting.Api.Controllers;

/// <summary>
/// الروابط المهمّة للعميل (CPW-R1B2). نفس نموذج صلاحيّة المستندات: القراءة بنطاق رؤية العميل،
/// والكتابة لمدير الحساب أو لمدير أساسيّ يرى العميل — لذا لا سياسة دور على الـController.
/// لا حذف نهائيّ: التعطيل هو المسار الوحيد. الرابط مرجع عنوان فقط ولا يقبل أيّ سرّ مضمَّن.
/// </summary>
[Authorize]
[Route("api/clients/{clientId:guid}/links")]
public class ClientExternalLinksController : ApiControllerBase
{
    private readonly IClientExternalLinkService _service;

    public ClientExternalLinksController(IClientExternalLinkService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(Guid clientId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => FromResult(await _service.ListAsync(clientId, includeInactive, ct));

    [HttpPost]
    public async Task<IActionResult> Create(Guid clientId, [FromBody] CreateClientExternalLinkRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(clientId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid clientId, Guid id, [FromBody] UpdateClientExternalLinkRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(clientId, id, request, ct));

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid clientId, Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(clientId, id, true, ct));

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid clientId, Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(clientId, id, false, ct));
}
