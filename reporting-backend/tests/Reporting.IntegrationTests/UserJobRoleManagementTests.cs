using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Directory;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// السطح المخصّص لتعديل المسمّى الوظيفي للموظف (PATCH /directory/users/{id}/job-role).
/// يتحقّق من: من يملك السماح (Admin/CeoSupport/HR/GM/CEO) ومن يُحجب (Manager/TeamLeader/Employee)،
/// وأن تغيير المسمّى يبدّل القوالب المعروضة (assignedOnly)، مع بقاء إسناد/استثناء الموظّف الأعلى أولويةً،
/// وتسجيل Audit (user.jobrole.changed)، وعدم تأثّر التقارير المُسلَّمة سابقًا.
/// </summary>
[Collection("Integration")]
public class UserJobRoleManagementTests
{
    private readonly CustomWebApplicationFactory _factory;

    public UserJobRoleManagementTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ── من يملك السماح بتعديل المسمّى (200) ──────────────────────────────

    [Fact]
    public async Task UpdateJobRole_AsAdmin_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var roleId = await CreateJobRoleAsync("ADMIN_OK");

        var res = await admin.PatchAsJsonAsync($"/api/directory/users/{targetId}/job-role",
            new UpdateUserJobRoleRequest(roleId, "تعيين أوّلي"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var dto = await res.ReadAsync<DirectoryUserDto>();
        Assert.Equal(roleId, dto!.JobRoleId);
    }

    [Fact]
    public async Task UpdateJobRole_AsCeoSupport_200()
    {
        var (ceoSupport, _) = await TestAuth.CreateUserAsync(_factory, Roles.CeoSupport);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var roleId = await CreateJobRoleAsync("CEOS_OK");

        var res = await ceoSupport.PatchAsJsonAsync($"/api/directory/users/{targetId}/job-role",
            new UpdateUserJobRoleRequest(roleId, null));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task UpdateJobRole_AsHr_200()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var roleId = await CreateJobRoleAsync("HR_OK");

        var res = await hr.PatchAsJsonAsync($"/api/directory/users/{targetId}/job-role",
            new UpdateUserJobRoleRequest(roleId, null));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task UpdateJobRole_AsGeneralManager_200()
    {
        var (gm, _) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var roleId = await CreateJobRoleAsync("GM_OK");

        var res = await gm.PatchAsJsonAsync($"/api/directory/users/{targetId}/job-role",
            new UpdateUserJobRoleRequest(roleId, null));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task UpdateJobRole_AsCeo_200()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var roleId = await CreateJobRoleAsync("CEO_OK");

        var res = await ceo.PatchAsJsonAsync($"/api/directory/users/{targetId}/job-role",
            new UpdateUserJobRoleRequest(roleId, null));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ── من يُحجب عن تعديل المسمّى (403) ──────────────────────────────────

    [Fact]
    public async Task UpdateJobRole_AsManager_403()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var roleId = await CreateJobRoleAsync("MGR_NO");

        var res = await manager.PatchAsJsonAsync($"/api/directory/users/{targetId}/job-role",
            new UpdateUserJobRoleRequest(roleId, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UpdateJobRole_AsTeamLeader_403()
    {
        var (leader, _) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var roleId = await CreateJobRoleAsync("TL_NO");

        var res = await leader.PatchAsJsonAsync($"/api/directory/users/{targetId}/job-role",
            new UpdateUserJobRoleRequest(roleId, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UpdateJobRole_AsEmployee_403()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var roleId = await CreateJobRoleAsync("EMP_NO");

        var res = await employee.PatchAsJsonAsync($"/api/directory/users/{targetId}/job-role",
            new UpdateUserJobRoleRequest(roleId, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UpdateJobRole_Anonymous_401()
    {
        var client = _factory.CreateClient();
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var roleId = await CreateJobRoleAsync("ANON_NO");

        var res = await client.PatchAsJsonAsync($"/api/directory/users/{targetId}/job-role",
            new UpdateUserJobRoleRequest(roleId, null));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── أثر تغيير المسمّى على قوالب التقارير (assignedOnly) ──────────────

    // تغيير المسمّى يبدّل القوالب المعروضة: من قالب المسمّى القديم إلى قالب المسمّى الجديد.
    [Fact]
    public async Task UpdateJobRole_ChangesAssignedTemplates()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleA = await CreateJobRoleAsync("CHG_A");
        var roleB = await CreateJobRoleAsync("CHG_B");
        var templateA = await PublishAsync(admin, jobRoleId: roleA);
        var templateB = await PublishAsync(admin, jobRoleId: roleB);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, roleA);

        // قبل التغيير: يرى قالب المسمّى A فقط.
        var before = await SelfListAsync(emp);
        Assert.Contains(before, t => t.Id == templateA);
        Assert.DoesNotContain(before, t => t.Id == templateB);

        var res = await admin.PatchAsJsonAsync($"/api/directory/users/{empId}/job-role",
            new UpdateUserJobRoleRequest(roleB, null));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // بعد التغيير: يرى قالب المسمّى B بدل A.
        var after = await SelfListAsync(emp);
        Assert.Contains(after, t => t.Id == templateB);
        Assert.DoesNotContain(after, t => t.Id == templateA);
    }

    // إسناد الموظّف الصريح يبقى الأعلى أولويةً حتى بعد تغيير المسمّى (لا يطغى المسمّى الجديد عليه).
    [Fact]
    public async Task EmployeeInclude_StaysAboveJobRole_AfterChange()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleA = await CreateJobRoleAsync("INC_A");
        var roleB = await CreateJobRoleAsync("INC_B");
        var empTemplate = await PublishAsync(admin, jobRoleId: null);  // أساسي عبر إسناد الموظّف
        var roleBTemplate = await PublishAsync(admin, jobRoleId: roleB); // أساسي عبر المسمّى B
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, roleA);
        await AddAssignment(admin, empTemplate, TemplateAssignmentScope.Employee, empId, TemplateAssignmentKind.Include);

        // غيّر المسمّى إلى B الذي له قالبه الأساسي.
        var res = await admin.PatchAsJsonAsync($"/api/directory/users/{empId}/job-role",
            new UpdateUserJobRoleRequest(roleB, null));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // إسناد الموظّف (الأخصّ) يفوز ويُسقط قالب المسمّى الأعمّ لنفس الدورية.
        var list = await SelfListAsync(emp);
        Assert.Contains(list, t => t.Id == empTemplate);
        Assert.DoesNotContain(list, t => t.Id == roleBTemplate);
    }

    // استثناء الموظّف الصريح يظل مانعًا للقالب حتى لو صار المسمّى الجديد يطابقه.
    [Fact]
    public async Task EmployeeExclude_StillBlocks_AfterChange()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleA = await CreateJobRoleAsync("EXC_A");
        var roleB = await CreateJobRoleAsync("EXC_B");
        var template = await PublishAsync(admin, jobRoleId: roleB); // يطابق المسمّى B
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, roleA);
        await AddAssignment(admin, template, TemplateAssignmentScope.Employee, empId, TemplateAssignmentKind.Exclude);

        // غيّر المسمّى إلى B (الذي يطابق القالب) — يجب أن يظل الاستثناء مانعًا.
        var res = await admin.PatchAsJsonAsync($"/api/directory/users/{empId}/job-role",
            new UpdateUserJobRoleRequest(roleB, null));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        Assert.DoesNotContain(await SelfListAsync(emp), t => t.Id == template);
    }

    // ── Audit + سلامة التقارير القديمة + حواجز الإدخال ───────────────────

    // كل تغيير يُسجَّل في Audit بالقيمة القديمة/الجديدة والمنفّذ — بلا أي بيانات حسّاسة.
    [Fact]
    public async Task UpdateJobRole_WritesAudit()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var roleId = await CreateJobRoleAsync("AUDIT_OK");

        await admin.PatchAsJsonAsync($"/api/directory/users/{targetId}/job-role",
            new UpdateUserJobRoleRequest(roleId, "سبب التغيير"));

        var logs = await (await admin.GetAsync($"/api/audit-logs?entityId={targetId}")).ReadAsync<List<AuditLogDto>>();
        Assert.NotNull(logs);
        Assert.Contains(logs!, l => l.Action == "user.jobrole.changed");
    }

    // التقارير المُسلَّمة/المسوّدة سابقًا لا تتأثّر بتغيير المسمّى (تحتفظ بنسخة القالب).
    [Fact]
    public async Task UpdateJobRole_DoesNotAffectExistingSubmissions()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleA = await CreateJobRoleAsync("OLD_A");
        var roleB = await CreateJobRoleAsync("OLD_B");
        var (templateA, fieldA) = await PublishWithFieldAsync(admin, roleA);
        await PublishAsync(admin, jobRoleId: roleB);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, roleA);

        // الموظّف ينشئ مسودة على قالب المسمّى A.
        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateA, PeriodType.Weekly, "2026-W20")))
            .ReadAsync<SubmissionDto>();
        Assert.NotNull(draft);
        var versionId = draft!.ReportTemplateVersionId;
        _ = fieldA;

        // غيّر المسمّى إلى B.
        await admin.PatchAsJsonAsync($"/api/directory/users/{empId}/job-role",
            new UpdateUserJobRoleRequest(roleB, null));

        // التقرير القديم ما زال موجودًا بنفس نسخة القالب — لم يُحذف ولم يتغيّر.
        var fetched = await (await emp.GetAsync($"/api/submissions/{draft.Id}")).ReadAsync<SubmissionDto>();
        Assert.NotNull(fetched);
        Assert.Equal(draft.Id, fetched!.Id);
        Assert.Equal(versionId, fetched.ReportTemplateVersionId);
    }

    // طلب مسمّى غير موجود يُرفض (404) دون تعديل المستخدم.
    [Fact]
    public async Task UpdateJobRole_NonexistentJobRole_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await admin.PatchAsJsonAsync($"/api/directory/users/{targetId}/job-role",
            new UpdateUserJobRoleRequest(Guid.NewGuid(), null));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
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

    private async Task SetJobRoleAsync(Guid userId, Guid? jobRoleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRoleId;
        await db.SaveChangesAsync();
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

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishWithFieldAsync(HttpClient admin, Guid? jobRoleId)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, jobRoleId, PeriodType.Weekly, TemplateClassification.Primary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private static async Task<HttpResponseMessage> AddAssignment(
        HttpClient admin, Guid templateId, TemplateAssignmentScope scope, Guid scopeId, TemplateAssignmentKind kind)
        => await admin.PostAsJsonAsync($"/api/report-templates/{templateId}/assignments",
            new CreateAssignmentRequest(scope, scopeId, kind, null));

    private static async Task<List<ReportTemplateDto>> SelfListAsync(HttpClient client)
        => (await (await client.GetAsync("/api/report-templates?status=Published&isActive=true&assignedOnly=true"))
            .ReadAsync<List<ReportTemplateDto>>())!;
}
