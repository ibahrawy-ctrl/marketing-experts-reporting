using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// أرشفة/إعادة تفعيل/حذف نهائي للعملاء والمشاريع (MVP بلا Migration — Closed = «مؤرشف»).
/// يثبت: الأرشفة وإعادة التفعيل، الحذف النهائي للعنصر غير المستخدم، منع الحذف عند وجود
/// ارتباط (تقرير مباشر، استخدام داخل ProjectRepeatableSection.ValueJson، مخاطر، ملاحظات)،
/// استثناء المؤرشف ومشاريع العميل المؤرشف من قائمة الاختيار (selectableOnly)،
/// وبقاء المشروع المؤرشف قابلًا للاسترجاع للعرض التاريخي للتقارير القديمة.
/// </summary>
[Collection("Integration")]
public class ClientProjectArchiveDeleteTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ClientProjectArchiveDeleteTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string SectionConfigJson =
        "{\"projectRequired\":true,\"minProjects\":1,\"maxProjects\":5," +
        "\"fields\":[{\"key\":\"spend\",\"label\":\"الميزانية\",\"type\":\"Currency\",\"required\":true}]}";

    // ===== 1: أرشفة عميل =====
    [Fact]
    public async Task Archive_Client_Sets_Closed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل للأرشفة", null);

        var res = await admin.PostAsync($"/api/clients/{client.Id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<ClientDto>();
        Assert.Equal(ClientStatus.Closed, dto!.Status);
    }

    // ===== 2: إعادة تفعيل عميل (Closed→Active) =====
    [Fact]
    public async Task Reactivate_Client_Sets_Active()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل لإعادة التفعيل", null);
        await admin.PostAsync($"/api/clients/{client.Id}/archive", null);

        var res = await admin.PostAsync($"/api/clients/{client.Id}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<ClientDto>();
        Assert.Equal(ClientStatus.Active, dto!.Status);
    }

    // ===== 3: أرشفة مشروع =====
    [Fact]
    public async Task Archive_Project_Sets_Closed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل مشروع للأرشفة", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع للأرشفة", ServiceType.Seo);

        var res = await admin.PostAsync($"/api/projects/{project.Id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<ProjectDto>();
        Assert.Equal(ProjectStatus.Closed, dto!.Status);
    }

    // ===== 4: إعادة تفعيل مشروع (Closed→Active) =====
    [Fact]
    public async Task Reactivate_Project_Sets_Active()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل مشروع لإعادة التفعيل", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع لإعادة التفعيل", ServiceType.Social);
        await admin.PostAsync($"/api/projects/{project.Id}/archive", null);

        var res = await admin.PostAsync($"/api/projects/{project.Id}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<ProjectDto>();
        Assert.Equal(ProjectStatus.Active, dto!.Status);
    }

    // ===== 5: حذف نهائي لعميل غير مستخدم =====
    [Fact]
    public async Task Delete_Unused_Client_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل غير مستخدم", null);

        var res = await admin.DeleteAsync($"/api/clients/{client.Id}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var get = await admin.GetAsync($"/api/clients/{client.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    // ===== 6: منع حذف عميل مستخدم (له مشروع) → 409 =====
    [Fact]
    public async Task Delete_Used_Client_With_Project_Blocked_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل مستخدم", null);
        await CreateProjectAsync(admin, client.Id, "مشروع يمنع الحذف", ServiceType.MediaBuying);

        var res = await admin.DeleteAsync($"/api/clients/{client.Id}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        // ما زال موجودًا.
        var get = await admin.GetAsync($"/api/clients/{client.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    // ===== 7: حذف نهائي لمشروع غير مستخدم =====
    [Fact]
    public async Task Delete_Unused_Project_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل مشروع غير مستخدم", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع غير مستخدم", ServiceType.Website);

        var res = await admin.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var get = await admin.GetAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    // ===== 8: منع حذف مشروع مرتبط بتقرير مباشرة → 409 =====
    [Fact]
    public async Task Delete_Project_Used_In_Direct_Submission_Blocked_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var client = await CreateClientAsync(admin, "عميل تقرير مباشر", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع تقرير مباشر", ServiceType.Video);

        await SubmitDirectAsync(admin, templateId, fieldId, "2026-W21", project.Id);

        var res = await admin.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ===== 9: منع حذف مشروع مستخدم داخل ProjectRepeatableSection.ValueJson → 409 =====
    [Fact]
    public async Task Delete_Project_Used_In_Section_ValueJson_Blocked_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishSectionTemplateAsync(admin);
        var client = await CreateClientAsync(admin, "عميل قسم متكرر", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع داخل القسم", ServiceType.MediaBuying);

        await SubmitSectionAsync(admin, templateId, fieldId, "2026-W22", project.Id);

        var res = await admin.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ===== 10: المشروع المؤرشف لا يظهر في قائمة الاختيار (selectableOnly) =====
    [Fact]
    public async Task Archived_Project_Not_In_SelectableOnly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل قائمة اختيار", null);
        var active = await CreateProjectAsync(admin, client.Id, "مشروع نشط", ServiceType.Seo);
        var archived = await CreateProjectAsync(admin, client.Id, "مشروع مؤرشف", ServiceType.Seo);
        await admin.PostAsync($"/api/projects/{archived.Id}/archive", null);

        var list = await (await admin.GetAsync($"/api/projects?clientId={client.Id}&selectableOnly=true"))
            .ReadAsync<List<ProjectDto>>();
        var ids = list!.Select(p => p.Id).ToList();

        Assert.Contains(active.Id, ids);
        Assert.DoesNotContain(archived.Id, ids);
    }

    // ===== 11: مشروع تابع لعميل مؤرشف لا يظهر في قائمة الاختيار =====
    [Fact]
    public async Task Project_Of_Archived_Client_Not_In_SelectableOnly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل سيُؤرشف", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع عميل مؤرشف", ServiceType.Social);

        await admin.PostAsync($"/api/clients/{client.Id}/archive", null);

        var list = await (await admin.GetAsync($"/api/projects?clientId={client.Id}&selectableOnly=true"))
            .ReadAsync<List<ProjectDto>>();
        Assert.DoesNotContain(project.Id, list!.Select(p => p.Id));
    }

    // ===== 12: المشروع المؤرشف يبقى قابلًا للاسترجاع للعرض التاريخي للتقرير القديم =====
    [Fact]
    public async Task Archived_Project_Still_Resolvable_For_Old_Report_Detail()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var client = await CreateClientAsync(admin, "عميل تاريخي", null);
        var project = await CreateProjectAsync(admin, client.Id, "مشروع تاريخي", ServiceType.Website);

        var sub = await SubmitDirectAsync(admin, templateId, fieldId, "2026-W23", project.Id);
        await admin.PostAsync($"/api/projects/{project.Id}/archive", null);

        // المشروع المؤرشف ما زال يُسترجَع كاملًا (للعرض في تفاصيل التقرير القديم).
        var got = await (await admin.GetAsync($"/api/projects/{project.Id}")).ReadAsync<ProjectDto>();
        Assert.NotNull(got);
        Assert.Equal(ProjectStatus.Closed, got!.Status);
        Assert.Equal("مشروع تاريخي", got.Name);

        // وما زال مرتبطًا بالتقرير القديم ضمن تقارير المشروع.
        var rows = await (await admin.GetAsync($"/api/projects/{project.Id}/reports"))
            .ReadAsync<List<LinkedReportRow>>();
        Assert.Contains(sub.Id, rows!.Select(r => r.SubmissionId));
    }

    // ===== مساعدون =====
    private static async Task<ClientDto> CreateClientAsync(HttpClient c, string name, Guid? amId)
        => (await (await c.PostAsJsonAsync("/api/clients", new CreateClientRequest(name, amId)))
            .ReadAsync<ClientDto>())!;

    private static async Task<ProjectDto> CreateProjectAsync(HttpClient c, Guid clientId, string name, ServiceType serviceType)
        => (await (await c.PostAsJsonAsync("/api/projects",
                new CreateProjectRequest(clientId, name, serviceType)))
            .ReadAsync<ProjectDto>())!;

    private static async Task<SubmissionDto> SubmitDirectAsync(HttpClient c, Guid templateId, Guid fieldId,
        string periodKey, Guid projectId)
    {
        var draft = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey, projectId)))
            .ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 1000m, null, null, null) }));
        return (await (await c.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
    }

    private static async Task<SubmissionDto> SubmitSectionAsync(HttpClient c, Guid templateId, Guid fieldId,
        string periodKey, Guid projectId)
    {
        var draft = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey)))
            .ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, null, null, null,
                $"[{{\"projectId\":\"{projectId}\",\"answers\":{{\"spend\":\"1500\"}}}}]") }));
        return (await (await c.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
    }

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishWeeklyTemplateAsync(HttpClient admin)
    {
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
}
