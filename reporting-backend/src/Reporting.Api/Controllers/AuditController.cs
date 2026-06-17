using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Audit;
using Reporting.Application.Common;

namespace Reporting.Api.Controllers;

[Authorize(Policy = Policies.ExecutiveOnly)]
[Route("api/audit-logs")]
public class AuditController : ApiControllerBase
{
    private readonly IAuditService _service;

    public AuditController(IAuditService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? actorId, [FromQuery] string? entityType,
        [FromQuery] Guid? entityId, [FromQuery] string? action, CancellationToken ct)
        => Ok(await _service.ListAsync(new AuditLogFilter(actorId, entityType, entityId, action), ct));
}
