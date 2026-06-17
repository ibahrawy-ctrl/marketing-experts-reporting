using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Directory;
using Reporting.Domain.Entities.Org;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class DirectoryTests
{
    private readonly CustomWebApplicationFactory _factory;

    public DirectoryTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ListUsers_ReturnsCreatedUsers_ForAuthenticated()
    {
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var users = await (await employee.GetAsync("/api/directory/users")).ReadAsync<List<DirectoryUserDto>>();
        Assert.NotNull(users);
        Assert.Contains(users!, u => u.Id == employeeId);
    }

    [Fact]
    public async Task ListDepartmentsAndTeams_AccessibleToAuthenticated()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var depts = await employee.GetAsync("/api/directory/departments");
        var teams = await employee.GetAsync("/api/directory/teams");
        Assert.Equal(HttpStatusCode.OK, depts.StatusCode);
        Assert.Equal(HttpStatusCode.OK, teams.StatusCode);
    }

    [Fact]
    public async Task ListJobRoles_AccessibleToAuthenticated()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await employee.GetAsync("/api/directory/job-roles");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var roles = await res.ReadAsync<List<JobRoleDto>>();
        Assert.NotNull(roles);
    }

    [Fact]
    public async Task Directory_Anonymous_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/directory/users");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task RoleMatrix_ReturnsAllRoles_WithScopeAndPermissions()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var matrix = await (await employee.GetAsync("/api/directory/role-matrix")).ReadAsync<List<RoleAccessDto>>();
        Assert.NotNull(matrix);
        Assert.Equal(9, matrix!.Count);
        var admin = matrix.Single(r => r.Role == "Admin");
        Assert.Equal("governance", admin.ScopeType);
        Assert.Contains("ManageUsers", admin.Permissions);
        var emp = matrix.Single(r => r.Role == "Employee");
        Assert.Equal("own", emp.ScopeType);
        Assert.DoesNotContain("ManageUsers", emp.Permissions);
        // V1.0.1-A: دور الموارد البشرية رسميّ لكنه ليس دورًا إداريًّا — نطاقه شخصيّ وبلا صلاحيات أدمن.
        var hr = matrix.Single(r => r.Role == "HR");
        Assert.Equal("own", hr.ScopeType);
        Assert.DoesNotContain("ManageUsers", hr.Permissions);
        Assert.DoesNotContain("ManageTemplates", hr.Permissions);
        Assert.DoesNotContain("ViewGovernance", hr.Permissions);
    }

    [Fact]
    public async Task UpdateUserRoles_AsAdmin_ChangesRoles()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var res = await admin.PutAsJsonAsync($"/api/directory/users/{targetId}/roles",
            new UpdateUserRolesRequest(new List<string> { "Manager" }));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var users = await (await admin.GetAsync("/api/directory/users?includeInactive=true")).ReadAsync<List<DirectoryUserDto>>();
        var updated = users!.Single(u => u.Id == targetId);
        Assert.Contains("Manager", updated.Roles);
        Assert.DoesNotContain("Employee", updated.Roles);
    }

    [Fact]
    public async Task UpdateUserRoles_AsNonAdmin_403()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await manager.PutAsJsonAsync($"/api/directory/users/{targetId}/roles",
            new UpdateUserRolesRequest(new List<string> { "TeamLeader" }));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRoles_Empty_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await admin.PutAsJsonAsync($"/api/directory/users/{targetId}/roles",
            new UpdateUserRolesRequest(new List<string>()));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRoles_SelfRemoveAdmin_Blocked_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        // المعرّف الخاص بالأدمن المضمّن في البذرة.
        var users = await (await admin.GetAsync("/api/directory/users?includeInactive=true")).ReadAsync<List<DirectoryUserDto>>();
        var self = users!.Single(u => u.Email == "admin@marketingexperts.local");
        var res = await admin.PutAsJsonAsync($"/api/directory/users/{self.Id}/roles",
            new UpdateUserRolesRequest(new List<string> { "Manager" }));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ── إنشاء/تعديل/حذف المستخدمين ──────────────────────────────────────────

    [Fact]
    public async Task CreateUser_AsAdmin_CreatesUser_WithRoles()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var email = $"new-{Guid.NewGuid():N}@test.local";
        var req = new CreateUserRequest(email, "موظف جديد", "Passw0rd#1",
            new List<string> { "Employee" }, null, null, null);

        var created = await (await admin.PostAsJsonAsync("/api/directory/users", req))
            .ReadAsync<DirectoryUserDto>();

        Assert.NotNull(created);
        Assert.Equal(email, created!.Email);
        Assert.Contains("Employee", created.Roles);
    }

    [Fact]
    public async Task CreateUser_AsNonAdmin_403()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var req = new CreateUserRequest($"x-{Guid.NewGuid():N}@test.local", "س", "Passw0rd#1",
            new List<string> { "Employee" }, null, null, null);
        var res = await manager.PostAsJsonAsync("/api/directory/users", req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var email = $"dup-{Guid.NewGuid():N}@test.local";
        var req = new CreateUserRequest(email, "أ", "Passw0rd#1",
            new List<string> { "Employee" }, null, null, null);
        await admin.PostAsJsonAsync("/api/directory/users", req);
        var res = await admin.PostAsJsonAsync("/api/directory/users", req);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_AsAdmin_ChangesNameAndActive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var res = await admin.PutAsJsonAsync($"/api/directory/users/{targetId}",
            new UpdateUserRequest("اسم معدّل", false, null, null, null));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var users = await (await admin.GetAsync("/api/directory/users?includeInactive=true")).ReadAsync<List<DirectoryUserDto>>();
        var updated = users!.Single(u => u.Id == targetId);
        Assert.Equal("اسم معدّل", updated.FullName);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task DeleteUser_AsAdmin_RemovesUser()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var res = await admin.DeleteAsync($"/api/directory/users/{targetId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var users = await (await admin.GetAsync("/api/directory/users?includeInactive=true")).ReadAsync<List<DirectoryUserDto>>();
        Assert.DoesNotContain(users!, u => u.Id == targetId);
    }

    [Fact]
    public async Task DeleteUser_Self_Blocked_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var users = await (await admin.GetAsync("/api/directory/users?includeInactive=true")).ReadAsync<List<DirectoryUserDto>>();
        var self = users!.Single(u => u.Email == "admin@marketingexperts.local");
        var res = await admin.DeleteAsync($"/api/directory/users/{self.Id}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AsNonAdmin_403()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await manager.DeleteAsync($"/api/directory/users/{targetId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── إدارة أعضاء الفرق ─────────────────────────────────────────────────

    [Fact]
    public async Task AddAndRemoveTeamMember_AsAdmin_UpdatesUserTeam()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (deptId, teamId) = await CreateTeamAsync();
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var add = await admin.PostAsJsonAsync($"/api/directory/teams/{teamId}/members",
            new TeamMemberRequest(userId));
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var users = await (await admin.GetAsync("/api/directory/users?includeInactive=true")).ReadAsync<List<DirectoryUserDto>>();
        var afterAdd = users!.Single(u => u.Id == userId);
        Assert.Equal(teamId, afterAdd.TeamId);
        Assert.Equal(deptId, afterAdd.DepartmentId);

        var remove = await admin.DeleteAsync($"/api/directory/teams/{teamId}/members/{userId}");
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

        users = await (await admin.GetAsync("/api/directory/users?includeInactive=true")).ReadAsync<List<DirectoryUserDto>>();
        Assert.Null(users!.Single(u => u.Id == userId).TeamId);
    }

    [Fact]
    public async Task AddTeamMember_AsNonAdmin_403()
    {
        var (deptId, teamId) = await CreateTeamAsync();
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await manager.PostAsJsonAsync($"/api/directory/teams/{teamId}/members",
            new TeamMemberRequest(userId));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        _ = deptId;
    }

    // ── البند 2: إدارة عضوية الفرق مقصورة على المستوى الإداري الأعلى فقط ──
    // السياسة الخادمية TeamManagement = Admin/CEO/GeneralManager فقط (+ HR/المساعد الإداري
    // مستقبلًا عند تعريفها). Manager (أقل من GM) وTeamLeader وEmployee محجوبون ⇒ 403،
    // حتى لو كان الفريق ضمن نطاق رؤيتهم — فالرؤية ليست تعديلًا. الفحص يقع في طبقة السياسة
    // قبل بلوغ منطق النطاق، فلا يهمّ كون الفريق داخل/خارج النطاق لهذه الأدوار.

    /// <summary>الموظّف لا يملك أدوات إدارة الفرق إطلاقًا ⇒ 403.</summary>
    [Fact]
    public async Task AddTeamMember_AsEmployee_403()
    {
        var (deptId, teamId) = await CreateTeamAsync();
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await employee.PostAsJsonAsync($"/api/directory/teams/{teamId}/members",
            new TeamMemberRequest(userId));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        _ = deptId;
    }

    /// <summary>المدير (أقل من GM) محجوب عن تعديل الفريق ولو كان ضمن نطاقه ⇒ 403.</summary>
    [Fact]
    public async Task UpdateTeam_AsManagerInScope_403()
    {
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (teamId, deptId, _) = await NewTeamWithManagerReportAsync(managerId);

        var res = await manager.PutAsJsonAsync($"/api/directory/teams/{teamId}",
            new UpdateTeamRequest("فريق المدير", null, deptId, null, true));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>المدير (أقل من GM) محجوب عن تعديل فريق خارج نطاقه أيضًا ⇒ 403.</summary>
    [Fact]
    public async Task UpdateTeam_AsManager_OutOfScopeTeam_403()
    {
        var (deptId, teamId) = await CreateTeamAsync();
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.PutAsJsonAsync($"/api/directory/teams/{teamId}",
            new UpdateTeamRequest("محاولة خارج النطاق", null, deptId, null, true));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>المدير (أقل من GM) محجوب عن ضمّ مرؤوس مباشر إلى فريقه ⇒ 403.</summary>
    [Fact]
    public async Task AddTeamMember_AsManager_DirectReportInScope_403()
    {
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (teamId, _, _) = await NewTeamWithManagerReportAsync(managerId);
        var (_, newReportId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var res = await manager.PostAsJsonAsync($"/api/directory/teams/{teamId}/members",
            new TeamMemberRequest(newReportId));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>المدير (أقل من GM) محجوب عن ضمّ مستخدم خارج نطاقه أيضًا ⇒ 403.</summary>
    [Fact]
    public async Task AddTeamMember_AsManager_OutOfScopeUser_403()
    {
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (teamId, _, _) = await NewTeamWithManagerReportAsync(managerId);
        var (_, strangerId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var res = await manager.PostAsJsonAsync($"/api/directory/teams/{teamId}/members",
            new TeamMemberRequest(strangerId));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>المدير (أقل من GM) محجوب عن إزالة عضو من فريقه ⇒ 403.</summary>
    [Fact]
    public async Task RemoveTeamMember_AsManagerInScope_403()
    {
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (teamId, _, memberId) = await NewTeamWithManagerReportAsync(managerId);

        var res = await manager.DeleteAsync($"/api/directory/teams/{teamId}/members/{memberId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>قائد الفريق (TeamLeader) لا يملك أدوات إدارة الفرق ⇒ 403.</summary>
    [Fact]
    public async Task UpdateTeam_AsTeamLeader_403()
    {
        var (leader, _) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (deptId, teamId) = await CreateTeamAsync();
        var res = await leader.PutAsJsonAsync($"/api/directory/teams/{teamId}",
            new UpdateTeamRequest("محاولة قائد الفريق", null, deptId, null, true));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>قائد الفريق (TeamLeader) محجوب عن ضمّ عضو إلى فريق ⇒ 403.</summary>
    [Fact]
    public async Task AddTeamMember_AsTeamLeader_403()
    {
        var (leader, _) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, teamId) = await CreateTeamAsync();
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await leader.PostAsJsonAsync($"/api/directory/teams/{teamId}/members",
            new TeamMemberRequest(userId));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>المدير العام (GeneralManager) من المستوى الإداري الأعلى ⇒ يعدّل أي فريق 200.</summary>
    [Fact]
    public async Task UpdateTeam_AsGeneralManager_200()
    {
        var (gm, _) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var (deptId, teamId) = await CreateTeamAsync();
        var res = await gm.PutAsJsonAsync($"/api/directory/teams/{teamId}",
            new UpdateTeamRequest("فريق المدير العام", null, deptId, null, true));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    /// <summary>المدير العام (GeneralManager) يضمّ أي مستخدم إلى أي فريق ⇒ 200.</summary>
    [Fact]
    public async Task AddTeamMember_AsGeneralManager_200()
    {
        var (gm, _) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var (_, teamId) = await CreateTeamAsync();
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await gm.PostAsJsonAsync($"/api/directory/teams/{teamId}/members",
            new TeamMemberRequest(userId));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    /// <summary>الرئيس التنفيذي (CEO) من المستوى الإداري الأعلى ⇒ يضمّ عضوًا 200.</summary>
    [Fact]
    public async Task AddTeamMember_AsCeo_200()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (_, teamId) = await CreateTeamAsync();
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await ceo.PostAsJsonAsync($"/api/directory/teams/{teamId}/members",
            new TeamMemberRequest(userId));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ── إنشاء/تعديل/حذف الفرق ────────────────────────────────────────────

    [Fact]
    public async Task CreateTeam_AsAdmin_CreatesTeam()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var deptId = await CreateDepartmentAsync();

        var created = await (await admin.PostAsJsonAsync("/api/directory/teams",
            new CreateTeamRequest("فريق جديد", "New Team", deptId, null)))
            .ReadAsync<TeamDto>();

        Assert.NotNull(created);
        Assert.Equal("فريق جديد", created!.NameAr);
        Assert.Equal(deptId, created.DepartmentId);
        Assert.True(created.IsActive);

        var teams = await (await admin.GetAsync("/api/directory/teams")).ReadAsync<List<TeamDto>>();
        Assert.Contains(teams!, t => t.Id == created.Id);
    }

    [Fact]
    public async Task CreateTeam_AsNonAdmin_403()
    {
        var deptId = await CreateDepartmentAsync();
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.PostAsJsonAsync("/api/directory/teams",
            new CreateTeamRequest("فريق", null, deptId, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_MissingDepartment_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/directory/teams",
            new CreateTeamRequest("فريق", null, Guid.NewGuid(), null));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_BlankName_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var deptId = await CreateDepartmentAsync();
        var res = await admin.PostAsJsonAsync("/api/directory/teams",
            new CreateTeamRequest("  ", null, deptId, null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UpdateTeam_AsAdmin_ChangesNameAndActive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (deptId, teamId) = await CreateTeamAsync();

        var res = await admin.PutAsJsonAsync($"/api/directory/teams/{teamId}",
            new UpdateTeamRequest("فريق معدّل", "Renamed", deptId, null, false));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var teams = await (await admin.GetAsync("/api/directory/teams")).ReadAsync<List<TeamDto>>();
        var updated = teams!.Single(t => t.Id == teamId);
        Assert.Equal("فريق معدّل", updated.NameAr);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task UpdateTeam_Missing_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var deptId = await CreateDepartmentAsync();
        var res = await admin.PutAsJsonAsync($"/api/directory/teams/{Guid.NewGuid()}",
            new UpdateTeamRequest("فريق", null, deptId, null, true));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task DeleteTeam_AsAdmin_RemovesTeam_AndClearsMembers()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, teamId) = await CreateTeamAsync();
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await admin.PostAsJsonAsync($"/api/directory/teams/{teamId}/members", new TeamMemberRequest(userId));

        var res = await admin.DeleteAsync($"/api/directory/teams/{teamId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var teams = await (await admin.GetAsync("/api/directory/teams")).ReadAsync<List<TeamDto>>();
        Assert.DoesNotContain(teams!, t => t.Id == teamId);

        var users = await (await admin.GetAsync("/api/directory/users?includeInactive=true")).ReadAsync<List<DirectoryUserDto>>();
        Assert.Null(users!.Single(u => u.Id == userId).TeamId);
    }

    [Fact]
    public async Task DeleteTeam_AsNonAdmin_403()
    {
        var (_, teamId) = await CreateTeamAsync();
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.DeleteAsync($"/api/directory/teams/{teamId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── إنشاء/تعديل/حذف الإدارات ─────────────────────────────────────────

    [Fact]
    public async Task CreateDepartment_AsAdmin_CreatesDepartment()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var code = $"ND{Guid.NewGuid():N}".Substring(0, 10);

        var created = await (await admin.PostAsJsonAsync("/api/directory/departments",
            new CreateDepartmentRequest("إدارة جديدة", "New Dept", code, null)))
            .ReadAsync<DepartmentDto>();

        Assert.NotNull(created);
        Assert.Equal("إدارة جديدة", created!.NameAr);
        Assert.Equal(code, created.Code);
        Assert.True(created.IsActive);

        var depts = await (await admin.GetAsync("/api/directory/departments")).ReadAsync<List<DepartmentDto>>();
        Assert.Contains(depts!, d => d.Id == created.Id);
    }

    [Fact]
    public async Task CreateDepartment_AsNonAdmin_403()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.PostAsJsonAsync("/api/directory/departments",
            new CreateDepartmentRequest("إدارة", null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CreateDepartment_BlankName_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/directory/departments",
            new CreateDepartmentRequest("  ", null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task CreateDepartment_MissingManager_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/directory/departments",
            new CreateDepartmentRequest("إدارة", null, null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UpdateDepartment_AsAdmin_ChangesNameAndActive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var deptId = await CreateDepartmentAsync();

        var res = await admin.PutAsJsonAsync($"/api/directory/departments/{deptId}",
            new UpdateDepartmentRequest("إدارة معدّلة", "Renamed", $"RN{Guid.NewGuid():N}".Substring(0, 10), null, false));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var depts = await (await admin.GetAsync("/api/directory/departments")).ReadAsync<List<DepartmentDto>>();
        var updated = depts!.Single(d => d.Id == deptId);
        Assert.Equal("إدارة معدّلة", updated.NameAr);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task UpdateDepartment_Missing_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PutAsJsonAsync($"/api/directory/departments/{Guid.NewGuid()}",
            new UpdateDepartmentRequest("إدارة", null, null, null, true));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task DeleteDepartment_AsAdmin_RemovesDepartment_AndClearsUsers()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var deptId = await CreateDepartmentAsync();
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await admin.PutAsJsonAsync($"/api/directory/users/{userId}",
            new UpdateUserRequest("موظف", true, deptId, null, null));

        var res = await admin.DeleteAsync($"/api/directory/departments/{deptId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var depts = await (await admin.GetAsync("/api/directory/departments")).ReadAsync<List<DepartmentDto>>();
        Assert.DoesNotContain(depts!, d => d.Id == deptId);

        var users = await (await admin.GetAsync("/api/directory/users?includeInactive=true")).ReadAsync<List<DirectoryUserDto>>();
        Assert.Null(users!.Single(u => u.Id == userId).DepartmentId);
    }

    [Fact]
    public async Task DeleteDepartment_WithTeams_Blocked_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (deptId, _) = await CreateTeamAsync();
        var res = await admin.DeleteAsync($"/api/directory/departments/{deptId}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task DeleteDepartment_AsNonAdmin_403()
    {
        var deptId = await CreateDepartmentAsync();
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.DeleteAsync($"/api/directory/departments/{deptId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private async Task<Guid> CreateDepartmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة {Guid.NewGuid():N}", IsActive = true };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        return dept.Id;
    }

    private async Task<(Guid DepartmentId, Guid TeamId)> CreateTeamAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة {Guid.NewGuid():N}", IsActive = true };
        var team = new Team { NameAr = $"فريق {Guid.NewGuid():N}", DepartmentId = dept.Id, IsActive = true };
        db.Departments.Add(dept);
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return (dept.Id, team.Id);
    }

    /// <summary>
    /// ينشئ إدارة+فريقًا جديدَين ويضع أحد مرؤوسي المدير المباشرين عضوًا فيه،
    /// فيصبح الفريق ضمن نطاق ذلك المدير (للاختبارات).
    /// </summary>
    private async Task<(Guid TeamId, Guid DeptId, Guid MemberId)> NewTeamWithManagerReportAsync(Guid managerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة {Guid.NewGuid():N}", IsActive = true };
        var team = new Team { NameAr = $"فريق {Guid.NewGuid():N}", DepartmentId = dept.Id, IsActive = true };
        db.Departments.Add(dept);
        db.Teams.Add(team);
        var member = await db.Users.FirstAsync(u => u.ManagerId == managerId);
        member.TeamId = team.Id;
        member.DepartmentId = dept.Id;
        await db.SaveChangesAsync();
        return (team.Id, dept.Id, member.Id);
    }
}
