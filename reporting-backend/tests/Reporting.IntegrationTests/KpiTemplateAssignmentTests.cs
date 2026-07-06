using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Kpi;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// Phase T1 — قواعد إسناد قوالب KPI: إسناد/استثناء صريح على مستوى (موظّف/مسمّى/فريق/إدارة)
/// بأولوية موحَّدة (استثناء موظّف > إسناد موظّف > مسمّى > فريق > إدارة > عام)، مع معاينة التغطية
/// والحفاظ على السلوك القديم (قالب الدور يطغى على العام) وأمان الأدمن حصرًا.
/// </summary>
[Collection("Integration")]
public class KpiTemplateAssignmentTests
{
    private readonly CustomWebApplicationFactory _factory;

    public KpiTemplateAssignmentTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<Guid> PublishKpiAsync(HttpClient admin, Guid? jobRoleId)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"قالب KPI {Guid.NewGuid():N}", null, jobRoleId, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("مؤشر", null, 100m, null, null, KpiCalcMethod.Manual, null));
        await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        return created.Id;
    }

    private async Task<Guid> CreateJobRoleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jr = new JobRole { NameAr = $"مسمّى {Guid.NewGuid():N}", Code = $"JR_{Guid.NewGuid():N}" };
        db.JobRoles.Add(jr);
        await db.SaveChangesAsync();
        return jr.Id;
    }

    private async Task<Guid> CreateDepartmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var d = new Department { NameAr = $"إدارة {Guid.NewGuid():N}" };
        db.Departments.Add(d);
        await db.SaveChangesAsync();
        return d.Id;
    }

    private async Task<Guid> CreateTeamAsync(Guid departmentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var team = new Team { NameAr = $"فريق {Guid.NewGuid():N}", DepartmentId = departmentId };
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }

    private async Task SetUserScopesAsync(Guid userId, Guid? jobRoleId = null, Guid? teamId = null, Guid? departmentId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = await db.Users.FirstAsync(x => x.Id == userId);
        if (jobRoleId is not null) u.JobRoleId = jobRoleId;
        if (teamId is not null) u.TeamId = teamId;
        if (departmentId is not null) u.DepartmentId = departmentId;
        await db.SaveChangesAsync();
    }

    private static async Task<List<KpiTemplateDto>> PickerForSubjectAsync(HttpClient client, Guid subjectId)
    {
        var res = await client.GetAsync(
            $"/api/kpi-templates?isActive=true&status=Published&cadence=WeeklyPulse&subjectUserId={subjectId}");
        return (await res.ReadAsync<List<KpiTemplateDto>>())!;
    }

    private static Task<HttpResponseMessage> AddAssignmentAsync(
        HttpClient client, Guid templateId, TemplateAssignmentScope scope, Guid scopeId, TemplateAssignmentKind kind)
        => client.PostAsJsonAsync($"/api/kpi-templates/{templateId}/assignments",
            new CreateKpiAssignmentRequest(scope, scopeId, kind, null));

    private async Task AssertAuditExistsAsync(string action)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == action), $"توقّعنا سجل تدقيق «{action}».");
    }

    // ===== السلوك القديم محفوظ =====

    [Fact]
    public async Task ExistingBehavior_RoleSpecificHidesGeneral_WithNoExplicitAssignments()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var role = await CreateJobRoleAsync();
        var general = await PublishKpiAsync(admin, null);
        var specialized = await PublishKpiAsync(admin, role);

        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserScopesAsync(subjectId, jobRoleId: role);

        var list = await PickerForSubjectAsync(admin, subjectId);
        Assert.Contains(list, t => t.Id == specialized);
        Assert.DoesNotContain(list, t => t.Id == general);
    }

    // ===== إسناد موظّف مباشر =====

    [Fact]
    public async Task DirectEmployeeInclude_SubjectSeesTemplate_NotMatchingTheirRole()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleA = await CreateJobRoleAsync();
        var specialized = await PublishKpiAsync(admin, roleA);

        // الموظّف بمسمّى مختلف ⇒ بلا إسناد لا يرى القالب المتخصص.
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserScopesAsync(subjectId, jobRoleId: await CreateJobRoleAsync());

        Assert.DoesNotContain(await PickerForSubjectAsync(admin, subjectId), t => t.Id == specialized);

        var res = await AddAssignmentAsync(admin, specialized, TemplateAssignmentScope.Employee, subjectId, TemplateAssignmentKind.Include);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var after = await PickerForSubjectAsync(admin, subjectId);
        Assert.Contains(after, t => t.Id == specialized);
    }

    [Fact]
    public async Task EmployeeExclude_WinsOverJobRoleMatch()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var role = await CreateJobRoleAsync();
        var general = await PublishKpiAsync(admin, null);
        var specialized = await PublishKpiAsync(admin, role);

        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserScopesAsync(subjectId, jobRoleId: role);

        // قبل الاستثناء: يرى المتخصص (والعام مخفي).
        var before = await PickerForSubjectAsync(admin, subjectId);
        Assert.Contains(before, t => t.Id == specialized);

        await AddAssignmentAsync(admin, specialized, TemplateAssignmentScope.Employee, subjectId, TemplateAssignmentKind.Exclude);

        // بعد الاستثناء: لا يرى المتخصص، ويؤول إلى العام (لا مطابقة أخصّ).
        var after = await PickerForSubjectAsync(admin, subjectId);
        Assert.DoesNotContain(after, t => t.Id == specialized);
        Assert.Contains(after, t => t.Id == general);
    }

    // ===== إسناد فريق وإدارة =====

    [Fact]
    public async Task TeamInclude_SubjectInTeam_SeesTemplate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var dept = await CreateDepartmentAsync();
        var team = await CreateTeamAsync(dept);
        var specialized = await PublishKpiAsync(admin, await CreateJobRoleAsync());

        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserScopesAsync(subjectId, jobRoleId: await CreateJobRoleAsync(), teamId: team);

        Assert.DoesNotContain(await PickerForSubjectAsync(admin, subjectId), t => t.Id == specialized);
        await AddAssignmentAsync(admin, specialized, TemplateAssignmentScope.Team, team, TemplateAssignmentKind.Include);
        Assert.Contains(await PickerForSubjectAsync(admin, subjectId), t => t.Id == specialized);
    }

    [Fact]
    public async Task DepartmentInclude_SubjectInDepartment_SeesTemplate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var dept = await CreateDepartmentAsync();
        var specialized = await PublishKpiAsync(admin, await CreateJobRoleAsync());

        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserScopesAsync(subjectId, jobRoleId: await CreateJobRoleAsync(), departmentId: dept);

        Assert.DoesNotContain(await PickerForSubjectAsync(admin, subjectId), t => t.Id == specialized);
        await AddAssignmentAsync(admin, specialized, TemplateAssignmentScope.Department, dept, TemplateAssignmentKind.Include);
        Assert.Contains(await PickerForSubjectAsync(admin, subjectId), t => t.Id == specialized);
    }

    [Fact]
    public async Task PriorityChain_EmployeeExcludeBeatsTeamInclude()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var dept = await CreateDepartmentAsync();
        var team = await CreateTeamAsync(dept);
        var specialized = await PublishKpiAsync(admin, await CreateJobRoleAsync());

        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserScopesAsync(subjectId, jobRoleId: await CreateJobRoleAsync(), teamId: team);

        await AddAssignmentAsync(admin, specialized, TemplateAssignmentScope.Team, team, TemplateAssignmentKind.Include);
        Assert.Contains(await PickerForSubjectAsync(admin, subjectId), t => t.Id == specialized);

        // استثناء الموظّف (الأخصّ) يتفوّق على إسناد الفريق.
        await AddAssignmentAsync(admin, specialized, TemplateAssignmentScope.Employee, subjectId, TemplateAssignmentKind.Exclude);
        Assert.DoesNotContain(await PickerForSubjectAsync(admin, subjectId), t => t.Id == specialized);
    }

    // ===== المعاينة =====

    [Fact]
    public async Task Preview_MatchedByJobRole_ThenExcludedManually()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var role = await CreateJobRoleAsync();
        var specialized = await PublishKpiAsync(admin, role);

        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserScopesAsync(subjectId, jobRoleId: role);

        var preview = await (await admin.GetAsync($"/api/kpi-templates/{specialized}/assignments"))
            .ReadAsync<KpiTemplateAssignmentsDto>();
        Assert.NotNull(preview);
        Assert.True(preview!.IsAssignable);
        Assert.True(preview.IsRoleSpecific);
        var matched = Assert.Single(preview.MatchedUsers, u => u.UserId == subjectId);
        Assert.Equal("matchedByJobRole", matched.MatchReason);

        await AddAssignmentAsync(admin, specialized, TemplateAssignmentScope.Employee, subjectId, TemplateAssignmentKind.Exclude);

        var preview2 = await (await admin.GetAsync($"/api/kpi-templates/{specialized}/assignments"))
            .ReadAsync<KpiTemplateAssignmentsDto>();
        Assert.DoesNotContain(preview2!.MatchedUsers, u => u.UserId == subjectId);
        var ex = Assert.Single(preview2.ExcludedUsers, u => u.UserId == subjectId);
        Assert.Equal("excludedManually", ex.ExclusionReason);
        Assert.Single(preview2.Assignments, a => a.ScopeType == TemplateAssignmentScope.Employee && a.Kind == TemplateAssignmentKind.Exclude);
    }

    // ===== إدارة الإسناد (تكرار/تعطيل/حذف/تدقيق) =====

    [Fact]
    public async Task AddAssignment_Duplicate_Returns409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var specialized = await PublishKpiAsync(admin, await CreateJobRoleAsync());
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var first = await AddAssignmentAsync(admin, specialized, TemplateAssignmentScope.Employee, subjectId, TemplateAssignmentKind.Include);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var dup = await AddAssignmentAsync(admin, specialized, TemplateAssignmentScope.Employee, subjectId, TemplateAssignmentKind.Include);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task DisableAssignment_RemovesFromPicker_AndDeleteWorks()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var specialized = await PublishKpiAsync(admin, await CreateJobRoleAsync());
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserScopesAsync(subjectId, jobRoleId: await CreateJobRoleAsync());

        var row = await (await AddAssignmentAsync(admin, specialized, TemplateAssignmentScope.Employee, subjectId, TemplateAssignmentKind.Include))
            .ReadAsync<KpiTemplateAssignmentRowDto>();
        Assert.Contains(await PickerForSubjectAsync(admin, subjectId), t => t.Id == specialized);

        // تعطيل الإسناد ⇒ يختفي من المنتقي.
        var upd = await admin.PutAsJsonAsync($"/api/kpi-templates/{specialized}/assignments/{row!.Id}",
            new UpdateKpiAssignmentRequest(false, null));
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);
        Assert.DoesNotContain(await PickerForSubjectAsync(admin, subjectId), t => t.Id == specialized);

        // حذف الإسناد.
        var del = await admin.DeleteAsync($"/api/kpi-templates/{specialized}/assignments/{row.Id}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        await AssertAuditExistsAsync("kpi_template.assignment.removed");
    }

    [Fact]
    public async Task AddAssignment_WritesAudit()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var specialized = await PublishKpiAsync(admin, await CreateJobRoleAsync());
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await AddAssignmentAsync(admin, specialized, TemplateAssignmentScope.Employee, subjectId, TemplateAssignmentKind.Include);
        await AssertAuditExistsAsync("kpi_template.assignment.added");
    }

    // ===== الأمان: الأدمن حصرًا =====

    [Fact]
    public async Task Security_AdminCanManage_OthersCannot()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var specialized = await PublishKpiAsync(admin, await CreateJobRoleAsync());
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var hr = await TestAuth.LoginAsRoleAsync(_factory, "Hr");
        var leader = await TestAuth.LoginAsRoleAsync(_factory, "TeamLeader");
        var anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/kpi-templates/{specialized}/assignments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.GetAsync($"/api/kpi-templates/{specialized}/assignments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leader.GetAsync($"/api/kpi-templates/{specialized}/assignments")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/kpi-templates/{specialized}/assignments")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await AddAssignmentAsync(hr, specialized, TemplateAssignmentScope.Employee, subjectId, TemplateAssignmentKind.Include)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AddAssignmentAsync(leader, specialized, TemplateAssignmentScope.Employee, subjectId, TemplateAssignmentKind.Include)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AddAssignmentAsync(anon, specialized, TemplateAssignmentScope.Employee, subjectId, TemplateAssignmentKind.Include)).StatusCode);
    }
}
