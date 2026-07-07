using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Services;

namespace Reporting.Api.Controllers;

[Authorize]
[Route("api/services")]
public class ServicesController : ApiControllerBase
{
    private readonly IServiceCatalogService _service;
    public ServicesController(IServiceCatalogService service) => _service = service;

    [HttpGet] public async Task<IActionResult> ListActive(CancellationToken ct) => Ok(await _service.ListAsync(includeInactive: false, ct));
}
