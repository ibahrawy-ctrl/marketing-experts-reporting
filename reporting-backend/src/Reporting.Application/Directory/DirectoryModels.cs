namespace Reporting.Application.Directory;

public record DirectoryUserDto(
    Guid Id,
    string FullName,
    string Email,
    bool IsActive,
    IReadOnlyList<string> Roles,
    Guid? DepartmentId,
    Guid? TeamId,
    Guid? ManagerId);

public record DepartmentDto(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? Code,
    Guid? ManagerId,
    bool IsActive);

public record TeamDto(
    Guid Id,
    string NameAr,
    string? NameEn,
    Guid DepartmentId,
    Guid? TeamLeaderId,
    bool IsActive);

public record JobRoleDto(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? Code,
    Guid? DepartmentId,
    bool IsActive);

/// <summary>صف في مصفوفة الأدوار: يصف نطاق الرؤية والصلاحيات لكل دور.</summary>
public record RoleAccessDto(
    string Role,
    string RoleLabelAr,
    string ScopeType,
    string ScopeDescriptionAr,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> PermissionLabelsAr);

public record UpdateUserRolesRequest(IReadOnlyList<string> Roles);

/// <summary>إنشاء مستخدم جديد في النظام — للأدمن فقط.</summary>
public record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    IReadOnlyList<string> Roles,
    Guid? DepartmentId,
    Guid? TeamId,
    Guid? ManagerId);

/// <summary>تعديل بيانات مستخدم قائم (الاسم/البريد/التفعيل/الانتماء التنظيمي) — للأدمن فقط.</summary>
public record UpdateUserRequest(
    string FullName,
    string Email,
    bool IsActive,
    Guid? DepartmentId,
    Guid? TeamId,
    Guid? ManagerId);

/// <summary>إضافة/إزالة عضو من فريق.</summary>
public record TeamMemberRequest(Guid UserId);

/// <summary>إنشاء فريق جديد — للأدمن فقط.</summary>
public record CreateTeamRequest(
    string NameAr,
    string? NameEn,
    Guid DepartmentId,
    Guid? TeamLeaderId);

/// <summary>تعديل بيانات فريق قائم — للأدمن فقط.</summary>
public record UpdateTeamRequest(
    string NameAr,
    string? NameEn,
    Guid DepartmentId,
    Guid? TeamLeaderId,
    bool IsActive);

/// <summary>إنشاء إدارة جديدة — للأدمن فقط.</summary>
public record CreateDepartmentRequest(
    string NameAr,
    string? NameEn,
    string? Code,
    Guid? ManagerId);

/// <summary>تعديل بيانات إدارة قائمة — للأدمن فقط.</summary>
public record UpdateDepartmentRequest(
    string NameAr,
    string? NameEn,
    string? Code,
    Guid? ManagerId,
    bool IsActive);
