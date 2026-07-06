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
/// ERDS Phase 4 — محرّك التجميع الرقمي (قراءة فقط). يجمّع قالبَي B2C حسب الدورة و B2B حسب الخدمة
/// بقراءة جدول TableGrid (string[][] في ValueJson) ومطابقة الأعمدة بالفهرس، مع قسمة آمنة ونطاق محكوم.
/// كل اختبار يُنشئ موظّفين جددًا (GUID فريد) ويصفّي النتائج على معرّفاتهم لعزل قاعدة الاختبار المشتركة المتراكمة.
/// لا يمسّ أيّ تسليم/قالب/مسار اعتماد قائم؛ يتحقّق أيضًا من بقاء دورة الحياة والقوالب القديمة سليمة.
/// </summary>
[Collection("Integration")]
public class ErdsPhase4AggregationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ErdsPhase4AggregationTests(CustomWebApplicationFactory factory) => _factory = factory;

    private record GridConfig(string[] Columns);

    private static async Task<ReportTemplateDetailDto> GetTemplateByTitleAsync(HttpClient admin, string title)
    {
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == title));
        return (await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>())!;
    }

    private static TemplateVersionDto PublishedVersion(ReportTemplateDetailDto t)
        => t.Versions.Single(v => v.IsPublished);

    private static TemplateFieldDto GridField(ReportTemplateDetailDto t)
        => PublishedVersion(t).Fields.Single(f => f.FieldType == FieldType.TableGrid);

    /// <summary>يُنشئ مسودّة، يعبّئ الجدول بالصفوف المعطاة، ثم يُرسِلها (Submitted كافٍ للتجميع).</summary>
    private static async Task SubmitGridAsync(HttpClient employee, Guid templateId, Guid gridFieldId,
        string periodKey, string[][] rows)
    {
        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey))).ReadAsync<SubmissionDto>();

        var gridJson = JsonSerializer.Serialize(rows);
        var save = await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(gridFieldId, null, null, null, null, gridJson) }));
        Assert.Equal(System.Net.HttpStatusCode.OK, save.StatusCode);

        var submitted = await (await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);
    }

    // صفّ B2C: [الدورة، ساعات العمل، Leads، Contacted، Qualified، Follow-ups، Sales، Revenue، Lost، Lost Reason]
    private static string[] B2cRow(string course, string work, string leads, string contacted, string qualified,
        string follow, string sales, string revenue, string lost, string reason = "")
        => new[] { course, work, leads, contacted, qualified, follow, sales, revenue, lost, reason };

    // صفّ B2B: [الخدمة، ساعات العمل، Leads، Meetings، Proposals، Negotiation، Won، Lost، Revenue، Next Step]
    private static string[] B2bRow(string service, string work, string leads, string meetings, string proposals,
        string negotiation, string won, string lost, string revenue, string nextStep = "")
        => new[] { service, work, leads, meetings, proposals, negotiation, won, lost, revenue, nextStep };

    private static async Task<B2cAggregationReport> AggB2cAsync(HttpClient client, string query)
        => (await (await client.GetAsync($"/api/reporting/aggregation/b2c?{query}")).ReadAsync<B2cAggregationReport>())!;

    private static async Task<B2bAggregationReport> AggB2bAsync(HttpClient client, string query)
        => (await (await client.GetAsync($"/api/reporting/aggregation/b2b?{query}")).ReadAsync<B2bAggregationReport>())!;

    // ===== (1) تجميع B2C من تقرير واحد =====
    [Fact]
    public async Task B2c_SingleReport_AggregatesOneRow()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W10",
            new[] { B2cRow("دورة التصميم", "40", "100", "80", "50", "20", "25", "8000", "10") });

        var report = await AggB2cAsync(admin, $"periodKey=2026-W10&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("دورة التصميم", row.Course);
        Assert.Equal(40m, row.WorkHours);
        Assert.Equal(100m, row.Leads);
        Assert.Equal(25m, row.Sales);
        Assert.Equal(8000m, row.Revenue);
    }

    // ===== (2) تجميع B2C لعدّة موظّفين وعدّة دورات =====
    [Fact]
    public async Task B2c_MultipleEmployeesAndCourses_GroupsCorrectly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp1, emp1Id) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (emp2, emp2Id) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp1, tpl.Id, grid.Id, "2026-W11", new[]
        {
            B2cRow("دورة A", "10", "50", "40", "20", "5", "10", "1000", "5"),
            B2cRow("دورة B", "20", "80", "60", "30", "8", "15", "3000", "8"),
        });
        await SubmitGridAsync(emp2, tpl.Id, grid.Id, "2026-W11", new[]
        {
            B2cRow("دورة A", "5", "20", "10", "8", "2", "4", "600", "2"),
        });

        var report = await AggB2cAsync(admin, "periodKey=2026-W11");

        var e1 = report.Rows.Where(r => r.EmployeeId == emp1Id).OrderBy(r => r.Course).ToList();
        Assert.Equal(2, e1.Count);
        Assert.Equal("دورة A", e1[0].Course);
        Assert.Equal("دورة B", e1[1].Course);
        Assert.Equal(3000m, e1[1].Revenue);

        var e2 = Assert.Single(report.Rows.Where(r => r.EmployeeId == emp2Id));
        Assert.Equal("دورة A", e2.Course);
        Assert.Equal(20m, e2.Leads);
    }

    // ===== (3) حساب Conversion Rate و Revenue per Hour بأمان =====
    [Fact]
    public async Task B2c_ComputesConversionAndRevenuePerHour()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W12",
            new[] { B2cRow("دورة C", "40", "100", "80", "50", "10", "25", "8000", "10") });

        var report = await AggB2cAsync(admin, $"periodKey=2026-W12&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(25.0m, row.ConversionRate);    // Sales/Leads = 25/100
        Assert.Equal(50.0m, row.QualificationRate); // Qualified/Leads = 50/100
        Assert.Equal(80.0m, row.ContactRate);       // Contacted/Leads = 80/100
        Assert.Equal(200.00m, row.RevenuePerHour);  // Revenue/WorkHours = 8000/40
    }

    // ===== (4) تجاهُل تقرير غير مطابق (قالب آخر بلا جدول مطابق) بلا فشل =====
    [Fact]
    public async Task B2c_IgnoresNonMatchingReport_WithoutFailure()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var financial = await GetTemplateByTitleAsync(admin, "التقرير المالي");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // تسليم على قالب قديم (حقول لا جدول ERDS) — لا يجب أن يظهر في تجميع B2C.
        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(financial.Id, financial.DefaultPeriodType, "2026-W13"))).ReadAsync<SubmissionDto>();
        Assert.NotNull(draft);

        var report = await AggB2cAsync(admin, $"periodKey=2026-W13&employeeId={empId}");
        Assert.Empty(report.Rows);
        Assert.Equal(0, report.RowCount);
    }

    // ===== (5) تجميع B2B من تقرير واحد =====
    [Fact]
    public async Task B2b_SingleReport_AggregatesOneRow()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bByServiceReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W14",
            new[] { B2bRow("خدمة السيو", "40", "100", "40", "20", "10", "8", "5", "16000") });

        var report = await AggB2bAsync(admin, $"periodKey=2026-W14&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("خدمة السيو", row.Service);
        Assert.Equal(40m, row.Meetings);
        Assert.Equal(8m, row.Won);
        Assert.Equal(40.0m, row.MeetingRate);  // Meetings/Leads = 40/100
        Assert.Equal(50.0m, row.ProposalRate); // Proposals/Meetings = 20/40
        Assert.Equal(40.0m, row.WinRate);      // Won/Proposals = 8/20
        Assert.Equal(400.00m, row.RevenuePerHour); // 16000/40
    }

    // ===== (6) تجميع B2B لعدّة خدمات =====
    [Fact]
    public async Task B2b_MultipleServices_GroupsCorrectly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bByServiceReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W15", new[]
        {
            B2bRow("خدمة 1", "10", "50", "20", "10", "5", "4", "3", "4000"),
            B2bRow("خدمة 2", "20", "60", "30", "15", "6", "5", "4", "9000"),
        });

        var report = await AggB2bAsync(admin, $"periodKey=2026-W15&employeeId={empId}");
        var rows = report.Rows.Where(r => r.EmployeeId == empId).OrderBy(r => r.Service).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("خدمة 1", rows[0].Service);
        Assert.Equal("خدمة 2", rows[1].Service);
        Assert.Equal(9000m, rows[1].Revenue);
    }

    // ===== (7) القسمة على صفر لا تُسبّب خطأ =====
    [Fact]
    public async Task DivisionByZero_ProducesZeroRates_NoError()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // صفّ باسم دورة فقط وكل المقاييس صفر (بلا نشاط ⇒ لا يتطلّب ساعات عمل) ⇒ كل النِسب/المعدّلات
        // تُحسَب بمقام صفر وتُرجِع 0 بلا استثناء. هذا هو جوهر اختبار القسمة على صفر.
        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W16",
            new[] { B2cRow("دورة صفرية", "0", "0", "0", "0", "0", "0", "0", "0") });

        var report = await AggB2cAsync(admin, $"periodKey=2026-W16&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(0m, row.ConversionRate);
        Assert.Equal(0m, row.QualificationRate);
        Assert.Equal(0m, row.ContactRate);
        Assert.Equal(0m, row.RevenuePerHour);
        Assert.Equal(0m, row.SalesPerHour);
        Assert.Equal(0m, row.LostRate);
    }

    // ===== (8) احترام النطاق/الصلاحيات: مستخدم خارج النطاق لا يرى بيانات غيره =====
    [Fact]
    public async Task Scope_UnrelatedUser_DoesNotSeeOthersData()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W17",
            new[] { B2cRow("دورة نطاق", "10", "50", "40", "20", "5", "10", "1000", "5") });

        // الأدمن (governance ⇒ SeesAll) يرى الصفّ.
        var asAdmin = await AggB2cAsync(admin, $"periodKey=2026-W17&employeeId={empId}");
        Assert.Single(asAdmin.Rows);

        // موظّف غير مرتبط (نطاق own) لا يرى بيانات موظّف آخر.
        var (outsider, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var asOutsider = await AggB2cAsync(outsider, $"periodKey=2026-W17&employeeId={empId}");
        Assert.Empty(asOutsider.Rows);
        Assert.Equal("self", asOutsider.ViewLevel);
    }

    // ===== (9) دورة حياة التسليم/المراجعة/الاعتماد غير متأثّرة =====
    [Fact]
    public async Task CurrentWorkflow_SubmitApprove_Unaffected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(tpl.Id, PeriodType.Weekly, "2026-W18"))).ReadAsync<SubmissionDto>();
        var gridJson = JsonSerializer.Serialize(new[]
            { B2cRow("دورة اعتماد", "40", "100", "80", "50", "10", "25", "8000", "10") });
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(grid.Id, null, null, null, null, gridJson) }));
        var submitted = await (await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);

        var approved = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);

        // التجميع يقرأ التسليم المُغلَق أيضًا (Status != Draft).
        var report = await AggB2cAsync(admin, $"periodKey=2026-W18&employeeId={empId}");
        Assert.Single(report.Rows);
    }

    // ===== (10) القوالب القديمة غير متأثّرة بمحرّك التجميع =====
    [Fact]
    public async Task OldTemplates_RemainPublishedAndReadable()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        // تشغيل التجميع لا يمسّ القوالب القديمة.
        _ = await AggB2cAsync(admin, "periodKey=2026-W19");
        _ = await AggB2bAsync(admin, "periodKey=2026-W19");

        var financial = await GetTemplateByTitleAsync(admin, "التقرير المالي");
        Assert.Equal(TemplateStatus.Published, financial.Status);
        Assert.NotEmpty(PublishedVersion(financial).Fields);
    }
}
