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
/// RC-3 Task 2 — قالب «📊 تقرير مبيعات B2B حسب الخدمة» (مُهيكَل، additive).
/// يتحقّق من: بذر القالب بالأعمدة العشرة والحقول النصية الأربعة، ظهوره لمندوب B2B (assignedOnly)
/// وعدم عرض القالب القديم «تقرير مبيعات B2B» كقالب جديد، دورة حياة الإرسال (تخزين string[][])،
/// تحقّق الجدول الرقمي/المنطقي، والتجميع حسب الخدمة (كتالوج + Legacy نصّي حرّ).
/// مندوب SALES_B2B تُفرَض دوريّته «يومية» خادميًّا فتُرسَل التقارير Daily.
/// </summary>
[Collection("Integration")]
public class B2bByServiceTemplateTests
{
    private readonly CustomWebApplicationFactory _factory;

    public B2bByServiceTemplateTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string GridInvalidCode = "submission.grid_invalid";

    // صفّ صحيح بـ10 خلايا: الخدمة، ساعات العمل، Leads، Meetings، Proposals، Negotiation، Won، Lost، Revenue، Next Step.
    // القيود مُحقَّقة: Meetings≤Leads، Proposals≤Meetings، Won≤Proposals، Lost≤Leads، WorkHours>0.
    private static string[] Row(string service, int work, int leads, int meetings, int proposals,
        int negotiation, int won, int lost, int revenue, string nextStep = "متابعة")
        => new[] { service, work.ToString(), leads.ToString(), meetings.ToString(), proposals.ToString(),
                   negotiation.ToString(), won.ToString(), lost.ToString(), revenue.ToString(), nextStep };

    private static readonly string[] ValidRow = Row("تصميم موقع إلكتروني", 12, 40, 20, 12, 6, 4, 5, 30000);

    private static async Task<ReportTemplateDetailDto> GetSeededB2bTemplateAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == B2bByServiceReportSchema.TemplateTitle));
        var detail = await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        return detail!;
    }

    private static TemplateVersionDto PublishedVersion(ReportTemplateDetailDto t)
        => t.Versions.Single(v => v.IsPublished);

    private static TemplateFieldDto GridField(ReportTemplateDetailDto t)
        => PublishedVersion(t).Fields.Single(f => f.FieldType == FieldType.TableGrid);

    private static async Task AssignTemplateToEmployeeAsync(HttpClient admin, Guid templateId, Guid employeeId)
    {
        var res = await admin.PostAsJsonAsync($"/api/report-templates/{templateId}/assignments",
            new CreateAssignmentRequest(TemplateAssignmentScope.Employee, employeeId, TemplateAssignmentKind.Include, null));
        res.EnsureSuccessStatusCode();
    }

    /// <summary>ينشئ مندوب B2B تحت مديرٍ أعلى، مسودّة يومية، يحفظ صفوف الجدول، ويعيد استجابة الإرسال.</summary>
    private async Task<(HttpResponseMessage Submit, HttpClient Ceo, HttpClient Emp, Guid EmployeeId, SubmissionDto Draft, Guid GridId)>
        SubmitB2bAsync(string date, string[][] rows)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetSeededB2bTemplateAsync(admin);
        var grid = GridField(detail);

        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp, empId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2B", ceoId);
        await AssignTemplateToEmployeeAsync(admin, detail.Id, empId);

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(detail.Id, PeriodType.Daily, date)))
            .ReadAsync<SubmissionDto>();

        var gridJson = JsonSerializer.Serialize(rows);
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(grid.Id, null, null, null, null, gridJson) }));

        var submit = await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        return (submit, ceo, emp, empId, draft, grid.Id);
    }

    private static async Task AssertGridInvalidAsync(HttpResponseMessage submit)
    {
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
        Assert.Contains(GridInvalidCode, await submit.Content.ReadAsStringAsync());
    }

    // ===== (1) القالب مبذور بجدول الأعمدة العشرة + أربعة حقول نصّية داعمة =====
    [Fact]
    public async Task SeededTemplate_Exists_WithTenColumnGrid_AndFourTextFields()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetSeededB2bTemplateAsync(admin);

        Assert.Equal(TemplateStatus.Published, detail.Status);
        var fields = PublishedVersion(detail).Fields;

        var grid = Assert.Single(fields.Where(f => f.FieldType == FieldType.TableGrid));
        Assert.True(grid.IsRequired);
        Assert.Equal(B2bByServiceReportSchema.MainTableLabel, grid.Label);
        var cols = JsonSerializer.Deserialize<GridConfig>(grid.ConfigJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Columns;
        Assert.Equal(B2bByServiceReportSchema.Columns, cols);
        // عمود «الخدمة» أوّل الأعمدة (منتقي Select في الواجهة يُغذّى من الكتالوج).
        Assert.Equal(B2bByServiceReportSchema.ColService, cols[0]);

        var textLabels = fields.Where(f => f.FieldType == FieldType.LongText).Select(f => f.Label).ToHashSet();
        Assert.Contains(B2bByServiceReportSchema.TopAchievements, textLabels);
        Assert.Contains(B2bByServiceReportSchema.TopChallenges, textLabels);
        Assert.Contains(B2bByServiceReportSchema.SupportNeeded, textLabels);
        Assert.Contains(B2bByServiceReportSchema.Notes, textLabels);
    }

    // ===== (2) مندوب B2B يرى القالب الجديد عبر assignedOnly، ولا يُعرَض القديم كقالب جديد =====
    [Fact]
    public async Task B2bRep_SeesNewTemplate_And_OldNotShownAsNew()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetSeededB2bTemplateAsync(admin);
        var (emp, empId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2B");
        await AssignTemplateToEmployeeAsync(admin, detail.Id, empId);

        var assigned = await (await emp.GetAsync(
            "/api/report-templates?status=Published&isActive=true&assignedOnly=true"))
            .ReadAsync<List<ReportTemplateDto>>();

        // القالب الجديد مُسنَد وظاهر.
        Assert.Contains(assigned!, t => t.Title == B2bByServiceReportSchema.TemplateTitle);
        // القالب القديم «تقرير مبيعات B2B» غير مُسنَد للمندوب فلا يظهر ضمن قوالبه (لا يُعرَض كقالب جديد).
        Assert.DoesNotContain(assigned!, t => t.Title == "تقرير مبيعات B2B");
    }

    // ===== (3) إرسال تقرير B2B جديد: تخزين القيم الجدولية واعتماده عبر المسار الحالي =====
    [Fact]
    public async Task Submit_B2bTable_StoresTabularValues_AndApprovesViaCurrentPath()
    {
        var (submit, ceo, emp, empId, draft, gridId) = await SubmitB2bAsync(TestCalendar.Day(0),
            new[] { ValidRow, Row("حملات إعلانية", 8, 25, 15, 10, 5, 3, 4, 18000) });

        var submitted = await submit.ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);

        var storedJson = Assert.Single(submitted.FieldValues.Where(v => v.TemplateFieldId == gridId)).ValueJson;
        var storedRows = JsonSerializer.Deserialize<string[][]>(storedJson!);
        Assert.Equal(2, storedRows!.Length);

        var approved = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);

        // القيم الجدولية باقية للقراءة بعد الإغلاق (توافق خلفي).
        var reread = await (await emp.GetAsync($"/api/submissions/{draft.Id}")).ReadAsync<SubmissionDto>();
        var afterClose = Assert.Single(reread!.FieldValues.Where(v => v.TemplateFieldId == gridId)).ValueJson;
        Assert.Equal(storedRows, JsonSerializer.Deserialize<string[][]>(afterClose!));
    }

    // ===== (4) جدول بلا صفّ فعلي ⇒ 400 =====
    [Fact]
    public async Task EmptyDataRows_Rejected_400()
    {
        var (submit, _, _, _, _, _) = await SubmitB2bAsync(TestCalendar.Day(1),
            new[] { new[] { "", "", "", "", "", "", "", "", "", "" } });
        await AssertGridInvalidAsync(submit);
    }

    // ===== (5) قيمة رقمية سالبة ⇒ 400 =====
    [Fact]
    public async Task NegativeNumber_Rejected_400()
    {
        var (submit, _, _, _, _, _) = await SubmitB2bAsync(TestCalendar.Day(2),
            new[] { Row("تصميم موقع إلكتروني", 10, -5, 3, 2, 1, 1, 1, 100) });
        await AssertGridInvalidAsync(submit);
    }

    // ===== (6) Meetings أكبر من Leads ⇒ 400 =====
    [Fact]
    public async Task MeetingsGreaterThanLeads_Rejected_400()
    {
        var (submit, _, _, _, _, _) = await SubmitB2bAsync(TestCalendar.Day(3),
            new[] { Row("تصميم موقع إلكتروني", 10, 10, 20, 5, 3, 2, 2, 100) });
        await AssertGridInvalidAsync(submit);
    }

    // ===== (7) Proposals أكبر من Meetings ⇒ 400 =====
    [Fact]
    public async Task ProposalsGreaterThanMeetings_Rejected_400()
    {
        var (submit, _, _, _, _, _) = await SubmitB2bAsync(TestCalendar.Day(4),
            new[] { Row("تصميم موقع إلكتروني", 10, 20, 8, 15, 3, 2, 2, 100) });
        await AssertGridInvalidAsync(submit);
    }

    // ===== (8) Won أكبر من Proposals ⇒ 400 =====
    [Fact]
    public async Task WonGreaterThanProposals_Rejected_400()
    {
        var (submit, _, _, _, _, _) = await SubmitB2bAsync(TestCalendar.Day(5),
            new[] { Row("تصميم موقع إلكتروني", 10, 20, 10, 5, 3, 8, 2, 100) });
        await AssertGridInvalidAsync(submit);
    }

    // ===== (9) Lost أكبر من Leads ⇒ 400 =====
    [Fact]
    public async Task LostGreaterThanLeads_Rejected_400()
    {
        var (submit, _, _, _, _, _) = await SubmitB2bAsync(TestCalendar.Day(6),
            new[] { Row("تصميم موقع إلكتروني", 10, 10, 8, 5, 3, 2, 15, 100) });
        await AssertGridInvalidAsync(submit);
    }

    // ===== (10) بيانات صحيحة تُقبَل وتُعتمَد عبر المسار الحالي =====
    [Fact]
    public async Task ValidB2bData_Accepted_AndApproves()
    {
        var (submit, ceo, _, _, draft, _) = await SubmitB2bAsync(TestCalendar.Day(7), new[] { ValidRow });
        var submitted = await submit.ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);

        var approved = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);
    }

    // ===== (11) التجميع يجمع حسب الخدمة (قيمة الكتالوج) عبر المسار الحالي =====
    [Fact]
    public async Task Aggregation_GroupsByService_CatalogValue()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetSeededB2bTemplateAsync(admin);
        var grid = GridField(detail);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp, empId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2B", ceoId);
        await AssignTemplateToEmployeeAsync(admin, detail.Id, empId);

        const string service = "تصميم موقع إلكتروني";
        var date = TestCalendar.Day(8);
        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(detail.Id, PeriodType.Daily, date))).ReadAsync<SubmissionDto>();
        var gridJson = JsonSerializer.Serialize(new[] { Row(service, 12, 40, 20, 12, 6, 4, 5, 30000) });
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(grid.Id, null, null, null, null, gridJson) }));
        (await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null)).EnsureSuccessStatusCode();
        var approved = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2b?periodType=Daily&periodKey={date}&employeeId={empId}"))
            .ReadAsync<B2bAggregationReport>();

        var row = Assert.Single(report!.Rows);
        Assert.Equal(service, row.Service);
        Assert.Equal(empId, row.EmployeeId);
        Assert.Equal(12m, row.WorkHours);
        Assert.Equal(40m, row.Leads);
        Assert.Equal(4m, row.Won);
        Assert.Equal(30000m, row.Revenue);
    }

    // ===== (12) التجميع يقرأ خدمة نصّية حرّة قديمة (Legacy) بلا كسر =====
    [Fact]
    public async Task Aggregation_LegacyFreeTextService_StillAggregated()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetSeededB2bTemplateAsync(admin);
        var grid = GridField(detail);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp, empId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2B", ceoId);
        await AssignTemplateToEmployeeAsync(admin, detail.Id, empId);

        // خدمة نصّية حرّة خارج الكتالوج (تحاكي تقريرًا قديمًا) — يجب أن تُجمَّع كالمعتاد.
        const string legacyService = "خدمة قديمة نصّية خارج الكتالوج";
        var date = TestCalendar.Day(9);
        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(detail.Id, PeriodType.Daily, date))).ReadAsync<SubmissionDto>();
        var gridJson = JsonSerializer.Serialize(new[] { Row(legacyService, 8, 32, 16, 10, 5, 3, 4, 20000) });
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(grid.Id, null, null, null, null, gridJson) }));
        (await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null)).EnsureSuccessStatusCode();
        (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).EnsureSuccessStatusCode();

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2b?periodType=Daily&periodKey={date}&employeeId={empId}"))
            .ReadAsync<B2bAggregationReport>();

        var row = Assert.Single(report!.Rows);
        Assert.Equal(legacyService, row.Service);
        Assert.Equal(3m, row.Won);
        Assert.Equal(20000m, row.Revenue);
    }

    // ===== (13) القوالب القديمة (بما فيها «تقرير مبيعات B2B» القديم) تبقى قابلة للقراءة =====
    [Fact]
    public async Task OldTemplates_RemainReadable_AndNewTemplateAdded()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();

        Assert.Contains(list!, t => t.Title == B2bByServiceReportSchema.TemplateTitle);
        var old = Assert.Single(list!.Where(t => t.Title == "تقرير مبيعات B2B"));
        var oldDetail = await (await admin.GetAsync($"/api/report-templates/{old.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        Assert.Equal(TemplateStatus.Published, oldDetail!.Status);
        Assert.NotEmpty(oldDetail.Versions.Single(v => v.IsPublished).Fields);
    }

    // ===== (14) تحقّق B2B لا يمسّ TableGrid آخر بأعمدة مختلفة (لا يُرفَض رقمه السالب) =====
    [Fact]
    public async Task OtherTableGrid_NotBrokenByB2bValidation()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

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

        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await AssignTemplateToEmployeeAsync(admin, created.Id, empId);
        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(created.Id, PeriodType.Weekly, TestCalendar.Cycle(1)))).ReadAsync<SubmissionDto>();

        var gridJson = JsonSerializer.Serialize(new[] { new[] { "مبيعات", "-999" } });
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(field!.Id, null, null, null, null, gridJson) }));

        var submit = await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
    }

    private record GridConfig(string[] Columns);
}
