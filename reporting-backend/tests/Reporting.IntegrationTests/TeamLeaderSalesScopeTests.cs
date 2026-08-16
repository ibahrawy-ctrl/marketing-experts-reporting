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
/// RC3-Task1 — لوحة مبيعات الفريق لقائد الفريق. تعتمد الصفحة على نقطة التجميع الحالية
/// «/api/reporting/aggregation/b2c/by-course» بلا فلتر employeeId؛ فالنطاق مفروض خادميًّا عبر IScopeResolver.
/// نطاق «team» = قائد الفريق نفسه + مرؤوسوه المباشرون (ManagerId==قائد الفريق).
/// هذه الاختبارات تتحقّق أنّ قائد الفريق يرى موظّفي فريقه فقط، وأنّ نطاق الأدوار الأعلى (CEO) لا ينكسر.
/// </summary>
[Collection("Integration")]
public class TeamLeaderSalesScopeTests
{
    private readonly CustomWebApplicationFactory _factory;

    public TeamLeaderSalesScopeTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string Course = "دورة التسويق الرقمي";

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
        // اعتماد المدير المباشر قد يُغلق التقرير (Closed) أو يبقيه معتمَدًا بانتظار مستوى أعلى
        // (ApprovedByDirectManager) حسب موقع المُعتمِد في السلسلة؛ التجميع يشمل كل ما ليس مسودّة.
        Assert.True(approved!.Status is SubmissionStatus.Closed or SubmissionStatus.ApprovedByDirectManager,
            $"حالة غير متوقّعة بعد الاعتماد: {approved.Status}");
    }

    private static string[] B2cRow(string course, int work, int leads, int contacted, int qualified,
        int follow, int sales, int revenue, int lost)
        => new[] { course, work.ToString(), leads.ToString(), contacted.ToString(), qualified.ToString(),
                   follow.ToString(), sales.ToString(), revenue.ToString(), lost.ToString(), "" };

    private static IReadOnlyList<Guid> EmployeeIds(B2cCourseGroupedReport report)
        => report.Courses.SelectMany(c => c.Employees).Select(e => e.EmployeeId).Distinct().ToList();

    /// <summary>
    /// قائد الفريق يرى موظّف فريقه فقط عبر by-course (بلا فلتر employeeId) — لا يرى موظّفًا يتبع مديرًا آخر.
    /// وفي المقابل CEO (نطاق شركة) يرى الاثنين ⇒ النطاق مُقيَّد لقائد الفريق دون كسر نطاق الأدوار الأعلى.
    /// </summary>
    [Fact]
    public async Task ByCourse_AsTeamLeader_SeesOnlyOwnTeamMember_ButCeoSeesBoth()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);

        // CEO في القمّة؛ قائد الفريق يتبع CEO؛ موظّف الفريق يتبع قائد الفريق؛ موظّف خارج الفريق يتبع CEO مباشرة.
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (teamLeader, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader", ceoId);
        var (inTeamEmp, inTeamId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", tlId);
        var (outTeamEmp, outTeamId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);

        await AssignTemplateToEmployeeAsync(admin, templateId, inTeamId);
        await AssignTemplateToEmployeeAsync(admin, templateId, outTeamId);

        // يوم واحد داخل أسبوع تشغيلي واحد لكلا الموظّفين (ثقافة ثابتة إلزاميًّا لتفادي التقويم الهجري).
        var weekKey = ReportCalendarPolicy.WeekKeyFor(TestCalendar.Today.AddDays(-14));
        var day = ReportCalendarPolicy.WeekRange(weekKey).Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // موظّف الفريق يعتمده قائد الفريق (مديره المباشر)؛ موظّف خارج الفريق يعتمده CEO.
        await SubmitDailyB2cAsync(inTeamEmp, teamLeader, templateId, gridId, day, B2cRow(Course, 10, 40, 30, 18, 9, 6, 18000, 3));
        await SubmitDailyB2cAsync(outTeamEmp, ceo, templateId, gridId, day, B2cRow(Course, 8, 32, 24, 14, 7, 5, 15000, 2));

        // قائد الفريق: بلا فلتر employeeId ⇒ النطاق الخادمي يقيّده على فريقه فقط.
        var tlReport = await (await teamLeader.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Weekly&periodKey={weekKey}"))
            .ReadAsync<B2cCourseGroupedReport>();
        var tlIds = EmployeeIds(tlReport!);
        Assert.Contains(inTeamId, tlIds);
        Assert.DoesNotContain(outTeamId, tlIds);

        // CEO (نطاق شركة): يرى الاثنين ⇒ لم ينكسر نطاق الأدوار الأعلى.
        var ceoReport = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Weekly&periodKey={weekKey}"))
            .ReadAsync<B2cCourseGroupedReport>();
        var ceoIds = EmployeeIds(ceoReport!);
        Assert.Contains(inTeamId, ceoIds);
        Assert.Contains(outTeamId, ceoIds);
    }

    /// <summary>
    /// تفصيل New/Old لقائد الفريق يقتصر على موظّفي فريقه: إجمالي إيراد فريقه = مساهمة موظّف الفريق فقط (18000)،
    /// بينما CEO يرى الاثنين (18000 + 15000 = 33000). (new-old تجميع على مستوى الدورة، فنتحقّق عبر الإجماليات.)
    /// </summary>
    [Fact]
    public async Task NewOld_AsTeamLeader_TotalsScopedToTeam()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);

        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (teamLeader, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader", ceoId);
        var (inTeamEmp, inTeamId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", tlId);
        var (outTeamEmp, outTeamId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);

        await AssignTemplateToEmployeeAsync(admin, templateId, inTeamId);
        await AssignTemplateToEmployeeAsync(admin, templateId, outTeamId);

        var weekKey = ReportCalendarPolicy.WeekKeyFor(TestCalendar.Today.AddDays(-14));
        var day = ReportCalendarPolicy.WeekRange(weekKey).Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        await SubmitDailyB2cAsync(inTeamEmp, teamLeader, templateId, gridId, day, B2cRow(Course, 10, 40, 30, 18, 9, 6, 18000, 3));
        await SubmitDailyB2cAsync(outTeamEmp, ceo, templateId, gridId, day, B2cRow(Course, 8, 32, 24, 14, 7, 5, 15000, 2));

        var tl = await (await teamLeader.GetAsync(
            $"/api/reporting/aggregation/b2c/new-old?periodType=Weekly&periodKey={weekKey}"))
            .ReadAsync<B2cNewOldReport>();
        Assert.Equal(18000m, tl!.NewTotals.Revenue + tl.OldTotals.Revenue);

        // CEO نطاقه على مستوى الشركة (قاعدة الاختبار مشتركة قد تحوي بياناتٍ متراكمة في نفس الأسبوع)،
        // فنكتفي بالحدّ الأدنى: يشمل مساهمتَي الموظّفَين معًا (18000 + 15000 = 33000) على الأقلّ.
        var all = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c/new-old?periodType=Weekly&periodKey={weekKey}"))
            .ReadAsync<B2cNewOldReport>();
        Assert.True(all!.NewTotals.Revenue + all.OldTotals.Revenue >= 33000m);
    }
}
