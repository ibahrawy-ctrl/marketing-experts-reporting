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
/// RC3-Task1.1 — سياق المبيعات الموثوق خادميًّا «/api/reporting/aggregation/sales-context».
/// يحدّد الأقسام المعروضة (B2C/B2B) وهل المستخدم مندوب فردي، وفق أولوية:
/// (1) المسمّى الوظيفي للمستخدم حاسم، (2) رؤية كاملة ⇒ الاثنان، (3) استنتاج من مسمّيات النطاق، (4) احتياط ⇒ الاثنان.
/// هذه الاختبارات تغطّي المطالب 1–7 (قائد B2C/B2B/مختلط، مندوب B2C/B2B، عدم تسرّب بيانات الزملاء، عدم كسر نطاق الأدوار الأعلى).
/// </summary>
[Collection("Integration")]
public class SalesContextTests
{
    private readonly CustomWebApplicationFactory _factory;

    public SalesContextTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string Course = "دورة التسويق الرقمي";

    private static async Task<SalesContextDto> GetContextAsync(HttpClient client)
        => (await (await client.GetAsync("/api/reporting/aggregation/sales-context"))
            .ReadAsync<SalesContextDto>())!;

    // ===== (1) قائد فريق B2C يرى B2C فقط =====
    [Fact]
    public async Task Context_B2cTeamLeader_ShowsB2cOnly()
    {
        var (tl, _) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "TeamLeader", "SALES_B2C_TL");
        var ctx = await GetContextAsync(tl);
        Assert.True(ctx.ShowB2c);
        Assert.False(ctx.ShowB2b);
        Assert.False(ctx.IsSalesRep);
        Assert.Null(ctx.RepType);
    }

    // ===== (2) قائد فريق B2B يرى B2B فقط =====
    [Fact]
    public async Task Context_B2bTeamLeader_ShowsB2bOnly()
    {
        var (tl, _) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "TeamLeader", "SALES_B2B_TL");
        var ctx = await GetContextAsync(tl);
        Assert.False(ctx.ShowB2c);
        Assert.True(ctx.ShowB2b);
        Assert.False(ctx.IsSalesRep);
        Assert.Null(ctx.RepType);
    }

    // ===== (3) قائد فريق مختلط (بلا مسمّى مبيعات حاسم، ونطاقه يضمّ B2C وB2B) يرى الاثنين =====
    [Fact]
    public async Task Context_MixedTeamLeader_ShowsBoth()
    {
        // قائد الفريق بمسمّى غير حاسم (مدير مبيعات) — يؤول للاستنتاج من مسمّيات أعضاء النطاق.
        var (tl, tlId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "TeamLeader", "SALES_MGR");
        // مرؤوسان مباشران: أحدهما B2C والآخر B2B.
        await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", tlId);
        await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2B", tlId);

        var ctx = await GetContextAsync(tl);
        Assert.True(ctx.ShowB2c);
        Assert.True(ctx.ShowB2b);
        Assert.False(ctx.IsSalesRep);
    }

    // ===== (4) مندوب B2C يرى B2C فقط وهو مندوب =====
    [Fact]
    public async Task Context_B2cRep_IsRepB2c()
    {
        var (rep, _) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C");
        var ctx = await GetContextAsync(rep);
        Assert.True(ctx.ShowB2c);
        Assert.False(ctx.ShowB2b);
        Assert.True(ctx.IsSalesRep);
        Assert.Equal("B2C", ctx.RepType);
    }

    // ===== (5) مندوب B2B يرى B2B فقط وهو مندوب =====
    [Fact]
    public async Task Context_B2bRep_IsRepB2b()
    {
        var (rep, _) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2B");
        var ctx = await GetContextAsync(rep);
        Assert.False(ctx.ShowB2c);
        Assert.True(ctx.ShowB2b);
        Assert.True(ctx.IsSalesRep);
        Assert.Equal("B2B", ctx.RepType);
    }

    // ===== (7) CEO (رؤية كاملة) يرى الاثنين ولا يُعدّ مندوبًا =====
    [Fact]
    public async Task Context_Ceo_ShowsBoth_NotRep()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var ctx = await GetContextAsync(ceo);
        Assert.True(ctx.ShowB2c);
        Assert.True(ctx.ShowB2b);
        Assert.False(ctx.IsSalesRep);
    }

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

        var approved = await approver.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"));
        approved.EnsureSuccessStatusCode();
    }

    private static string[] B2cRow(string course, int work, int leads, int contacted, int qualified,
        int follow, int sales, int revenue, int lost)
        => new[] { course, work.ToString(), leads.ToString(), contacted.ToString(), qualified.ToString(),
                   follow.ToString(), sales.ToString(), revenue.ToString(), lost.ToString(), "" };

    private static IReadOnlyList<Guid> EmployeeIds(B2cCourseGroupedReport report)
        => report.Courses.SelectMany(c => c.Employees).Select(e => e.EmployeeId).Distinct().ToList();

    // ===== (6) المندوب لا يرى زملاءه: حتى بتمرير employeeId لزميل، النطاق الخادمي (تقاطع) يقيّده على نفسه =====
    [Fact]
    public async Task Aggregation_AsRep_CannotSeeColleagueData()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);

        // قائد فريق في الأعلى؛ مندوبان يتبعانه — كلاهما B2C.
        var (teamLeader, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (repA, repAId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", tlId);
        var (repB, repBId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", tlId);

        await AssignTemplateToEmployeeAsync(admin, templateId, repAId);
        await AssignTemplateToEmployeeAsync(admin, templateId, repBId);

        var weekKey = ReportCalendarPolicy.WeekKeyFor(new DateOnly(2028, 9, 7));
        var day = ReportCalendarPolicy.WeekRange(weekKey).Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        await SubmitDailyB2cAsync(repA, teamLeader, templateId, gridId, day, B2cRow(Course, 10, 40, 30, 18, 9, 6, 18000, 3));
        await SubmitDailyB2cAsync(repB, teamLeader, templateId, gridId, day, B2cRow(Course, 8, 32, 24, 14, 7, 5, 15000, 2));

        // المندوب A يحاول رؤية بيانات زميله B صراحةً عبر employeeId — يجب أن يعيد الخادم لا شيء (تقاطع النطاق=نفسه فقط).
        var leaked = await (await repA.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Weekly&periodKey={weekKey}&employeeId={repBId}"))
            .ReadAsync<B2cCourseGroupedReport>();
        Assert.DoesNotContain(repBId, EmployeeIds(leaked!));

        // بلا فلتر: المندوب A يرى نفسه فقط (لا زميله).
        var own = await (await repA.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Weekly&periodKey={weekKey}"))
            .ReadAsync<B2cCourseGroupedReport>();
        var ownIds = EmployeeIds(own!);
        Assert.Contains(repAId, ownIds);
        Assert.DoesNotContain(repBId, ownIds);
    }
}
