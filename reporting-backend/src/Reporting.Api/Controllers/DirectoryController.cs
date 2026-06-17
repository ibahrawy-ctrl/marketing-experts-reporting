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
    public async Task<IActionResult> JobRoles(CancellationToken ct)
        => Ok(await _service.ListJobRolesAsync(ct));

    // مصفوفة الأدوار: نطاق الرؤية والصلاحيات لكل دور (لعرضها كمرجع في لوحة الأدمن).
    [HttpGet("role-matrix")]
    public IActionResult RoleMatrix() => Ok(_service.GetRoleMatrix());

    // توزيع/تعديل أدوار مستخدم — للأدمن فقط.
    [HttpPut("users/{id:guid}/roles")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> UpdateUserRoles(Guid id, [FromBody] UpdateUserRolesRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateUserRolesAsync(id, req.Roles ?? new List<string>(), CurrentUserId, ct));

    // إنشاء مستخدم جديد — للأدمن فقط.
    [HttpPost("users")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req, CancellationToken ct)
        => FromResult(await _service.CreateUserAsync(req, ct));

    // تعديل بيانات مستخدم — للأدمن فقط.
    [HttpPut("users/{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateUserAsync(id, req, CurrentUserId, ct));

    // حذف مستخدم — للأدمن فقط.
    [HttpDelete("users/{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
        => FromResult(await _service.DeleteUserAsync(id, CurrentUserId, ct));

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

    // إنشاء فريق جديد (تغيير هيكلي للمنظومة) — للأدمن فقط.
    [HttpPost("teams")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequest req, CancellationToken ct)
        => FromResult(await _service.CreateTeamAsync(req, ct));

    // تعديل بيانات فريق (الاسم/القائد) — للمستوى الإداري الأعلى فقط (Admin/CEO/GM؛ + HR لاحقًا). Manager/TeamLeader ممنوعون.
    [HttpPut("teams/{teamId:guid}")]
    [Authorize(Policy = Policies.TeamManagement)]
    public async Task<IActionResult> UpdateTeam(Guid teamId, [FromBody] UpdateTeamRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateTeamAsync(teamId, req, ct));

    // حذف فريق — للأدمن فقط.
    [HttpDelete("teams/{teamId:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> DeleteTeam(Guid teamId, CancellationToken ct)
        => FromResult(await _service.DeleteTeamAsync(teamId, ct));

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
