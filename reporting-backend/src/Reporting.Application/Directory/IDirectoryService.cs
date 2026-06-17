using Reporting.Application.Common;

namespace Reporting.Application.Directory;

public interface IDirectoryService
{
    Task<IReadOnlyList<DirectoryUserDto>> ListUsersAsync(bool includeInactive, CancellationToken ct = default);
    Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TeamDto>> ListTeamsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<JobRoleDto>> ListJobRolesAsync(CancellationToken ct = default);

    /// <summary>مصفوفة الأدوار: نطاق الرؤية والصلاحيات لكل دور في النظام.</summary>
    IReadOnlyList<RoleAccessDto> GetRoleMatrix();

    /// <summary>تحديث أدوار مستخدم (إضافة/إزالة) مع حواجز أمان ضد قفل النظام.</summary>
    Task<Result> UpdateUserRolesAsync(Guid userId, IReadOnlyList<string> roles, Guid actingUserId, CancellationToken ct = default);

    /// <summary>إنشاء مستخدم جديد مع أدواره وانتمائه التنظيمي — للأدمن فقط.</summary>
    Task<Result<DirectoryUserDto>> CreateUserAsync(CreateUserRequest req, CancellationToken ct = default);

    /// <summary>تعديل بيانات مستخدم قائم — للأدمن فقط.</summary>
    Task<Result<DirectoryUserDto>> UpdateUserAsync(Guid userId, UpdateUserRequest req, Guid actingUserId, CancellationToken ct = default);

    /// <summary>حذف مستخدم من النظام مع حواجز أمان (لا حذف ذاتي ولا آخر أدمن).</summary>
    Task<Result> DeleteUserAsync(Guid userId, Guid actingUserId, CancellationToken ct = default);

    /// <summary>إضافة عضو إلى فريق (يضبط الفريق والإدارة تلقائيًا).</summary>
    Task<Result> AddTeamMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default);

    /// <summary>إزالة عضو من فريق (يفرّغ ربط الفريق).</summary>
    Task<Result> RemoveTeamMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default);

    /// <summary>إنشاء فريق جديد — للأدمن فقط.</summary>
    Task<Result<TeamDto>> CreateTeamAsync(CreateTeamRequest req, CancellationToken ct = default);

    /// <summary>تعديل بيانات فريق قائم — للأدمن فقط.</summary>
    Task<Result<TeamDto>> UpdateTeamAsync(Guid teamId, UpdateTeamRequest req, CancellationToken ct = default);

    /// <summary>حذف فريق (يفرّغ ربط الأعضاء بالفريق) — للأدمن فقط.</summary>
    Task<Result> DeleteTeamAsync(Guid teamId, CancellationToken ct = default);

    /// <summary>إنشاء إدارة جديدة — للأدمن فقط.</summary>
    Task<Result<DepartmentDto>> CreateDepartmentAsync(CreateDepartmentRequest req, CancellationToken ct = default);

    /// <summary>تعديل بيانات إدارة قائمة — للأدمن فقط.</summary>
    Task<Result<DepartmentDto>> UpdateDepartmentAsync(Guid departmentId, UpdateDepartmentRequest req, CancellationToken ct = default);

    /// <summary>حذف إدارة (يمنع الحذف إذا كانت بها فرق) — للأدمن فقط.</summary>
    Task<Result> DeleteDepartmentAsync(Guid departmentId, CancellationToken ct = default);
}
