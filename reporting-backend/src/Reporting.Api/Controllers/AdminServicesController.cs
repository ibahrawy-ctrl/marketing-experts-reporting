using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Services;

namespace Reporting.Api.Controllers;

[Authorize(Policy = Policies.TemplateGovernance)]
[Route("api/admin/services")]
public class AdminServicesController : ApiControllerBase
{
    private readonly IServiceCatalogService _service;
    public AdminServicesController(IServiceCatalogService service) => _service = service;

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await _service.ListAsync(includeInactive: true, ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => FromResult(await _service.GetAsync(id, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CreateServiceRequest req, CancellationToken ct) => FromResult(await _service.CreateAsync(req, CurrentUserId, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] UpdateServiceRequest req, CancellationToken ct) => FromResult(await _service.UpdateAsync(id, req, CurrentUserId, ct));
    [HttpPatch("{id:guid}/activate")] public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => FromResult(await _service.SetActiveAsync(id, true, CurrentUserId, ct));
    [HttpPatch("{id:guid}/deactivate")] public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => FromResult(await _service.SetActiveAsync(id, false, CurrentUserId, ct));
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => FromResult(await _service.DeleteAsync(id, CurrentUserId, ct));
}
