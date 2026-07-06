using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Directory;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// إدارة المسمّيات الوظيفية (CRUD) عبر /directory/job-roles*:
/// من يملك السماح (Admin/CeoSupport/HR/GM/CEO) ومن يُحجب (Manager/TeamLeader/Employee/Anonymous)،
/// منع تكرار الاسم العربي، الأرشفة/إعادة التفعيل (بلا حذف صلب)، إخفاء المؤرشف من الاختيارات الجديدة (activeOnly)،
/// عدّادات الموظفين/القوالب، ظهور المسمّى الجديد للربط بالقوالب وتعيين الموظف، وتسجيل Audit لكل عملية.
/// </summary>
[Collection("Integration")]
public class JobRoleManagementTests
{
    private readonly CustomWebApplicationFactory _factory;

    public JobRoleManagementTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static CreateJobRoleRequest NewRole(string tag, Guid? deptId = null)
        => new($"مسمّى {tag} {Guid.NewGuid():N}".Substring(0, 28), $"Role {tag}", $"{tag}{Guid.NewGuid():N}".Substring(0, 12), deptId);

    // ── من يملك السماح بالإنشاء (200) ──────────────────────────────

    [Fact]
    public async Task Create_AsAdmin_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/directory/job-roles", NewRole("ADMIN"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<JobRoleDetailDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.IsActive);
    }

    [Fact]
    public async Task Create_AsCeo_200()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var res = await ceo.PostAsJsonAsync("/api/directory/job-roles", NewRole("CEO"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Create_AsGeneralManager_200()
    {
        var (gm, _) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var res = await gm.PostAsJsonAsync("/api/directory/job-roles", NewRole("GM"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Create_AsCeoSupport_200()
    {
        var (ceoSupport, _) = await TestAuth.CreateUserAsync(_factory, Roles.CeoSupport);
        var res = await ceoSupport.PostAsJsonAsync("/api/directory/job-roles", NewRole("CEOS"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Create_AsHr_200()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var res = await hr.PostAsJsonAsync("/api/directory/job-roles", NewRole("HR"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ── من يُحجب (403/401) ──────────────────────────────────

    [Fact]
    public async Task Create_AsManager_403()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var res = await manager.PostAsJsonAsync("/api/directory/job-roles", NewRole("MGR"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Create_AsTeamLeader_403()
    {
        var (tl, _) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var res = await tl.PostAsJsonAsync("/api/directory/job-roles", NewRole("TL"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Create_AsEmployee_403()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await emp.PostAsJsonAsync("/api/directory/job-roles", NewRole("EMP"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Create_Anonymous_401()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/directory/job-roles", NewRole("ANON"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Manage_AsEmployee_403()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await emp.GetAsync("/api/directory/job-roles/manage");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── منع تكرار الاسم العربي ──────────────────────────────────

    [Fact]
    public async Task Create_DuplicateArabicName_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var name = $"مسمّى مكرّر {Guid.NewGuid():N}".Substring(0, 24);
        var first = await admin.PostAsJsonAsync("/api/directory/job-roles",
            new CreateJobRoleRequest(name, null, null, null));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var dup = await admin.PostAsJsonAsync("/api/directory/job-roles",
            new CreateJobRoleRequest(name, null, null, null));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Create_EmptyArabicName_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/directory/job-roles",
            new CreateJobRoleRequest("   ", null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ── التعديل ──────────────────────────────────

    [Fact]
    public async Task Update_RenamesRole_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/directory/job-roles", NewRole("UPD"))).ReadAsync<JobRoleDetailDto>();
        var newName = $"اسم معدّل {Guid.NewGuid():N}".Substring(0, 22);
        var newCode = $"UPD{Guid.NewGuid():N}".Substring(0, 12);

        var res = await admin.PutAsJsonAsync($"/api/directory/job-roles/{created!.Id}",
            new UpdateJobRoleRequest(newName, "Updated EN", newCode, null));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<JobRoleDetailDto>();
        Assert.Equal(newName, dto!.NameAr);
        Assert.Equal("Updated EN", dto.NameEn);
    }

    [Fact]
    public async Task Create_DuplicateCode_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var code = $"DUP{Guid.NewGuid():N}".Substring(0, 12);
        var first = await admin.PostAsJsonAsync("/api/directory/job-roles",
            new CreateJobRoleRequest($"رمز أ {Guid.NewGuid():N}".Substring(0, 20), null, code, null));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var dup = await admin.PostAsJsonAsync("/api/directory/job-roles",
            new CreateJobRoleRequest($"رمز ب {Guid.NewGuid():N}".Substring(0, 20), null, code, null));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Update_ToExistingName_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var nameA = $"اسم أ {Guid.NewGuid():N}".Substring(0, 20);
        await admin.PostAsJsonAsync("/api/directory/job-roles", new CreateJobRoleRequest(nameA, null, null, null));
        var b = await (await admin.PostAsJsonAsync("/api/directory/job-roles", NewRole("BX"))).ReadAsync<JobRoleDetailDto>();

        var res = await admin.PutAsJsonAsync($"/api/directory/job-roles/{b!.Id}",
            new UpdateJobRoleRequest(nameA, null, null, null));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Update_NonexistentRole_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PutAsJsonAsync($"/api/directory/job-roles/{Guid.NewGuid()}",
            new UpdateJobRoleRequest("لا يوجد", null, null, null));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── الأرشفة وإعادة التفعيل (بلا حذف صلب) ──────────────────────────────────

    [Fact]
    public async Task Archive_ThenReactivate_TogglesActive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/directory/job-roles", NewRole("ARCH"))).ReadAsync<JobRoleDetailDto>();

        var archived = await admin.PostAsync($"/api/directory/job-roles/{created!.Id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        Assert.False((await archived.ReadAsync<JobRoleDetailDto>())!.IsActive);

        var reactivated = await admin.PostAsync($"/api/directory/job-roles/{created.Id}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.True((await reactivated.ReadAsync<JobRoleDetailDto>())!.IsActive);
    }

    [Fact]
    public async Task Archive_AlreadyArchived_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/directory/job-roles", NewRole("DBLARCH"))).ReadAsync<JobRoleDetailDto>();
        await admin.PostAsync($"/api/directory/job-roles/{created!.Id}/archive", null);

        var again = await admin.PostAsync($"/api/directory/job-roles/{created.Id}/archive", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    // المؤرشف لا يظهر في الاختيارات الجديدة (activeOnly) لكنه يبقى في القائمة الكاملة بحالة مؤرشف.
    [Fact]
    public async Task Archived_HiddenFromActiveOnly_ButVisibleInFullList()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/directory/job-roles", NewRole("HIDE"))).ReadAsync<JobRoleDetailDto>();
        await admin.PostAsync($"/api/directory/job-roles/{created!.Id}/archive", null);

        var activeOnly = await (await admin.GetAsync("/api/directory/job-roles?activeOnly=true")).ReadAsync<List<JobRoleDto>>();
        Assert.DoesNotContain(activeOnly!, r => r.Id == created.Id);

        var full = await (await admin.GetAsync("/api/directory/job-roles")).ReadAsync<List<JobRoleDto>>();
        Assert.Contains(full!, r => r.Id == created.Id && !r.IsActive);
    }

    // ── العدّادات ──────────────────────────────────

    [Fact]
    public async Task Manage_ReportsEmployeeAndTemplateCounts()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/directory/job-roles", NewRole("CNT"))).ReadAsync<JobRoleDetailDto>();
        var roleId = created!.Id;

        // اربط موظّفًا واحدًا + قالبًا واحدًا بالمسمّى.
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await admin.PatchAsJsonAsync($"/api/directory/users/{empId}/job-role", new UpdateUserJobRoleRequest(roleId, null));
        await PublishAsync(admin, roleId);

        var manage = await (await admin.GetAsync("/api/directory/job-roles/manage")).ReadAsync<List<JobRoleDetailDto>>();
        var row = manage!.Single(r => r.Id == roleId);
        Assert.Equal(1, row.EmployeeCount);
        Assert.Equal(1, row.TemplateCount);
    }

    // ── الظهور للربط/التعيين ──────────────────────────────────

    // المسمّى الجديد يظهر فورًا في قائمة المسمّيات (التي تغذّي شاشة تغيير مسمّى الموظف والربط بالقوالب).
    [Fact]
    public async Task NewRole_AppearsInJobRolesList()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/directory/job-roles", NewRole("VIS"))).ReadAsync<JobRoleDetailDto>();

        var list = await (await admin.GetAsync("/api/directory/job-roles")).ReadAsync<List<JobRoleDto>>();
        Assert.Contains(list!, r => r.Id == created!.Id);

        // ويمكن تعيينه لموظف.
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var assign = await admin.PatchAsJsonAsync($"/api/directory/users/{empId}/job-role",
            new UpdateUserJobRoleRequest(created!.Id, null));
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
    }

    // المسمّى الجديد يمكن ربطه بقالب، فيراه الموظّف صاحب المسمّى.
    [Fact]
    public async Task NewRole_CanBeBoundToTemplate_VisibleToEmployee()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/directory/job-roles", NewRole("BIND"))).ReadAsync<JobRoleDetailDto>();
        var templateId = await PublishAsync(admin, created!.Id);

        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await admin.PatchAsJsonAsync($"/api/directory/users/{empId}/job-role", new UpdateUserJobRoleRequest(created.Id, null));

        var list = await (await emp.GetAsync("/api/report-templates?status=Published&isActive=true&assignedOnly=true"))
            .ReadAsync<List<ReportTemplateDto>>();
        Assert.Contains(list!, t => t.Id == templateId);
    }

    // ── Audit ──────────────────────────────────

    [Fact]
    public async Task Lifecycle_WritesAudit_CreatedUpdatedArchivedReactivated()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/directory/job-roles", NewRole("AUD"))).ReadAsync<JobRoleDetailDto>();
        var id = created!.Id;
        await admin.PutAsJsonAsync($"/api/directory/job-roles/{id}",
            new UpdateJobRoleRequest($"معدّل {Guid.NewGuid():N}".Substring(0, 18), null, null, null));
        await admin.PostAsync($"/api/directory/job-roles/{id}/archive", null);
        await admin.PostAsync($"/api/directory/job-roles/{id}/reactivate", null);

        var logs = await (await admin.GetAsync($"/api/audit-logs?entityId={id}")).ReadAsync<List<AuditLogDto>>();
        Assert.NotNull(logs);
        Assert.Contains(logs!, l => l.Action == "jobrole.created");
        Assert.Contains(logs!, l => l.Action == "jobrole.updated");
        Assert.Contains(logs!, l => l.Action == "jobrole.archived");
        Assert.Contains(logs!, l => l.Action == "jobrole.reactivated");
    }

    // ===== أدوات مساعدة =====

    private static async Task<Guid> PublishAsync(HttpClient admin, Guid? jobRoleId)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, jobRoleId, PeriodType.Weekly, TemplateClassification.Primary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return created.Id;
    }
}
