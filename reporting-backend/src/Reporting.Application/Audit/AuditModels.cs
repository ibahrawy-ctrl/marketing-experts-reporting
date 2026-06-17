namespace Reporting.Application.Audit;

public record AuditLogDto(
    Guid Id,
    Guid? ActorId,
    string? ActorName,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? DataJson,
    string? IpAddress,
    DateTime CreatedAtUtc);

public record AuditLogFilter(
    Guid? ActorId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Action = null);
