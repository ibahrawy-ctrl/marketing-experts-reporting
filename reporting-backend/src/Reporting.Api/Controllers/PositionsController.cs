using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Positions;

namespace Reporting.Api.Controllers;

// إدارة المناصب المرنة (Phase 1A — رؤية فقط). كل العمليات للأدمن فقط عبر سياسة PositionManagement.
// المناصب توسّع نطاق الرؤية فقط — لا تمنح أي قدرة اعتماد/كتابة.
[Authorize(Policy = Policies.PositionManagement)]
[Route("api/positions")]
public class PositionsController : ApiControllerBase
{
    private readonly IPositionService _service;

    public PositionsController(IPositionService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));

    // مفاتيح الصلاحيات المتاحة في هذه المرحلة (للعرض في الواجهة).
    [HttpGet("permission-options")]
    public IActionResult PermissionOptions()
        => Ok(_service.PermissionOptions());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => FromResult(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePositionRequest req, CancellationToken ct)
        => FromResult(await _service.CreateAsync(req, CurrentUserId, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePositionRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(id, req, CurrentUserId, ct));

    [HttpPost("{id:guid}/enable")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(id, true, CurrentUserId, ct));

    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(id, false, CurrentUserId, ct));

    [HttpPost("{id:guid}/permissions")]
    public async Task<IActionResult> AddPermission(Guid id, [FromBody] PermissionKeyRequest req, CancellationToken ct)
        => FromResult(await _service.AddPermissionAsync(id, req.PermissionKey, CurrentUserId, ct));

    [HttpDelete("{id:guid}/permissions/{permissionKey}")]
    public async Task<IActionResult> RemovePermission(Guid id, string permissionKey, CancellationToken ct)
        => FromResult(await _service.RemovePermissionAsync(id, permissionKey, CurrentUserId, ct));

    [HttpPost("{id:guid}/scopes")]
    public async Task<IActionResult> AddScope(Guid id, [FromBody] AddPositionScopeRequest req, CancellationToken ct)
        => FromResult(await _service.AddScopeAsync(id, req, CurrentUserId, ct));

    [HttpDelete("{id:guid}/scopes/{scopeId:guid}")]
    public async Task<IActionResult> RemoveScope(Guid id, Guid scopeId, CancellationToken ct)
        => FromResult(await _service.RemoveScopeAsync(id, scopeId, CurrentUserId, ct));

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignPositionRequest req, CancellationToken ct)
        => FromResult(await _service.AssignAsync(id, req.UserId, CurrentUserId, ct));

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] AssignPositionRequest req, CancellationToken ct)
        => FromResult(await _service.RevokeAsync(id, req.UserId, CurrentUserId, ct));

    // المناصب المُسنَدة لمستخدم معيّن (لصفحة المستخدمين).
    [HttpGet("/api/users/{userId:guid}/positions")]
    public async Task<IActionResult> ForUser(Guid userId, CancellationToken ct)
        => Ok(await _service.ListForUserAsync(userId, ct));
}

/// <summary>جسم إضافة صلاحية لمنصب.</summary>
public record PermissionKeyRequest(string PermissionKey);
