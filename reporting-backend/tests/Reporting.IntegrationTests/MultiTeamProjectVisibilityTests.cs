using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// OFFICIAL-LAUNCH-FIX-PACK-R1A (Fix 1) — رؤية عضو بعضويات فرق متعددة لمشاريع كل فرقه.
/// الموظف صاحب أكثر من عضوية فريق (الفريق الأساسي TeamId + عضويات إضافية نشطة في user_team_memberships)
/// يرى مشاريع كل تلك الفرق داخل تقارير PRS. لا توسيع صلاحية إدارة/اعتماد؛ العضوية غير النشطة لا تُحتسب؛
/// المستخدم بلا فِرق لا يرى مشاريع غير مصرّح بها.
/// </summary>
[Collection("Integration")]
public class MultiTeamProjectVisibilityTests
{
    private readonly CustomWebApplicationFactory _factory;

    public MultiTeamProjectVisibilityTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<ProjectDto> CreateProjectAsync(HttpClient admin, string name, Guid? ownerTeamId)
    {
        var client = (await (await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest($"عميل {Guid.NewGuid():N}", null))).ReadAsync<ClientDto>())!;
        return (await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, name, ServiceType.Seo, OwnerTeamId: ownerTeamId)))
            .ReadAsync<ProjectDto>())!;
    }

    // ===== 1: عضو بفريق أساسي فقط يرى مشاريع فريقه الأساسي =====
    [Fact]
    public async Task PrimaryOnly_SeesPrimaryTeamProjects()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var primaryTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, memberId);
        var primaryProject = await CreateProjectAsync(admin, "مشروع الفريق الأساسي", primaryTeam);

        var list = await (await member.GetAsync("/api/projects?selectableOnly=true"))
            .ReadAsync<List<ProjectDto>>();
        Assert.Contains(list!, p => p.Id == primaryProject.Id);
    }

    // ===== 2: عضو بفريق أساسي + عضوية إضافية نشطة يرى مشاريع الفريقين =====
    [Fact]
    public async Task PrimaryPlusExtra_SeesBothTeamsProjects()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leaderA, leaderAId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (leaderB, leaderBId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);

        var primaryTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderAId, memberId);
        var extraTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderBId);
        await TestAuth.AddExtraTeamMembershipAsync(_factory, memberId, extraTeam);

        var primaryProject = await CreateProjectAsync(admin, "مشروع الأساسي", primaryTeam);
        var extraProject = await CreateProjectAsync(admin, "مشروع الإضافي", extraTeam);

        var list = await (await member.GetAsync("/api/projects?selectableOnly=true"))
            .ReadAsync<List<ProjectDto>>();
        Assert.Contains(list!, p => p.Id == primaryProject.Id);
        Assert.Contains(list!, p => p.Id == extraProject.Id);

        // رؤية مباشرة GET لمشروع الفريق الإضافي.
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/projects/{extraProject.Id}")).StatusCode);
    }

    // ===== 3: المشروع نفسه لا يتكرّر في القائمة =====
    [Fact]
    public async Task SameProject_NotDuplicated()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leaderA, leaderAId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (leaderB, leaderBId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);

        var primaryTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderAId, memberId);
        var extraTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderBId);
        await TestAuth.AddExtraTeamMembershipAsync(_factory, memberId, extraTeam);

        var primaryProject = await CreateProjectAsync(admin, "مشروع بلا تكرار", primaryTeam);
        var extraProject = await CreateProjectAsync(admin, "مشروع الإضافي 2", extraTeam);

        var list = await (await member.GetAsync("/api/projects?selectableOnly=true"))
            .ReadAsync<List<ProjectDto>>();
        Assert.Equal(1, list!.Count(p => p.Id == primaryProject.Id));
        Assert.Equal(1, list!.Count(p => p.Id == extraProject.Id));
    }

    // ===== 4: العضوية الإضافية غير النشطة لا تُحتسب =====
    [Fact]
    public async Task InactiveExtraMembership_NotCounted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leaderA, leaderAId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (leaderB, leaderBId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);

        var primaryTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderAId, memberId);
        var extraTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderBId);
        await TestAuth.AddExtraTeamMembershipAsync(_factory, memberId, extraTeam, isActive: false);

        var extraProject = await CreateProjectAsync(admin, "مشروع فريق معطّل", extraTeam);

        var list = await (await member.GetAsync("/api/projects?selectableOnly=true"))
            .ReadAsync<List<ProjectDto>>();
        Assert.DoesNotContain(list!, p => p.Id == extraProject.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/projects/{extraProject.Id}")).StatusCode);
    }

    // ===== 5: المستخدم بلا أي فريق لا يرى مشاريع غير مصرّح بها =====
    [Fact]
    public async Task NoTeams_SeesNoUnauthorizedProjects()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var someTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId);
        var project = await CreateProjectAsync(admin, "مشروع فريق أجنبي", someTeam);

        var list = await (await member.GetAsync("/api/projects?selectableOnly=true"))
            .ReadAsync<List<ProjectDto>>();
        Assert.DoesNotContain(list!, p => p.Id == project.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/projects/{project.Id}")).StatusCode);
    }

    // ===== 6: سيناريو مكافئ لشيماء (3 مشاريع أساسية + 4 إضافية = 7) =====
    [Fact]
    public async Task ShaimaaEquivalent_ThreePlusFour_SeesSeven()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, memberId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leaderA, leaderAId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (leaderB, leaderBId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);

        var primaryTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderAId, memberId);
        var extraTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderBId);
        await TestAuth.AddExtraTeamMembershipAsync(_factory, memberId, extraTeam);

        var primaryIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
            primaryIds.Add((await CreateProjectAsync(admin, $"أساسي {i}", primaryTeam)).Id);

        var extraIds = new List<Guid>();
        for (var i = 0; i < 4; i++)
            extraIds.Add((await CreateProjectAsync(admin, $"إضافي {i}", extraTeam)).Id);

        var list = await (await member.GetAsync("/api/projects?selectableOnly=true"))
            .ReadAsync<List<ProjectDto>>();
        var visible = list!.Select(p => p.Id).ToHashSet();

        Assert.All(primaryIds, id => Assert.Contains(id, visible));
        Assert.All(extraIds, id => Assert.Contains(id, visible));
        Assert.Equal(7, primaryIds.Concat(extraIds).Count(visible.Contains));
    }
}
