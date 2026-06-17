using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// PHASE 6 — بُعد العميل/المشروع وإدارة الحسابات. يثبت أن نطاق رؤية العملاء/المشاريع
/// مفروض خادمًا (403/404 خارج النطاق)، وأن ربط التقارير بالمشروع يشتقّ العميل،
/// وأن صحّة العملاء وملخّص المشروع وربط الحوكمة تعمل وفق الدور والنطاق — بلا أسماء مثبّتة.
/// </summary>
[Collection("Integration")]
public class Phase6ClientProjectTests
{
    private readonly CustomWebApplicationFactory _factory;

    public Phase6ClientProjectTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Mgr;        // مدير حساب (AccountManager) داخل النطاق
        public required (HttpClient C, Guid Id) OtherMgr;   // مدير آخر خارج نطاق Mgr
        public required (HttpClient C, Guid Id) Tl;         // قائد فريق تحت Mgr
        public required (HttpClient C, Guid Id) Emp;        // موظف بلا صلة بالمشاريع
    }

    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var mgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var otherMgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, mgr.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gm.UserId);

        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            Mgr = (mgr.Client, mgr.UserId),
            OtherMgr = (otherMgr.Client, otherMgr.UserId),
            Tl = (tl.Client, tl.UserId),
            Emp = (emp.Client, emp.UserId),
        };
    }

    // ===== 1: المسؤول (رؤية كاملة) يرى كل العملاء =====
    [Fact]
    public async Task Admin_Lists_All_Clients_Including_Others()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var org = await BuildOrgAsync();

        var c1 = await CreateClientAsync(admin, "عميل أ", org.Mgr.Id);
        var c2 = await CreateClientAsync(admin, "عميل ب", org.OtherMgr.Id);

        var list = await (await admin.GetAsync("/api/clients")).ReadAsync<List<ClientDto>>();
        var ids = list!.Select(c => c.Id).ToList();

        Assert.Contains(c1.Id, ids);
        Assert.Contains(c2.Id, ids);
    }

    // ===== 2: عميل خارج النطاق → 403 =====
    [Fact]
    public async Task OutOfScope_Client_Get_Returns_Forbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var org = await BuildOrgAsync();

        var client = await CreateClientAsync(admin, "عميل خاص", org.OtherMgr.Id);

        var res = await org.Mgr.C.GetAsync($"/api/clients/{client.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 3: مشروع خارج النطاق → 403 =====
    [Fact]
    public async Task OutOfScope_Project_Get_Returns_Forbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var org = await BuildOrgAsync();

        var client = await CreateClientAsync(admin, "عميل مشروع", org.OtherMgr.Id);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع خاص", ServiceType.Social, amId: org.OtherMgr.Id);

        var res = await org.Mgr.C.GetAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 4: الموظف ممنوع من إنشاء عميل =====
    [Fact]
    public async Task Employee_Create_Client_Forbidden()
    {
        var org = await BuildOrgAsync();
        var res = await org.Emp.C.PostAsJsonAsync("/api/clients", new CreateClientRequest("محاولة موظف"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 5: الموظف ممنوع من إنشاء مشروع =====
    [Fact]
    public async Task Employee_Create_Project_Forbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var org = await BuildOrgAsync();
        var client = await CreateClientAsync(admin, "عميل للمشروع", org.Mgr.Id);

        var res = await org.Emp.C.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, "محاولة موظف", ServiceType.Seo));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 6: المدير غير ذي الرؤية الكاملة يجب أن يضع نفسه مدير حساب =====
    [Fact]
    public async Task Manager_Create_Client_Must_Set_Self_As_AccountManager()
    {
        var org = await BuildOrgAsync();

        // محاولة بوضع مدير حساب آخر → 403
        var bad = await org.Mgr.C.PostAsJsonAsync("/api/clients",
            new CreateClientRequest("عميل المدير", org.OtherMgr.Id));
        Assert.Equal(HttpStatusCode.Forbidden, bad.StatusCode);

        // وضع نفسه مدير حساب → 200
        var ok = await org.Mgr.C.PostAsJsonAsync("/api/clients",
            new CreateClientRequest("عميل المدير", org.Mgr.Id));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    // ===== 7: مدير الحساب يرى عميله فقط في القائمة =====
    [Fact]
    public async Task AccountManager_Sees_Own_Client_In_List_NotOthers()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var org = await BuildOrgAsync();

        var mine = await CreateClientAsync(admin, "عميلي", org.Mgr.Id);
        var theirs = await CreateClientAsync(admin, "عميل الآخر", org.OtherMgr.Id);

        var list = await (await org.Mgr.C.GetAsync("/api/clients")).ReadAsync<List<ClientDto>>();
        var ids = list!.Select(c => c.Id).ToList();

        Assert.Contains(mine.Id, ids);
        Assert.DoesNotContain(theirs.Id, ids);
    }

    // ===== 8: مدير الحساب يرى مشروعه في القائمة =====
    [Fact]
    public async Task AccountManager_Sees_Own_Project_In_List()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var org = await BuildOrgAsync();

        var client = await CreateClientAsync(admin, "عميل المشاريع", org.Mgr.Id);
        var mine = await CreateProjectAsync(admin, client.Id, "مشروعي", ServiceType.MediaBuying, amId: org.Mgr.Id);
        var theirs = await CreateProjectAsync(admin, client.Id, "مشروع الآخر", ServiceType.Seo, amId: org.OtherMgr.Id);

        var list = await (await org.Mgr.C.GetAsync("/api/projects")).ReadAsync<List<ProjectDto>>();
        var ids = list!.Select(p => p.Id).ToList();

        Assert.Contains(mine.Id, ids);
        Assert.DoesNotContain(theirs.Id, ids);
    }

    // ===== 9: ربط التقرير بالمشروع يشتقّ العميل =====
    [Fact]
    public async Task Report_Linked_To_Project_Sets_ClientId()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var client = await CreateClientAsync(admin, "عميل التقرير", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع التقرير", ServiceType.Social);

        var sub = await SubmitAsync(admin, templateId, fieldId, "2026-W31", project.Id);

        Assert.Equal(project.Id, sub.ProjectId);
        Assert.Equal(client.Id, sub.ClientId);
    }

    // ===== 10: التقرير المرتبط يظهر ضمن تقارير المشروع =====
    [Fact]
    public async Task Linked_Report_Appears_In_Project_Reports()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var client = await CreateClientAsync(admin, "عميل ١٠", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع ١٠", ServiceType.Video);

        var sub = await SubmitAsync(admin, templateId, fieldId, "2026-W32", project.Id);

        var rows = await (await admin.GetAsync($"/api/projects/{project.Id}/reports"))
            .ReadAsync<List<LinkedReportRow>>();
        Assert.Contains(sub.Id, rows!.Select(r => r.SubmissionId));
    }

    // ===== 11: التقرير المرتبط يظهر ضمن تقارير العميل =====
    [Fact]
    public async Task Linked_Report_Appears_In_Client_Reports()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var client = await CreateClientAsync(admin, "عميل ١١", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع ١١", ServiceType.Seo);

        var sub = await SubmitAsync(admin, templateId, fieldId, "2026-W33", project.Id);

        var rows = await (await admin.GetAsync($"/api/clients/{client.Id}/reports"))
            .ReadAsync<List<LinkedReportRow>>();
        Assert.Contains(sub.Id, rows!.Select(r => r.SubmissionId));
    }

    // ===== 12: ربط تقرير بمشروع خارج النطاق ممنوع =====
    [Fact]
    public async Task OutOfScope_Project_Link_Is_Forbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishWeeklyTemplateAsync(admin);
        var org = await BuildOrgAsync();

        var client = await CreateClientAsync(admin, "عميل ١٢", org.OtherMgr.Id);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع ١٢", ServiceType.Social, amId: org.OtherMgr.Id);

        var res = await org.Emp.C.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W34", project.Id));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 13: صحّة العملاء — المسؤول (رؤية كاملة) يحصل على ملخّص بلا صفوف =====
    [Fact]
    public async Task ClientHealth_SeesAll_Admin_Returns_Summary_NoRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        await CreateClientAsync(admin, "عميل صحّة", null);

        var report = await (await admin.GetAsync("/api/clients/health")).ReadAsync<ClientHealthReport>();

        Assert.False(report!.CanViewRows);
        Assert.Empty(report.Rows);
        Assert.Equal("summary", report.ViewLevel);
        Assert.True(report.TotalClients >= 1);
    }

    // ===== 14: صحّة العملاء — مدير الحساب (نطاق) يحصل على صفوف =====
    [Fact]
    public async Task ClientHealth_Scoped_AccountManager_Returns_Rows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var org = await BuildOrgAsync();
        var client = await CreateClientAsync(admin, "عميل مدير الحساب", org.Mgr.Id);

        var report = await (await org.Mgr.C.GetAsync("/api/clients/health")).ReadAsync<ClientHealthReport>();

        Assert.True(report!.CanViewRows);
        Assert.Equal("scoped", report.ViewLevel);
        Assert.Contains(client.Id, report.Rows.Select(r => r.ClientId));
    }

    // ===== 15: ملخّص المشروع يَعُدّ التقارير المرتبطة =====
    [Fact]
    public async Task Project_Summary_Counts_Linked_Reports()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var client = await CreateClientAsync(admin, "عميل ١٥", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع ١٥", ServiceType.Social);

        await SubmitAsync(admin, templateId, fieldId, "2026-W35", project.Id);

        var summary = await (await admin.GetAsync($"/api/projects/{project.Id}/summary"))
            .ReadAsync<ProjectSummaryDto>();

        Assert.Equal(1, summary!.TotalReports);
        Assert.Equal(1, summary.ClosedReports + summary.PendingReports);
        Assert.NotNull(summary.LastReportAtUtc);
    }

    // ===== 16: المشروع المؤرشف يُستثنى من القائمة الافتراضية =====
    [Fact]
    public async Task Archived_Project_Excluded_From_Default_List_IncludedWithFlag()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل ١٦", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع ١٦", ServiceType.Seo);

        await admin.PostAsync($"/api/projects/{project.Id}/archive", null);

        var def = await (await admin.GetAsync($"/api/projects?clientId={client.Id}")).ReadAsync<List<ProjectDto>>();
        Assert.DoesNotContain(project.Id, def!.Select(p => p.Id));

        var all = await (await admin.GetAsync($"/api/projects?clientId={client.Id}&includeClosed=true"))
            .ReadAsync<List<ProjectDto>>();
        Assert.Contains(project.Id, all!.Select(p => p.Id));
    }

    // ===== 17: مخاطرة مرتبطة بالمشروع تنعكس في عدّاد المخاطر بالملخّص =====
    [Fact]
    public async Task Risk_Linked_To_Project_Reflected_In_Project_Summary_OpenRiskCount()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل ١٧", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع ١٧", ServiceType.Social);

        await admin.PostAsJsonAsync("/api/risks", new CreateRiskRequest(
            "مخاطرة مشروع", null, RiskSeverity.High, null, null, null, ProjectId: project.Id, ClientId: client.Id));

        var summary = await (await admin.GetAsync($"/api/projects/{project.Id}/summary"))
            .ReadAsync<ProjectSummaryDto>();

        Assert.Equal(1, summary!.OpenRiskCount);
    }

    // ===== 18: قائد الفريق يرى المشروع عبر فريقه المسؤول =====
    [Fact]
    public async Task TeamLeader_Sees_Project_Via_OwnerTeam()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var org = await BuildOrgAsync();
        var deptId = await CreateDepartmentAsync($"إدارة {Guid.NewGuid():N}", org.Mgr.Id);
        var teamId = await CreateTeamAsync($"فريق {Guid.NewGuid():N}", deptId, org.Tl.Id);

        var client = await CreateClientAsync(admin, "عميل الفريق", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع الفريق", ServiceType.Website, ownerTeamId: teamId);

        var res = await org.Tl.C.GetAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await (await org.Tl.C.GetAsync("/api/projects")).ReadAsync<List<ProjectDto>>();
        Assert.Contains(project.Id, list!.Select(p => p.Id));
    }

    // ===== مساعدون =====
    private static async Task<ClientDto> CreateClientAsync(HttpClient c, string name, Guid? amId)
        => (await (await c.PostAsJsonAsync("/api/clients", new CreateClientRequest(name, amId)))
            .ReadAsync<ClientDto>())!;

    private static async Task<ProjectDto> CreateProjectAsync(HttpClient c, Guid clientId, string name,
        ServiceType serviceType, Guid? ownerTeamId = null, Guid? amId = null)
        => (await (await c.PostAsJsonAsync("/api/projects",
                new CreateProjectRequest(clientId, name, serviceType, OwnerTeamId: ownerTeamId, AccountManagerId: amId)))
            .ReadAsync<ProjectDto>())!;

    private static async Task<SubmissionDto> SubmitAsync(HttpClient c, Guid templateId, Guid fieldId,
        string periodKey, Guid? projectId)
    {
        var draft = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey, projectId)))
            .ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 1000m, null, null, null) }));
        return (await (await c.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
    }

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishWeeklyTemplateAsync(HttpClient admin)
    {
        // قالب تكميلي (Supplementary) كي لا يصطدم بحارس ازدواج التقرير الأساسي في قاعدة الاختبار المشتركة.
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"تقرير أسبوعي {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
                TemplateClassification.Supplementary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private async Task<Guid> CreateDepartmentAsync(string nameAr, Guid managerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = nameAr, ManagerId = managerId, IsActive = true };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        return dept.Id;
    }

    private async Task<Guid> CreateTeamAsync(string nameAr, Guid departmentId, Guid? leaderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var team = new Team { NameAr = nameAr, DepartmentId = departmentId, TeamLeaderId = leaderId, IsActive = true };
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }
}
