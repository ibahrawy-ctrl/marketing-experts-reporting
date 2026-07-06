using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api/clients")]
public class ClientsController : ApiControllerBase
{
    private readonly IClientService _service;

    public ClientsController(IClientService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ClientStatus? status, [FromQuery] Guid? accountManagerId,
        [FromQuery] bool includeClosed = false, CancellationToken ct = default)
        => FromResult(await _service.ListAsync(new ClientFilter(status, accountManagerId, includeClosed), ct));

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
        => FromResult(await _service.GetHealthAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => FromResult(await _service.GetAsync(id, ct));

    [HttpGet("{id:guid}/reports")]
    public async Task<IActionResult> Reports(Guid id, CancellationToken ct)
        => FromResult(await _service.GetReportsAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest request, CancellationToken ct)
        => FromResult(await _service.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
        => FromResult(await _service.ArchiveAsync(id, ct));

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
        => FromResult(await _service.ReactivateAsync(id, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await _service.DeleteAsync(id, ct));
}
