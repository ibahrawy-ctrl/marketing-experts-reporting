using System.Net;
using System.Net.Http.Json;
using Reporting.Application.AccountPortfolio;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// محفظة مدير الحساب (ACCOUNT-MANAGER-PORTFOLIO-FULL-R1) — عرض فقط، نطاق مفروض خادمًا على
/// مشاريع المستخدم نفسه (Project.AccountManagerId == المستخدم). يثبت: الحاجة للدور،
/// قصر الرؤية على مشاريع المستخدم، اشتقاق العملاء من المشاريع المرئية فقط، رفض ما هو خارج
/// النطاق (403) وغير الموجود (404)، واستثناء المسودّات/المُعادة من المخرجات.
/// </summary>
[Collection("Integration")]
public class AccountPortfolioTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AccountPortfolioTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== 1: الدور مطلوب — موظف عادي يُرفَض 403 =====
    [Fact]
    public async Task Projects_RequiresRole_Employee_403()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await employee.GetAsync("/api/account-portfolio/projects");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 2: مجهول يُرفَض 401 =====
    [Fact]
    public async Task Projects_Anonymous_401()
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync("/api/account-portfolio/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ===== 3: صاحب المحفظة بلا مشاريع — قائمة فارغة 200 =====
    [Fact]
    public async Task Projects_ReaderWithNoProjects_EmptyList()
    {
        var (reader, _) = await TestAuth.CreateUserAsync(_factory, Roles.AccountPortfolioReader);
        var res = await reader.GetAsync("/api/account-portfolio/projects");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.ReadAsync<List<PortfolioProjectDto>>();
        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    // ===== 4: يرى مشاريعه فقط (لا مشاريع مدير حساب آخر) =====
    [Fact]
    public async Task Projects_OnlyOwnProjects_Visible()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (reader, readerId) = await TestAuth.CreateUserAsync(_factory, Roles.AccountPortfolioReader);
        var (_, otherAmId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var client = await CreateClientAsync(admin, $"عميل محفظة {Guid.NewGuid():N}");
        var mine1 = await CreateProjectAsync(admin, client.Id, "مشروعي 1", ServiceType.Seo, readerId);
        var mine2 = await CreateProjectAsync(admin, client.Id, "مشروعي 2", ServiceType.Social, readerId);
        var notMine = await CreateProjectAsync(admin, client.Id, "ليس لي", ServiceType.Video, otherAmId);

        var list = await (await reader.GetAsync("/api/account-portfolio/projects"))
            .ReadAsync<List<PortfolioProjectDto>>();
        var ids = list!.Select(p => p.Id).ToList();

        Assert.Contains(mine1.Id, ids);
        Assert.Contains(mine2.Id, ids);
        Assert.DoesNotContain(notMine.Id, ids);
    }

    // ===== 5: العملاء مشتقّون من المشاريع المرئية فقط (لا من Client.AccountManagerId) =====
    [Fact]
    public async Task Clients_DerivedFromVisibleProjectsOnly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (reader, readerId) = await TestAuth.CreateUserAsync(_factory, Roles.AccountPortfolioReader);
        var (_, otherAmId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        // عميل أُسنِد القارئ مديرَ حسابٍ له لكن بلا مشروع مرئيّ تابع له ⇒ يجب ألّا يظهر.
        var clientNoVisibleProject = await CreateClientAsync(admin, $"عميل بلا مشروع مرئي {Guid.NewGuid():N}", readerId);
        await CreateProjectAsync(admin, clientNoVisibleProject.Id, "مشروع لمدير آخر", ServiceType.Website, otherAmId);

        // عميل له مشروع مرئيّ للقارئ ⇒ يجب أن يظهر.
        var clientWithVisible = await CreateClientAsync(admin, $"عميل بمشروع مرئي {Guid.NewGuid():N}");
        await CreateProjectAsync(admin, clientWithVisible.Id, "مشروعي المرئي", ServiceType.Seo, readerId);

        var clients = await (await reader.GetAsync("/api/account-portfolio/clients"))
            .ReadAsync<List<PortfolioClientDto>>();
        var ids = clients!.Select(c => c.Id).ToList();

        Assert.Contains(clientWithVisible.Id, ids);
        Assert.DoesNotContain(clientNoVisibleProject.Id, ids);
    }

    // ===== 6: مشروع خارج النطاق ⇒ 403، غير موجود ⇒ 404، ضمن النطاق ⇒ 200 =====
    [Fact]
    public async Task Project_Detail_ScopeEnforced()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (reader, readerId) = await TestAuth.CreateUserAsync(_factory, Roles.AccountPortfolioReader);
        var (_, otherAmId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var client = await CreateClientAsync(admin, $"عميل تفاصيل {Guid.NewGuid():N}");
        var mine = await CreateProjectAsync(admin, client.Id, "مشروعي", ServiceType.Seo, readerId);
        var other = await CreateProjectAsync(admin, client.Id, "مشروع آخر", ServiceType.Video, otherAmId);

        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/account-portfolio/projects/{mine.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/account-portfolio/projects/{other.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/account-portfolio/projects/{Guid.NewGuid()}")).StatusCode);
    }

    // ===== 7: عميل بلا مشروع مرئيّ للقارئ ⇒ تفاصيله 403 =====
    [Fact]
    public async Task Client_Detail_OutOfScope_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (reader, _) = await TestAuth.CreateUserAsync(_factory, Roles.AccountPortfolioReader);
        var (_, otherAmId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var client = await CreateClientAsync(admin, $"عميل خارج النطاق {Guid.NewGuid():N}");
        await CreateProjectAsync(admin, client.Id, "مشروع لمدير آخر", ServiceType.Social, otherAmId);

        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/account-portfolio/clients/{client.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/account-portfolio/clients/{Guid.NewGuid()}")).StatusCode);
    }

    // ===== 8: المخرجات تستثني المسودّة وتشمل المُسلَّم =====
    [Fact]
    public async Task Outputs_ExcludeDraft_IncludeSubmitted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (reader, readerId) = await TestAuth.CreateUserAsync(_factory, Roles.AccountPortfolioReader);

        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var client = await CreateClientAsync(admin, $"عميل مخرجات {Guid.NewGuid():N}");
        var project = await CreateProjectAsync(admin, client.Id, "مشروع مخرجات", ServiceType.Seo, readerId);

        // مسودّة (لا تُسلَّم) — يجب أن تُستثنى.
        await CreateDraftAsync(admin, templateId, fieldId, "2026-W30", project.Id);
        // تقرير مُسلَّم — يجب أن يظهر.
        var submitted = await SubmitDirectAsync(admin, templateId, fieldId, "2026-W31", project.Id);

        var outputs = await (await reader.GetAsync($"/api/account-portfolio/projects/{project.Id}/outputs"))
            .ReadAsync<List<PortfolioOutputDto>>();
        var subIds = outputs!.Select(o => o.SubmissionId).ToList();

        Assert.Contains(submitted.Id, subIds);
        Assert.All(outputs!, o => Assert.NotEqual(SubmissionStatus.Draft, o.Status));
    }

    // ===== 9: مخرجات مشروع خارج النطاق ⇒ 403 =====
    [Fact]
    public async Task Outputs_OutOfScope_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (reader, _) = await TestAuth.CreateUserAsync(_factory, Roles.AccountPortfolioReader);
        var (_, otherAmId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var client = await CreateClientAsync(admin, $"عميل مخرجات خارج النطاق {Guid.NewGuid():N}");
        var other = await CreateProjectAsync(admin, client.Id, "مشروع آخر", ServiceType.Video, otherAmId);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await reader.GetAsync($"/api/account-portfolio/projects/{other.Id}/outputs")).StatusCode);
    }

    // ===== مساعدون =====
    private static async Task<ClientDto> CreateClientAsync(HttpClient c, string name, Guid? amId = null)
        => (await (await c.PostAsJsonAsync("/api/clients", new CreateClientRequest(name, amId)))
            .ReadAsync<ClientDto>())!;

    private static async Task<ProjectDto> CreateProjectAsync(HttpClient c, Guid clientId, string name,
        ServiceType serviceType, Guid accountManagerId)
        => (await (await c.PostAsJsonAsync("/api/projects",
                new CreateProjectRequest(clientId, name, serviceType, AccountManagerId: accountManagerId)))
            .ReadAsync<ProjectDto>())!;

    private static async Task<SubmissionDto> CreateDraftAsync(HttpClient c, Guid templateId, Guid fieldId,
        string periodKey, Guid projectId)
    {
        var draft = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey, projectId)))
            .ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 1000m, null, null, null) }));
        return draft;
    }

    private static async Task<SubmissionDto> SubmitDirectAsync(HttpClient c, Guid templateId, Guid fieldId,
        string periodKey, Guid projectId)
    {
        var draft = await CreateDraftAsync(c, templateId, fieldId, periodKey, projectId);
        return (await (await c.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
    }

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishWeeklyTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"تقرير محفظة {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
                TemplateClassification.Supplementary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }
}
