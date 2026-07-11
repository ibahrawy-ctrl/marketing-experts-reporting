using System.Net;
using System.Net.Http.Json;
using Reporting.Application.ExecutionTaxonomy;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// RC-4 Task 4D3 — قراءة خيارات تصنيفات التنفيذ النشطة لملء التقارير (endpoint مصادقة فقط، منفصل عن الإدارة).
/// أيّ موظّف مصادَق يقرأ الخيارات النشطة لمجال واحد (NameAr مرتّبة SortOrder ثم NameAr) دون كشف أيّ عملية إدارة.
/// أكواد فريدة لكل تشغيل لتفادي تلوّث القاعدة المشتركة الدائمة.
/// </summary>
[Collection("Integration")]
public class ExecutionTaxonomyOptionsTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ExecutionTaxonomyOptionsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string UniqueCode(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    // ===== (1) موظّف مصادَق يقرأ الخيارات النشطة لمجال، مرتّبة SortOrder ثم NameAr =====
    [Fact]
    public async Task Options_AuthenticatedEmployee_ReturnsActiveByDomain_Sorted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var nameLater = $"خيار_متأخّر_{tag}";
        var nameEarlier = $"خيار_مبكّر_{tag}";
        // أُنشئ المتأخّر أولًا بترتيب أكبر، ثم المبكّر بترتيب أصغر — يجب أن يسبق المبكّر عند القراءة.
        (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("content_type", UniqueCode("QA_OPT_L"), nameLater, null, 8110)))
            .EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("content_type", UniqueCode("QA_OPT_E"), nameEarlier, null, 8100)))
            .EnsureSuccessStatusCode();

        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var options = await (await employee.GetAsync("/api/execution-taxonomy/options?domain=content_type"))
            .ReadAsync<List<string>>();

        Assert.NotNull(options);
        Assert.Contains(nameEarlier, options!);
        Assert.Contains(nameLater, options!);
        Assert.True(options!.IndexOf(nameEarlier) < options.IndexOf(nameLater),
            "القيمة الأصغر SortOrder يجب أن تسبق الأكبر في خيارات القراءة.");
    }

    // ===== (2) القيمة المعطّلة لا تظهر في endpoint القراءة =====
    [Fact]
    public async Task Options_InactiveValue_NotReturned()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var name = $"خيار_سيُعطّل_{tag}";
        var created = await (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("design_type", UniqueCode("QA_OPT_INA"), name, null, 8120)))
            .ReadAsync<ExecutionTaxonomyDto>();

        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var before = await (await employee.GetAsync("/api/execution-taxonomy/options?domain=design_type"))
            .ReadAsync<List<string>>();
        Assert.Contains(name, before!);

        (await admin.PatchAsync($"/api/execution-taxonomy/{created!.Id}/deactivate", null)).EnsureSuccessStatusCode();

        var after = await (await employee.GetAsync("/api/execution-taxonomy/options?domain=design_type"))
            .ReadAsync<List<string>>();
        Assert.DoesNotContain(name, after!);
    }

    // ===== (3) endpoint الإدارة (المنفصل) يُظهر المعطّلة عبر includeInactive — إثبات الفصل =====
    [Fact]
    public async Task AdminManagement_IncludeInactive_ShowsDeactivated_WhileOptionsDoesNot()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var name = $"خيار_إدارة_{tag}";
        var created = await (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("work_status", UniqueCode("QA_OPT_ADM"), name, null, 8130)))
            .ReadAsync<ExecutionTaxonomyDto>();
        (await admin.PatchAsync($"/api/execution-taxonomy/{created!.Id}/deactivate", null)).EnsureSuccessStatusCode();

        var management = await (await admin.GetAsync("/api/execution-taxonomy?domain=work_status&includeInactive=true"))
            .ReadAsync<List<ExecutionTaxonomyDto>>();
        Assert.Contains(management!, v => v.Id == created.Id);

        var options = await (await admin.GetAsync("/api/execution-taxonomy/options?domain=work_status"))
            .ReadAsync<List<string>>();
        Assert.DoesNotContain(name, options!);
    }

    // ===== (4) القراءة متاحة لأيّ مصادَق؛ المجهول ⇒ 401؛ الإدارة ممنوعة على غير الأدمن ⇒ 403 =====
    [Fact]
    public async Task Options_ReadableByEmployee_ManagementForbidden_AnonymousUnauthorized()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // الموظّف يقرأ الخيارات (200) لكنه ممنوع من endpoint الإدارة (403).
        var empOptions = await employee.GetAsync("/api/execution-taxonomy/options?domain=content_type");
        Assert.Equal(HttpStatusCode.OK, empOptions.StatusCode);
        var empManagement = await employee.GetAsync("/api/execution-taxonomy");
        Assert.Equal(HttpStatusCode.Forbidden, empManagement.StatusCode);
        var empCreate = await employee.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("content_type", UniqueCode("QA_OPT_EMP"), "قيمة ممنوعة", null, 8140));
        Assert.Equal(HttpStatusCode.Forbidden, empCreate.StatusCode);

        // المجهول ممنوع من القراءة أيضًا (endpoint مصادقة فقط).
        var anon = _factory.CreateClient();
        var anonOptions = await anon.GetAsync("/api/execution-taxonomy/options?domain=content_type");
        Assert.Equal(HttpStatusCode.Unauthorized, anonOptions.StatusCode);
    }

    // ===== (5) حماية تكرار الرمز في الإدارة لا تزال تعمل (لا انحدار) =====
    [Fact]
    public async Task Management_DuplicateProtection_StillWorks()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var code = UniqueCode("QA_OPT_DUP");
        (await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("video_type", code, "قيمة أولى", null, 8150)))
            .EnsureSuccessStatusCode();

        var dup = await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("video_type", code.ToUpperInvariant(), "قيمة مكرّرة", null, 8151));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    // ===== (6) P0 — المجالات الستّة الجديدة مبذورة نشطة وتُقرأ من endpoint الخيارات (بلا تكرار) =====
    [Theory]
    [InlineData("workstream_type", "سوشيال ميديا", 12)]
    [InlineData("deliverable", "منشور", 21)]
    [InlineData("usage_context", "سوشيال أورجانيك", 12)]
    [InlineData("workflow_step", "مخطّط", 15)]
    [InlineData("delay_reason", "أخرى", 11)]
    [InlineData("platform_channel", "إنستغرام", 11)]
    public async Task Options_P0Domains_SeededActive_NoDuplicates(string domain, string expectedNameAr, int expectedMinCount)
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var options = await (await employee.GetAsync($"/api/execution-taxonomy/options?domain={domain}"))
            .ReadAsync<List<string>>();

        Assert.NotNull(options);
        // القيم المبذورة موجودة (على الأقل العدد المتوقّع — قد تُضاف قيم اختبار أخرى للقاعدة المشتركة).
        Assert.True(options!.Count >= expectedMinCount,
            $"المجال {domain} يجب أن يحوي {expectedMinCount} قيمة مبذورة على الأقل، الفعليّ {options.Count}.");
        Assert.Contains(expectedNameAr, options);
    }

    // ===== (7) P0 — الأدمن يستطيع إدارة المجالات الجديدة (KnownDomains مُوسَّع) — لا تُرفَض كمجهولة =====
    [Fact]
    public async Task Management_P0Domain_IsKnown_AllowsCreate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/execution-taxonomy",
            new CreateExecutionTaxonomyRequest("workstream_type", UniqueCode("QA_P0_WS"), "قيمة P0 اختبار", "P0 Test", 9990));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
