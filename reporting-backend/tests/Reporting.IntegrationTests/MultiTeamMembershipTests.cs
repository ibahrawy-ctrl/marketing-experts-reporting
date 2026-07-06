using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Directory;
using Reporting.Domain.Entities.Org;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// MULTI-TEAM-MEMBERSHIP-MVP-R1 — عضويات الفريق الإضافية (الثانوية).
/// تتحقّق من أنّ العضوية الإضافية لا تغيّر الفريق/الإدارة/المدير/المسمّى الأساسي،
/// ولا تدخل في عدّ الإدارة، ويُمنع حذف الفريق إذا كان به عضوية إضافية نشطة.
/// </summary>
[Collection("Integration")]
public class MultiTeamMembershipTests
{
    private readonly CustomWebApplicationFactory _factory;

    public MultiTeamMembershipTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(Guid DeptId, Guid TeamId)> CreateTeamAsync(Guid? primaryMemberId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة اختبار {Guid.NewGuid():N}", IsActive = true };
        db.Set<Department>().Add(dept);
        var team = new Team
        {
            NameAr = $"فريق اختبار {Guid.NewGuid():N}",
            DepartmentId = dept.Id,
            IsActive = true
        };
        db.Set<Team>().Add(team);
        if (primaryMemberId is Guid pid)
        {
            var u = await db.Users.FirstAsync(x => x.Id == pid);
            u.TeamId = team.Id;
            u.DepartmentId = dept.Id;
        }
        await db.SaveChangesAsync();
        return (dept.Id, team.Id);
    }

    [Fact]
    public async Task AddAdditional_AsAdmin_Succeeds_AndAppearsInBothTeams()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, teamA) = await CreateTeamAsync(employeeId);   // primary in A
        var (_, teamB) = await CreateTeamAsync();              // additional target

        var add = await admin.PostAsJsonAsync($"/api/directory/teams/{teamB}/additional-members",
            new AddAdditionalMemberRequest(employeeId, null, null, null));
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        // يظهر كعضو إضافي في الفريق B.
        var bMembers = await (await admin.GetAsync($"/api/directory/teams/{teamB}/memberships"))
            .ReadAsync<TeamMembershipsDto>();
        Assert.NotNull(bMembers);
        Assert.Contains(bMembers!.AdditionalMembers, m => m.UserId == employeeId);
        Assert.DoesNotContain(bMembers.PrimaryMembers, m => m.UserId == employeeId);

        // يظهر كعضو أساسي في الفريق A.
        var aMembers = await (await admin.GetAsync($"/api/directory/teams/{teamA}/memberships"))
            .ReadAsync<TeamMembershipsDto>();
        Assert.Contains(aMembers!.PrimaryMembers, m => m.UserId == employeeId);

        // عضويات المستخدم: أساسي A + إضافي B.
        var userM = await (await admin.GetAsync($"/api/directory/users/{employeeId}/team-memberships"))
            .ReadAsync<UserTeamMembershipsDto>();
        Assert.Equal(teamA, userM!.PrimaryTeamId);
        Assert.Contains(userM.AdditionalMemberships, m => m.TeamId == teamB);
    }

    [Fact]
    public async Task AddAdditional_DoesNotChangeOrgFields()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, teamA) = await CreateTeamAsync(employeeId);
        var (_, teamB) = await CreateTeamAsync();

        Guid? beforeTeam, beforeDept, beforeManager, beforeJobRole;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == employeeId);
            (beforeTeam, beforeDept, beforeManager, beforeJobRole) = (u.TeamId, u.DepartmentId, u.ManagerId, u.JobRoleId);
        }

        var add = await admin.PostAsJsonAsync($"/api/directory/teams/{teamB}/additional-members",
            new AddAdditionalMemberRequest(employeeId, null, null, null));
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == employeeId);
            Assert.Equal(beforeTeam, u.TeamId);          // لا يزال أساسيًّا في A
            Assert.Equal(teamA, u.TeamId);
            Assert.Equal(beforeDept, u.DepartmentId);    // الإدارة لم تتغيّر
            Assert.Equal(beforeManager, u.ManagerId);
            Assert.Equal(beforeJobRole, u.JobRoleId);
        }
    }

    [Fact]
    public async Task AddAdditional_NonAdmin_403()
    {
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, teamB) = await CreateTeamAsync();
        var res = await employee.PostAsJsonAsync($"/api/directory/teams/{teamB}/additional-members",
            new AddAdditionalMemberRequest(employeeId, null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Memberships_Anonymous_401()
    {
        var client = _factory.CreateClient();
        var (_, teamB) = await CreateTeamAsync();
        var res = await client.GetAsync($"/api/directory/teams/{teamB}/memberships");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task AddAdditional_DuplicateActive_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, teamB) = await CreateTeamAsync();

        var first = await admin.PostAsJsonAsync($"/api/directory/teams/{teamB}/additional-members",
            new AddAdditionalMemberRequest(employeeId, null, null, null));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await admin.PostAsJsonAsync($"/api/directory/teams/{teamB}/additional-members",
            new AddAdditionalMemberRequest(employeeId, null, null, null));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task AddAdditional_PrimaryMemberOfSameTeam_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, teamA) = await CreateTeamAsync(employeeId);

        var res = await admin.PostAsJsonAsync($"/api/directory/teams/{teamA}/additional-members",
            new AddAdditionalMemberRequest(employeeId, null, null, null));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task AddAdditional_SensitiveAccount_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (_, teamB) = await CreateTeamAsync();

        var res = await admin.PostAsJsonAsync($"/api/directory/teams/{teamB}/additional-members",
            new AddAdditionalMemberRequest(ceoId, null, null, null));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Remove_DeactivatesMembership()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, teamB) = await CreateTeamAsync();

        await admin.PostAsJsonAsync($"/api/directory/teams/{teamB}/additional-members",
            new AddAdditionalMemberRequest(employeeId, null, null, null));

        var del = await admin.DeleteAsync($"/api/directory/teams/{teamB}/additional-members/{employeeId}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var bMembers = await (await admin.GetAsync($"/api/directory/teams/{teamB}/memberships"))
            .ReadAsync<TeamMembershipsDto>();
        Assert.DoesNotContain(bMembers!.AdditionalMembers, m => m.UserId == employeeId);

        var userM = await (await admin.GetAsync($"/api/directory/users/{employeeId}/team-memberships"))
            .ReadAsync<UserTeamMembershipsDto>();
        Assert.DoesNotContain(userM!.AdditionalMemberships, m => m.TeamId == teamB);
    }

    [Fact]
    public async Task DeleteTeam_BlockedWhenActiveAdditionalMembership()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, teamB) = await CreateTeamAsync();   // لا أعضاء أساسيون ولا مشاريع

        await admin.PostAsJsonAsync($"/api/directory/teams/{teamB}/additional-members",
            new AddAdditionalMemberRequest(employeeId, null, null, null));

        var del = await admin.DeleteAsync($"/api/directory/teams/{teamB}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
    }

    [Fact]
    public async Task TeamSummary_CountsAdditionalSeparately_AndPrimaryUnchanged()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, teamB) = await CreateTeamAsync();

        await admin.PostAsJsonAsync($"/api/directory/teams/{teamB}/additional-members",
            new AddAdditionalMemberRequest(employeeId, null, null, null));

        var summaries = await (await admin.GetAsync("/api/directory/teams/summary"))
            .ReadAsync<List<TeamSummaryDto>>();
        var b = summaries!.Single(t => t.Id == teamB);
        Assert.Equal(1, b.AdditionalMemberCount);
        Assert.Equal(0, b.PrimaryMemberCount);
        Assert.Equal(0, b.MemberCount);   // memberCount يبقى أساسيًّا فقط
    }

    [Fact]
    public async Task AddAdditional_DoesNotIncreaseDepartmentMemberCount()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await CreateTeamAsync(employeeId);            // primary team in dept A
        var (deptB, teamB) = await CreateTeamAsync(); // empty team in dept B

        var beforeB = (await (await admin.GetAsync("/api/directory/departments/summary"))
            .ReadAsync<List<DepartmentSummaryDto>>())!.Single(d => d.Id == deptB).MemberCount;

        await admin.PostAsJsonAsync($"/api/directory/teams/{teamB}/additional-members",
            new AddAdditionalMemberRequest(employeeId, null, null, null));

        var afterB = (await (await admin.GetAsync("/api/directory/departments/summary"))
            .ReadAsync<List<DepartmentSummaryDto>>())!.Single(d => d.Id == deptB).MemberCount;

        // الإدارة الثانوية لا تَعُدّ العضو الإضافي (DepartmentId مبني على الفريق الأساسي فقط).
        Assert.Equal(beforeB, afterB);
    }
}
