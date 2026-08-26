using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Directory;

namespace Reporting.Api.Controllers;

// دليل تنظيمي للقراءة فقط (مستخدمون/إدارات/فرق) لدعم قوائم الاختيار والفلاتر في الواجهة.
[Authorize]
[Route("api/directory")]
public class DirectoryController : ApiControllerBase
{
    private readonly IDirectoryService _service;

    public DirectoryController(IDirectoryService service) => _service = service;

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await _service.ListUsersAsync(includeInactive, ct));

    [HttpGet("departments")]
    public async Task<IActionResult> Departments(CancellationToken ct)
        => Ok(await _service.ListDepartmentsAsync(ct));

    [HttpGet("teams")]
    public async Task<IActionResult> Teams(CancellationToken ct)
        => Ok(await _service.ListTeamsAsync(ct));

    [HttpGet("job-roles")]
    public async Task<IActionResult> JobRoles([FromQuery] bool activeOnly, CancellationToken ct)
        => Ok(await _service.ListJobRolesAsync(activeOnly, ct));

    // إدارة المسمّيات الوظيفية (قائمة بعدّادات الموظفين/القوالب) — Admin/CeoSupport/HR/GM/CEO.
    [HttpGet("job-roles/manage")]
    [Authorize(Policy = Policies.UserJobRoleManagement)]
    public async Task<IActionResult> JobRolesManage(CancellationToken ct)
        => Ok(await _service.ListJobRolesWithCountsAsync(ct));

    // إنشاء مسمّى وظيفي جديد — Admin/CeoSupport/HR/GM/CEO.
    [HttpPost("job-roles")]
    [Authorize(Policy = Policies.UserJobRoleManagement)]
    public async Task<IActionResult> CreateJobRole([FromBody] CreateJobRoleRequest req, CancellationToken ct)
        => FromResult(await _service.CreateJobRoleAsync(req, CurrentUserId, ct));

    // تعديل بيانات مسمّى وظيفي — Admin/CeoSupport/HR/GM/CEO.
    [HttpPut("job-roles/{id:guid}")]
    [Authorize(Policy = Policies.UserJobRoleManagement)]
    public async Task<IActionResult> UpdateJobRole(Guid id, [FromBody] UpdateJobRoleRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateJobRoleAsync(id, req, CurrentUserId, ct));

    // أرشفة مسمّى وظيفي (بلا حذف صلب) — Admin/CeoSupport/HR/GM/CEO.
    [HttpPost("job-roles/{id:guid}/archive")]
    [Authorize(Policy = Policies.UserJobRoleManagement)]
    public async Task<IActionResult> ArchiveJobRole(Guid id, CancellationToken ct)
        => FromResult(await _service.SetJobRoleActiveAsync(id, false, CurrentUserId, ct));

    // إعادة تفعيل مسمّى وظيفي مؤرشف — Admin/CeoSupport/HR/GM/CEO.
    [HttpPost("job-roles/{id:guid}/reactivate")]
    [Authorize(Policy = Policies.UserJobRoleManagement)]
    public async Task<IActionResult> ReactivateJobRole(Guid id, CancellationToken ct)
        => FromResult(await _service.SetJobRoleActiveAsync(id, true, CurrentUserId, ct));

    // ===== دليل الموارد البشرية المخصّص (قراءة فقط لحزمة A) — Admin/CeoSupport/HR/GM/CEO (Policy=HrDirectoryRead) =====
    // قوائم اختيار على مستوى الشركة لأغراض: تعديل الاسم، النقل التنظيمي، متابعة الالتزام. منفصلة عن الدليل العام أعلاه.
    // لا تمنح أيّ قدرة كتابة ولا رؤية محتوى تقارير؛ الحسابات الحسّاسة تظهر بعلم IsSensitive/CanEdit=false ويمنعها الخادم.

    [HttpGet("hr/users")]
    [Authorize(Policy = Policies.HrDirectoryRead)]
    public async Task<IActionResult> HrUsers([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await _service.ListHrDirectoryUsersAsync(includeInactive, User.IsInRole(Roles.Admin), ct));

    [HttpGet("hr/departments")]
    [Authorize(Policy = Policies.HrDirectoryRead)]
    public async Task<IActionResult> HrDepartments(CancellationToken ct)
        => Ok(await _service.ListHrDirectoryDepartmentsAsync(ct));

    [HttpGet("hr/teams")]
    [Authorize(Policy = Policies.HrDirectoryRead)]
    public async Task<IActionResult> HrTeams(CancellationToken ct)
        => Ok(await _service.ListHrDirectoryTeamsAsync(ct));

    [HttpGet("hr/managers")]
    [Authorize(Policy = Policies.HrDirectoryRead)]
    public async Task<IActionResult> HrManagers(CancellationToken ct)
        => Ok(await _service.ListHrDirectoryManagersAsync(User.IsInRole(Roles.Admin), ct));

    // مصفوفة الأدوار: نطاق الرؤية والصلاحيات لكل دور (لعرضها كمرجع في لوحة الأدمن).
    [HttpGet("role-matrix")]
    public IActionResult RoleMatrix() => Ok(_service.GetRoleMatrix());

    // توزيع/تعديل أدوار مستخدم — Admin + CEO (GOV-R1).
    [HttpPut("users/{id:guid}/roles")]
    [Authorize(Policy = Policies.UserManagement)]
    public async Task<IActionResult> UpdateUserRoles(Guid id, [FromBody] UpdateUserRolesRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateUserRolesAsync(id, req.Roles ?? new List<string>(), CurrentUserId, ct));

    // قراءة مفاتيح الصلاحيّات الدقيقة (perm) الممنوحة لمستخدم — Admin + CEO (نفس سلطة توزيع الأدوار).
    [HttpGet("users/{id:guid}/permissions")]
    [Authorize(Policy = Policies.UserManagement)]
    public async Task<IActionResult> UserPermissions(Guid id, CancellationToken ct)
        => FromResult(await _service.GetUserPermissionsAsync(id, ct));

    // ضبط مفاتيح perm لمستخدم (منح وإلغاء معًا) — Admin + CEO. مُدقَّق وIdempotent.
    // الإلغاء يسري على الرمز الحاليّ عند تجديده أو إعادة تسجيل الدخول (المفاتيح محقونة في JWT).
    [HttpPut("users/{id:guid}/permissions")]
    [Authorize(Policy = Policies.UserManagement)]
    public async Task<IActionResult> SetUserPermissions(Guid id, [FromBody] SetUserPermissionsRequest req, CancellationToken ct)
        => FromResult(await _service.SetUserPermissionsAsync(
            id, req.Permissions ?? new List<string>(), CurrentUserId, ct));

    // إنشاء مستخدم جديد — Admin + CEO (GOV-R1).
    [HttpPost("users")]
    [Authorize(Policy = Policies.UserManagement)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req, CancellationToken ct)
        => FromResult(await _service.CreateUserAsync(req, ct));

    // تعديل بيانات مستخدم (يشمل التفعيل/التعطيل IsActive) — Admin + CEO (GOV-R1).
    [HttpPut("users/{id:guid}")]
    [Authorize(Policy = Policies.UserManagement)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateUserAsync(id, req, CurrentUserId, ct));

    // حذف مستخدم — Admin + CEO (GOV-R1).
    [HttpDelete("users/{id:guid}")]
    [Authorize(Policy = Policies.UserManagement)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
        => FromResult(await _service.DeleteUserAsync(id, CurrentUserId, ct));

    // إعادة تعيين كلمة مرور مستخدم — Admin + CeoSupport (فاطمة) فقط. الحماية الإضافية لحسابات Admin في الخدمة.
    [HttpPost("users/{id:guid}/reset-password")]
    [Authorize(Policy = Policies.UserPasswordReset)]
    public async Task<IActionResult> ResetUserPassword(Guid id, [FromBody] ResetPasswordRequest req, CancellationToken ct)
        => FromResult(await _service.ResetUserPasswordAsync(id, req.NewPassword, CurrentUserId, User.IsInRole(Roles.Admin), ct));

    // تعديل المسمّى الوظيفي للموظف فقط (سطح مخصّص) — Admin/CeoSupport/HR/GM/CEO. لا يمسّ أي حقل آخر.
    [HttpPatch("users/{id:guid}/job-role")]
    [Authorize(Policy = Policies.UserJobRoleManagement)]
    public async Task<IActionResult> UpdateUserJobRole(Guid id, [FromBody] UpdateUserJobRoleRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateUserJobRoleAsync(id, req, CurrentUserId, ct));

    // تعديل البيانات الأساسية غير الحسّاسة للموظف (الاسم فقط) — Admin/CeoSupport/HR. لا يمسّ الأدوار/الصلاحيات/كلمة المرور/التفعيل.
    [HttpPatch("users/{id:guid}/basic")]
    [Authorize(Policy = Policies.UserBasicManagement)]
    public async Task<IActionResult> UpdateUserBasic(Guid id, [FromBody] UpdateUserBasicRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateUserBasicAsync(id, req, CurrentUserId, User.IsInRole(Roles.Admin), ct));

    // تعديل الانتماء التنظيمي للموظف (الإدارة/الفريق/المدير) — Admin/CeoSupport/HR/GM/CEO. القيود الأمنية في طبقة الخدمة.
    [HttpPatch("users/{id:guid}/org-assignment")]
    [Authorize(Policy = Policies.UserOrgAssignment)]
    public async Task<IActionResult> UpdateUserOrgAssignment(Guid id, [FromBody] UpdateUserOrgAssignmentRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateUserOrgAssignmentAsync(id, req, CurrentUserId, User.IsInRole(Roles.Admin), ct));

    // إضافة عضو إلى فريق — للمستوى الإداري الأعلى فقط (Admin/CEO/GM؛ + HR لاحقًا). Manager/TeamLeader ممنوعون.
    [HttpPost("teams/{teamId:guid}/members")]
    [Authorize(Policy = Policies.TeamManagement)]
    public async Task<IActionResult> AddTeamMember(Guid teamId, [FromBody] TeamMemberRequest req, CancellationToken ct)
        => FromResult(await _service.AddTeamMemberAsync(teamId, req.UserId, ct));

    // إزالة عضو من فريق — للمستوى الإداري الأعلى فقط (Admin/CEO/GM؛ + HR لاحقًا). Manager/TeamLeader ممنوعون.
    [HttpDelete("teams/{teamId:guid}/members/{userId:guid}")]
    [Authorize(Policy = Policies.TeamManagement)]
    public async Task<IActionResult> RemoveTeamMember(Guid teamId, Guid userId, CancellationToken ct)
        => FromResult(await _service.RemoveTeamMemberAsync(teamId, userId, ct));

    // ===== عضويات الفريق الإضافية (MULTI-TEAM-MEMBERSHIP-MVP-R1) — للأدمن فقط =====
    // منفصلة عن إضافة العضو الأساسي أعلاه: لا تنقل المستخدم ولا تغيّر أيّ حقل تنظيمي عليه.

    // أعضاء الفريق مفصولين: الأساسيون (TeamId) والإضافيون (جدول العضويات) — للأدمن فقط.
    [HttpGet("teams/{teamId:guid}/memberships")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> TeamMemberships(Guid teamId, CancellationToken ct)
        => FromResult(await _service.ListTeamMembershipsAsync(teamId, ct));

    // إضافة عضو إضافي (ثانوي) إلى فريق دون نقله من فريقه الأساسي — للأدمن فقط.
    [HttpPost("teams/{teamId:guid}/additional-members")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> AddAdditionalTeamMember(Guid teamId, [FromBody] AddAdditionalMemberRequest req, CancellationToken ct)
        => FromResult(await _service.AddAdditionalTeamMemberAsync(teamId, req, CurrentUserId, ct));

    // إزالة/إلغاء تفعيل عضوية إضافية لمستخدم في فريق — للأدمن فقط.
    [HttpDelete("teams/{teamId:guid}/additional-members/{userId:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> RemoveAdditionalTeamMember(Guid teamId, Guid userId, CancellationToken ct)
        => FromResult(await _service.RemoveAdditionalTeamMemberAsync(teamId, userId, CurrentUserId, ct));

    // عضويات المستخدم: فريقه الأساسي وفرقه الإضافية النشطة — للأدمن فقط.
    [HttpGet("users/{userId:guid}/team-memberships")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> UserTeamMemberships(Guid userId, CancellationToken ct)
        => FromResult(await _service.ListUserTeamMembershipsAsync(userId, ct));

    // إنشاء فريق جديد (تغيير هيكلي للمنظومة) — للأدمن فقط.
    [HttpPost("teams")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequest req, CancellationToken ct)
        => FromResult(await _service.CreateTeamAsync(req, ct));

    // تعديل بيانات فريق (الاسم/القائد/التفعيل/نقل الإدارة مع مزامنة الأعضاء) — Admin/CEO/GM ضمن النطاق. Manager/TeamLeader ممنوعون.
    [HttpPut("teams/{teamId:guid}")]
    [Authorize(Policy = Policies.TeamManagement)]
    public async Task<IActionResult> UpdateTeam(Guid teamId, [FromBody] UpdateTeamRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateTeamAsync(teamId, req, CurrentUserId, ct));

    // حذف فريق (محروس ضدّ وجود أعضاء/مشاريع) — للأدمن فقط.
    [HttpDelete("teams/{teamId:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> DeleteTeam(Guid teamId, CancellationToken ct)
        => FromResult(await _service.DeleteTeamAsync(teamId, CurrentUserId, ct));

    // ملخّص الفرق بعدّاداتها (أعضاء/مشاريع/عدم تطابق الإدارة) — قراءة فقط ضمن النطاق.
    [HttpGet("teams/summary")]
    public async Task<IActionResult> TeamSummaries(CancellationToken ct)
        => Ok(await _service.ListTeamSummariesAsync(ct));

    // ملخّص الإدارات مع فرقها وعدّاداتها — قراءة فقط ضمن النطاق.
    [HttpGet("departments/summary")]
    public async Task<IActionResult> DepartmentSummaries(CancellationToken ct)
        => Ok(await _service.ListDepartmentSummariesAsync(ct));

    // ملخّص أثر نقل فريق إلى إدارة مستهدفة قبل الحفظ — Admin/CEO/GM ضمن النطاق، قراءة فقط.
    [HttpGet("teams/{teamId:guid}/move-impact")]
    [Authorize(Policy = Policies.TeamManagement)]
    public async Task<IActionResult> TeamMoveImpact(Guid teamId, [FromQuery] Guid targetDepartmentId, CancellationToken ct)
        => FromResult(await _service.GetTeamMoveImpactAsync(teamId, targetDepartmentId, ct));

    // إنشاء إدارة جديدة — للأدمن فقط.
    [HttpPost("departments")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest req, CancellationToken ct)
        => FromResult(await _service.CreateDepartmentAsync(req, ct));

    // تعديل بيانات إدارة — للأدمن فقط.
    [HttpPut("departments/{departmentId:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> UpdateDepartment(Guid departmentId, [FromBody] UpdateDepartmentRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateDepartmentAsync(departmentId, req, ct));

    // حذف إدارة — للأدمن فقط.
    [HttpDelete("departments/{departmentId:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> DeleteDepartment(Guid departmentId, CancellationToken ct)
        => FromResult(await _service.DeleteDepartmentAsync(departmentId, ct));
}
