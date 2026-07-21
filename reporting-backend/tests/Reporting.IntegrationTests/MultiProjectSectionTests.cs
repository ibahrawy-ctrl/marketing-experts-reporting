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
/// أقسام المشاريع المتكررة (ProjectRepeatableSection) — MVP بلا Migration.
/// يثبت تحقق الخادم: الحد الأدنى للمشاريع، اشتراط اختيار المشروع، فرض النطاق على
/// المشروع داخل قيمة القسم (منع IDOR)، والحقول الفرعية المطلوبة لكل مشروع،
/// مع المسار الإيجابي حين يكون المشروع ضمن نطاق المُسلِّم.
/// </summary>
[Collection("Integration")]
public class MultiProjectSectionTests
{
    private readonly CustomWebApplicationFactory _factory;

    public MultiProjectSectionTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string ConfigJson =
        "{\"projectRequired\":true,\"minProjects\":1,\"maxProjects\":5," +
        "\"fields\":[{\"key\":\"spend\",\"label\":\"الميزانية\",\"type\":\"Currency\",\"required\":true}]}";

    /// <summary>قالب تكميلي منشور بحقل واحد من نوع ProjectRepeatableSection (مطلوب).</summary>
    private static async Task<(Guid TemplateId, Guid FieldId)> PublishSectionTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب مشاريع {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
                TemplateClassification.Supplementary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("أداء المشاريع", "projects", FieldType.ProjectRepeatableSection, true, null, ConfigJson)))
            .ReadAsync<TemplateFieldDto>();

        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private static async Task<Guid> CreateDraftAsync(HttpClient c, Guid templateId, string periodKey)
        => (await (await c.PostAsJsonAsync("/api/submissions",
                new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey)))
            .ReadAsync<SubmissionDto>())!.Id;

    private static Task SaveSectionAsync(HttpClient c, Guid submissionId, Guid fieldId, string valueJson)
        => c.PutAsJsonAsync($"/api/submissions/{submissionId}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, null, null, null, valueJson) }));

    private static async Task<ProjectDto> CreateProjectAsync(HttpClient admin, string name)
    {
        var client = (await (await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest($"عميل {Guid.NewGuid():N}", null))).ReadAsync<ClientDto>())!;
        return (await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, name, ServiceType.MediaBuying))).ReadAsync<ProjectDto>())!;
    }

    // ===== 1: لا مشاريع مُدخَلة مع قسم مطلوب → 400 =====
    [Fact]
    public async Task Submit_NoProjects_WhenRequired_Returns400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishSectionTemplateAsync(admin);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var draftId = await CreateDraftAsync(employee, templateId, "2026-W41");
        var res = await employee.PostAsync($"/api/submissions/{draftId}/submit", null);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 2: عنصر بلا اختيار مشروع (projectRequired) → 400 =====
    [Fact]
    public async Task Submit_EntryMissingProject_Returns400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishSectionTemplateAsync(admin);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var draftId = await CreateDraftAsync(employee, templateId, "2026-W42");
        await SaveSectionAsync(employee, draftId, fieldId,
            "[{\"projectId\":null,\"answers\":{\"spend\":\"1500\"}}]");

        var res = await employee.PostAsync($"/api/submissions/{draftId}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 3: مشروع خارج نطاق المُسلِّم → 400 (منع IDOR عبر قيمة القسم) =====
    [Fact]
    public async Task Submit_OutOfScopeProject_Returns400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishSectionTemplateAsync(admin);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        // مشروع يراه المسؤول فقط — الموظف خارج نطاقه.
        var project = await CreateProjectAsync(admin, "مشروع خارج النطاق");

        var draftId = await CreateDraftAsync(employee, templateId, "2026-W43");
        await SaveSectionAsync(employee, draftId, fieldId,
            $"[{{\"projectId\":\"{project.Id}\",\"answers\":{{\"spend\":\"1500\"}}}}]");

        var res = await employee.PostAsync($"/api/submissions/{draftId}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 4: حقل فرعي مطلوب مفقود → 400 =====
    [Fact]
    public async Task Submit_MissingRequiredSubField_Returns400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishSectionTemplateAsync(admin);
        var project = await CreateProjectAsync(admin, "مشروع بلا ميزانية");

        var draftId = await CreateDraftAsync(admin, templateId, "2026-W44");
        await SaveSectionAsync(admin, draftId, fieldId,
            $"[{{\"projectId\":\"{project.Id}\",\"answers\":{{\"spend\":\"\"}}}}]");

        var res = await admin.PostAsync($"/api/submissions/{draftId}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 5: مشروع ضمن النطاق + حقل مطلوب مُعبّأ → نجاح =====
    [Fact]
    public async Task Submit_InScopeProject_WithRequiredAnswer_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishSectionTemplateAsync(admin);
        // المسؤول يرى كل المشاريع، فالمشروع ضمن نطاقه.
        var project = await CreateProjectAsync(admin, "مشروع صالح");

        var draftId = await CreateDraftAsync(admin, templateId, "2026-W45");
        await SaveSectionAsync(admin, draftId, fieldId,
            $"[{{\"projectId\":\"{project.Id}\",\"answers\":{{\"spend\":\"1500\"}}}}]");

        var submitted = await (await admin.PostAsync($"/api/submissions/{draftId}/submit", null))
            .ReadAsync<SubmissionDto>();

        // المسؤول بلا مدير مباشر ⇒ يُغلق التسليم مباشرة.
        Assert.Equal(SubmissionStatus.Closed, submitted!.Status);
    }

    // مفتاح دورة أسبوعية صالحة وقد بدأت فعلًا (حسب تاريخ ريّاض الحالي) كي تمرّ حارس التقويم
    // (calendar.cycle_not_open) ونصل فعليًّا إلى تحقّق القسم المتكرر. مستقل عن تاريخ التشغيل.
    private static string CurrentCycleKey()
        => ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhToday());

    // ===== 6: تكرار نفس المشروع في القسم الواحد → 400 (صفّ واحد لكل مشروع/فترة) =====
    [Fact]
    public async Task Submit_DuplicateProject_Returns400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishSectionTemplateAsync(admin);
        var project = await CreateProjectAsync(admin, "مشروع مكرر");

        var draftId = await CreateDraftAsync(admin, templateId, CurrentCycleKey());
        // نفس المشروع مرّتين ضمن نفس التسليم ⇒ يجب أن يُرفَض.
        await SaveSectionAsync(admin, draftId, fieldId,
            $"[{{\"projectId\":\"{project.Id}\",\"answers\":{{\"spend\":\"1000\"}}}}," +
            $"{{\"projectId\":\"{project.Id}\",\"answers\":{{\"spend\":\"2000\"}}}}]");

        var res = await admin.PostAsync($"/api/submissions/{draftId}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 7: مشروعان مختلفان في القسم الواحد → نجاح (الحارس يمنع التكرار فقط) =====
    [Fact]
    public async Task Submit_TwoDistinctProjects_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishSectionTemplateAsync(admin);
        var projectA = await CreateProjectAsync(admin, "مشروع أ");
        var projectB = await CreateProjectAsync(admin, "مشروع ب");

        var draftId = await CreateDraftAsync(admin, templateId, CurrentCycleKey());
        await SaveSectionAsync(admin, draftId, fieldId,
            $"[{{\"projectId\":\"{projectA.Id}\",\"answers\":{{\"spend\":\"1000\"}}}}," +
            $"{{\"projectId\":\"{projectB.Id}\",\"answers\":{{\"spend\":\"2000\"}}}}]");

        var submitted = await (await admin.PostAsync($"/api/submissions/{draftId}/submit", null))
            .ReadAsync<SubmissionDto>();

        // المسؤول بلا مدير مباشر ⇒ يُغلق التسليم مباشرة.
        Assert.Equal(SubmissionStatus.Closed, submitted!.Status);
    }
}
