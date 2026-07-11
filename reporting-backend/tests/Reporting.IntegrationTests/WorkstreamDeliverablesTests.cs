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
/// P2 — مخرَجات خطّة الإنتاج داخل تيار العمل (Workstream Deliverables). يثبت الإنشاء/القائمة/التحديث/
/// التفعيل-التعطيل (بلا حذف نهائيّ)، والتحقّق من صحّة تيار العمل/نوع المخرَج (Domain=deliverable)/سياق
/// الاستخدام (Domain=usage_context)/المسؤول، وعدم وجود قيد تفرّد (مخرَجات غير محدودة)، وأنّ الإدارة فقط
/// تُنشئ (الموظّف 403). **تخطيط فقط — لا يُسجَّل أيّ تنفيذ فعليّ.**
/// </summary>
[Collection("Integration")]
public class WorkstreamDeliverablesTests
{
    private readonly CustomWebApplicationFactory _factory;

    public WorkstreamDeliverablesTests(CustomWebApplicationFactory factory) => _factory = factory;

    // رموز مبذورة في ExecutionTaxonomySeeder.
    private const string TypeWorkstreamWeb = "web_development";
    private const string TypePost = "post";           // Domain=deliverable
    private const string TypeCarousel = "carousel";   // Domain=deliverable
    private const string TypeBlog = "blog_article";   // Domain=deliverable
    private const string UsageOrganic = "organic_social"; // Domain=usage_context

    // ===== 1: الإنشاء ينجح ويعيد المخرَج مع أسماء مُثراة =====
    [Fact]
    public async Task Create_Deliverable_Succeeds_And_Enriches_Names()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);

        var created = await CreateDeliverableAsync(admin, projectId, workstreamId,
            new CreateWorkstreamDeliverableRequest(TypePost, UsageContextCode: UsageOrganic,
                Name: "منشورات ثابتة", PlannedQuantity: 12, EstimatedHours: 24m,
                Priority: DeliverablePriority.High));

        Assert.Equal(workstreamId, created.WorkstreamId);
        Assert.Equal(TypePost, created.DeliverableTypeCode);
        Assert.Equal("منشور", created.DeliverableTypeNameAr);
        Assert.Equal(UsageOrganic, created.UsageContextCode);
        Assert.Equal("سوشيال أورجانيك", created.UsageContextNameAr);
        Assert.Equal("منشورات ثابتة", created.Name);
        Assert.Equal(12, created.PlannedQuantity);
        Assert.Equal(24m, created.EstimatedHours);
        Assert.Equal(DeliverablePriority.High, created.Priority);
        Assert.True(created.IsActive);
    }

    // ===== 2: الاسم الافتراضي = اسم النوع العربيّ عند غيابه =====
    [Fact]
    public async Task Create_Without_Name_Defaults_To_TypeNameAr()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);

        var created = await CreateDeliverableAsync(admin, projectId, workstreamId,
            new CreateWorkstreamDeliverableRequest(TypeCarousel));
        Assert.Equal("كاروسيل", created.Name);
    }

    // ===== 3: القائمة تُرجِع مخرَجات تيار العمل النشطة =====
    [Fact]
    public async Task List_Returns_Active_Deliverables_For_Workstream()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);

        var d1 = await CreateDeliverableAsync(admin, projectId, workstreamId, new CreateWorkstreamDeliverableRequest(TypePost));
        var d2 = await CreateDeliverableAsync(admin, projectId, workstreamId, new CreateWorkstreamDeliverableRequest(TypeCarousel));

        var list = await (await admin.GetAsync($"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables"))
            .ReadAsync<List<WorkstreamDeliverableDto>>();
        var ids = list!.Select(d => d.Id).ToList();

        Assert.Contains(d1.Id, ids);
        Assert.Contains(d2.Id, ids);
    }

    // ===== 4: تيار عمل غير موجود ضمن المشروع → 404 =====
    [Fact]
    public async Task Create_For_Missing_Workstream_Returns_NotFound()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, _) = await CreateProjectWorkstreamAsync(admin);

        var res = await admin.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{Guid.NewGuid()}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== 5: نوع مخرَج غير معروف → 400 =====
    [Fact]
    public async Task Create_With_Invalid_Type_Returns_BadRequest()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);

        var res = await admin.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables",
            new CreateWorkstreamDeliverableRequest("nonexistent_deliverable"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 6: سياق استخدام غير معروف → 400 =====
    [Fact]
    public async Task Create_With_Invalid_Usage_Returns_BadRequest()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);

        var res = await admin.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost, UsageContextCode: "nonexistent_usage"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 7: مسؤول غير موجود → 400 =====
    [Fact]
    public async Task Create_With_Missing_Responsible_Returns_BadRequest()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);

        var res = await admin.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost, ResponsibleUserId: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 8: الإنشاء بمسؤول موجود ينجح =====
    [Fact]
    public async Task Create_With_Valid_Responsible_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);
        var (_, responsibleId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var created = await CreateDeliverableAsync(admin, projectId, workstreamId,
            new CreateWorkstreamDeliverableRequest(TypeBlog, ResponsibleUserId: responsibleId));
        Assert.Equal(responsibleId, created.ResponsibleUserId);
        Assert.NotNull(created.ResponsibleUserName);
    }

    // ===== 9: التحديث يغيّر الحقول (النوع ثابت) =====
    [Fact]
    public async Task Update_Changes_Fields_But_Not_Type()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);
        var created = await CreateDeliverableAsync(admin, projectId, workstreamId,
            new CreateWorkstreamDeliverableRequest(TypePost, PlannedQuantity: 5));

        var updated = await (await admin.PutAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables/{created.Id}",
            new UpdateWorkstreamDeliverableRequest(UsageContextCode: UsageOrganic, Name: "منشورات محدّثة",
                PlannedQuantity: 20, EstimatedHours: 40m, Priority: DeliverablePriority.Urgent, SortOrder: 3)))
            .ReadAsync<WorkstreamDeliverableDto>();

        Assert.Equal("منشورات محدّثة", updated!.Name);
        Assert.Equal(20, updated.PlannedQuantity);
        Assert.Equal(40m, updated.EstimatedHours);
        Assert.Equal(DeliverablePriority.Urgent, updated.Priority);
        Assert.Equal(UsageOrganic, updated.UsageContextCode);
        Assert.Equal(3, updated.SortOrder);
        // النوع لم يتغيّر — لقطة ثابتة.
        Assert.Equal(TypePost, updated.DeliverableTypeCode);
    }

    // ===== 10: التعطيل ثم التفعيل (بلا حذف نهائيّ) =====
    [Fact]
    public async Task Deactivate_Then_Activate_Toggles_IsActive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);
        var created = await CreateDeliverableAsync(admin, projectId, workstreamId, new CreateWorkstreamDeliverableRequest(TypePost));

        var deactivated = await (await admin.PatchAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables/{created.Id}/deactivate", null))
            .ReadAsync<WorkstreamDeliverableDto>();
        Assert.False(deactivated!.IsActive);

        var def = await (await admin.GetAsync($"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables"))
            .ReadAsync<List<WorkstreamDeliverableDto>>();
        Assert.DoesNotContain(created.Id, def!.Select(d => d.Id));

        var all = await (await admin.GetAsync($"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables?includeInactive=true"))
            .ReadAsync<List<WorkstreamDeliverableDto>>();
        Assert.Contains(created.Id, all!.Select(d => d.Id));

        var activated = await (await admin.PatchAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables/{created.Id}/activate", null))
            .ReadAsync<WorkstreamDeliverableDto>();
        Assert.True(activated!.IsActive);
    }

    // ===== 11: مخرَجات غير محدودة — نفس النوع مرّتين مسموح (لا تكرار) =====
    [Fact]
    public async Task Unlimited_Deliverables_Same_Type_Allowed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);

        await CreateDeliverableAsync(admin, projectId, workstreamId, new CreateWorkstreamDeliverableRequest(TypePost, Name: "دفعة أولى"));

        var res = await admin.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost, Name: "دفعة ثانية"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 12: الموظّف (غير الإدارة) ممنوع من الإنشاء → 403 =====
    [Fact]
    public async Task Employee_Cannot_Create_Deliverable_Forbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await emp.Client.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 13: الكمية المخطَّطة صفر → 400 quantity_invalid =====
    [Fact]
    public async Task Create_With_Zero_Quantity_Returns_BadRequest()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);

        var res = await admin.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost, PlannedQuantity: 0));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 14: ساعات مقدَّرة سالبة → 400 hours_invalid =====
    [Fact]
    public async Task Create_With_Negative_Hours_Returns_BadRequest()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);

        var res = await admin.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost, PlannedQuantity: 3, EstimatedHours: -1m));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 15: تاريخ الاستحقاق قبل تاريخ البداية → 400 date_range_invalid =====
    [Fact]
    public async Task Create_With_DueBeforeStart_Returns_BadRequest()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);

        var res = await admin.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost, PlannedQuantity: 3,
                StartDate: new DateOnly(2026, 7, 10), DueDate: new DateOnly(2026, 7, 1)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 16: الإنشاء على هدف عمل معطّل → 409 workstream.inactive.conflict =====
    [Fact]
    public async Task Create_On_Inactive_Workstream_Returns_Conflict()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);
        await DeactivateWorkstreamAsync(admin, projectId, workstreamId);

        var res = await admin.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost, PlannedQuantity: 3));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ===== 17: التحديث بكمية صفر → 400 (نفس التحقّق على مسار التحديث) =====
    [Fact]
    public async Task Update_With_Zero_Quantity_Returns_BadRequest()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);
        var created = await CreateDeliverableAsync(admin, projectId, workstreamId,
            new CreateWorkstreamDeliverableRequest(TypePost, PlannedQuantity: 5));

        var res = await admin.PutAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables/{created.Id}",
            new UpdateWorkstreamDeliverableRequest(PlannedQuantity: 0));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 18: مدير الحسابات (Account Manager) يُنشئ مخرَجًا لمشروعه — النطاق لا الدور =====
    [Fact]
    public async Task AccountManager_Can_Create_Deliverable_For_Own_Project()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);
        var (am, amId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetAccountManagerAsync(projectId, amId);

        var res = await am.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost, PlannedQuantity: 4));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 19: مدير الحسابات يعدّل/يفعّل/يعطّل مخرَجات مشروعه =====
    [Fact]
    public async Task AccountManager_Can_Manage_Own_Project_Deliverables()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);
        var (am, amId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetAccountManagerAsync(projectId, amId);

        var created = await CreateDeliverableAsync(am, projectId, workstreamId,
            new CreateWorkstreamDeliverableRequest(TypePost, PlannedQuantity: 4));

        var updated = await am.PutAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables/{created.Id}",
            new UpdateWorkstreamDeliverableRequest(Name: "محدَّث بواسطة مدير الحسابات", PlannedQuantity: 6));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var deactivated = await am.PatchAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        var activated = await am.PatchAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables/{created.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
    }

    // ===== 20: مدير الحسابات يقرأ مخرَجات مشروعه =====
    [Fact]
    public async Task AccountManager_Can_List_Own_Project_Deliverables()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);
        var (am, amId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetAccountManagerAsync(projectId, amId);
        await CreateDeliverableAsync(admin, projectId, workstreamId,
            new CreateWorkstreamDeliverableRequest(TypePost, PlannedQuantity: 2));

        var res = await am.GetAsync($"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.ReadAsync<List<WorkstreamDeliverableDto>>();
        Assert.Single(list!);
    }

    // ===== 21: مدير حسابات مشروع آخر لا يرى/يعدّل مشروعًا ليس له → 403 (خارج النطاق) =====
    [Fact]
    public async Task AccountManager_Of_Other_Project_Is_Forbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectA, workstreamA) = await CreateProjectWorkstreamAsync(admin);
        var (_, amAId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetAccountManagerAsync(projectA, amAId);

        // مدير حسابات مشروع مختلف تمامًا (B) يحاول الكتابة في مشروع A.
        var (projectB, _) = await CreateProjectWorkstreamAsync(admin);
        var (amB, amBId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetAccountManagerAsync(projectB, amBId);

        var res = await amB.PostAsJsonAsync(
            $"/api/projects/{projectA}/workstreams/{workstreamA}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost, PlannedQuantity: 2));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 22: العبث بـ projectId لا يفلت من النطاق — قراءة مشروع الغير → 403 =====
    [Fact]
    public async Task AccountManager_Cannot_Tamper_ProjectId_To_Escape_Scope()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectA, workstreamA) = await CreateProjectWorkstreamAsync(admin);
        var (amB, amBId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (projectB, _) = await CreateProjectWorkstreamAsync(admin);
        await SetAccountManagerAsync(projectB, amBId);

        // amB يملك projectB فقط؛ يبدّل projectId إلى A (ليس له) — يجب أن يُرفض عند القراءة أيضًا.
        var res = await amB.GetAsync($"/api/projects/{projectA}/workstreams/{workstreamA}/deliverables");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 23: Manager (ضمن ProjectPlanManagers) وله رؤية المشروع يُنشئ — مسار الدور =====
    [Fact]
    public async Task Manager_With_Project_View_Can_Create_By_Role()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);
        var (mgr, mgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        // يمنح Manager رؤية المشروع بجعله قائد الفريق المالك — الإدارة عبر الدور لا النطاق.
        await SetOwnerTeamWithLeaderAsync(projectId, mgrId);

        var res = await mgr.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost, PlannedQuantity: 3));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 24: TeamLeader وله رؤية المشروع مُستثنى من إدارة الخطّة → 403 (البوّابة تستبعده) =====
    [Fact]
    public async Task TeamLeader_With_Project_View_Cannot_Manage_Plan()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (projectId, workstreamId) = await CreateProjectWorkstreamAsync(admin);
        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        // TeamLeader يقود الفريق المالك ⇒ يرى المشروع، لكنه ليس في ProjectPlanManagers ولا مدير حساباته.
        await SetOwnerTeamWithLeaderAsync(projectId, tlId);

        // يقرأ (رؤية) بنجاح لكن لا يدير.
        var read = await tl.GetAsync($"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var write = await tl.PostAsJsonAsync(
            $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables",
            new CreateWorkstreamDeliverableRequest(TypePost, PlannedQuantity: 3));
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    // ===== مساعدون =====
    private static async Task DeactivateWorkstreamAsync(HttpClient admin, Guid projectId, Guid workstreamId)
        => (await admin.PatchAsync($"/api/projects/{projectId}/workstreams/{workstreamId}/deactivate", null))
            .EnsureSuccessStatusCode();

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

    private static async Task<WorkstreamDeliverableDto> CreateDeliverableAsync(
        HttpClient c, Guid projectId, Guid workstreamId, CreateWorkstreamDeliverableRequest req)
        => (await (await c.PostAsJsonAsync(
                $"/api/projects/{projectId}/workstreams/{workstreamId}/deliverables", req))
            .ReadAsync<WorkstreamDeliverableDto>())!;

    private async Task<(Guid ProjectId, Guid WorkstreamId)> CreateProjectWorkstreamAsync(HttpClient admin)
    {
        var client = await (await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest($"عميل {Guid.NewGuid():N}"))).ReadAsync<ClientDto>();
        var project = await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client!.Id, $"مشروع {Guid.NewGuid():N}", ServiceType.Website)))
            .ReadAsync<ProjectDto>();
        var teamId = await CreateTeamAsync();
        var workstream = await (await admin.PostAsJsonAsync($"/api/projects/{project!.Id}/workstreams",
            new CreateProjectWorkstreamRequest(TypeWorkstreamWeb, teamId)))
            .ReadAsync<ProjectWorkstreamDto>();
        return (project.Id, workstream!.Id);
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
