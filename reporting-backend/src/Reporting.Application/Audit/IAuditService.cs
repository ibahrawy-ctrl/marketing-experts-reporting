namespace Reporting.Application.Audit;

/// <summary>سجل تدقيق غير قابل للتعديل لكل إجراء حسّاس.</summary>
public interface IAuditService
{
    Task LogAsync(Guid? actorId, string action, string entityType, Guid? entityId,
        string? dataJson = null, string? ipAddress = null, CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogDto>> ListAsync(AuditLogFilter filter, CancellationToken ct = default);
}
