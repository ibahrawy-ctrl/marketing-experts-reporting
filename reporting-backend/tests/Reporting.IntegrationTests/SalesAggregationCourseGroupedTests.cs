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
/// تجميع مبيعات B2C «حسب الدورة» (العرض الافتراضي للمدير): الدورة تظهر مرّة واحدة عبر كل الموظّفين،
/// وإجماليات الدورة = مجموع مساهمات الموظّفين، مع تفصيل موظّفين (Drill-down). تُغطّى أيضًا التقارير
/// القديمة ذات الدورة النصّية (تبقى تُجمَّع بلا كسر). النطاق معزول عبر employeeId/teamId على مستخدمي اختبار فريدين.
/// </summary>
[Collection("Integration")]
public class SalesAggregationCourseGroupedTests
{
    private readonly CustomWebApplicationFactory _factory;

    public SalesAggregationCourseGroupedTests(CustomWebApplicationFactory factory) => _factory = factory;

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

    private static async Task AssignTemplateToEmployeeAsync(HttpClient admin, Guid templateId, Guid employeeId)
    {
        var res = await admin.PostAsJsonAsync($"/api/report-templates/{templateId}/assignments",
            new CreateAssignmentRequest(TemplateAssignmentScope.Employee, employeeId, TemplateAssignmentKind.Include, null));
        res.EnsureSuccessStatusCode();
    }

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

    [Fact]
    public async Task Grouped_SingleCourseSingleEmployee_ReturnsOneCourseWithOneEmployee()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, employeeId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, employeeId);

        const string course = "دورة سيو الجماعية أ";
        var date = TestCalendar.Day(0);
        await SubmitDailyB2cAsync(employee, ceo, templateId, gridId, date, B2cRow(course, 10, 40, 30, 18, 9, 6, 18000, 3));

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Daily&periodKey={date}&employeeId={employeeId}"))
            .ReadAsync<B2cCourseGroupedReport>();

        var group = Assert.Single(report!.Courses);
        Assert.Equal(course, group.Course);
        Assert.Equal(1, group.EmployeeCount);
        Assert.Equal(10m, group.WorkHours);
        Assert.Equal(40m, group.Leads);
        Assert.Equal(6m, group.Sales);
        Assert.Equal(18000m, group.Revenue);
        var emp = Assert.Single(group.Employees);
        Assert.Equal(employeeId, emp.EmployeeId);
        Assert.Equal(6m, emp.Sales);
    }

    [Fact]
    public async Task Grouped_TwoEmployeesSameCourse_MergeIntoOneCourseRow_WithBothInDrillDown()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        // مدير واحد (CEO) يرى الموظّفَين ⇒ نطاق CEO عبر شجرة ManagerId يشملهما.
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp1, emp1Id) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        var (emp2, emp2Id) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, emp1Id);
        await AssignTemplateToEmployeeAsync(admin, templateId, emp2Id);

        // اسم دورة فريد لكل تشغيل — القاعدة المشتركة الدائمة تتراكم، والتصفية بالاسم فقط تحتاج عزلًا.
        var course = $"دورة جماعية {Guid.NewGuid():N}";
        var date = TestCalendar.Day(1);
        await SubmitDailyB2cAsync(emp1, ceo, templateId, gridId, date, B2cRow(course, 10, 40, 30, 18, 9, 6, 18000, 3));
        await SubmitDailyB2cAsync(emp2, ceo, templateId, gridId, date, B2cRow(course, 5, 20, 15, 9, 4, 3, 9000, 1));

        // نصفّي على الدورة نفسها لعزل بيانات القاعدة المشتركة (النطاق قد يشمل موظّفين آخرين بدورات أخرى).
        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Daily&periodKey={date}&course={Uri.EscapeDataString(course)}"))
            .ReadAsync<B2cCourseGroupedReport>();

        var group = Assert.Single(report!.Courses.Where(c => c.Course == course));
        // الدورة تظهر مرّة واحدة رغم موظّفَين اثنين.
        Assert.Equal(2, group.EmployeeCount);
        // الإجماليات = مجموع مساهمات الموظّفين.
        Assert.Equal(15m, group.WorkHours);
        Assert.Equal(60m, group.Leads);
        Assert.Equal(9m, group.Sales);
        Assert.Equal(27000m, group.Revenue);
        // Drill-down يحوي الموظّفَين كليهما.
        Assert.Equal(2, group.Employees.Count);
        Assert.Contains(group.Employees, e => e.EmployeeId == emp1Id && e.Sales == 6m);
        Assert.Contains(group.Employees, e => e.EmployeeId == emp2Id && e.Sales == 3m);
        // تحقّق «الإجمالي = مجموع الموظّفين» صراحةً.
        Assert.Equal(group.Sales, group.Employees.Sum(e => e.Sales));
        Assert.Equal(group.Revenue, group.Employees.Sum(e => e.Revenue));
        Assert.Equal(group.WorkHours, group.Employees.Sum(e => e.WorkHours));
    }

    [Fact]
    public async Task Grouped_LegacyTextCourse_StillAggregated_NotBroken()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, employeeId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, employeeId);

        // دورة نصّية حرّة غير موجودة في الكتالوج (تحاكي تقريرًا قديمًا) — يجب أن تُجمَّع كالمعتاد.
        const string legacyCourse = "دورة قديمة نصّية خارج الكتالوج";
        var date = TestCalendar.Day(2);
        await SubmitDailyB2cAsync(employee, ceo, templateId, gridId, date, B2cRow(legacyCourse, 8, 32, 24, 12, 6, 4, 12000, 2));

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Daily&periodKey={date}&employeeId={employeeId}"))
            .ReadAsync<B2cCourseGroupedReport>();

        var group = Assert.Single(report!.Courses);
        Assert.Equal(legacyCourse, group.Course);
        Assert.Equal(4m, group.Sales);
        Assert.Equal(12000m, group.Revenue);
    }
}
