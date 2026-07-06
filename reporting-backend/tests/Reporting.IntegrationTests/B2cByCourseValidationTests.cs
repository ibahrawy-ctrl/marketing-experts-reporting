using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// ERDS Phase 2A — تحقّق خلايا جدول «تقرير مبيعات B2C حسب الدورة» (رقمي/غير سالب/منطقي آمن).
/// يثبت أنّ الإرسال يرفض البيانات غير المنطقية (حتى من الـAPI مباشرة) بكود submission.grid_invalid (400)،
/// وأنّ البيانات الصحيحة تُقبَل وتُعتمَد عبر المسار الحالي، مع عدم كسر أي TableGrid آخر ولا القوالب/التقارير القديمة.
/// نطاق التحقّق محصور بمطابقة الأعمدة العشرة تمامًا (SequenceEqual) فلا يمسّ جداول أخرى.
/// </summary>
[Collection("Integration")]
public class B2cByCourseValidationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public B2cByCourseValidationTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string GridInvalidCode = "submission.grid_invalid";

    // صفّ صحيح (كل القيود مُحقَّقة): Contacted≤Leads، Qualified≤Contacted، Sales≤Qualified، Lost≤Leads، WorkHours>0.
    private static readonly string[] ValidRow =
        { "دورة التسويق الرقمي", "12", "40", "30", "18", "9", "6", "18000", "3", "السعر" };

    private static async Task<ReportTemplateDetailDto> GetSeededB2cTemplateAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == B2cByCourseReportSchema.TemplateTitle));
        var detail = await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        return detail!;
    }

    private static TemplateFieldDto GridField(ReportTemplateDetailDto t)
        => t.Versions.Single(v => v.IsPublished).Fields.Single(f => f.FieldType == FieldType.TableGrid);

    /// <summary>ينشئ موظّفًا تحت مديرٍ أعلى، ثمّ مسودّة B2C ويحفظ صفوف الجدول المُمرَّرة، ويعيد استجابة الإرسال.</summary>
    private async Task<(HttpResponseMessage Submit, HttpClient Ceo, HttpClient Emp, SubmissionDto Draft, Guid GridId)>
        SubmitB2cAsync(string periodKey, string[][] rows)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetSeededB2cTemplateAsync(admin);
        var grid = GridField(detail);

        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(detail.Id, PeriodType.Weekly, periodKey)))
            .ReadAsync<SubmissionDto>();

        var gridJson = JsonSerializer.Serialize(rows);
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(grid.Id, null, null, null, null, gridJson) }));

        var submit = await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        return (submit, ceo, emp, draft, grid.Id);
    }

    private static async Task AssertGridInvalidAsync(HttpResponseMessage submit)
    {
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
        Assert.Contains(GridInvalidCode, await submit.Content.ReadAsStringAsync());
    }

    // ===== (1) بيانات B2C صحيحة ⇒ تُقبَل وتُعتمَد عبر المسار الحالي =====
    [Fact]
    public async Task ValidB2cData_Accepted_AndApprovesViaCurrentPath()
    {
        var (submit, ceo, _, draft, gridId) = await SubmitB2cAsync("2026-W41",
            new[] { ValidRow, new[] { "دورة إدارة المشاريع", "8", "25", "20", "12", "5", "4", "12000", "2", "التوقيت" } });

        var submitted = await submit.ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);

        var approved = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);
    }

    // ===== (2) جدول بلا أي صفّ يحمل بيانات فعلية ⇒ 400 (صفوف فارغة تمامًا) =====
    [Fact]
    public async Task EmptyDataRows_Rejected_400()
    {
        var (submit, _, _, _, _) = await SubmitB2cAsync("2026-W42",
            new[] { new[] { "", "", "", "", "", "", "", "", "", "" } });
        await AssertGridInvalidAsync(submit);
    }

    // ===== (3) قيمة رقمية سالبة ⇒ 400 =====
    [Fact]
    public async Task NegativeNumber_Rejected_400()
    {
        var (submit, _, _, _, _) = await SubmitB2cAsync("2026-W43",
            new[] { new[] { "دورة", "12", "-5", "3", "2", "1", "1", "100", "0", "" } });
        await AssertGridInvalidAsync(submit);
    }

    // ===== (4) Contacted أكبر من Leads ⇒ 400 =====
    [Fact]
    public async Task ContactedGreaterThanLeads_Rejected_400()
    {
        var (submit, _, _, _, _) = await SubmitB2cAsync("2026-W44",
            new[] { new[] { "دورة", "12", "10", "20", "5", "3", "2", "100", "0", "" } });
        await AssertGridInvalidAsync(submit);
    }

    // ===== (5) Qualified أكبر من Contacted ⇒ 400 =====
    [Fact]
    public async Task QualifiedGreaterThanContacted_Rejected_400()
    {
        var (submit, _, _, _, _) = await SubmitB2cAsync("2026-W45",
            new[] { new[] { "دورة", "12", "10", "8", "15", "3", "2", "100", "0", "" } });
        await AssertGridInvalidAsync(submit);
    }

    // ===== (6) Sales أكبر من Qualified ⇒ 400 =====
    [Fact]
    public async Task SalesGreaterThanQualified_Rejected_400()
    {
        var (submit, _, _, _, _) = await SubmitB2cAsync("2026-W46",
            new[] { new[] { "دورة", "12", "10", "8", "5", "3", "8", "100", "0", "" } });
        await AssertGridInvalidAsync(submit);
    }

    // ===== (7) ساعات العمل = 0 في صفّ يحتوي على نشاط ⇒ 400 =====
    [Fact]
    public async Task WorkHoursZeroWithActivity_Rejected_400()
    {
        var (submit, _, _, _, _) = await SubmitB2cAsync("2026-W47",
            new[] { new[] { "دورة", "0", "10", "8", "5", "3", "2", "100", "0", "" } });
        await AssertGridInvalidAsync(submit);
    }

    // ===== (8) القوالب القديمة غير متأثّرة (قابلة للقراءة بحقولها) =====
    [Fact]
    public async Task OldTemplates_Unaffected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();

        var old = Assert.Single(list!.Where(t => t.Title == "التقرير المالي"));
        var detail = await (await admin.GetAsync($"/api/report-templates/{old.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        Assert.Equal(TemplateStatus.Published, detail!.Status);
        Assert.NotEmpty(detail.Versions.Single(v => v.IsPublished).Fields);
    }

    // ===== (9) TableGrid آخر بأعمدة مختلفة لا يتأثّر بتحقّق B2C (يُقبَل رغم مخالفته لقيود B2C) =====
    [Fact]
    public async Task OtherTableGrid_NotBroken()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        // قالب عام جديد بجدول أعمدته مختلفة تمامًا عن جدول B2C ⇒ خارج نطاق التحقّق.
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب جدول آخر {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
                TemplateClassification.Supplementary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("جدول عام", "grid", FieldType.TableGrid, true, null,
                "{\"columns\":[\"البند\",\"القيمة\"]}")))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);

        var (emp, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(created.Id, PeriodType.Weekly, "2026-W48"))).ReadAsync<SubmissionDto>();

        // قيمة «سالبة» بأعمدة غير B2C: لو طُبِّق تحقّق B2C خطأً لرُفِضت — لكنها تُقبَل لأن الأعمدة مختلفة.
        var gridJson = JsonSerializer.Serialize(new[] { new[] { "مبيعات", "-999" } });
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(field!.Id, null, null, null, null, gridJson) }));

        var submit = await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
    }

    // ===== (10) بعد اعتماد تقرير B2C صحيح تبقى قيم الجدول قابلة للقراءة (توافق خلفي) =====
    [Fact]
    public async Task ApprovedB2cReport_GridValues_RemainReadable()
    {
        var (submit, ceo, emp, draft, gridId) = await SubmitB2cAsync("2026-W49", new[] { ValidRow });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        var approved = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);

        // إعادة قراءة التقرير بعد الإغلاق: القيم الجدولية باقية مطابقة.
        var reread = await (await emp.GetAsync($"/api/submissions/{draft.Id}")).ReadAsync<SubmissionDto>();
        var storedJson = Assert.Single(reread!.FieldValues.Where(v => v.TemplateFieldId == gridId)).ValueJson;
        Assert.Equal(new[] { ValidRow }, JsonSerializer.Deserialize<string[][]>(storedJson!));
    }
}
