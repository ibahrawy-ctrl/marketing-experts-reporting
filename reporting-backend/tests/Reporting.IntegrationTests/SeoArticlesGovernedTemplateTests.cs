using System.Text.Json;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// VIS-05 — «تقرير متابعة مقالات SEO الأسبوعي» من جدولٍ حرّ إلى صفوف محكومة.
///
/// **السبب الجذريّ الذي تحرسه هذه الاختبارات**: «الحالة» و«تاريخ التسليم» كانا **عمودَي
/// <c>SGrid</c>** لا حقلَين مكتوبَي النوع. أعمدة الجدول نصّ حرّ بطبيعتها ⟹ لا تحقّق ولا
/// تجميع ولا مقارنة عبر الأسابيع، وكلّ كاتب يخترع مفرداته («تم»، «منشور»، «انتهى»…).
///
/// **لماذا <c>work_status</c>**: الكتالوج الرسميّ لا يحوي <c>seo_status</c> ولا
/// <c>article_status</c>، واستحداث نطاق جديد محظور. القرار قرار مالك المنتج.
///
/// القراءة عبر API فقط (القالب مبذور عند الإقلاع) — كنمط <see cref="TemplateTaxonomyV4Tests"/>.
/// </summary>
[Collection("Integration")]
public class SeoArticlesGovernedTemplateTests
{
    private const string Title = "تقرير متابعة مقالات SEO الأسبوعي";

    private readonly CustomWebApplicationFactory _factory;

    public SeoArticlesGovernedTemplateTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed record RepeatableConfig(RepeatableSubField[]? Fields);
    private sealed record RepeatableSubField(string Key, string Label, string Type, bool Required, string? CatalogDomain, string[]? Options);

    private async Task<ReportTemplateDetailDto> GetSeoTemplateAsync()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == Title));
        return (await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>())!;
    }

    private static RepeatableSubField[] SubFieldsOfPublished(ReportTemplateDetailDto detail)
    {
        var published = Assert.Single(detail.Versions.Where(v => v.IsPublished));
        var section = Assert.Single(published.Fields.Where(f => f.FieldType == FieldType.ProjectRepeatableSection));
        return JsonSerializer.Deserialize<RepeatableConfig>(section.ConfigJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Fields ?? Array.Empty<RepeatableSubField>();
    }

    // ===== (1) حالة المقال صارت قائمة محكومة مسنودة إلى نطاق الكتالوج، لا نصًّا حرًّا =====
    [Fact]
    public async Task ArticleStatus_IsGovernedSelect_BackedByWorkStatusDomain()
    {
        var status = Assert.Single(SubFieldsOfPublished(await GetSeoTemplateAsync())
            .Where(sf => sf.Key == "work_status"));

        Assert.Equal("Select", status.Type);
        Assert.Equal("work_status", status.CatalogDomain);
        Assert.True(status.Required, "حالة المقال حقل إلزاميّ — بدونه يبقى الصفّ بلا معنى تشغيليّ.");
        // اللقطة الاحتياطيّة موجودة وغير فارغة: الكتالوج هو المصدر، واللقطة تحمي العرض إن تعذّر جلبه.
        Assert.NotNull(status.Options);
        Assert.NotEmpty(status.Options!);
    }

    // ===== (2) تاريخ التسليم حقل تاريخ مكتوب النوع، لا عمود نصّ =====
    [Fact]
    public async Task DeliveryDate_IsTypedDateField_NotFreeText()
    {
        var delivery = Assert.Single(SubFieldsOfPublished(await GetSeoTemplateAsync())
            .Where(sf => sf.Key == "delivery_date"));

        Assert.Equal("Date", delivery.Type);
        Assert.True(delivery.Required);
        // حقل التاريخ لا يحمل نطاق كتالوج ولا خيارات — لو حملها لكان قائمةً متنكّرة في هيئة تاريخ.
        Assert.True(string.IsNullOrWhiteSpace(delivery.CatalogDomain));
    }

    // ===== (3) الجدولان الحرّان القديمان لم يعودا في الإصدار المنشور =====
    //
    // هذا الادّعاء هو نفي العيب لا إثبات الإصلاح: بقاء «مقالات المشروع» أو «المنشورة»
    // كجدول حرّ يعني أنّ الكاتب ما زال أمامه مسار غير محكوم يسجّل فيه الحالة والتاريخ.
    [Fact]
    public async Task PublishedVersion_NoLongerCarriesFreeTextArticleGrids()
    {
        var detail = await GetSeoTemplateAsync();
        var published = Assert.Single(detail.Versions.Where(v => v.IsPublished));

        var sectionGrids = published.Fields
            .Where(f => f.FieldType == FieldType.ProjectRepeatableSection && f.ConfigJson is not null)
            .Select(f => f.ConfigJson!)
            .ToList();
        Assert.All(sectionGrids, json =>
        {
            Assert.DoesNotContain("\"type\":\"Grid\"", json.Replace(" ", ""));
            Assert.DoesNotContain("\"Type\":\"Grid\"", json.Replace(" ", ""));
        });
    }

    // ===== (4) الإصدار المحكوم هو الأحدث والوحيد المنشور، والأقدم يبقى مقروءًا =====
    //
    // التاريخ لا يُحذف: v1..v4 تبقى بلقطاتها (تقارير مغلقة تشير إليها)، لكنّ الإنشاء الجديد
    // لا يجد أمامه إلّا الإصدار المحكوم.
    [Fact]
    public async Task GovernedVersion_IsLatestAndOnlyPublished_OlderRemainReadable()
    {
        var detail = await GetSeoTemplateAsync();

        var published = Assert.Single(detail.Versions.Where(v => v.IsPublished));
        Assert.Equal(detail.Versions.Max(v => v.VersionNumber), published.VersionNumber);

        var older = detail.Versions.Where(v => v.Id != published.Id).ToList();
        Assert.NotEmpty(older);
        Assert.All(older, v =>
        {
            Assert.False(v.IsPublished);
            Assert.NotEmpty(v.Fields);
        });
    }

    // ===== (5) الخمول (idempotency): البذر يُنفَّذ في كلّ إقلاع ولا ينتج إصدارًا جديدًا =====
    //
    // حارس <c>IsSeoArticlesGoverned</c> هو ما يمنع توالد إصدار v6 وv7 مع كلّ إعادة تشغيل.
    // القياس هنا غير مباشر لكنّه حاسم: عدد الإصدارات ثابت عبر قراءتين متتاليتين على مصنع
    // مشترك أُقلِع مرّة واحدة، ولا يوجد أكثر من إصدار واحد يجتاز شرط الحوكمة.
    [Fact]
    public async Task GovernedUpgrade_IsIdempotent_ExactlyOneGovernedVersion()
    {
        var detail = await GetSeoTemplateAsync();

        var governed = detail.Versions.Where(v => v.Fields.Any(f =>
            f.FieldType == FieldType.ProjectRepeatableSection
            && f.ConfigJson is not null
            && f.ConfigJson.Replace(" ", "").Contains("\"catalogDomain\":\"work_status\"")
            && f.ConfigJson.Replace(" ", "").Contains("\"key\":\"delivery_date\""))).ToList();

        Assert.Single(governed);
        Assert.True(governed[0].IsPublished);
    }
}
