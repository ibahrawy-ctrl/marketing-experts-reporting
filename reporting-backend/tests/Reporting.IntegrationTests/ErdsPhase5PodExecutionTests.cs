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
/// ERDS Phase 5 — محرّك تجميع التنفيذ (قراءة فقط). يجمّع القوالب التنفيذية الرقمية الستة
/// (محتوى/تصميم/فيديو/نشر/ميديا باير/مشاريع) بقراءة جدول TableGrid (string[][] في ValueJson)
/// ومطابقة الأعمدة بالفهرس، حسب (الفترة، الفريق/Pod، الموظّف، العميل، المشروع)، مع قسمة آمنة ونطاق محكوم.
/// كل اختبار يُنشئ موظّفين جددًا (GUID فريد) وفترات فريدة (2026-W20…) ويصفّي على معرّفاتهم لعزل قاعدة الاختبار المشتركة.
/// لا يمسّ أيّ تسليم/قالب/مسار اعتماد قائم، ولا يمسّ Phase 4 (B2C/B2B).
/// </summary>
[Collection("Integration")]
public class ErdsPhase5PodExecutionTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ErdsPhase5PodExecutionTests(CustomWebApplicationFactory factory) => _factory = factory;

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
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var submitted = await (await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);
    }

    private static async Task<PodExecutionReport> AggPodsAsync(HttpClient client, string query)
        => (await (await client.GetAsync($"/api/reporting/aggregation/pods?{query}")).ReadAsync<PodExecutionReport>())!;

    private static async Task<ClientExecutionReport> AggClientsAsync(HttpClient client, string query)
        => (await (await client.GetAsync($"/api/reporting/aggregation/clients?{query}")).ReadAsync<ClientExecutionReport>())!;

    private static async Task<ProjectExecutionReport> AggProjectsAsync(HttpClient client, string query)
        => (await (await client.GetAsync($"/api/reporting/aggregation/projects?{query}")).ReadAsync<ProjectExecutionReport>())!;

    // ===== بُناة صفوف بترتيب أعمدة كل Schema (حرفيًّا) =====

    // المحتوى: [العميل، قطع مطلوبة، أفكار، كابشنات، سكربتات، Reels، معتمد، متأخر، سبب التأخير]
    private static string[] ContentRow(string client, string required, string approved, string late)
        => new[] { client, required, "0", "0", "0", "0", approved, late, "" };

    // التصميم: [العميل، تصميمات مطلوبة، منجزة، تعديلات، معتمدة، متأخرة، سبب التأخير]
    private static string[] DesignRow(string client, string required, string done, string late)
        => new[] { client, required, done, "0", "0", late, "" };

    // الفيديو: [العميل، فيديوهات مطلوبة، مصورة، مونتاج، معتمدة، منشورة، متأخرة، سبب التأخير]
    private static string[] VideoRow(string client, string required, string edited, string late)
        => new[] { client, required, "0", edited, "0", "0", late, "" };

    // النشر: [العميل، Posts Scheduled، Posts Published، Stories، Reels، Missed Posts، Comments، Engagement Notes]
    private static string[] SocialRow(string client, string scheduled, string published, string missed)
        => new[] { client, scheduled, published, "0", "0", missed, "0", "" };

    // Media Buyer: [العميل، المنصة، Spend، Leads، CPL، Purchases، CPA، Revenue، ROAS، ملاحظات]
    private static string[] MediaRow(string client, string platform, string spend, string leads, string purchases, string revenue)
        => new[] { client, platform, spend, leads, "0", purchases, "0", revenue, "0", "" };

    // المشاريع: [العميل، المشروع، ساعات العمل، المهام المخططة، المنجزة، المتأخرة، المتوقفة، نسبة التقدم %، مستوى الخطر، الدعم المطلوب]
    private static string[] ProjectRow(string client, string project, string hours, string planned, string done,
        string late, string blocked, string progress, string risk)
        => new[] { client, project, hours, planned, done, late, blocked, progress, risk, "" };

    // ===== (1) المحتوى حسب الفريق/العميل =====
    [Fact]
    public async Task Content_ByTeamAndClient_AggregatesAndComputesDelayRate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, empId);

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W20",
            new[] { ContentRow("عميل ألفا", "25", "20", "5") });

        var report = await AggPodsAsync(admin, $"periodKey=2026-W20&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(teamId, row.TeamId);
        Assert.False(string.IsNullOrEmpty(row.TeamName));
        Assert.Equal("عميل ألفا", row.Client);
        Assert.Equal(20m, row.ContentPieces);
        Assert.Equal(5m, row.DelayedItems);
        Assert.Equal(25m, row.ProductionRequiredItems);
        Assert.Equal(20.0m, row.ProductivityIndicators.DelayRate); // 5/25
    }

    // ===== (2) التصميم حسب الفريق/العميل =====
    [Fact]
    public async Task Design_ByTeamAndClient_Aggregates()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, DesignProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, empId);

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W21",
            new[] { DesignRow("عميل بيتا", "30", "24", "6") });

        var report = await AggPodsAsync(admin, $"periodKey=2026-W21&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(teamId, row.TeamId);
        Assert.Equal("عميل بيتا", row.Client);
        Assert.Equal(24m, row.DesignsCompleted);
        Assert.Equal(6m, row.DelayedItems);
        Assert.Equal(20.0m, row.ProductivityIndicators.DelayRate); // 6/30
    }

    // ===== (3) الفيديو حسب الفريق/العميل =====
    [Fact]
    public async Task Video_ByTeamAndClient_Aggregates()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, VideoProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W22",
            new[] { VideoRow("عميل جاما", "10", "8", "2") });

        var report = await AggPodsAsync(admin, $"periodKey=2026-W22&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل جاما", row.Client);
        Assert.Equal(8m, row.VideosCompleted);
        Assert.Equal(2m, row.DelayedItems);
        Assert.Equal(20.0m, row.ProductivityIndicators.DelayRate); // 2/10
    }

    // ===== (4) النشر حسب الفريق/العميل + Missed Posting Rate =====
    [Fact]
    public async Task Publishing_ByTeamAndClient_ComputesMissedPostingRate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, SocialPublishingReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W23",
            new[] { SocialRow("عميل دلتا", "50", "40", "5") });

        var report = await AggPodsAsync(admin, $"periodKey=2026-W23&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل دلتا", row.Client);
        Assert.Equal(40m, row.PostsPublished);
        Assert.Equal(5m, row.MissedPosts);
        Assert.Equal(50m, row.PostsScheduled);
        Assert.Equal(10.0m, row.ProductivityIndicators.MissedPostingRate); // 5/50
    }

    // ===== (5) Media Buyer — CPL/CPA/ROAS محسوبة من المجاميع لا مجموعة =====
    [Fact]
    public async Task MediaBuyer_CplCpaRoas_ComputedFromTotals_NotSummed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, MediaBuyerByClientReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // منصّتان لنفس العميل: المجاميع Spend=200, Leads=40, Purchases=10, Revenue=2000.
        // CPL من المجاميع = 200/40 = 5 (لو جُمِعت النِسب لكانت 10+3.33). CPA=200/10=20، ROAS=2000/200=10.
        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W24", new[]
        {
            MediaRow("عميل إبسيلون", "Meta", "100", "10", "5", "500"),
            MediaRow("عميل إبسيلون", "Google", "100", "30", "5", "1500"),
        });

        var report = await AggClientsAsync(admin, $"periodKey=2026-W24&employeeId={empId}&client=عميل إبسيلون");
        var row = Assert.Single(report.Rows);
        Assert.Equal(200m, row.TotalSpend);
        Assert.Equal(40m, row.TotalLeads);
        Assert.Equal(2000m, row.TotalRevenue);
        Assert.Equal(5.00m, row.Cpl);  // 200/40 — من المجاميع
        Assert.Equal(20.00m, row.Cpa); // 200/10
        Assert.Equal(10.00m, row.Roas); // 2000/200
    }

    // ===== (6) المشاريع حسب العميل/المشروع =====
    [Fact]
    public async Task Projects_ByClientAndProject_ComputesCompletionAndProgress()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectsByClientReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W25",
            new[] { ProjectRow("عميل زيتا", "موقع إلكتروني", "40", "10", "6", "2", "1", "50", "مرتفع") });

        var report = await AggProjectsAsync(admin, $"periodKey=2026-W25&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل زيتا", row.Client);
        Assert.Equal("موقع إلكتروني", row.Project);
        Assert.Equal(40m, row.WorkHours);
        Assert.Equal(10m, row.PlannedTasks);
        Assert.Equal(6m, row.CompletedTasks);
        Assert.Equal(1m, row.BlockedTasks);
        Assert.Equal(50m, row.ProgressPercentAvg);
        Assert.Equal(60.0m, row.CompletionRate); // 6/10
        Assert.Equal("مرتفع", row.RiskLevel);
    }

    // ===== (7) عدّة موظّفين في نفس الـPod =====
    [Fact]
    public async Task MultipleEmployees_SamePod_ShareTeam()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (emp1, emp1Id) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (emp2, emp2Id) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, emp1Id, emp2Id);

        await SubmitGridAsync(emp1, tpl.Id, grid.Id, "2026-W26", new[] { ContentRow("عميل مشترك", "10", "8", "1") });
        await SubmitGridAsync(emp2, tpl.Id, grid.Id, "2026-W26", new[] { ContentRow("عميل مشترك", "20", "15", "3") });

        var report = await AggPodsAsync(admin, $"periodKey=2026-W26&teamId={teamId}");
        var rows = report.Rows.Where(r => r.TeamId == teamId).ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(teamId, r.TeamId));
        Assert.Contains(rows, r => r.EmployeeId == emp1Id && r.ContentPieces == 8m);
        Assert.Contains(rows, r => r.EmployeeId == emp2Id && r.ContentPieces == 15m);
    }

    // ===== (8) عدّة عملاء في نفس الـPod =====
    [Fact]
    public async Task MultipleClients_SamePod_GroupsByClient()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (leader, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, empId);

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W27", new[]
        {
            ContentRow("عميل واحد", "10", "9", "1"),
            ContentRow("عميل اثنان", "20", "18", "2"),
        });

        var report = await AggPodsAsync(admin, $"periodKey=2026-W27&employeeId={empId}");
        var rows = report.Rows.OrderBy(r => r.Client).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("عميل اثنان", rows[0].Client);
        Assert.Equal(18m, rows[0].ContentPieces);
        Assert.Equal("عميل واحد", rows[1].Client);
        Assert.Equal(9m, rows[1].ContentPieces);
    }

    // ===== (9) تجاهُل تقرير غير مطابق بلا فشل =====
    [Fact]
    public async Task IgnoresNonMatchingReport_WithoutFailure()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var financial = await GetTemplateByTitleAsync(admin, "التقرير المالي");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(financial.Id, financial.DefaultPeriodType, "2026-W28"))).ReadAsync<SubmissionDto>();
        Assert.NotNull(draft);

        var pods = await AggPodsAsync(admin, $"periodKey=2026-W28&employeeId={empId}");
        Assert.Empty(pods.Rows);
        Assert.Equal(0, pods.RowCount);

        var clients = await AggClientsAsync(admin, $"periodKey=2026-W28&employeeId={empId}");
        Assert.Empty(clients.Rows);

        var projects = await AggProjectsAsync(admin, $"periodKey=2026-W28&employeeId={empId}");
        Assert.Empty(projects.Rows);
    }

    // ===== (10) احترام النطاق/الصلاحيات =====
    [Fact]
    public async Task Scope_UnrelatedUser_DoesNotSeeOthersData_AndUnauthenticated401()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2026-W29", new[] { ContentRow("عميل نطاق", "10", "8", "1") });

        // الأدمن (governance ⇒ SeesAll) يرى الصفّ.
        var asAdmin = await AggPodsAsync(admin, $"periodKey=2026-W29&employeeId={empId}");
        Assert.Single(asAdmin.Rows);

        // موظّف غير مرتبط (نطاق own) لا يرى بيانات غيره.
        var (outsider, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var asOutsider = await AggPodsAsync(outsider, $"periodKey=2026-W29&employeeId={empId}");
        Assert.Empty(asOutsider.Rows);
        Assert.Equal("self", asOutsider.ViewLevel);

        // غير مصادَق ⇒ 401.
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/api/reporting/aggregation/pods?periodKey=2026-W29");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ===== (11) Phase 4 (B2C/B2B) غير متأثّرة =====
    [Fact]
    public async Task Phase4_B2cB2b_Unaffected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var b2c = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var b2cGrid = GridField(b2c);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // تسليم B2C: [الدورة، ساعات، Leads، Contacted، Qualified، Follow، Sales، Revenue، Lost، السبب]
        await SubmitGridAsync(emp, b2c.Id, b2cGrid.Id, "2026-W30",
            new[] { new[] { "دورة تحقّق", "40", "100", "80", "50", "10", "25", "8000", "10", "" } });

        // محرّك Phase 4 يقرأ B2C كما هو.
        var b2cReport = (await (await admin.GetAsync($"/api/reporting/aggregation/b2c?periodKey=2026-W30&employeeId={empId}"))
            .ReadAsync<B2cAggregationReport>())!;
        Assert.Single(b2cReport.Rows);
        Assert.Equal("دورة تحقّق", b2cReport.Rows[0].Course);

        // محرّك Phase 5 لا يرى قالب B2C (ليس ضمن القوالب الستة) ⇒ لا صفوف.
        var pods = await AggPodsAsync(admin, $"periodKey=2026-W30&employeeId={empId}");
        Assert.Empty(pods.Rows);
    }

    // ===== (12) دورة حياة التسليم/المراجعة/الاعتماد غير متأثّرة =====
    [Fact]
    public async Task CurrentWorkflow_SubmitApprove_Unaffected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(tpl.Id, PeriodType.Weekly, "2026-W31"))).ReadAsync<SubmissionDto>();
        var gridJson = JsonSerializer.Serialize(new[] { ContentRow("عميل اعتماد", "10", "8", "1") });
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(grid.Id, null, null, null, null, gridJson) }));
        var submitted = await (await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);

        var approved = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);

        // التجميع يقرأ التسليم المُغلَق أيضًا (Status != Draft).
        var pods = await AggPodsAsync(admin, $"periodKey=2026-W31&employeeId={empId}");
        Assert.Single(pods.Rows);
        Assert.Equal(8m, pods.Rows[0].ContentPieces);
    }
}
