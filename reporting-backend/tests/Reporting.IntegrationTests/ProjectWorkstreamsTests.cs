using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// P1 — تيّارات العمل داخل المشروع (Project Workstreams). يثبت الإنشاء/القائمة/التحديث/التفعيل-التعطيل
/// (بلا حذف نهائيّ)، والتحقّق من صحّة المشروع/نوع تيار العمل (من الكتالوج)/الفريق، والتفرّد المنطقيّ
/// (مشروع + نوع + فريق)، وأنّ الإدارة فقط تُنشئ (الموظّف 403). النوع يُتحقَّق مقابل Domain=workstream_type.
/// </summary>
[Collection("Integration")]
public class ProjectWorkstreamsTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ProjectWorkstreamsTests(CustomWebApplicationFactory factory) => _factory = factory;

    // رموز نوع تيار العمل المبذورة في ExecutionTaxonomySeeder (Domain=workstream_type).
    private const string TypeWeb = "web_development";
    private const string TypeSeo = "seo";
    private const string TypeContent = "content";

    // ===== 1: الإنشاء ينجح ويعيد التيّار مع أسماء مُثراة =====
    [Fact]
    public async Task Create_Workstream_Succeeds_And_Enriches_Names()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamId = await CreateTeamAsync();

        var created = await CreateWorkstreamAsync(admin, projectId, TypeWeb, teamId);

        Assert.Equal(projectId, created.ProjectId);
        Assert.Equal(TypeWeb, created.WorkstreamTypeCode);
        Assert.Equal("تطوير الويب", created.WorkstreamTypeNameAr);
        Assert.Equal(teamId, created.ResponsibleTeamId);
        Assert.NotNull(created.ResponsibleTeamName);
        Assert.True(created.IsActive);
        Assert.Equal(WorkstreamStatus.Active, created.Status);
    }

    // ===== 2: القائمة تُرجِع التيّارات النشطة للمشروع =====
    [Fact]
    public async Task List_Returns_Active_Workstreams_For_Project()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamA = await CreateTeamAsync();
        var teamB = await CreateTeamAsync();

        var w1 = await CreateWorkstreamAsync(admin, projectId, TypeWeb, teamA);
        var w2 = await CreateWorkstreamAsync(admin, projectId, TypeSeo, teamB);

        var list = await (await admin.GetAsync($"/api/projects/{projectId}/workstreams"))
            .ReadAsync<List<ProjectWorkstreamDto>>();
        var ids = list!.Select(w => w.Id).ToList();

        Assert.Contains(w1.Id, ids);
        Assert.Contains(w2.Id, ids);
    }

    // ===== 3: الإنشاء لمشروع غير موجود → 404 =====
    [Fact]
    public async Task Create_For_Missing_Project_Returns_NotFound()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var teamId = await CreateTeamAsync();

        var res = await admin.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/workstreams",
            new CreateProjectWorkstreamRequest(TypeWeb, teamId));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== 4: نوع تيار عمل غير معروف/غير نشط → 400 =====
    [Fact]
    public async Task Create_With_Invalid_WorkstreamType_Returns_BadRequest()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamId = await CreateTeamAsync();

        var res = await admin.PostAsJsonAsync($"/api/projects/{projectId}/workstreams",
            new CreateProjectWorkstreamRequest("nonexistent_type", teamId));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 5: فريق مسؤول غير موجود → 400 =====
    [Fact]
    public async Task Create_With_Invalid_Team_Returns_BadRequest()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);

        var res = await admin.PostAsJsonAsync($"/api/projects/{projectId}/workstreams",
            new CreateProjectWorkstreamRequest(TypeWeb, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 6: المدير المسؤول اختياريّ — الإنشاء بدونه ينجح =====
    [Fact]
    public async Task Create_Without_Manager_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamId = await CreateTeamAsync();

        var created = await CreateWorkstreamAsync(admin, projectId, TypeContent, teamId);
        Assert.Null(created.ResponsibleManagerId);
    }

    // ===== 7: التحديث يغيّر الفريق والحالة والاسم =====
    [Fact]
    public async Task Update_Changes_Team_Status_And_Name()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamA = await CreateTeamAsync();
        var teamB = await CreateTeamAsync();

        var created = await CreateWorkstreamAsync(admin, projectId, TypeWeb, teamA);

        var updated = await (await admin.PutAsJsonAsync($"/api/projects/{projectId}/workstreams/{created.Id}",
            new UpdateProjectWorkstreamRequest(teamB, WorkstreamStatus.Paused, Name: "مسار محدّث")))
            .ReadAsync<ProjectWorkstreamDto>();

        Assert.Equal(teamB, updated!.ResponsibleTeamId);
        Assert.Equal(WorkstreamStatus.Paused, updated.Status);
        Assert.Equal("مسار محدّث", updated.Name);
    }

    // ===== 8: التعطيل ثم التفعيل (بلا حذف نهائيّ) =====
    [Fact]
    public async Task Deactivate_Then_Activate_Toggles_IsActive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamId = await CreateTeamAsync();
        var created = await CreateWorkstreamAsync(admin, projectId, TypeWeb, teamId);

        var deactivated = await (await admin.PatchAsync($"/api/projects/{projectId}/workstreams/{created.Id}/deactivate", null))
            .ReadAsync<ProjectWorkstreamDto>();
        Assert.False(deactivated!.IsActive);

        // مستثنى من القائمة الافتراضية، ظاهر مع includeInactive.
        var def = await (await admin.GetAsync($"/api/projects/{projectId}/workstreams"))
            .ReadAsync<List<ProjectWorkstreamDto>>();
        Assert.DoesNotContain(created.Id, def!.Select(w => w.Id));

        var all = await (await admin.GetAsync($"/api/projects/{projectId}/workstreams?includeInactive=true"))
            .ReadAsync<List<ProjectWorkstreamDto>>();
        Assert.Contains(created.Id, all!.Select(w => w.Id));

        var activated = await (await admin.PatchAsync($"/api/projects/{projectId}/workstreams/{created.Id}/activate", null))
            .ReadAsync<ProjectWorkstreamDto>();
        Assert.True(activated!.IsActive);
    }

    // ===== 9: منع التكرار (مشروع + نوع + فريق) → 409 =====
    [Fact]
    public async Task Duplicate_Project_Type_Team_Returns_Conflict()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamId = await CreateTeamAsync();

        await CreateWorkstreamAsync(admin, projectId, TypeWeb, teamId);

        var res = await admin.PostAsJsonAsync($"/api/projects/{projectId}/workstreams",
            new CreateProjectWorkstreamRequest(TypeWeb, teamId));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ===== 10: نفس النوع مع فريق مختلف مسموح (لا تكرار) =====
    [Fact]
    public async Task Same_Type_Different_Team_Is_Allowed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamA = await CreateTeamAsync();
        var teamB = await CreateTeamAsync();

        await CreateWorkstreamAsync(admin, projectId, TypeWeb, teamA);

        var res = await admin.PostAsJsonAsync($"/api/projects/{projectId}/workstreams",
            new CreateProjectWorkstreamRequest(TypeWeb, teamB));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 11: الموظّف (غير الإدارة) ممنوع من الإنشاء → 403 =====
    [Fact]
    public async Task Employee_Cannot_Create_Workstream_Forbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamId = await CreateTeamAsync();
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await emp.Client.PostAsJsonAsync($"/api/projects/{projectId}/workstreams",
            new CreateProjectWorkstreamRequest(TypeWeb, teamId));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 12: مدير الحسابات (Account Manager) يُنشئ هدف عمل لمشروعه — النطاق لا الدور =====
    [Fact]
    public async Task AccountManager_Can_Create_Workstream_For_Own_Project()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamId = await CreateTeamAsync();
        var (am, amId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetAccountManagerAsync(projectId, amId);

        var res = await am.PostAsJsonAsync($"/api/projects/{projectId}/workstreams",
            new CreateProjectWorkstreamRequest(TypeWeb, teamId));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 13: مدير الحسابات يعدّل/يفعّل/يعطّل أهداف عمل مشروعه =====
    [Fact]
    public async Task AccountManager_Can_Manage_Own_Project_Workstreams()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamA = await CreateTeamAsync();
        var teamB = await CreateTeamAsync();
        var (am, amId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetAccountManagerAsync(projectId, amId);

        var created = await CreateWorkstreamAsync(am, projectId, TypeWeb, teamA);

        var updated = await am.PutAsJsonAsync($"/api/projects/{projectId}/workstreams/{created.Id}",
            new UpdateProjectWorkstreamRequest(teamB, WorkstreamStatus.Active, Name: "محدَّث بواسطة مدير الحسابات"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var deactivated = await am.PatchAsync($"/api/projects/{projectId}/workstreams/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        var activated = await am.PatchAsync($"/api/projects/{projectId}/workstreams/{created.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
    }

    // ===== 14: مدير حسابات مشروع آخر لا يكتب في مشروع ليس له → 403 (خارج النطاق) =====
    [Fact]
    public async Task AccountManager_Of_Other_Project_Cannot_Create_Forbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectA = await CreateProjectAsync(admin);
        var teamId = await CreateTeamAsync();

        // مدير حسابات مشروع مختلف (B) يحاول الكتابة في مشروع A.
        var projectB = await CreateProjectAsync(admin);
        var (amB, amBId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetAccountManagerAsync(projectB, amBId);

        var res = await amB.PostAsJsonAsync($"/api/projects/{projectA}/workstreams",
            new CreateProjectWorkstreamRequest(TypeWeb, teamId));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 15: Manager (ضمن ProjectPlanManagers) وله رؤية المشروع يُنشئ — مسار الدور =====
    [Fact]
    public async Task Manager_With_Project_View_Can_Create_By_Role()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamId = await CreateTeamAsync();
        var (mgr, mgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        // يمنح Manager رؤية المشروع بجعله قائد الفريق المالك — الإدارة عبر الدور لا النطاق.
        await SetOwnerTeamWithLeaderAsync(projectId, mgrId);

        var res = await mgr.PostAsJsonAsync($"/api/projects/{projectId}/workstreams",
            new CreateProjectWorkstreamRequest(TypeWeb, teamId));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 16: TeamLeader وله رؤية المشروع مُستثنى من إدارة الخطّة → 403 (البوّابة تستبعده) =====
    [Fact]
    public async Task TeamLeader_With_Project_View_Cannot_Manage_Plan()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projectId = await CreateProjectAsync(admin);
        var teamId = await CreateTeamAsync();
        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        // TeamLeader يقود الفريق المالك ⇒ يرى المشروع، لكنه ليس في ProjectPlanManagers ولا مدير حساباته.
        await SetOwnerTeamWithLeaderAsync(projectId, tlId);

        // يقرأ (رؤية) بنجاح لكن لا يدير.
        var read = await tl.GetAsync($"/api/projects/{projectId}/workstreams");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var write = await tl.PostAsJsonAsync($"/api/projects/{projectId}/workstreams",
            new CreateProjectWorkstreamRequest(TypeWeb, teamId));
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    // ===== مساعدون =====
    private async Task SetAccountManagerAsync(Guid projectId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = await db.Projects.FirstAsync(p => p.Id == projectId);
        project.AccountManagerId = userId;
        await db.SaveChangesAsync();
    }

    private async Task SetOwnerTeamWithLeaderAsync(Guid projectId, Guid leaderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة {Guid.NewGuid():N}", IsActive = true };
        db.Departments.Add(dept);
        var team = new Team { NameAr = $"فريق {Guid.NewGuid():N}", DepartmentId = dept.Id, TeamLeaderId = leaderId, IsActive = true };
        db.Teams.Add(team);
        var project = await db.Projects.FirstAsync(p => p.Id == projectId);
        project.OwnerTeamId = team.Id;
        await db.SaveChangesAsync();
    }

    private static async Task<ProjectWorkstreamDto> CreateWorkstreamAsync(
        HttpClient c, Guid projectId, string typeCode, Guid teamId)
        => (await (await c.PostAsJsonAsync($"/api/projects/{projectId}/workstreams",
                new CreateProjectWorkstreamRequest(typeCode, teamId)))
            .ReadAsync<ProjectWorkstreamDto>())!;

    private static async Task<Guid> CreateProjectAsync(HttpClient admin)
    {
        var client = await (await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest($"عميل {Guid.NewGuid():N}"))).ReadAsync<ClientDto>();
        var project = await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client!.Id, $"مشروع {Guid.NewGuid():N}", ServiceType.Website)))
            .ReadAsync<ProjectDto>();
        return project!.Id;
    }

    private async Task<Guid> CreateTeamAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة {Guid.NewGuid():N}", IsActive = true };
        db.Departments.Add(dept);
        var team = new Team { NameAr = $"فريق {Guid.NewGuid():N}", DepartmentId = dept.Id, IsActive = true };
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }
}
