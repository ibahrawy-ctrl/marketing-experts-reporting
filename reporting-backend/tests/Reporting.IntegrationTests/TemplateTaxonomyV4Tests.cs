using System.Text.Json;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// RC-4 Task 4D3 — التحقّق من ترقية قوالب التنفيذ الأربعة إلى Taxonomy v4 (خيارات Select ديناميكية عبر catalogDomain).
/// v4 هو الإصدار المنشور الوحيد؛ الإصدارات الأقدم (v1/v2/v3) تبقى موجودة/مقروءة كلقطات؛ حقول Select تحمل catalogDomain
/// بجانب options كلقطة احتياطيّة (fallback). الاختبارات تقرأ عبر API فقط (القوالب مبذورة عند الإقلاع).
/// </summary>
[Collection("Integration")]
public class TemplateTaxonomyV4Tests
{
    private readonly CustomWebApplicationFactory _factory;

    public TemplateTaxonomyV4Tests(CustomWebApplicationFactory factory) => _factory = factory;

    private static readonly string[] V4Templates =
    {
        "تقرير كاتب المحتوى الأسبوعي",
        "تقرير فريق التصميم",
        "تقرير فريق الفيديو",
        "تقرير المديرشن الأسبوعي",
    };

    public static IEnumerable<object[]> V4TemplateTitles() => V4Templates.Select(t => new object[] { t });

    private sealed record RepeatableConfig(RepeatableSubField[]? Fields);
    private sealed record RepeatableSubField(string Key, string Label, string Type, string? CatalogDomain, string[]? Options);

    private async Task<ReportTemplateDetailDto> GetTemplateByTitleAsync(HttpClient admin, string title)
    {
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == title));
        return (await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>())!;
    }

    private static TemplateFieldDto ProjectSectionField(TemplateVersionDto version)
        => Assert.Single(version.Fields.Where(f => f.FieldType == FieldType.ProjectRepeatableSection));

    private static RepeatableSubField[] SubFields(TemplateFieldDto field)
        => JsonSerializer.Deserialize<RepeatableConfig>(field.ConfigJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Fields ?? Array.Empty<RepeatableSubField>();

    // ===== (1) النسخة المنشورة لكل قالب من الأربعة تحوي حقل Select واحدًا على الأقل بـ catalogDomain غير فارغ =====
    [Theory]
    [MemberData(nameof(V4TemplateTitles))]
    public async Task V4_PublishedVersion_HasSelectWithCatalogDomain(string title)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetTemplateByTitleAsync(admin, title);
        var published = Assert.Single(detail.Versions.Where(v => v.IsPublished));

        var section = ProjectSectionField(published);
        var selects = SubFields(section).Where(sf => sf.Type == "Select").ToList();
        Assert.NotEmpty(selects);
        Assert.All(selects, sf => Assert.False(string.IsNullOrWhiteSpace(sf.CatalogDomain),
            $"حقل Select «{sf.Key}» يجب أن يحمل catalogDomain في v4."));
    }

    // ===== (2) v4 هو الإصدار المنشور الوحيد وهو الأحدث (أعلى VersionNumber) =====
    [Theory]
    [MemberData(nameof(V4TemplateTitles))]
    public async Task V4_IsLatest_And_OnlyPublishedVersion(string title)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetTemplateByTitleAsync(admin, title);

        var published = Assert.Single(detail.Versions.Where(v => v.IsPublished));
        var maxNumber = detail.Versions.Max(v => v.VersionNumber);
        Assert.Equal(maxNumber, published.VersionNumber);

        // v4 يحمل catalogDomain — تأكيد أنّ الإصدار المنشور هو v4 لا لقطة قديمة.
        var section = ProjectSectionField(published);
        Assert.Contains(SubFields(section), sf => !string.IsNullOrWhiteSpace(sf.CatalogDomain));
    }

    // ===== (3) الإصدارات الأقدم (v1/v2/v3) تبقى موجودة ومقروءة كلقطات (توافق خلفي) =====
    [Theory]
    [MemberData(nameof(V4TemplateTitles))]
    public async Task OlderVersions_RemainReadable_AsSnapshots(string title)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetTemplateByTitleAsync(admin, title);

        // بعد v2 (Project-First) + v3 (Taxonomy) + v4 (Dynamic) يوجد أكثر من إصدار واحد.
        Assert.True(detail.Versions.Count >= 2, "يجب بقاء إصدارات أقدم إلى جانب v4.");

        var published = detail.Versions.Single(v => v.IsPublished);
        var older = detail.Versions.Where(v => v.Id != published.Id).ToList();
        Assert.NotEmpty(older);
        // كل إصدار أقدم غير منشور لكنه ما زال يحمل حقوله (مقروء).
        Assert.All(older, v =>
        {
            Assert.False(v.IsPublished);
            Assert.NotEmpty(v.Fields);
        });
    }

    // ===== (4) حقول Select في v4 تحمل catalogDomain المطابق لمفتاح الحقل (key == domain في قوالب التنفيذ) =====
    [Theory]
    [MemberData(nameof(V4TemplateTitles))]
    public async Task V4_SelectFields_CatalogDomain_MatchesFieldKey(string title)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetTemplateByTitleAsync(admin, title);
        var published = detail.Versions.Single(v => v.IsPublished);

        var selects = SubFields(ProjectSectionField(published)).Where(sf => sf.Type == "Select").ToList();
        Assert.NotEmpty(selects);
        Assert.All(selects, sf => Assert.Equal(sf.Key, sf.CatalogDomain));
    }

    // ===== (5) v4 لا يعتمد على اللقطة إلا كاحتياط: options موجودة fallback بجانب catalogDomain الديناميكيّ =====
    [Theory]
    [MemberData(nameof(V4TemplateTitles))]
    public async Task V4_KeepsOptions_AsFallbackAlongsideCatalogDomain(string title)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetTemplateByTitleAsync(admin, title);
        var published = detail.Versions.Single(v => v.IsPublished);

        var selects = SubFields(ProjectSectionField(published)).Where(sf => sf.Type == "Select").ToList();
        Assert.NotEmpty(selects);
        // المصدر الأساسيّ ديناميكيّ (catalogDomain موجود)، واللقطة تبقى موجودة كحقل fallback (قد تكون فارغة إن خلا الكتالوج).
        Assert.All(selects, sf =>
        {
            Assert.False(string.IsNullOrWhiteSpace(sf.CatalogDomain));
            Assert.NotNull(sf.Options); // حقل اللقطة الاحتياطيّة موجود (وإن كان مصفوفة فارغة).
        });
    }
}
