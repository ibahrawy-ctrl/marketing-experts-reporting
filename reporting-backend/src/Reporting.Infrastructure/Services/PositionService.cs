using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Positions;
using Reporting.Domain.Entities.Positions;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// إدارة المناصب المرنة (Phase 1A — رؤية فقط). كل التحقّقات والأكواد رسائل عربية.
/// لا تمنح أي قدرة اعتماد/كتابة — أثرها الوحيد توسيع نطاق الرؤية عبر ScopeResolver.
/// </summary>
public class PositionService : IPositionService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public PositionService(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public IReadOnlyList<PositionPermissionOptionDto> PermissionOptions() =>
        PositionPermissions.Allowed
            .Select(k => new PositionPermissionOptionDto(k, PositionPermissions.LabelsAr.GetValueOrDefault(k, k)))
            .ToList();

    public async Task<IReadOnlyList<PositionDto>> ListAsync(CancellationToken ct = default)
    {
        var positions = await _db.Positions.AsNoTracking()
            .Include(p => p.Permissions)
            .Include(p => p.Scopes)
            .Include(p => p.Assignments)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        return await BuildManyAsync(positions, ct);
    }

    public async Task<Result<PositionDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var p = await LoadAsync(id, ct);
        if (p is null) return Result<PositionDto>.Failure("المنصب غير موجود.", "position.not_found");
        return Result<PositionDto>.Success(await BuildAsync(p, ct));
    }

    public async Task<Result<PositionDto>> CreateAsync(CreatePositionRequest req, Guid actorId, CancellationToken ct = default)
    {
        var code = (req.Code ?? string.Empty).Trim();
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code)) return Result<PositionDto>.Failure("رمز المنصب مطلوب.", "position.code_required");
        if (string.IsNullOrWhiteSpace(name)) return Result<PositionDto>.Failure("اسم المنصب مطلوب.", "position.name_required");
        if (await _db.Positions.AnyAsync(p => p.Code == code, ct))
            return Result<PositionDto>.Failure("رمز المنصب مستخدم بالفعل.", "position.code_duplicate.conflict");

        var entity = new Position { Code = code, Name = name, Description = req.Description?.Trim(), IsActive = true };
        _db.Positions.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "position.created", "Position", entity.Id,
            JsonSerializer.Serialize(new { entity.Code, entity.Name }), null, ct);
        return Result<PositionDto>.Success(await BuildAsync(await LoadAsync(entity.Id, ct) ?? entity, ct));
    }

    public async Task<Result<PositionDto>> UpdateAsync(Guid id, UpdatePositionRequest req, Guid actorId, CancellationToken ct = default)
    {
        var p = await LoadAsync(id, ct);
        if (p is null) return Result<PositionDto>.Failure("المنصب غير موجود.", "position.not_found");
        var code = (req.Code ?? string.Empty).Trim();
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code)) return Result<PositionDto>.Failure("رمز المنصب مطلوب.", "position.code_required");
        if (string.IsNullOrWhiteSpace(name)) return Result<PositionDto>.Failure("اسم المنصب مطلوب.", "position.name_required");
        if (await _db.Positions.AnyAsync(x => x.Code == code && x.Id != id, ct))
            return Result<PositionDto>.Failure("رمز المنصب مستخدم بالفعل.", "position.code_duplicate.conflict");

        var old = new { p.Code, p.Name, p.Description };
        p.Code = code; p.Name = name; p.Description = req.Description?.Trim();
        p.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "position.updated", "Position", p.Id,
            JsonSerializer.Serialize(new { old, @new = new { p.Code, p.Name, p.Description } }), null, ct);
        return Result<PositionDto>.Success(await BuildAsync(p, ct));
    }

    public async Task<Result<PositionDto>> SetActiveAsync(Guid id, bool isActive, Guid actorId, CancellationToken ct = default)
    {
        var p = await LoadAsync(id, ct);
        if (p is null) return Result<PositionDto>.Failure("المنصب غير موجود.", "position.not_found");
        if (p.IsActive == isActive)
            return Result<PositionDto>.Failure(isActive ? "المنصب مُفعّل بالفعل." : "المنصب معطّل بالفعل.", "position.state_unchanged.conflict");
        p.IsActive = isActive;
        p.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "position.updated", "Position", p.Id,
            JsonSerializer.Serialize(new { p.Code, isActive }), null, ct);
        return Result<PositionDto>.Success(await BuildAsync(p, ct));
    }

    public async Task<Result<PositionDto>> AddPermissionAsync(Guid id, string permissionKey, Guid actorId, CancellationToken ct = default)
    {
        var p = await LoadAsync(id, ct);
        if (p is null) return Result<PositionDto>.Failure("المنصب غير موجود.", "position.not_found");
        var key = (permissionKey ?? string.Empty).Trim();
        if (!PositionPermissions.IsValid(key))
            return Result<PositionDto>.Failure("مفتاح صلاحية غير مسموح في هذه المرحلة.", "position.permission_invalid");
        if (p.Permissions.Any(x => x.PermissionKey == key))
            return Result<PositionDto>.Failure("الصلاحية مضافة بالفعل.", "position.permission_duplicate.conflict");

        _db.PositionPermissions.Add(new PositionPermission { PositionId = p.Id, PermissionKey = key });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "position.permission_changed", "Position", p.Id,
            JsonSerializer.Serialize(new { p.Code, added = key }), null, ct);
        return Result<PositionDto>.Success(await BuildAsync(await LoadAsync(id, ct) ?? p, ct));
    }

    public async Task<Result<PositionDto>> RemovePermissionAsync(Guid id, string permissionKey, Guid actorId, CancellationToken ct = default)
    {
        var p = await LoadAsync(id, ct);
        if (p is null) return Result<PositionDto>.Failure("المنصب غير موجود.", "position.not_found");
        var key = (permissionKey ?? string.Empty).Trim();
        var existing = p.Permissions.FirstOrDefault(x => x.PermissionKey == key);
        if (existing is null) return Result<PositionDto>.Failure("الصلاحية غير موجودة على المنصب.", "position.permission_not_found");

        _db.PositionPermissions.Remove(existing);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "position.permission_changed", "Position", p.Id,
            JsonSerializer.Serialize(new { p.Code, removed = key }), null, ct);
        return Result<PositionDto>.Success(await BuildAsync(await LoadAsync(id, ct) ?? p, ct));
    }

    public async Task<Result<PositionDto>> AddScopeAsync(Guid id, AddPositionScopeRequest req, Guid actorId, CancellationToken ct = default)
    {
        var p = await LoadAsync(id, ct);
        if (p is null) return Result<PositionDto>.Failure("المنصب غير موجود.", "position.not_found");

        var scope = new PositionScope { PositionId = p.Id, Kind = req.Kind };
        switch (req.Kind)
        {
            case PositionScopeKind.Department:
                if (req.DepartmentId is null) return Result<PositionDto>.Failure("معرّف الإدارة مطلوب لنطاق الإدارة.", "position.scope_department_required");
                if (!await _db.Departments.AnyAsync(d => d.Id == req.DepartmentId, ct))
                    return Result<PositionDto>.Failure("الإدارة غير موجودة.", "position.department_not_found");
                scope.DepartmentId = req.DepartmentId;
                break;
            case PositionScopeKind.Team:
                if (req.TeamId is null) return Result<PositionDto>.Failure("معرّف الفريق مطلوب لنطاق الفريق.", "position.scope_team_required");
                if (!await _db.Teams.AnyAsync(t => t.Id == req.TeamId, ct))
                    return Result<PositionDto>.Failure("الفريق غير موجود.", "position.team_not_found");
                scope.TeamId = req.TeamId;
                break;
            case PositionScopeKind.SpecificUsers:
                if (req.TargetUserId is null) return Result<PositionDto>.Failure("معرّف المستخدم مطلوب لنطاق مستخدمين محدّدين.", "position.scope_user_required");
                if (!await _db.Users.AnyAsync(u => u.Id == req.TargetUserId, ct))
                    return Result<PositionDto>.Failure("المستخدم غير موجود.", "position.user_not_found");
                scope.TargetUserId = req.TargetUserId;
                break;
            case PositionScopeKind.AllCompany:
                break;
            default:
                return Result<PositionDto>.Failure("نوع نطاق غير معروف.", "position.scope_kind_invalid");
        }

        _db.PositionScopes.Add(scope);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "position.scope_changed", "Position", p.Id,
            JsonSerializer.Serialize(new { p.Code, added = new { kind = req.Kind.ToString(), req.DepartmentId, req.TeamId, req.TargetUserId } }), null, ct);
        return Result<PositionDto>.Success(await BuildAsync(await LoadAsync(id, ct) ?? p, ct));
    }

    public async Task<Result<PositionDto>> RemoveScopeAsync(Guid id, Guid scopeId, Guid actorId, CancellationToken ct = default)
    {
        var p = await LoadAsync(id, ct);
        if (p is null) return Result<PositionDto>.Failure("المنصب غير موجود.", "position.not_found");
        var existing = p.Scopes.FirstOrDefault(s => s.Id == scopeId);
        if (existing is null) return Result<PositionDto>.Failure("النطاق غير موجود على المنصب.", "position.scope_not_found");

        _db.PositionScopes.Remove(existing);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "position.scope_changed", "Position", p.Id,
            JsonSerializer.Serialize(new { p.Code, removed = new { existing.Kind, existing.DepartmentId, existing.TeamId, existing.TargetUserId } }), null, ct);
        return Result<PositionDto>.Success(await BuildAsync(await LoadAsync(id, ct) ?? p, ct));
    }

    public async Task<Result> AssignAsync(Guid id, Guid userId, Guid actorId, CancellationToken ct = default)
    {
        var p = await _db.Positions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return Result.Failure("المنصب غير موجود.", "position.not_found");
        if (!await _db.Users.AnyAsync(u => u.Id == userId, ct))
            return Result.Failure("المستخدم غير موجود.", "position.user_not_found");
        if (await _db.UserPositions.AnyAsync(up => up.PositionId == id && up.UserId == userId, ct))
            return Result.Failure("المنصب مُسنَد لهذا المستخدم بالفعل.", "position.already_assigned.conflict");

        _db.UserPositions.Add(new UserPosition { PositionId = id, UserId = userId, AssignedBy = actorId });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "position.assigned", "Position", id,
            JsonSerializer.Serialize(new { p.Code, userId }), null, ct);
        return Result.Success();
    }

    public async Task<Result> RevokeAsync(Guid id, Guid userId, Guid actorId, CancellationToken ct = default)
    {
        var p = await _db.Positions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return Result.Failure("المنصب غير موجود.", "position.not_found");
        var existing = await _db.UserPositions.FirstOrDefaultAsync(up => up.PositionId == id && up.UserId == userId, ct);
        if (existing is null) return Result.Failure("المنصب غير مُسنَد لهذا المستخدم.", "position.not_assigned");

        _db.UserPositions.Remove(existing);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "position.revoked", "Position", id,
            JsonSerializer.Serialize(new { p.Code, userId }), null, ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<UserPositionDto>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.UserPositions.AsNoTracking()
            .Where(up => up.UserId == userId)
            .OrderBy(up => up.Position!.Name)
            .Select(up => new UserPositionDto(
                up.Id, up.Position!.Id, up.Position.Code, up.Position.Name, up.Position.IsActive))
            .ToListAsync(ct);
    }

    private Task<Position?> LoadAsync(Guid id, CancellationToken ct) =>
        _db.Positions
            .Include(p => p.Permissions)
            .Include(p => p.Scopes)
            .Include(p => p.Assignments)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    private async Task<PositionDto> BuildAsync(Position p, CancellationToken ct) =>
        (await BuildManyAsync(new[] { p }, ct))[0];

    private async Task<IReadOnlyList<PositionDto>> BuildManyAsync(IReadOnlyList<Position> positions, CancellationToken ct)
    {
        // حلّ أسماء المراجع (إدارات/فِرق/مستخدمين) دفعةً واحدة للعرض.
        var deptIds = positions.SelectMany(p => p.Scopes).Where(s => s.DepartmentId != null).Select(s => s.DepartmentId!.Value).Distinct().ToList();
        var teamIds = positions.SelectMany(p => p.Scopes).Where(s => s.TeamId != null).Select(s => s.TeamId!.Value).Distinct().ToList();
        var userIds = positions.SelectMany(p => p.Scopes).Where(s => s.TargetUserId != null).Select(s => s.TargetUserId!.Value).Distinct().ToList();

        var depts = await _db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);
        var teams = await _db.Teams.AsNoTracking().Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return positions.Select(p => new PositionDto(
            p.Id, p.Code, p.Name, p.Description, p.IsActive,
            p.Permissions.Select(x => x.PermissionKey).OrderBy(x => x).ToList(),
            p.Scopes.Select(s => new PositionScopeDto(
                s.Id, s.Kind,
                s.DepartmentId, s.DepartmentId != null ? depts.GetValueOrDefault(s.DepartmentId.Value) : null,
                s.TeamId, s.TeamId != null ? teams.GetValueOrDefault(s.TeamId.Value) : null,
                s.TargetUserId, s.TargetUserId != null ? users.GetValueOrDefault(s.TargetUserId.Value) : null))
                .ToList(),
            p.Assignments.Count)).ToList();
    }
}
