using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// TEMPLATE-ROLE-GUARD-R1 — الحارس المركزي للإسناد على الخادم.
/// يثبت أنّ إنشاء/حفظ/إرسال أي تقرير لقالب غير مُسنَد للمستخدم يُرفَض بـ 403 وكود
/// report.template_not_assigned، باستخدام نفس منطق assignedOnly المصدر الوحيد للحقيقة،
/// مع عدم كسر القوالب التكميلية ولا فحص نطاق المشروع (400) ولا قراءة/مراجعة الإدارة.
/// </summary>
[Collection("Integration")]
public class TemplateRoleGuardTests
{
    private readonly CustomWebApplicationFactory _factory;

    public TemplateRoleGuardTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string GuardCode = "report.template_not_assigned";

    private const string SectionConfigJson =
        "{\"projectRequired\":true,\"minProjects\":1,\"maxProjects\":5," +
        "\"fields\":[{\"key\":\"spend\",\"label\":\"الميزانية\",\"type\":\"Currency\",\"required\":true}]}";

    // ===== (1-5) قوالب متقاطعة لمستخدم بمسمّى غير مطابق ⇒ createDraft = 403 + الكود =====

    [Fact] // (1) قالب «مودريشن» يحاول استخدامه مستخدم بمسمّى «مصمّم».
    public async Task CrossTemplate_SocialModToDesigner_CreateDraft_403()
        => await AssertCrossTemplateForbiddenAsync("SOCIALMOD", "DESIGNER");

    [Fact] // (2) قالب «مصمّم» يحاول استخدامه مستخدم بمسمّى «فيديو».
    public async Task CrossTemplate_DesignerToVideo_CreateDraft_403()
        => await AssertCrossTemplateForbiddenAsync("DESIGNER", "VIDEO");

    [Fact] // (3) قالب «أخصائي SEO» يحاول استخدامه مستخدم بمسمّى «كاتب مقالات SEO».
    public async Task CrossTemplate_SeoSpecialistToArticleWriter_CreateDraft_403()
        => await AssertCrossTemplateForbiddenAsync("SEOSPEC", "SEOARTW");

    [Fact] // (4) قالب «كاتب محتوى» يحاول استخدامه مستخدم بمسمّى «مدير حسابات».
    public async Task CrossTemplate_ContentWriterToAccountMgr_CreateDraft_403()
        => await AssertCrossTemplateForbiddenAsync("CONTENTW", "ACCMGR");

    [Fact] // (5) قائد فريق لا يملك قالبًا تنفيذيًّا غير مُسنَد له صراحةً ⇒ 403.
    public async Task TeamLeader_ExecutiveTemplate_Unassigned_CreateDraft_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var execRoleId = await CreateJobRoleAsync("EXEC");
        var templateId = await PublishRoleTemplateAsync(admin, execRoleId);

        var (tl, _) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);

        var res = await tl.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W31"));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Contains(GuardCode, await res.Content.ReadAsStringAsync());
    }

    // ===== (6) المستخدم الصحيح ينشئ ويُرسِل قالب مسمّاه ⇒ نجاح =====
    [Fact]
    public async Task AssignedUser_OwnRoleTemplate_CreateDraftAndSubmit_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateJobRoleAsync("OWNROLE");
        var (templateId, fieldId) = await PublishRoleTemplateWithFieldAsync(admin, roleId);

        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, roleId);

        var draftRes = await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W32"));
        Assert.Equal(HttpStatusCode.OK, draftRes.StatusCode);
        var draft = await draftRes.ReadAsync<SubmissionDto>();

        var save = await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 42m, null, null, null) }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var submit = await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
    }

    // ===== (7) قالب تكميلي مُسنَد (نفس قاعدة الإسناد) ⇒ createDraft ينجح =====
    [Fact]
    public async Task SupplementaryAssigned_CreateDraft_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateJobRoleAsync("SUPPROLE");
        var templateId = await PublishRoleTemplateAsync(admin, roleId, TemplateClassification.Supplementary);

        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, roleId);

        var res = await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W33"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== (8) قالب مُسنَد (عام) + مشروع خارج النطاق ⇒ 400 repeatable_section_invalid (لا 403) =====
    [Fact]
    public async Task OutOfScopeProject_AssignedTemplate_Returns400_NotForbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishSectionTemplateAsync(admin);
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        // القالب عام (fallback) ⇒ الحارس يسمح بإنشاء المسودة.
        var draftRes = await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W34"));
        Assert.Equal(HttpStatusCode.OK, draftRes.StatusCode);
        var draft = await draftRes.ReadAsync<SubmissionDto>();

        // مشروع يراه المسؤول فقط — خارج نطاق الموظف.
        var project = await CreateProjectAsync(admin, "مشروع خارج النطاق");
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, null, null, null,
                $"[{{\"projectId\":\"{project.Id}\",\"answers\":{{\"spend\":\"1500\"}}}}]") }));

        var submit = await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
        var raw = await submit.Content.ReadAsStringAsync();
        Assert.Contains("submission.repeatable_section_invalid", raw);
        Assert.DoesNotContain(GuardCode, raw);
    }

    // ===== (9) قراءة/مراجعة الإدارة غير مكسورة =====
    [Fact]
    public async Task AdminReadAndReviewWorkflow_NotBroken()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        // (أ) الإدارة تقرأ تفاصيل قالب مرتبط بمسمّى لا يخصّها ⇒ 200 (القراءة لم تُكسَر).
        var roleId = await CreateJobRoleAsync("READROLE");
        var roleTemplateId = await PublishRoleTemplateAsync(admin, roleId);
        var read = await admin.GetAsync($"/api/report-templates/{roleTemplateId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        // (ب) مسار المراجعة: موظف بقالب عام يُرسِل لمديره، والمدير يعتمد ⇒ 200.
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (genTemplateId, genFieldId) = await PublishRoleTemplateWithFieldAsync(admin, null);
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId);

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(genTemplateId, PeriodType.Weekly, TestCalendar.Cycle(1)))).ReadAsync<SubmissionDto>();
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(genFieldId, null, 10m, null, null, null) }));
        var submit = await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        // المدير (الموافِق الحالي) يعتمد ⇒ 200 (المراجعة لم تُكسَر بالحارس).
        var approve = await manager.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest(null));
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
    }

    // ===== (10) مسودة لمستخدم مُسنَد لا تتأثّر (تُحفَظ وتُرسَل بشكل طبيعي) =====
    [Fact]
    public async Task ExistingDraftByAssignedUser_SaveAndSubmit_Unaffected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateJobRoleAsync("KEEPROLE");
        var (templateId, fieldId) = await PublishRoleTemplateWithFieldAsync(admin, roleId);

        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, roleId);

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, TestCalendar.Cycle(2)))).ReadAsync<SubmissionDto>();

        // حفظ أوّل ثم تعديل (محاكاة متابعة مسودة قائمة) ثم إرسال — كلها ناجحة.
        var save1 = await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 5m, null, null, null) }));
        Assert.Equal(HttpStatusCode.OK, save1.StatusCode);
        var save2 = await emp.PutAsJsonAsync($"/api/submissions/{draft.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 7m, null, null, null) }));
        Assert.Equal(HttpStatusCode.OK, save2.StatusCode);
        var submit = await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
    }

    // ===== أدوات مساعدة =====

    private async Task AssertCrossTemplateForbiddenAsync(string templateRoleTag, string userRoleTag)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var templateRoleId = await CreateJobRoleAsync(templateRoleTag);
        var templateId = await PublishRoleTemplateAsync(admin, templateRoleId);

        var userRoleId = await CreateJobRoleAsync(userRoleTag);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, userRoleId);

        var res = await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W30"));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Contains(GuardCode, await res.Content.ReadAsStringAsync());
    }

    private async Task<Guid> CreateJobRoleAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new JobRole { NameAr = $"دور {tag}", Code = $"{tag}_{Guid.NewGuid():N}".Substring(0, 18) };
        db.JobRoles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task SetJobRoleAsync(Guid userId, Guid roleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = roleId;
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> PublishRoleTemplateAsync(HttpClient admin, Guid? jobRoleId,
        TemplateClassification classification = TemplateClassification.Primary)
    {
        var (id, _) = await PublishRoleTemplateWithFieldAsync(admin, jobRoleId, classification);
        return id;
    }

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishRoleTemplateWithFieldAsync(
        HttpClient admin, Guid? jobRoleId,
        TemplateClassification classification = TemplateClassification.Primary)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, jobRoleId, PeriodType.Weekly, classification)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishSectionTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب مشاريع {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
                TemplateClassification.Supplementary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("أداء المشاريع", "projects", FieldType.ProjectRepeatableSection, true, null, SectionConfigJson)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private static async Task<ProjectDto> CreateProjectAsync(HttpClient admin, string name)
    {
        var client = (await (await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest($"عميل {Guid.NewGuid():N}", null))).ReadAsync<ClientDto>())!;
        return (await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, name, ServiceType.MediaBuying))).ReadAsync<ProjectDto>())!;
    }
}
