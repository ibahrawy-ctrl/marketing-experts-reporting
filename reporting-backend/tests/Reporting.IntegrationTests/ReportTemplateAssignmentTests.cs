using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// نظام الإسناد الكامل للقوالب (Employee/JobRole/Team/Department + General + استثناءات).
/// يتحقّق من أولوية الخادم: استثناء/إسناد الموظّف ← المسمّى ← الفريق ← الإدارة ← العام،
/// مع منع أكثر من تقرير أساسي لنفس (موظّف، دورية)، والسماح بالتكميلي بجانب الأساسي،
/// وكشف التعارضات وأسباب الربط/الاستثناء، وعدم كسر القوالب المربوطة بالمسمّى قديمًا.
/// </summary>
[Collection("Integration")]
public class ReportTemplateAssignmentTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportTemplateAssignmentTests(CustomWebApplicationFactory factory) => _factory = factory;

    // (1) إسناد صريح للموظّف يُظهِر القالب في قائمته حتى وإن لم يطابق مسمّاه.
    [Fact]
    public async Task EmployeeInclude_ShowsTemplateInSelfList()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var role = await CreateJobRoleAsync("EI_ROLE");
        var template = await PublishAsync(admin, jobRoleId: null); // عام، ثم نُسنده صراحة للموظّف
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, role, null, null);

        await AddAssignment(admin, template, TemplateAssignmentScope.Employee, empId, TemplateAssignmentKind.Include);

        var list = await SelfListAsync(emp);
        Assert.Contains(list, t => t.Id == template);
    }

    // (2) استثناء صريح للموظّف يُخفي القالب الذي كان سيستلمه عبر مسمّاه.
    [Fact]
    public async Task EmployeeExclude_HidesRoleMatchedTemplate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var role = await CreateJobRoleAsync("EE_ROLE");
        var template = await PublishAsync(admin, jobRoleId: role); // يطابق المسمّى
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, role, null, null);

        // قبل الاستثناء: القالب ظاهر.
        Assert.Contains(await SelfListAsync(emp), t => t.Id == template);

        await AddAssignment(admin, template, TemplateAssignmentScope.Employee, empId, TemplateAssignmentKind.Exclude);

        Assert.DoesNotContain(await SelfListAsync(emp), t => t.Id == template);
    }

    // (3) إسناد على مستوى المسمّى الوظيفي يُظهِر القالب لكل من يحمل المسمّى.
    [Fact]
    public async Task JobRoleInclude_ShowsTemplateForRoleHolders()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var role = await CreateJobRoleAsync("JRI_ROLE");
        var template = await PublishAsync(admin, jobRoleId: null);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, role, null, null);

        await AddAssignment(admin, template, TemplateAssignmentScope.JobRole, role, TemplateAssignmentKind.Include);

        Assert.Contains(await SelfListAsync(emp), t => t.Id == template);
    }

    // (4) إسناد على مستوى الفريق يُظهِر القالب لأعضاء الفريق.
    [Fact]
    public async Task TeamInclude_ShowsTemplateForTeamMembers()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var team = await CreateTeamAsync("TI_TEAM");
        var template = await PublishAsync(admin, jobRoleId: null);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, null, team, null);

        await AddAssignment(admin, template, TemplateAssignmentScope.Team, team, TemplateAssignmentKind.Include);

        Assert.Contains(await SelfListAsync(emp), t => t.Id == template);
    }

    // (5) إسناد على مستوى الإدارة يُظهِر القالب لمنتسبي الإدارة.
    [Fact]
    public async Task DepartmentInclude_ShowsTemplateForDepartmentMembers()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var dept = await CreateDepartmentAsync("DI_DEPT");
        var template = await PublishAsync(admin, jobRoleId: null);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, null, null, dept);

        await AddAssignment(admin, template, TemplateAssignmentScope.Department, dept, TemplateAssignmentKind.Include);

        Assert.Contains(await SelfListAsync(emp), t => t.Id == template);
    }

    // (6) في غياب أي قالب لمسمّى الموظّف، يقع على القالب العام (fallback).
    [Fact]
    public async Task GeneralFallback_AppliesWhenNoMoreSpecificTemplate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var lonelyRole = await CreateJobRoleAsync("GF_LONELY");
        var otherRole = await CreateJobRoleAsync("GF_OTHER");
        var general = await PublishAsync(admin, jobRoleId: null);
        var otherTemplate = await PublishAsync(admin, jobRoleId: otherRole);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, lonelyRole, null, null);

        var list = await SelfListAsync(emp);
        Assert.Contains(list, t => t.Id == general);
        Assert.DoesNotContain(list, t => t.Id == otherTemplate);
    }

    // (7) إسناد الموظّف يتفوّق على الفريق والإدارة (أولوية أعلى) — يظهر قالب الموظّف.
    [Fact]
    public async Task EmployeeInclude_BeatsTeamAndDepartment()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var role = await CreateJobRoleAsync("EB_ROLE");
        var team = await CreateTeamAsync("EB_TEAM");
        var dept = await CreateDepartmentAsync("EB_DEPT");
        var empTemplate = await PublishAsync(admin, jobRoleId: null);   // أساسي عبر إسناد الموظّف
        var teamTemplate = await PublishAsync(admin, jobRoleId: null);  // أساسي عبر إسناد الفريق
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, role, team, dept);

        await AddAssignment(admin, empTemplate, TemplateAssignmentScope.Employee, empId, TemplateAssignmentKind.Include);
        await AddAssignment(admin, teamTemplate, TemplateAssignmentScope.Team, team, TemplateAssignmentKind.Include);

        var list = await SelfListAsync(emp);
        Assert.Contains(list, t => t.Id == empTemplate);
        Assert.DoesNotContain(list, t => t.Id == teamTemplate); // أُسقط لأن قالب الموظّف أخصّ
    }

    // (8) استثناء الموظّف يتفوّق على كل المستويات الأدنى (مسمّى/فريق/إدارة/عام).
    [Fact]
    public async Task EmployeeExclude_BeatsAllLowerIncludes()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var role = await CreateJobRoleAsync("EXB_ROLE");
        var template = await PublishAsync(admin, jobRoleId: role); // يطابق المسمّى (Include ضمني)
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, role, null, null);
        // إسناد إضافي على المسمّى ليتأكد أن استثناء الموظّف يتفوّق رغمه.
        await AddAssignment(admin, template, TemplateAssignmentScope.JobRole, role, TemplateAssignmentKind.Include);

        await AddAssignment(admin, template, TemplateAssignmentScope.Employee, empId, TemplateAssignmentKind.Exclude);

        Assert.DoesNotContain(await SelfListAsync(emp), t => t.Id == template);
    }

    // (9) استثناء الفريق يمنع القالب عن أعضاء الفريق.
    [Fact]
    public async Task TeamExclude_BlocksTemplateForTeamMembers()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var team = await CreateTeamAsync("TX_TEAM");
        var template = await PublishAsync(admin, jobRoleId: null);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, null, team, null);
        await AddAssignment(admin, template, TemplateAssignmentScope.Team, team, TemplateAssignmentKind.Include);
        Assert.Contains(await SelfListAsync(emp), t => t.Id == template);

        await AddAssignment(admin, template, TemplateAssignmentScope.Team, team, TemplateAssignmentKind.Exclude);

        Assert.DoesNotContain(await SelfListAsync(emp), t => t.Id == template);
    }

    // (10) استثناء الإدارة يمنع القالب عن منتسبي الإدارة.
    [Fact]
    public async Task DepartmentExclude_BlocksTemplateForDepartmentMembers()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var dept = await CreateDepartmentAsync("DX_DEPT");
        var template = await PublishAsync(admin, jobRoleId: null);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, null, null, dept);
        await AddAssignment(admin, template, TemplateAssignmentScope.Department, dept, TemplateAssignmentKind.Include);
        Assert.Contains(await SelfListAsync(emp), t => t.Id == template);

        await AddAssignment(admin, template, TemplateAssignmentScope.Department, dept, TemplateAssignmentKind.Exclude);

        Assert.DoesNotContain(await SelfListAsync(emp), t => t.Id == template);
    }

    // (11) لا يزيد التقرير الأساسي عن واحد لكل (موظّف، دورية): الأخصّ يفوز والأعمّ يُسقَط.
    [Fact]
    public async Task NoMoreThanOnePrimaryPerEmployeePeriod_MostSpecificWins()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var role = await CreateJobRoleAsync("P1_ROLE");
        var roleTemplate = await PublishAsync(admin, jobRoleId: role); // أساسي عبر المسمّى
        var empTemplate = await PublishAsync(admin, jobRoleId: null);   // أساسي عبر إسناد الموظّف
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, role, null, null);

        await AddAssignment(admin, empTemplate, TemplateAssignmentScope.Employee, empId, TemplateAssignmentKind.Include);

        var list = await SelfListAsync(emp);
        var primaries = list.Where(t => t.Classification == TemplateClassification.Primary
            && (t.Id == roleTemplate || t.Id == empTemplate)).ToList();
        Assert.Single(primaries);
        Assert.Equal(empTemplate, primaries[0].Id); // الأخصّ (الموظّف) فاز
    }

    // (12) القالب التكميلي يظهر بجانب الأساسي دون إسقاط.
    [Fact]
    public async Task SupplementaryShowsAlongsidePrimary()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var role = await CreateJobRoleAsync("SUP_ROLE");
        var primary = await PublishAsync(admin, jobRoleId: role, classification: TemplateClassification.Primary);
        var supp = await PublishAsync(admin, jobRoleId: null, classification: TemplateClassification.Supplementary);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, role, null, null);

        await AddAssignment(admin, supp, TemplateAssignmentScope.Employee, empId, TemplateAssignmentKind.Include);

        var list = await SelfListAsync(emp);
        Assert.Contains(list, t => t.Id == primary);
        Assert.Contains(list, t => t.Id == supp);
    }

    // (13) نقطة assignments تُرجِع سبب الربط لكل موظّف مرتبط.
    [Fact]
    public async Task AssignmentsEndpoint_ReturnsPerUserMatchReason()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var role = await CreateJobRoleAsync("MR_ROLE");
        var template = await PublishAsync(admin, jobRoleId: null);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, role, null, null);
        await AddAssignment(admin, template, TemplateAssignmentScope.Employee, empId, TemplateAssignmentKind.Include);

        var dto = await GetAssignmentsAsync(admin, template);
        var matched = dto.MatchedUsers.FirstOrDefault(u => u.UserId == empId);
        Assert.NotNull(matched);
        Assert.Equal("matchedByUser", matched!.MatchReason);
    }

    // (14) نقطة assignments تكشف تعارض «أكثر من أساسي لنفس الدورية» لموظّف.
    [Fact]
    public async Task AssignmentsEndpoint_ReturnsConflicts()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var t1 = await PublishAsync(admin, jobRoleId: null); // أساسي
        var t2 = await PublishAsync(admin, jobRoleId: null); // أساسي
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, null, null, null);
        // كلاهما عبر إسناد الموظّف ⇒ نفس المستوى (Employee) ونفس الدورية ⇒ تعارض.
        await AddAssignment(admin, t1, TemplateAssignmentScope.Employee, empId, TemplateAssignmentKind.Include);
        await AddAssignment(admin, t2, TemplateAssignmentScope.Employee, empId, TemplateAssignmentKind.Include);

        var dto = await GetAssignmentsAsync(admin, t1);
        Assert.Contains(dto.Conflicts, c => c.UserId == empId
            && (c.OtherTemplateId == t2 || c.ThisTemplateId == t2));
    }

    // (15) القوالب المربوطة بالمسمّى قديمًا (بلا أي إسناد صريح) تعمل كما كانت.
    [Fact]
    public async Task LegacyJobRoleLinkedTemplates_StillWork()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var role = await CreateJobRoleAsync("LEG_ROLE");
        var roleTemplate = await PublishAsync(admin, jobRoleId: role);
        var general = await PublishAsync(admin, jobRoleId: null);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetScopesAsync(empId, role, null, null);

        var list = await SelfListAsync(emp);
        Assert.Contains(list, t => t.Id == roleTemplate);
        Assert.DoesNotContain(list, t => t.Id == general);
    }

    // ===== أدوات مساعدة =====

    private async Task<Guid> CreateJobRoleAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new JobRole { NameAr = $"دور {tag}", Code = $"{tag}_{Guid.NewGuid():N}".Substring(0, 18) };
        db.JobRoles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task<Guid> CreateDepartmentAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة {tag}", Code = $"{tag}_{Guid.NewGuid():N}".Substring(0, 18) };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        return dept.Id;
    }

    private async Task<Guid> CreateTeamAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة فريق {tag}", Code = $"D{tag}_{Guid.NewGuid():N}".Substring(0, 18) };
        db.Departments.Add(dept);
        var team = new Team { NameAr = $"فريق {tag}", DepartmentId = dept.Id };
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }

    private static async Task<Guid> PublishAsync(
        HttpClient admin, Guid? jobRoleId,
        TemplateClassification classification = TemplateClassification.Primary,
        PeriodType period = PeriodType.Weekly)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, jobRoleId, period, classification)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return created.Id;
    }

    private static async Task<HttpResponseMessage> AddAssignment(
        HttpClient admin, Guid templateId, TemplateAssignmentScope scope, Guid scopeId, TemplateAssignmentKind kind)
        => await admin.PostAsJsonAsync($"/api/report-templates/{templateId}/assignments",
            new CreateAssignmentRequest(scope, scopeId, kind, null));

    private async Task SetScopesAsync(Guid userId, Guid? jobRoleId, Guid? teamId, Guid? departmentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRoleId;
        user.TeamId = teamId;
        user.DepartmentId = departmentId;
        await db.SaveChangesAsync();
    }

    private static async Task<List<ReportTemplateDto>> SelfListAsync(HttpClient client)
        => (await (await client.GetAsync("/api/report-templates?status=Published&isActive=true&assignedOnly=true"))
            .ReadAsync<List<ReportTemplateDto>>())!;

    private static async Task<TemplateAssignmentsDto> GetAssignmentsAsync(HttpClient admin, Guid templateId)
        => (await (await admin.GetAsync($"/api/report-templates/{templateId}/assignments"))
            .ReadAsync<TemplateAssignmentsDto>())!;
}
