using System.Net;
using System.Net.Http.Json;
using Reporting.Application.ExecutionTaxonomy;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// RC-4 Task 4D2 — إدارة كتالوج تصنيفات التنفيذ (الأدمن/CEO/GM عبر سياسة TemplateGovernance).
/// قراءة/إنشاء/تعديل/تفعيل/تعطيل فقط — لا حذف نهائيّ. Domain و Code غير قابلين للتعديل.
/// أكواد فريدة لكل تشغيل لتفادي تلوّث القاعدة المشتركة الدائمة + تفرّد الفهرس (Domain,Code).
/// </summary>
[Collection("Integration")]
public class ExecutionTaxonomyAdminTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ExecutionTaxonomyAdminTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string UniqueCode(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    // ===== (1) إنشاء + قراءة (Get) + تعديل قيمة تصنيف =====
    [Fact]
    public async Task Admin_CanCreate_Get_Update_Taxonomy()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var code = UniqueCode("QA_CT");

        var created = await (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("content_type", code, "قيمة اختبار", "Test Value", 900)))
            .ReadAsync<ExecutionTaxonomyDto>();
        Assert.NotNull(created);
        Assert.Equal("content_type", created!.Domain);
        Assert.Equal(code, created.Code);
        Assert.Equal("قيمة اختبار", created.NameAr);
        Assert.Equal("Test Value", created.NameEn);
        Assert.True(created.IsActive);
        Assert.Equal(900, created.SortOrder);

        var got = await (await admin.GetAsync($"/api/execution-taxonomy/{created.Id}")).ReadAsync<ExecutionTaxonomyDto>();
        Assert.Equal(created.Id, got!.Id);

        var updated = await (await admin.PutAsJsonAsync($"/api/execution-taxonomy/{created.Id}",
            new UpdateExecutionTaxonomyRequest("قيمة معدّلة", null, 910))).ReadAsync<ExecutionTaxonomyDto>();
        Assert.Equal("قيمة معدّلة", updated!.NameAr);
        Assert.Null(updated.NameEn);
        Assert.Equal(910, updated.SortOrder);
    }

    // ===== (2) الفلترة بالمجال تُعيد قيم ذلك المجال فقط =====
    [Fact]
    public async Task Admin_List_FilterByDomain_ReturnsOnlyThatDomain()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("design_type", UniqueCode("QA_DT"), "تصميم اختبار", null, 920)))
            .EnsureSuccessStatusCode();

        var list = await (await admin.GetAsync("/api/execution-taxonomy?domain=design_type"))
            .ReadAsync<List<ExecutionTaxonomyDto>>();
        Assert.NotNull(list);
        Assert.NotEmpty(list!);
        Assert.All(list!, v => Assert.Equal("design_type", v.Domain));
    }

    // ===== (3) includeInactive يُظهر المعطّلة؛ الافتراضي يُخفيها =====
    [Fact]
    public async Task Admin_List_IncludeInactive_ShowsDeactivated()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("work_status", UniqueCode("QA_WS"), "حالة اختبار", null, 930)))
            .ReadAsync<ExecutionTaxonomyDto>();
        (await admin.PatchAsync($"/api/execution-taxonomy/{created!.Id}/deactivate", null)).EnsureSuccessStatusCode();

        var activeOnly = await (await admin.GetAsync("/api/execution-taxonomy?domain=work_status"))
            .ReadAsync<List<ExecutionTaxonomyDto>>();
        Assert.DoesNotContain(activeOnly!, v => v.Id == created.Id);

        var withInactive = await (await admin.GetAsync("/api/execution-taxonomy?domain=work_status&includeInactive=true"))
            .ReadAsync<List<ExecutionTaxonomyDto>>();
        Assert.Contains(withInactive!, v => v.Id == created.Id);
    }

    // ===== (4) رفض الرمز المكرّر داخل نفس المجال (case-insensitive) ⇒ 409 =====
    [Fact]
    public async Task Create_DuplicateCode_SameDomain_Conflict()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var code = UniqueCode("QA_DUP");
        (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("video_type", code, "قيمة أولى", null, 940)))
            .EnsureSuccessStatusCode();

        var dup = await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("video_type", code.ToUpperInvariant(), "قيمة مكرّرة", null, 941));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    // ===== (5) نفس الرمز في مجال مختلف مسموح =====
    [Fact]
    public async Task Create_SameCode_DifferentDomain_Allowed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var code = UniqueCode("QA_XD");
        (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("design_status", code, "حالة تصميم", null, 950)))
            .EnsureSuccessStatusCode();

        var other = await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("video_status", code, "حالة فيديو", null, 951));
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    // ===== (6) مجال غير معروف ⇒ 400 (لا إنشاء مجالات جديدة من الواجهة) =====
    [Fact]
    public async Task Create_UnknownDomain_BadRequest()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("not_a_real_domain", UniqueCode("QA_BAD"), "قيمة", null, 960));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== (7) التعديل لا يمسّ Domain و Code (غير قابلين للتعديل) =====
    [Fact]
    public async Task Update_DoesNotChange_Domain_Or_Code()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var code = UniqueCode("QA_IMM");
        var created = await (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("edit_type", code, "نوع مونتاج", null, 970)))
            .ReadAsync<ExecutionTaxonomyDto>();

        var updated = await (await admin.PutAsJsonAsync($"/api/execution-taxonomy/{created!.Id}",
            new UpdateExecutionTaxonomyRequest("اسم جديد", "New", 971))).ReadAsync<ExecutionTaxonomyDto>();
        Assert.Equal("edit_type", updated!.Domain);
        Assert.Equal(code, updated.Code);
        Assert.Equal("اسم جديد", updated.NameAr);
    }

    // ===== (8) تعطيل ثم تفعيل يعكسان IsActive =====
    [Fact]
    public async Task Deactivate_ThenActivate_TogglesIsActive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("video_duration", UniqueCode("QA_VD"), "مدة اختبار", null, 980)))
            .ReadAsync<ExecutionTaxonomyDto>();

        var deactivated = await (await admin.PatchAsync($"/api/execution-taxonomy/{created!.Id}/deactivate", null))
            .ReadAsync<ExecutionTaxonomyDto>();
        Assert.False(deactivated!.IsActive);

        var activated = await (await admin.PatchAsync($"/api/execution-taxonomy/{created.Id}/activate", null))
            .ReadAsync<ExecutionTaxonomyDto>();
        Assert.True(activated!.IsActive);
    }

    // ===== (9) تعطيل قيمة معطّلة بالفعل ⇒ 409 (state_unchanged) =====
    [Fact]
    public async Task Deactivate_AlreadyInactive_Conflict()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("activity_type", UniqueCode("QA_AT"), "نشاط اختبار", null, 990)))
            .ReadAsync<ExecutionTaxonomyDto>();
        (await admin.PatchAsync($"/api/execution-taxonomy/{created!.Id}/deactivate", null)).EnsureSuccessStatusCode();

        var again = await admin.PatchAsync($"/api/execution-taxonomy/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    // ===== (10) قيمة غير موجودة ⇒ 404 =====
    [Fact]
    public async Task Get_UnknownTaxonomy_NotFound()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.GetAsync($"/api/execution-taxonomy/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== (11) الموظّف ⇒ 403 والمجهول ⇒ 401 على القراءة والكتابة =====
    [Fact]
    public async Task Employee_Forbidden_And_Anonymous_Unauthorized()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var empList = await employee.GetAsync("/api/execution-taxonomy");
        Assert.Equal(HttpStatusCode.Forbidden, empList.StatusCode);
        var empCreate = await employee.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("content_type", UniqueCode("QA_EMP"), "قيمة ممنوعة", null, 995));
        Assert.Equal(HttpStatusCode.Forbidden, empCreate.StatusCode);

        var (leader, _) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var tlList = await leader.GetAsync("/api/execution-taxonomy");
        Assert.Equal(HttpStatusCode.Forbidden, tlList.StatusCode);

        var anon = _factory.CreateClient();
        var anonList = await anon.GetAsync("/api/execution-taxonomy");
        Assert.Equal(HttpStatusCode.Unauthorized, anonList.StatusCode);
    }
}
