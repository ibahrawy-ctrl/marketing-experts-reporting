using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api/management-notes")]
public class ManagementNotesController : ApiControllerBase
{
    private readonly IManagementNoteService _service;

    public ManagementNotesController(IManagementNoteService service) => _service = service;

    // ملاحظات كيان معيّن حسب السياق (تُقيَّد بمن يحق له رؤية الكيان).
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ManagementNoteEntityType entityType, [FromQuery] Guid entityId, CancellationToken ct)
        => FromResult(await _service.ListAsync(entityType, entityId, ct));

    [HttpPost]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> Create([FromBody] CreateManagementNoteRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(request, ct));

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
        => FromResult(await _service.ResolveAsync(id, ct));
}
