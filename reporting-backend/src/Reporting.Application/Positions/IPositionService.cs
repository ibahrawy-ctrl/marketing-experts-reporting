using Reporting.Application.Common;

namespace Reporting.Application.Positions;

/// <summary>
/// إدارة المناصب المرنة (Phase 1A — رؤية فقط). كل العمليات للأدمن فقط عبر سياسة PositionManagement.
/// لا تمنح أي قدرة اعتماد/كتابة — توسّع نطاق الرؤية فقط عبر ScopeResolver.
/// </summary>
public interface IPositionService
{
    Task<IReadOnlyList<PositionDto>> ListAsync(CancellationToken ct = default);
    Task<Result<PositionDto>> GetAsync(Guid id, CancellationToken ct = default);
    IReadOnlyList<PositionPermissionOptionDto> PermissionOptions();

    Task<Result<PositionDto>> CreateAsync(CreatePositionRequest req, Guid actorId, CancellationToken ct = default);
    Task<Result<PositionDto>> UpdateAsync(Guid id, UpdatePositionRequest req, Guid actorId, CancellationToken ct = default);
    Task<Result<PositionDto>> SetActiveAsync(Guid id, bool isActive, Guid actorId, CancellationToken ct = default);

    Task<Result<PositionDto>> AddPermissionAsync(Guid id, string permissionKey, Guid actorId, CancellationToken ct = default);
    Task<Result<PositionDto>> RemovePermissionAsync(Guid id, string permissionKey, Guid actorId, CancellationToken ct = default);

    Task<Result<PositionDto>> AddScopeAsync(Guid id, AddPositionScopeRequest req, Guid actorId, CancellationToken ct = default);
    Task<Result<PositionDto>> RemoveScopeAsync(Guid id, Guid scopeId, Guid actorId, CancellationToken ct = default);

    Task<Result> AssignAsync(Guid id, Guid userId, Guid actorId, CancellationToken ct = default);
    Task<Result> RevokeAsync(Guid id, Guid userId, Guid actorId, CancellationToken ct = default);

    /// <summary>المناصب المُسنَدة لمستخدم معيّن (لصفحة المستخدمين).</summary>
    Task<IReadOnlyList<UserPositionDto>> ListForUserAsync(Guid userId, CancellationToken ct = default);
}
