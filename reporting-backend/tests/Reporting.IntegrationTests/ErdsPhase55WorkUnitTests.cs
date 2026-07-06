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
/// ERDS Phase 5.5 — تطبيع وحدة العمل (Work Unit). يتحقّق من إلحاق أعمدة (المشروع/ساعات العمل/الحالة)
/// بنهاية القوالب التنفيذية الستة، وأنّ محرّك التجميع (Phase 5) صار يقرأ ساعات العمل + المشروع من الكتل
/// الإنتاجية الخمس (محتوى/تصميم/فيديو/نشر/ميديا باير) إضافةً لقالب المشاريع، ويشتقّ مؤشّرات «لكل ساعة».
/// التوافق الخلفي: صفوف قديمة أقصر بلا عمود ساعات ⇒ 0 بلا فشل. لا يمسّ Phase 4 (B2C/B2B) ولا مسار الاعتماد.
/// كل اختبار يُنشئ موظّفًا جديدًا (GUID فريد) وفترة فريدة (2028-Wnn) لعزل قاعدة الاختبار المشتركة.
/// </summary>
[Collection("Integration")]
public class ErdsPhase55WorkUnitTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ErdsPhase55WorkUnitTests(CustomWebApplicationFactory factory) => _factory = factory;

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

    private static async Task SubmitGridAsync(HttpClient employee, Guid templateId, Guid gridFieldId,
        string periodKey, string[][] rows)
    {
        var draftResp = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey));
        Assert.True(draftResp.StatusCode == HttpStatusCode.OK,
            $"create draft failed: {draftResp.StatusCode} — {await draftResp.Content.ReadAsStringAsync()}");
        var draft = await draftResp.ReadAsync<SubmissionDto>();

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

    // ===== بُناة صفوف بترتيب Schema الجديد (مع أعمدة Work Unit المُلحقة: المشروع/ساعات العمل/الحالة) =====

    // المحتوى (12 عمودًا): [العميل، قطع مطلوبة، أفكار، كابشنات، سكربتات، Reels، معتمد، متأخر، سبب التأخير، المشروع، ساعات العمل، الحالة]
    private static string[] ContentRowWU(string client, string required, string approved, string late, string project, string hours)
        => new[] { client, required, "0", "0", "0", "0", approved, late, "", project, hours, "" };

    // المحتوى بالترتيب القديم (9 أعمدة، بلا أعمدة Work Unit) — لاختبارات التوافق الخلفي.
    private static string[] ContentRowLegacy(string client, string required, string approved, string late)
        => new[] { client, required, "0", "0", "0", "0", approved, late, "" };

    // التصميم (10 أعمدة): [العميل، مطلوبة، منجزة، تعديلات، معتمدة، متأخرة، سبب التأخير، المشروع، ساعات العمل، الحالة]
    private static string[] DesignRowWU(string client, string required, string done, string late, string project, string hours)
        => new[] { client, required, done, "0", "0", late, "", project, hours, "" };

    // الفيديو (11 عمودًا): [العميل، مطلوبة، مصورة، مونتاج، معتمدة، منشورة، متأخرة، سبب التأخير، المشروع، ساعات العمل، الحالة]
    private static string[] VideoRowWU(string client, string required, string edited, string late, string project, string hours)
        => new[] { client, required, "0", edited, "0", "0", late, "", project, hours, "" };

    // النشر (11 عمودًا): [العميل، Scheduled، Published، Stories، Reels، Missed، Comments، Engagement Notes، المشروع، ساعات العمل، الحالة]
    private static string[] SocialRowWU(string client, string scheduled, string published, string missed, string project, string hours)
        => new[] { client, scheduled, published, "0", "0", missed, "0", "", project, hours, "" };

    // Media Buyer (13 عمودًا): [العميل، المنصة، Spend، Leads، CPL، Purchases، CPA، Revenue، ROAS، ملاحظات، المشروع، ساعات العمل، الحالة]
    private static string[] MediaRowWU(string client, string platform, string spend, string leads, string purchases, string revenue, string project, string hours)
        => new[] { client, platform, spend, leads, "0", purchases, "0", revenue, "0", "", project, hours, "" };

    // المشاريع (11 عمودًا): [العميل، المشروع، ساعات العمل، مخططة، منجزة، متأخرة، متوقفة، نسبة التقدم %، مستوى الخطر، الدعم المطلوب، الحالة]
    private static string[] ProjectRowWU(string client, string project, string hours, string planned, string done,
        string late, string blocked, string progress, string risk)
        => new[] { client, project, hours, planned, done, late, blocked, progress, risk, "", "" };

    // ===== (1) المحتوى — عمود ساعات العمل موجود ويُقرأ حتى التجميع =====
    [Fact]
    public async Task Content_WorkHoursColumn_PresentAndAggregated()
    {
        Assert.Contains(ContentProductionReportSchema.ColProject, ContentProductionReportSchema.Columns);
        Assert.Contains(ContentProductionReportSchema.ColWorkHours, ContentProductionReportSchema.Columns);
        Assert.Contains(ContentProductionReportSchema.ColStatus, ContentProductionReportSchema.Columns);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2028-W01",
            new[] { ContentRowWU("عميل محتوى", "25", "20", "5", "مشروع محتوى", "12") });

        var report = await AggPodsAsync(admin, $"periodKey=2028-W01&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل محتوى", row.Client);
        Assert.Equal("مشروع محتوى", row.Project);
        Assert.Equal(12m, row.WorkHours);
        Assert.Equal(20m, row.ContentPieces);
    }

    // ===== (2) التصميم — عمود ساعات العمل موجود ويُقرأ =====
    [Fact]
    public async Task Design_WorkHoursColumn_PresentAndAggregated()
    {
        Assert.Contains(DesignProductionReportSchema.ColProject, DesignProductionReportSchema.Columns);
        Assert.Contains(DesignProductionReportSchema.ColWorkHours, DesignProductionReportSchema.Columns);
        Assert.Contains(DesignProductionReportSchema.ColStatus, DesignProductionReportSchema.Columns);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, DesignProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2028-W02",
            new[] { DesignRowWU("عميل تصميم", "30", "24", "6", "مشروع تصميم", "8") });

        var report = await AggPodsAsync(admin, $"periodKey=2028-W02&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل تصميم", row.Client);
        Assert.Equal("مشروع تصميم", row.Project);
        Assert.Equal(8m, row.WorkHours);
        Assert.Equal(24m, row.DesignsCompleted);
    }

    // ===== (3) الفيديو — عمود ساعات العمل موجود ويُقرأ =====
    [Fact]
    public async Task Video_WorkHoursColumn_PresentAndAggregated()
    {
        Assert.Contains(VideoProductionReportSchema.ColProject, VideoProductionReportSchema.Columns);
        Assert.Contains(VideoProductionReportSchema.ColWorkHours, VideoProductionReportSchema.Columns);
        Assert.Contains(VideoProductionReportSchema.ColStatus, VideoProductionReportSchema.Columns);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, VideoProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2028-W03",
            new[] { VideoRowWU("عميل فيديو", "10", "8", "2", "مشروع فيديو", "6") });

        var report = await AggPodsAsync(admin, $"periodKey=2028-W03&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل فيديو", row.Client);
        Assert.Equal("مشروع فيديو", row.Project);
        Assert.Equal(6m, row.WorkHours);
        Assert.Equal(8m, row.VideosCompleted);
    }

    // ===== (4) النشر — عمود ساعات العمل موجود ويُقرأ =====
    [Fact]
    public async Task Publishing_WorkHoursColumn_PresentAndAggregated()
    {
        Assert.Contains(SocialPublishingReportSchema.ColProject, SocialPublishingReportSchema.Columns);
        Assert.Contains(SocialPublishingReportSchema.ColWorkHours, SocialPublishingReportSchema.Columns);
        Assert.Contains(SocialPublishingReportSchema.ColStatus, SocialPublishingReportSchema.Columns);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, SocialPublishingReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2028-W04",
            new[] { SocialRowWU("عميل نشر", "50", "40", "5", "مشروع نشر", "10") });

        var report = await AggPodsAsync(admin, $"periodKey=2028-W04&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل نشر", row.Client);
        Assert.Equal("مشروع نشر", row.Project);
        Assert.Equal(10m, row.WorkHours);
        Assert.Equal(40m, row.PostsPublished);
    }

    // ===== (5) Media Buyer — عمود ساعات العمل موجود ويُقرأ =====
    [Fact]
    public async Task MediaBuyer_WorkHoursColumn_PresentAndAggregated()
    {
        Assert.Contains(MediaBuyerByClientReportSchema.ColProject, MediaBuyerByClientReportSchema.Columns);
        Assert.Contains(MediaBuyerByClientReportSchema.ColWorkHours, MediaBuyerByClientReportSchema.Columns);
        Assert.Contains(MediaBuyerByClientReportSchema.ColStatus, MediaBuyerByClientReportSchema.Columns);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, MediaBuyerByClientReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2028-W05",
            new[] { MediaRowWU("عميل ميديا", "Meta", "300", "60", "12", "3000", "مشروع ميديا", "9") });

        var report = await AggPodsAsync(admin, $"periodKey=2028-W05&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل ميديا", row.Client);
        Assert.Equal("مشروع ميديا", row.Project);
        Assert.Equal(9m, row.WorkHours);
        Assert.Equal(300m, row.Spend);
    }

    // ===== (6) المشاريع — لا يزال يحمل ساعات العمل =====
    [Fact]
    public async Task Projects_StillHasWorkHours()
    {
        Assert.Contains(ProjectsByClientReportSchema.ColProject, ProjectsByClientReportSchema.Columns);
        Assert.Contains(ProjectsByClientReportSchema.ColWorkHours, ProjectsByClientReportSchema.Columns);
        Assert.Contains(ProjectsByClientReportSchema.ColStatus, ProjectsByClientReportSchema.Columns);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ProjectsByClientReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2028-W06",
            new[] { ProjectRowWU("عميل مشاريع", "بوابة", "40", "10", "6", "2", "1", "50", "متوسط") });

        var report = await AggProjectsAsync(admin, $"periodKey=2028-W06&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل مشاريع", row.Client);
        Assert.Equal("بوابة", row.Project);
        Assert.Equal(40m, row.WorkHours);
    }

    // ===== (7) التجميع يجمع ساعات العمل عبر أكثر من قالب لنفس العميل/المشروع =====
    [Fact]
    public async Task Aggregation_SumsWorkHours_AcrossMultipleTemplates()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var content = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var projects = await GetTemplateByTitleAsync(admin, ProjectsByClientReportSchema.TemplateTitle);
        var contentGrid = GridField(content);
        var projectsGrid = GridField(projects);
        // موظّفان مختلفان (كلا القالبَين أساسيّ ⇒ لا يجوز تقريران أساسيان لنفس الموظّف/الفترة).
        // التجميع على مستوى العميل يجمع حسب (العميل، المشروع) عبر كامل النطاق بغضّ النظر عن الموظّف.
        var (emp1, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (emp2, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // اسم عميل/مشروع فريد لكل تشغيل — تجميع العميل يجمع عبر الموظفين فلا يمكن عزله بموظّف؛
        // العزل عبر اسم فريد يمنع تراكم قاعدة الاختبار المشتركة عبر إعادات التشغيل.
        var tag = Guid.NewGuid().ToString("N")[..8];
        var client = $"عميل موحّد {tag}";
        var project = $"مشروع موحّد {tag}";

        // نفس العميل + نفس المشروع من قالبَين مختلفَين ⇒ يُجمَع في صفّ عميل/مشروع واحد.
        await SubmitGridAsync(emp1, content.Id, contentGrid.Id, "2028-W07",
            new[] { ContentRowWU(client, "25", "20", "0", project, "10") });
        await SubmitGridAsync(emp2, projects.Id, projectsGrid.Id, "2028-W07",
            new[] { ProjectRowWU(client, project, "15", "10", "5", "1", "2", "50", "متوسط") });

        var report = await AggClientsAsync(admin, $"periodKey=2028-W07&client={client}&project={project}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(client, row.Client);
        Assert.Equal(project, row.Project);
        Assert.Equal(25m, row.TotalWorkHours);       // 10 (محتوى) + 15 (مشاريع)
        Assert.Equal(20m, row.TotalContentPieces);   // من قالب المحتوى
        Assert.Equal(2m, row.TotalBlockedTasks);     // من قالب المشاريع
    }

    // ===== (8) المحتوى لكل ساعة =====
    [Fact]
    public async Task ContentPerHour_Computed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2028-W08",
            new[] { ContentRowWU("عميل ساعة محتوى", "25", "20", "0", "مشروع", "10") });

        var report = await AggPodsAsync(admin, $"periodKey=2028-W08&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(2.00m, row.ProductivityIndicators.ContentPerHour); // 20 / 10
    }

    // ===== (9) التصميمات لكل ساعة =====
    [Fact]
    public async Task DesignsPerHour_Computed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, DesignProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2028-W09",
            new[] { DesignRowWU("عميل ساعة تصميم", "30", "15", "0", "مشروع", "10") });

        var report = await AggPodsAsync(admin, $"periodKey=2028-W09&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(1.50m, row.ProductivityIndicators.DesignsPerHour); // 15 / 10
    }

    // ===== (10) الفيديوهات لكل ساعة =====
    [Fact]
    public async Task VideosPerHour_Computed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, VideoProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2028-W10",
            new[] { VideoRowWU("عميل ساعة فيديو", "10", "8", "0", "مشروع", "4") });

        var report = await AggPodsAsync(admin, $"periodKey=2028-W10&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(2.00m, row.ProductivityIndicators.VideosPerHour); // 8 / 4
    }

    // ===== (11) المنشورات لكل ساعة =====
    [Fact]
    public async Task PostsPerHour_Computed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, SocialPublishingReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2028-W11",
            new[] { SocialRowWU("عميل ساعة نشر", "50", "20", "0", "مشروع", "5") });

        var report = await AggPodsAsync(admin, $"periodKey=2028-W11&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(4.00m, row.ProductivityIndicators.PostsPerHour); // 20 / 5
    }

    // ===== (12) صفّ قديم بلا ساعات عمل ⇒ يُعالَج بأمان (WorkHours=0، قسمة آمنة) بلا فشل =====
    [Fact]
    public async Task OldRow_WithoutWorkHours_HandledSafely()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // صفّ بترتيب قديم (9 خلايا فقط) بلا أعمدة Work Unit.
        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2028-W12",
            new[] { ContentRowLegacy("عميل قديم", "25", "20", "5") });

        var report = await AggPodsAsync(admin, $"periodKey=2028-W12&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل قديم", row.Client);
        Assert.Equal(string.Empty, row.Project);   // عمود المشروع خارج الحدود ⇒ فارغ
        Assert.Equal(0m, row.WorkHours);           // عمود الساعات خارج الحدود ⇒ 0
        Assert.Equal(20m, row.ContentPieces);      // الأعمدة القديمة تُقرأ كما هي
        Assert.Equal(0m, row.ProductivityIndicators.ContentPerHour); // قسمة آمنة على 0
    }

    // ===== (13) Phase 4 (B2C/B2B) غير متأثّرة =====
    [Fact]
    public async Task Phase4_B2cB2b_Unaffected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var b2c = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var b2cGrid = GridField(b2c);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // B2C: [الدورة، ساعات، Leads، Contacted، Qualified، Follow، Sales، Revenue، Lost، السبب]
        await SubmitGridAsync(emp, b2c.Id, b2cGrid.Id, "2028-W13",
            new[] { new[] { "دورة وحدة العمل", "40", "100", "80", "50", "10", "25", "8000", "10", "" } });

        var b2cReport = (await (await admin.GetAsync($"/api/reporting/aggregation/b2c?periodKey=2028-W13&employeeId={empId}"))
            .ReadAsync<B2cAggregationReport>())!;
        Assert.Single(b2cReport.Rows);
        Assert.Equal("دورة وحدة العمل", b2cReport.Rows[0].Course);
        Assert.Equal(40m, b2cReport.Rows[0].WorkHours);

        // محرّك Phase 5 لا يرى قالب B2C ⇒ لا صفوف.
        var pods = await AggPodsAsync(admin, $"periodKey=2028-W13&employeeId={empId}");
        Assert.Empty(pods.Rows);
    }

    // ===== (14) دورة حياة التسليم/الاعتماد غير متأثّرة، وساعات العمل تُقرأ من تسليم مُغلَق =====
    [Fact]
    public async Task CurrentWorkflow_SubmitApprove_Unaffected_AndWorkHoursRead()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(tpl.Id, PeriodType.Weekly, "2028-W14"))).ReadAsync<SubmissionDto>();
        var gridJson = JsonSerializer.Serialize(new[] { ContentRowWU("عميل اعتماد", "10", "8", "1", "مشروع اعتماد", "5") });
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(grid.Id, null, null, null, null, gridJson) }));
        var submitted = await (await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);

        var approved = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);

        var pods = await AggPodsAsync(admin, $"periodKey=2028-W14&employeeId={empId}");
        var row = Assert.Single(pods.Rows);
        Assert.Equal(8m, row.ContentPieces);
        Assert.Equal(5m, row.WorkHours);
        Assert.Equal("مشروع اعتماد", row.Project);
    }

    // ===== (15) القوالب القديمة غير مكسورة — صفوف بالترتيب القديم لا تزال تُجمَع بفهارسها الأصلية =====
    [Fact]
    public async Task OldTemplates_NotBroken_LegacyIndicesStable()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var grid = GridField(tpl);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // صفّ بالترتيب القديم: العميل(0)/معتمد(6)/متأخر(7) — إلحاق الأعمدة الجديدة لم يُزِح هذه الفهارس.
        await SubmitGridAsync(emp, tpl.Id, grid.Id, "2028-W15",
            new[] { ContentRowLegacy("عميل استقرار", "40", "30", "4") });

        var report = await AggPodsAsync(admin, $"periodKey=2028-W15&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل استقرار", row.Client);
        Assert.Equal(30m, row.ContentPieces);                     // فهرس المعتمد (6) ثابت
        Assert.Equal(4m, row.DelayedItems);                       // فهرس المتأخر (7) ثابت
        Assert.Equal(10.0m, row.ProductivityIndicators.DelayRate); // 4 / 40 = 10% (متأخر ÷ مطلوب)
    }
}
