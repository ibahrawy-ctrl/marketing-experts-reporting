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
/// Phase 7 — قالب «تقرير مبيعات B2C — بيانات جديدة/قديمة» (جدولان: New Leads + Old CRM Data) + تجميع مفصول.
/// يتحقّق من: بذر القالب بجدولين مطلوبين، دورة حياة الإرسال لكلا الجدولين، تحقّق الأعمدة الخاص بالجدولين،
/// وأنّ تجميع New/Old يفصل البيانات الجديدة عن القديمة ويقرأ القالب القديم أحادي الجدول ضمن دلو New (Legacy).
/// أسماء دورات فريدة لكل تشغيل لعزل قاعدة الاختبار المشتركة المتراكمة.
/// </summary>
[Collection("Integration")]
public class B2cNewOldTests
{
    private readonly CustomWebApplicationFactory _factory;

    public B2cNewOldTests(CustomWebApplicationFactory factory) => _factory = factory;

    private record GridConfig(string[] Columns);

    private static string Uniq(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static async Task<ReportTemplateDetailDto> GetTemplateByTitleAsync(HttpClient admin, string title)
    {
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == title));
        return (await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>())!;
    }

    private static TemplateVersionDto PublishedVersion(ReportTemplateDetailDto t)
        => t.Versions.Single(v => v.IsPublished);

    private static TemplateFieldDto GridByLabel(ReportTemplateDetailDto t, string label)
        => PublishedVersion(t).Fields.Single(f => f.FieldType == FieldType.TableGrid && f.Label == label);

    // صفّ New Leads: [الدورة، ساعات العمل، New Leads، Contacted، Qualified، Follow-ups، Sales، Revenue، Lost، Lost Reason]
    private static string[] NewRow(string course, string work, string leads, string contacted, string qualified,
        string follow, string sales, string revenue, string lost, string reason = "")
        => new[] { course, work, leads, contacted, qualified, follow, sales, revenue, lost, reason };

    // صفّ Old CRM: [الدورة، ساعات العمل، Old Leads Worked، Contacted، Requalified، Follow-ups، Sales، Revenue، Lost، Lost Reason]
    private static string[] OldRow(string course, string work, string leadsWorked, string contacted, string requalified,
        string follow, string sales, string revenue, string lost, string reason = "")
        => new[] { course, work, leadsWorked, contacted, requalified, follow, sales, revenue, lost, reason };

    /// <summary>يُنشئ مسودّة على القالب الجديد، يعبّئ الجدولين، ثم يُرسِل (Submitted كافٍ للتجميع).</summary>
    private async Task<HttpClient> SubmitNewOldAsync(Guid empManagerId, ReportTemplateDetailDto tpl,
        string periodKey, string[][] newRows, string[][] oldRows)
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, "Employee", empManagerId);
        var newGrid = GridByLabel(tpl, B2cNewOldReportSchema.NewLeadsTableLabel);
        var oldGrid = GridByLabel(tpl, B2cNewOldReportSchema.OldCrmTableLabel);

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(tpl.Id, PeriodType.Weekly, periodKey))).ReadAsync<SubmissionDto>();

        var save = await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(newGrid.Id, null, null, null, null, JsonSerializer.Serialize(newRows)),
                new FieldValueInput(oldGrid.Id, null, null, null, null, JsonSerializer.Serialize(oldRows)),
            }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var submitted = await (await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);
        return emp;
    }

    private static async Task<B2cNewOldReport> AggNewOldAsync(HttpClient client, string query)
        => (await (await client.GetAsync($"/api/reporting/aggregation/b2c/new-old?{query}")).ReadAsync<B2cNewOldReport>())!;

    // ===== (1) القالب الجديد مبذور بجدولين مطلوبين + الحقول النصية =====
    [Fact]
    public async Task SeededTemplate_HasTwoRequiredGrids_WithExpectedColumns()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2cNewOldReportSchema.TemplateTitle);
        Assert.Equal(TemplateStatus.Published, tpl.Status);

        var grids = PublishedVersion(tpl).Fields.Where(f => f.FieldType == FieldType.TableGrid).ToList();
        Assert.Equal(2, grids.Count);

        var newGrid = GridByLabel(tpl, B2cNewOldReportSchema.NewLeadsTableLabel);
        var oldGrid = GridByLabel(tpl, B2cNewOldReportSchema.OldCrmTableLabel);
        Assert.True(newGrid.IsRequired);
        Assert.True(oldGrid.IsRequired);

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        Assert.Equal(B2cNewOldReportSchema.NewLeadsColumns,
            JsonSerializer.Deserialize<GridConfig>(newGrid.ConfigJson!, opts)!.Columns);
        Assert.Equal(B2cNewOldReportSchema.OldCrmColumns,
            JsonSerializer.Deserialize<GridConfig>(oldGrid.ConfigJson!, opts)!.Columns);

        // الحقول النصية الداعمة الأربعة موجودة.
        var textLabels = PublishedVersion(tpl).Fields.Where(f => f.FieldType == FieldType.LongText)
            .Select(f => f.Label).ToHashSet();
        Assert.Contains(B2cNewOldReportSchema.TopAchievements, textLabels);
        Assert.Contains(B2cNewOldReportSchema.TopChallenges, textLabels);
        Assert.Contains(B2cNewOldReportSchema.SupportNeeded, textLabels);
        Assert.Contains(B2cNewOldReportSchema.ExceptionalNotes, textLabels);
    }

    // ===== (2) إرسال جدولَي New Leads + Old CRM يُخزّن القيم كما هي =====
    [Fact]
    public async Task Submit_BothTables_StoresTabularValues()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2cNewOldReportSchema.TemplateTitle);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var course = Uniq("دورة");

        var newRows = new[] { NewRow(course, "40", "100", "80", "50", "10", "25", "8000", "10", "السعر") };
        var oldRows = new[] { OldRow(course, "20", "60", "50", "30", "8", "12", "4000", "6", "التوقيت") };

        var emp = await SubmitNewOldAsync(ceoId, tpl, TestCalendar.Cycle(1), newRows, oldRows);

        // القيم مُخزَّنة ومُعادة كما هي لكلا الجدولين.
        var newGrid = GridByLabel(tpl, B2cNewOldReportSchema.NewLeadsTableLabel);
        var oldGrid = GridByLabel(tpl, B2cNewOldReportSchema.OldCrmTableLabel);
        var list = await (await emp.GetAsync($"/api/submissions?period={TestCalendar.Cycle(1)}")).ReadAsync<List<SubmissionDto>>();
        var sub = Assert.Single(list!.Where(s => s.TemplateTitle == tpl.Title));
        var full = await (await emp.GetAsync($"/api/submissions/{sub.Id}")).ReadAsync<SubmissionDto>();
        var storedNew = Assert.Single(full!.FieldValues.Where(v => v.TemplateFieldId == newGrid.Id)).ValueJson;
        var storedOld = Assert.Single(full.FieldValues.Where(v => v.TemplateFieldId == oldGrid.Id)).ValueJson;
        Assert.Equal(newRows, JsonSerializer.Deserialize<string[][]>(storedNew!));
        Assert.Equal(oldRows, JsonSerializer.Deserialize<string[][]>(storedOld!));
    }

    // ===== (3) عمود Old CRM المخالف للسلسلة (Requalified>Contacted) ⇒ 400 grid_invalid =====
    [Fact]
    public async Task Submit_InvalidOldChain_Rejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2cNewOldReportSchema.TemplateTitle);
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var newGrid = GridByLabel(tpl, B2cNewOldReportSchema.NewLeadsTableLabel);
        var oldGrid = GridByLabel(tpl, B2cNewOldReportSchema.OldCrmTableLabel);
        var course = Uniq("دورة");

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(tpl.Id, PeriodType.Weekly, TestCalendar.Cycle(2)))).ReadAsync<SubmissionDto>();

        // Old CRM: Requalified(40) > Contacted(30) ⇒ سلسلة مخالفة.
        var newRows = new[] { NewRow(course, "40", "100", "80", "50", "10", "25", "8000", "10") };
        var badOld = new[] { OldRow(course, "20", "60", "30", "40", "8", "12", "4000", "6") };
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(newGrid.Id, null, null, null, null, JsonSerializer.Serialize(newRows)),
                new FieldValueInput(oldGrid.Id, null, null, null, null, JsonSerializer.Serialize(badOld)),
            }));

        var submit = await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
    }

    // ===== (4) تجميع New/Old يفصل الدلوين بمؤشرات مستقلّة =====
    [Fact]
    public async Task Aggregation_DistinguishesNewFromOld()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2cNewOldReportSchema.TemplateTitle);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var course = Uniq("دورة");

        var newRows = new[] { NewRow(course, "40", "100", "80", "50", "10", "25", "8000", "10") };
        var oldRows = new[] { OldRow(course, "20", "60", "50", "30", "8", "12", "4000", "6") };
        await SubmitNewOldAsync(ceoId, tpl, TestCalendar.Cycle(3), newRows, oldRows);

        var report = await AggNewOldAsync(admin, $"periodKey={TestCalendar.Cycle(3)}&course={Uri.EscapeDataString(course)}");
        var row = Assert.Single(report.Courses);
        Assert.Equal(course, row.Course);

        // دلو New: من جدول New Leads.
        Assert.Equal(100m, row.New.Leads);
        Assert.Equal(25m, row.New.Sales);
        Assert.Equal(8000m, row.New.Revenue);
        Assert.Equal(40m, row.New.WorkHours);
        Assert.Equal(25.0m, row.New.ConversionRate);   // Sales/NewLeads = 25/100
        Assert.Equal(200.00m, row.New.RevenuePerHour); // 8000/40

        // دلو Old: من جدول Old CRM Data.
        Assert.Equal(60m, row.Old.Leads);
        Assert.Equal(12m, row.Old.Sales);
        Assert.Equal(4000m, row.Old.Revenue);
        Assert.Equal(20m, row.Old.WorkHours);
        Assert.Equal(20.0m, row.Old.ConversionRate);   // Recovery = Sales/OldLeadsWorked = 12/60
        Assert.Equal(200.00m, row.Old.RevenuePerHour); // 4000/20

        // الإجماليات تعكس الدلوين.
        Assert.Equal(100m, report.NewTotals.Leads);
        Assert.Equal(60m, report.OldTotals.Leads);
    }

    // ===== (5) القالب القديم أحادي الجدول (Legacy) يُغذّي دلو New ولا يظهر في دلو Old =====
    [Fact]
    public async Task Aggregation_LegacyTemplate_FeedsNewBucket()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var legacy = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var legacyGrid = PublishedVersion(legacy).Fields.Single(f => f.FieldType == FieldType.TableGrid);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var course = Uniq("دورة قديمة");

        // تقرير على القالب القديم أحادي الجدول (Leads=70، Sales=14).
        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(legacy.Id, PeriodType.Weekly, TestCalendar.Cycle(4)))).ReadAsync<SubmissionDto>();
        var legacyRows = new[] { new[] { course, "35", "70", "60", "40", "9", "14", "7000", "7", "السعر" } };
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(legacyGrid.Id, null, null, null, null, JsonSerializer.Serialize(legacyRows)) }));
        var submitted = await (await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);

        var report = await AggNewOldAsync(admin,
            $"periodKey={TestCalendar.Cycle(4)}&employeeId={empId}&course={Uri.EscapeDataString(course)}");
        var row = Assert.Single(report.Courses);
        // القالب القديم يُعامَل New مؤقّتًا (لا مصدر Old فيه).
        Assert.Equal(70m, row.New.Leads);
        Assert.Equal(14m, row.New.Sales);
        Assert.Equal(0m, row.Old.Leads);
        Assert.Equal(0m, row.Old.Sales);
    }
}
