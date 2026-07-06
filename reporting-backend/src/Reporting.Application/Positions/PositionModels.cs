using Reporting.Domain.Enums;

namespace Reporting.Application.Positions;

/// <summary>منصب مرن مع صلاحياته ونطاقاته وعدد المُسنَدين — لشاشة إدارة المناصب (Admin فقط).</summary>
public record PositionDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<PositionScopeDto> Scopes,
    int AssignedUsersCount);

/// <summary>نطاق رؤية واحد ضمن منصب (مع أسماء معروضة اختيارية للإدارة/الفريق/المستخدم).</summary>
public record PositionScopeDto(
    Guid Id,
    PositionScopeKind Kind,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? TeamId,
    string? TeamName,
    Guid? TargetUserId,
    string? TargetUserName);

/// <summary>إسناد منصب لمستخدم (للعرض في صفحة المستخدمين).</summary>
public record UserPositionDto(
    Guid Id,
    Guid PositionId,
    string PositionCode,
    string PositionName,
    bool PositionIsActive);

/// <summary>مفتاح صلاحية متاح في هذه المرحلة (للعرض في الواجهة).</summary>
public record PositionPermissionOptionDto(string Key, string LabelAr);

public record CreatePositionRequest(string Code, string Name, string? Description);

public record UpdatePositionRequest(string Code, string Name, string? Description);

public record AddPositionScopeRequest(
    PositionScopeKind Kind,
    Guid? DepartmentId,
    Guid? TeamId,
    Guid? TargetUserId);

public record AssignPositionRequest(Guid UserId);
