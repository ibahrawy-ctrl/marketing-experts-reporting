using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// RC-3 Task 2B — التطبيع الرقمي على مستوى النظام (طبقة الخادم الدفاعية).
/// يتحقّق أنّ خانات الأرقام العربية-الهندية (١٢٣) المُرسَلة من أيّ عميل: (أ) تُخزَّن لاتينية في القاعدة
/// (لا خانة عربية تتسرّب إلى ValueJson)، (ب) يُحسبها التجميع بصورة صحيحة، (ج) تعطي نتيجة مطابقة تمامًا
/// لإرسال مكافئ بخانات لاتينية — بصرف النظر عن لغة لوحة المفاتيح. أسماء خدمات فريدة لعزل القاعدة المشتركة.
/// </summary>
[Collection("Integration")]
public class NumericNormalizationTests
{
    private readonly CustomWebApplicationFactory _factory;
    public NumericNormalizationTests(CustomWebApplicationFactory factory) => _factory = factory;

    private record GridConfig(string[] Columns);
    private static string Uniq(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static async Task<ReportTemplateDetailDto> GetTemplateByTitleAsync(HttpClient admin, string title)
    {
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == title));
        return (await (await admin.GetAsync($"/api/report-templates/{summary.Id}")).ReadAsync<ReportTemplateDetailDto>())!;
    }

    private static TemplateFieldDto GridByLabel(ReportTemplateDetailDto t, string label)
        => t.Versions.Single(v => v.IsPublished).Fields.Single(f => f.FieldType == FieldType.TableGrid && f.Label == label);

    // صفّ New Leads بخانات لاتينية: [الخدمة، ساعات العمل، New Leads، Contacted، Meetings، Proposals، Negotiation، Won، Revenue]
    private static string[] AsciiNew(string service)
        => new[] { service, "40", "100", "80", "50", "20", "10", "8", "8000" };

    // نفس القيم لكن بخانات عربية-هندية (والخدمة تبقى نصًّا كما هي).
    private static string[] ArabicNew(string service)
        => new[] { service, "٤٠", "١٠٠", "٨٠", "٥٠", "٢٠", "١٠", "٨", "٨٠٠٠" };

    private async Task<(Guid empId, Guid draftId, Guid gridId)> SubmitNewLeadsAsync(
        HttpClient admin, ReportTemplateDetailDto tpl, string periodKey, string[] newRow)
    {
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);
        var newGrid = GridByLabel(tpl, B2bBySourceReportSchema.NewLeadsTableLabel);
        var dataGrid = GridByLabel(tpl, B2bBySourceReportSchema.DataScrapingTableLabel);

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(tpl.Id, PeriodType.Weekly, periodKey))).ReadAsync<SubmissionDto>();
        var save = await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(newGrid.Id, null, null, null, null, JsonSerializer.Serialize(new[] { newRow })),
                new FieldValueInput(dataGrid.Id, null, null, null, null, JsonSerializer.Serialize(Array.Empty<string[]>())),
            }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var submitted = await (await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);
        return (empId, draft.Id, newGrid.Id);
    }

    private static bool HasArabicDigit(string s) => s.Any(c => (c >= '\u0660' && c <= '\u0669') || (c >= '\u06F0' && c <= '\u06F9'));

    // ===== (1) خانات عربية تُخزَّن لاتينية في القاعدة (لا خانة عربية في ValueJson) =====
    [Fact]
    public async Task ArabicDigits_StoredAsAscii_NoArabicDigitInDb()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        var service = Uniq("خدمة");

        var (_, draftId, gridId) = await SubmitNewLeadsAsync(admin, tpl, "2026-W20", ArabicNew(service));

        var full = await (await admin.GetAsync($"/api/submissions/{draftId}")).ReadAsync<SubmissionDto>();
        var storedJson = Assert.Single(full!.FieldValues.Where(v => v.TemplateFieldId == gridId)).ValueJson!;

        Assert.False(HasArabicDigit(storedJson)); // لا خانة عربية تسرّبت للقاعدة
        var rows = JsonSerializer.Deserialize<string[][]>(storedJson)!;
        // القيم الرقمية صارت لاتينية (الخدمة النصّية بقيت كما هي).
        Assert.Equal(new[] { service, "40", "100", "80", "50", "20", "10", "8", "8000" }, rows[0]);
    }

    // ===== (2) التجميع يحسب الخانات العربية بصورة صحيحة =====
    [Fact]
    public async Task ArabicDigits_AggregatedCorrectly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        var service = Uniq("خدمة");

        await SubmitNewLeadsAsync(admin, tpl, "2026-W21", ArabicNew(service));

        var r = (await (await admin.GetAsync(
            $"/api/reporting/aggregation/b2b/by-source?periodKey=2026-W21&service={Uri.EscapeDataString(service)}"))
            .ReadAsync<B2bSourceReport>())!;
        Assert.Equal(100m, r.NewLeadsTotals.Leads);   // ١٠٠ ⇒ 100
        Assert.Equal(8m, r.NewLeadsTotals.Won);       // ٨ ⇒ 8
        Assert.Equal(8000m, r.NewLeadsTotals.Revenue); // ٨٠٠٠ ⇒ 8000
    }

    // ===== (3) تكافؤ تام: إرسال عربي وإرسال لاتيني ⇒ نفس نتيجة التجميع =====
    [Fact]
    public async Task ArabicAndAscii_ProduceIdenticalAggregation()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        var arabicService = Uniq("خدمة-عربي");
        var asciiService = Uniq("خدمة-لاتيني");

        await SubmitNewLeadsAsync(admin, tpl, "2026-W22", ArabicNew(arabicService));
        await SubmitNewLeadsAsync(admin, tpl, "2026-W22", AsciiNew(asciiService));

        var ar = (await (await admin.GetAsync(
            $"/api/reporting/aggregation/b2b/by-source?periodKey=2026-W22&service={Uri.EscapeDataString(arabicService)}"))
            .ReadAsync<B2bSourceReport>())!;
        var asc = (await (await admin.GetAsync(
            $"/api/reporting/aggregation/b2b/by-source?periodKey=2026-W22&service={Uri.EscapeDataString(asciiService)}"))
            .ReadAsync<B2bSourceReport>())!;

        Assert.Equal(asc.NewLeadsTotals.Leads, ar.NewLeadsTotals.Leads);
        Assert.Equal(asc.NewLeadsTotals.Won, ar.NewLeadsTotals.Won);
        Assert.Equal(asc.NewLeadsTotals.Revenue, ar.NewLeadsTotals.Revenue);
        Assert.Equal(asc.NewLeadsTotals.WorkHours, ar.NewLeadsTotals.WorkHours);
    }

    // ===== (4) خانات فارسية (۱۲۳) أيضًا تُطبَّع وتُحسَب =====
    [Fact]
    public async Task PersianDigits_NormalizedAndAggregated()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        var service = Uniq("خدمة");
        // Persian digits U+06Fx for the numeric cells.
        var persianRow = new[] { service, "\u06F4\u06F0", "\u06F9\u06F9\u06F9", "\u06F8\u06F0",
            "\u06F5\u06F0", "\u06F2\u06F0", "\u06F1\u06F0", "\u06F8", "\u06F9\u06F0\u06F0\u06F0" };

        await SubmitNewLeadsAsync(admin, tpl, "2026-W23", persianRow);

        var r = (await (await admin.GetAsync(
            $"/api/reporting/aggregation/b2b/by-source?periodKey=2026-W23&service={Uri.EscapeDataString(service)}"))
            .ReadAsync<B2bSourceReport>())!;
        Assert.Equal(999m, r.NewLeadsTotals.Leads); // ۹۹۹ ⇒ 999
    }
}
