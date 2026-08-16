using System.Globalization;
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
/// تجميع المبيعات (B2C) — تقارير المبيعات تُخزَّن دائمًا «يوميًّا» (ReportCadencePolicy)،
/// فعند اختيار فترة أسبوعية/شهرية/ربع سنوية يجب أن يجمع المحرّك كل التقارير اليومية داخل نطاق تلك الفترة
/// تحت مفتاح واحد (بدل البحث عن تطابق نوع/مفتاح تامّ الذي لا يُرجِع شيئًا). كما يجب أن يبقى الوضع اليومي كما هو.
/// النطاق معزول عبر فلتر employeeId على موظّف اختبار فريد (لتفادي بيانات القاعدة المشتركة).
/// </summary>
[Collection("Integration")]
public class SalesAggregationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public SalesAggregationTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<(Guid TemplateId, Guid GridId)> GetSeededB2cTemplateAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == B2cByCourseReportSchema.TemplateTitle));
        var detail = await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        var version = detail!.Versions.Single(v => v.IsPublished);
        var grid = Assert.Single(version.Fields.Where(f => f.FieldType == FieldType.TableGrid));
        return (detail.Id, grid.Id);
    }

    // يُسنِد القالب صراحةً للموظّف (Employee Include). ضروري لأن قاعدة الاختبار المشتركة تحوي قوالب
    // مرتبطة بمسمّى SALES_B2C، فيُسقِط محرّكُ الإسناد القوالبَ العامة (ومنها B2C) لمن يملك مطابقة أخصّ؛
    // الإسناد الصريح على مستوى الموظّف (أعلى أولوية) يتجاوز ذلك ويُتيح حارسَ الإسناد قبول التسليم.
    private static async Task AssignTemplateToEmployeeAsync(HttpClient admin, Guid templateId, Guid employeeId)
    {
        var res = await admin.PostAsJsonAsync($"/api/report-templates/{templateId}/assignments",
            new CreateAssignmentRequest(TemplateAssignmentScope.Employee, employeeId, TemplateAssignmentKind.Include, null));
        res.EnsureSuccessStatusCode();
    }

    // ينشئ تقرير مبيعات B2C يوميًّا ويُسلّمه ثم يعتمده المعتمِد ⇒ Closed (نفس المسار الحالي).
    private static async Task SubmitDailyB2cAsync(
        HttpClient employee, HttpClient approver, Guid templateId, Guid gridId, string date, string[] row)
    {
        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Daily, date)))
            .ReadAsync<SubmissionDto>();

        var gridJson = JsonSerializer.Serialize(new[] { row });
        var save = await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(gridId, null, null, null, null, gridJson) }));
        save.EnsureSuccessStatusCode();

        var submitted = await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        submitted.EnsureSuccessStatusCode();

        var approved = await (await approver.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);
    }

    // صفّ B2C بـ10 خلايا: الدورة، ساعات العمل، Leads، Contacted، Qualified، Follow-ups، Sales، Revenue، Lost، سبب الضياع.
    private static string[] B2cRow(string course, int work, int leads, int contacted, int qualified,
        int follow, int sales, int revenue, int lost)
        => new[] { course, work.ToString(), leads.ToString(), contacted.ToString(), qualified.ToString(),
                   follow.ToString(), sales.ToString(), revenue.ToString(), lost.ToString(), "" };

    private const string Course = "دورة التسويق الرقمي";

    [Fact]
    public async Task Daily_Aggregation_ReturnsSingleRow_ForThatDay()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, employeeId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, employeeId);

        var date = TestCalendar.Day(0);
        await SubmitDailyB2cAsync(employee, ceo, templateId, gridId, date, B2cRow(Course, 10, 40, 30, 18, 9, 6, 18000, 3));

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c?periodType=Daily&periodKey={date}&employeeId={employeeId}"))
            .ReadAsync<B2cAggregationReport>();

        var row = Assert.Single(report!.Rows);
        Assert.Equal(date, row.PeriodKey);
        Assert.Equal(employeeId, row.EmployeeId);
        Assert.Equal(Course, row.Course);
        Assert.Equal(10m, row.WorkHours);
        Assert.Equal(40m, row.Leads);
        Assert.Equal(6m, row.Sales);
        Assert.Equal(18000m, row.Revenue);
    }

    [Fact]
    public async Task Weekly_Aggregation_SumsDailyReports_UnderWeekKey()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, employeeId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, employeeId);

        // يومان داخل نفس الأسبوع التشغيلي (الخميس + الأحد) لضمان انتمائهما لنفس مفتاح الأسبوع.
        // الأسبوع منقضٍ لا مستقبليّ: بوّابة calendar.future_day_locked ترفض المستقبل،
        // والجمعة مرفوضة بـcalendar.day_is_holiday ⟹ لا تصلح يومًا ثانيًا.
        var weekKey = ReportCalendarPolicy.WeekKeyFor(TestCalendar.Today.AddDays(-14));
        var start = ReportCalendarPolicy.WeekRange(weekKey).Start; // الخميس
        // ثقافة ثابتة إلزاميًّا: ثقافة النظام قد تكون هجرية فتُنتِج مفتاحًا خاطئًا (مثل 1448-08-27).
        var d1 = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var d2 = start.AddDays(3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        await SubmitDailyB2cAsync(employee, ceo, templateId, gridId, d1, B2cRow(Course, 10, 40, 30, 18, 9, 6, 18000, 3));
        await SubmitDailyB2cAsync(employee, ceo, templateId, gridId, d2, B2cRow(Course, 5, 20, 15, 9, 4, 3, 9000, 1));

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c?periodType=Weekly&periodKey={weekKey}&employeeId={employeeId}"))
            .ReadAsync<B2cAggregationReport>();

        var row = Assert.Single(report!.Rows);
        Assert.Equal(weekKey, row.PeriodKey);
        Assert.Equal(15m, row.WorkHours);
        Assert.Equal(60m, row.Leads);
        Assert.Equal(9m, row.Sales);
        Assert.Equal(27000m, row.Revenue);
    }

    [Fact]
    public async Task Monthly_Aggregation_SumsDailyReports_UnderMonthKey()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, employeeId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, employeeId);

        // يومان داخل الشهر المنقضي ⟹ نفس المفتاح الشهريّ، وكلاهما ماضٍ فتقبلهما بوّابة التقويم.
        var days = TestCalendar.DaysInPreviousMonth(2);
        var monthKey = TestCalendar.MonthKeyOf(days[0]);
        await SubmitDailyB2cAsync(employee, ceo, templateId, gridId, days[0], B2cRow(Course, 10, 40, 30, 18, 9, 6, 18000, 3));
        await SubmitDailyB2cAsync(employee, ceo, templateId, gridId, days[1], B2cRow(Course, 5, 20, 15, 9, 4, 3, 9000, 1));

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c?periodType=Monthly&periodKey={monthKey}&employeeId={employeeId}"))
            .ReadAsync<B2cAggregationReport>();

        var row = Assert.Single(report!.Rows);
        Assert.Equal(monthKey, row.PeriodKey);
        Assert.Equal(15m, row.WorkHours);
        Assert.Equal(60m, row.Leads);
        Assert.Equal(9m, row.Sales);
        Assert.Equal(27000m, row.Revenue);
    }

    [Fact]
    public async Task Quarterly_Aggregation_SumsDailyReports_UnderQuarterKey()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, employeeId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, employeeId);

        // يومان داخل الشهر المنقضي ⟹ نفس الربع حتمًا، وكلاهما ماضٍ فتقبلهما بوّابة التقويم.
        var days = TestCalendar.DaysInPreviousMonth(2);
        var quarterKey = TestCalendar.QuarterKeyOf(days[0]);
        await SubmitDailyB2cAsync(employee, ceo, templateId, gridId, days[0], B2cRow(Course, 10, 40, 30, 18, 9, 6, 18000, 3));
        await SubmitDailyB2cAsync(employee, ceo, templateId, gridId, days[1], B2cRow(Course, 5, 20, 15, 9, 4, 3, 9000, 1));

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c?periodType=Quarterly&periodKey={quarterKey}&employeeId={employeeId}"))
            .ReadAsync<B2cAggregationReport>();

        var row = Assert.Single(report!.Rows);
        Assert.Equal(quarterKey, row.PeriodKey);
        Assert.Equal(15m, row.WorkHours);
        Assert.Equal(60m, row.Leads);
        Assert.Equal(9m, row.Sales);
        Assert.Equal(27000m, row.Revenue);
    }
}
