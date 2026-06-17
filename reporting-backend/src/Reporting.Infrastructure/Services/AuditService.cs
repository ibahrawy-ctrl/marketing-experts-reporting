using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Domain.Entities.System;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db) => _db = db;

    public async Task LogAsync(Guid? actorId, string action, string entityType, Guid? entityId,
        string? dataJson = null, string? ipAddress = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DataJson = dataJson,
            IpAddress = ipAddress
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogDto>> ListAsync(AuditLogFilter filter, CancellationToken ct = default)
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (filter.ActorId is not null) q = q.Where(a => a.ActorId == filter.ActorId);
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) q = q.Where(a => a.EntityType == filter.EntityType);
        if (filter.EntityId is not null) q = q.Where(a => a.EntityId == filter.EntityId);
        if (!string.IsNullOrWhiteSpace(filter.Action)) q = q.Where(a => a.Action == filter.Action);

        var rows = await q.OrderByDescending(a => a.CreatedAtUtc).Take(500).ToListAsync(ct);

        var actorIds = rows.Where(r => r.ActorId is not null).Select(r => r.ActorId!.Value).Distinct().ToList();
        var names = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return rows.Select(a => new AuditLogDto(
            a.Id, a.ActorId, a.ActorId is Guid id ? names.GetValueOrDefault(id) : null,
            a.Action, a.EntityType, a.EntityId, a.DataJson, a.IpAddress, a.CreatedAtUtc)).ToList();
    }
}
