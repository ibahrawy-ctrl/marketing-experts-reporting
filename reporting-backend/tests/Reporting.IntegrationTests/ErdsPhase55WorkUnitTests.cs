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
/// ERDS Phase 5.5 — تطبيع وحدة العمل (Work Unit) على القوالب التنفيذية القديمة الستة.
///
/// بعد RC-4 أصبحت هذه القوالب الستة <b>مؤرشفة</b> (Status=Archived, IsActive=false) فلا تسمح
/// بإنشاء تسليم جديد عبر الـAPI (الحارس المركزي يعيد <c>report.template_not_assigned</c> / HTTP 403).
/// إثبات الأرشفة + رفض الإنشاء للقوالب الستة كاملةً موجود في <see cref="ErdsPhase5PodExecutionTests"/>
/// (Theory واحدة تغطّي الستة) — لا يُكرَّر هنا. يضيف هذا الملف حارسًا واحدًا مختصرًا يربط قراءات
/// وحدة العمل التاريخية بأنها فعلًا على قالب مؤرشف (بيانات Legacy).
///
/// هذا الملف يُثبِت <b>القراءة التاريخية لوحدة العمل</b>: تُزرَع تسليمات Legacy (قبل الأرشفة) — تحمل أعمدة
/// وحدة العمل المُلحقة (المشروع/ساعات العمل/الحالة) — مباشرةً في قاعدة الاختبار المعزولة عبر
/// <see cref="LegacyExecutionFixture"/>، ويُثبَت أن محرّك التجميع (Phase 5) لا يزال يقرأ ساعات العمل +
/// المشروع من الكتل الإنتاجية الخمس + قالب المشاريع ويشتقّ مؤشّرات «لكل ساعة» بنفس الحسابات.
/// التوافق الخلفي: صفوف قديمة أقصر بلا عمود ساعات ⇒ 0 بلا فشل. Phase 4 (B2C/B2B) نشطة وغير متأثّرة.
///
/// كل اختبار يُنشئ موظّفًا جديدًا (GUID فريد) وفترة فريدة (2028-Wnn) لعزل قاعدة الاختبار المشتركة.
/// لا يمسّ أيّ تسليم/قالب/مسار اعتماد إنتاجيّ، ولا يُنشئ تسليمًا عبر الـAPI من قالب مؤرشف.
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

    /// <summary>يُنشئ مسودّة، يعبّئ الجدول، ثم يُرسِلها — يُستخدم لقوالب <b>نشطة</b> فقط (Phase 4 B2C/B2B).</summary>
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

    // ===== بُناة صفوف Legacy بترتيب Schema (مع أعمدة Work Unit المُلحقة: المشروع/ساعات العمل/الحالة) =====

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

    // ============================================================================
    //  (أ) حارس مختصر — قراءات وحدة العمل التاريخية تجري على قالب مؤرشف (بيانات Legacy)
    // ============================================================================

    /// <summary>
    /// القالب التنفيذيّ الأساسيّ لوحدة العمل (المحتوى) مؤرشف وغير نشط، ويرفض إنشاء تسليم جديد عبر الـAPI
    /// بكود الرفض الرسمي <c>report.template_not_assigned</c> / HTTP 403 — ما يثبت أن قراءات وحدة العمل
    /// التالية تجري على بيانات Legacy مزروعة (لا على إنشاء API من قالب مؤرشف). إثبات الستة كاملةً في Phase 5.
    /// </summary>
    [Fact]
    public async Task WorkUnitTemplate_IsArchived_AndRejectsNewSubmission()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        var (status, isActive) = await LegacyExecutionFixture.GetTemplateStatusAsync(
            _factory, ContentProductionReportSchema.TemplateTitle);
        Assert.Equal(TemplateStatus.Archived, status);
        Assert.False(isActive);

        var tpl = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var resp = await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(tpl.Id, PeriodType.Weekly, TestCalendar.Cycle(1)));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Contains("report.template_not_assigned", await resp.Content.ReadAsStringAsync());
    }

    // ============================================================================
    //  (ب) القراءة التاريخية لوحدة العمل — تُزرَع بيانات Legacy ويُثبَت قراءة ساعات العمل/المؤشّرات
    // ============================================================================

    // ===== (1) المحتوى — عمود ساعات العمل موجود ويُقرأ حتى التجميع =====
    [Fact]
    public async Task Content_HistoricalRead_WorkHoursColumn_PresentAndAggregated()
    {
        Assert.Contains(ContentProductionReportSchema.ColProject, ContentProductionReportSchema.Columns);
        Assert.Contains(ContentProductionReportSchema.ColWorkHours, ContentProductionReportSchema.Columns);
        Assert.Contains(ContentProductionReportSchema.ColStatus, ContentProductionReportSchema.Columns);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            ContentProductionReportSchema.TemplateTitle, ContentProductionReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(2),
            new[] { ContentRowWU("عميل محتوى", "25", "20", "5", "مشروع محتوى", "12") });

        var report = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(2)}&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل محتوى", row.Client);
        Assert.Equal("مشروع محتوى", row.Project);
        Assert.Equal(12m, row.WorkHours);
        Assert.Equal(20m, row.ContentPieces);
    }

    // ===== (2) التصميم — عمود ساعات العمل موجود ويُقرأ =====
    [Fact]
    public async Task Design_HistoricalRead_WorkHoursColumn_PresentAndAggregated()
    {
        Assert.Contains(DesignProductionReportSchema.ColProject, DesignProductionReportSchema.Columns);
        Assert.Contains(DesignProductionReportSchema.ColWorkHours, DesignProductionReportSchema.Columns);
        Assert.Contains(DesignProductionReportSchema.ColStatus, DesignProductionReportSchema.Columns);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            DesignProductionReportSchema.TemplateTitle, DesignProductionReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(3),
            new[] { DesignRowWU("عميل تصميم", "30", "24", "6", "مشروع تصميم", "8") });

        var report = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(3)}&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل تصميم", row.Client);
        Assert.Equal("مشروع تصميم", row.Project);
        Assert.Equal(8m, row.WorkHours);
        Assert.Equal(24m, row.DesignsCompleted);
    }

    // ===== (3) الفيديو — عمود ساعات العمل موجود ويُقرأ =====
    [Fact]
    public async Task Video_HistoricalRead_WorkHoursColumn_PresentAndAggregated()
    {
        Assert.Contains(VideoProductionReportSchema.ColProject, VideoProductionReportSchema.Columns);
        Assert.Contains(VideoProductionReportSchema.ColWorkHours, VideoProductionReportSchema.Columns);
        Assert.Contains(VideoProductionReportSchema.ColStatus, VideoProductionReportSchema.Columns);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            VideoProductionReportSchema.TemplateTitle, VideoProductionReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(4),
            new[] { VideoRowWU("عميل فيديو", "10", "8", "2", "مشروع فيديو", "6") });

        var report = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(4)}&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل فيديو", row.Client);
        Assert.Equal("مشروع فيديو", row.Project);
        Assert.Equal(6m, row.WorkHours);
        Assert.Equal(8m, row.VideosCompleted);
    }

    // ===== (4) النشر — عمود ساعات العمل موجود ويُقرأ =====
    [Fact]
    public async Task Publishing_HistoricalRead_WorkHoursColumn_PresentAndAggregated()
    {
        Assert.Contains(SocialPublishingReportSchema.ColProject, SocialPublishingReportSchema.Columns);
        Assert.Contains(SocialPublishingReportSchema.ColWorkHours, SocialPublishingReportSchema.Columns);
        Assert.Contains(SocialPublishingReportSchema.ColStatus, SocialPublishingReportSchema.Columns);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            SocialPublishingReportSchema.TemplateTitle, SocialPublishingReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(5),
            new[] { SocialRowWU("عميل نشر", "50", "40", "5", "مشروع نشر", "10") });

        var report = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(5)}&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل نشر", row.Client);
        Assert.Equal("مشروع نشر", row.Project);
        Assert.Equal(10m, row.WorkHours);
        Assert.Equal(40m, row.PostsPublished);
    }

    // ===== (5) Media Buyer — عمود ساعات العمل موجود ويُقرأ =====
    [Fact]
    public async Task MediaBuyer_HistoricalRead_WorkHoursColumn_PresentAndAggregated()
    {
        Assert.Contains(MediaBuyerByClientReportSchema.ColProject, MediaBuyerByClientReportSchema.Columns);
        Assert.Contains(MediaBuyerByClientReportSchema.ColWorkHours, MediaBuyerByClientReportSchema.Columns);
        Assert.Contains(MediaBuyerByClientReportSchema.ColStatus, MediaBuyerByClientReportSchema.Columns);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            MediaBuyerByClientReportSchema.TemplateTitle, MediaBuyerByClientReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(6),
            new[] { MediaRowWU("عميل ميديا", "Meta", "300", "60", "12", "3000", "مشروع ميديا", "9") });

        var report = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(6)}&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل ميديا", row.Client);
        Assert.Equal("مشروع ميديا", row.Project);
        Assert.Equal(9m, row.WorkHours);
        Assert.Equal(300m, row.Spend);
    }

    // ===== (6) المشاريع — لا يزال يحمل ساعات العمل =====
    [Fact]
    public async Task Projects_HistoricalRead_StillHasWorkHours()
    {
        Assert.Contains(ProjectsByClientReportSchema.ColProject, ProjectsByClientReportSchema.Columns);
        Assert.Contains(ProjectsByClientReportSchema.ColWorkHours, ProjectsByClientReportSchema.Columns);
        Assert.Contains(ProjectsByClientReportSchema.ColStatus, ProjectsByClientReportSchema.Columns);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            ProjectsByClientReportSchema.TemplateTitle, ProjectsByClientReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(7),
            new[] { ProjectRowWU("عميل مشاريع", "بوابة", "40", "10", "6", "2", "1", "50", "متوسط") });

        var report = await AggProjectsAsync(admin, $"periodKey={TestCalendar.Cycle(7)}&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل مشاريع", row.Client);
        Assert.Equal("بوابة", row.Project);
        Assert.Equal(40m, row.WorkHours);
    }

    // ===== (7) التجميع يجمع ساعات العمل عبر أكثر من قالب لنفس العميل/المشروع =====
    [Fact]
    public async Task Aggregation_HistoricalRead_SumsWorkHours_AcrossMultipleTemplates()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        // موظّفان مختلفان (كلا القالبَين أساسيّ ⇒ لا يجوز تقريران أساسيان لنفس الموظّف/الفترة).
        var (_, emp1Id) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, emp2Id) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // اسم عميل/مشروع فريد لكل تشغيل — تجميع العميل يجمع عبر الموظفين فلا يمكن عزله بموظّف؛
        // العزل عبر اسم فريد يمنع تراكم قاعدة الاختبار المشتركة عبر إعادات التشغيل.
        var tag = Guid.NewGuid().ToString("N")[..8];
        var client = $"عميل موحّد {tag}";
        var project = $"مشروع موحّد {tag}";

        // نفس العميل + نفس المشروع من قالبَين مختلفَين ⇒ يُجمَع في صفّ عميل/مشروع واحد.
        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            ContentProductionReportSchema.TemplateTitle, ContentProductionReportSchema.MainTableLabel,
            emp1Id, null, TestCalendar.Cycle(8),
            new[] { ContentRowWU(client, "25", "20", "0", project, "10") });
        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            ProjectsByClientReportSchema.TemplateTitle, ProjectsByClientReportSchema.MainTableLabel,
            emp2Id, null, TestCalendar.Cycle(8),
            new[] { ProjectRowWU(client, project, "15", "10", "5", "1", "2", "50", "متوسط") });

        var report = await AggClientsAsync(admin, $"periodKey={TestCalendar.Cycle(8)}&client={client}&project={project}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(client, row.Client);
        Assert.Equal(project, row.Project);
        Assert.Equal(25m, row.TotalWorkHours);       // 10 (محتوى) + 15 (مشاريع)
        Assert.Equal(20m, row.TotalContentPieces);   // من قالب المحتوى
        Assert.Equal(2m, row.TotalBlockedTasks);     // من قالب المشاريع
    }

    // ===== (8) المحتوى لكل ساعة =====
    [Fact]
    public async Task ContentPerHour_HistoricalRead_Computed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            ContentProductionReportSchema.TemplateTitle, ContentProductionReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(9),
            new[] { ContentRowWU("عميل ساعة محتوى", "25", "20", "0", "مشروع", "10") });

        var report = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(9)}&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(2.00m, row.ProductivityIndicators.ContentPerHour); // 20 / 10
    }

    // ===== (9) التصميمات لكل ساعة =====
    [Fact]
    public async Task DesignsPerHour_HistoricalRead_Computed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            DesignProductionReportSchema.TemplateTitle, DesignProductionReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(10),
            new[] { DesignRowWU("عميل ساعة تصميم", "30", "15", "0", "مشروع", "10") });

        var report = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(10)}&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(1.50m, row.ProductivityIndicators.DesignsPerHour); // 15 / 10
    }

    // ===== (10) الفيديوهات لكل ساعة =====
    [Fact]
    public async Task VideosPerHour_HistoricalRead_Computed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            VideoProductionReportSchema.TemplateTitle, VideoProductionReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(11),
            new[] { VideoRowWU("عميل ساعة فيديو", "10", "8", "0", "مشروع", "4") });

        var report = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(11)}&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(2.00m, row.ProductivityIndicators.VideosPerHour); // 8 / 4
    }

    // ===== (11) المنشورات لكل ساعة =====
    [Fact]
    public async Task PostsPerHour_HistoricalRead_Computed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            SocialPublishingReportSchema.TemplateTitle, SocialPublishingReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(12),
            new[] { SocialRowWU("عميل ساعة نشر", "50", "20", "0", "مشروع", "5") });

        var report = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(12)}&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal(4.00m, row.ProductivityIndicators.PostsPerHour); // 20 / 5
    }

    // ===== (12) صفّ قديم بلا ساعات عمل ⇒ يُعالَج بأمان (WorkHours=0، قسمة آمنة) بلا فشل =====
    [Fact]
    public async Task OldRow_HistoricalRead_WithoutWorkHours_HandledSafely()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // صفّ بترتيب قديم (9 خلايا فقط) بلا أعمدة Work Unit.
        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            ContentProductionReportSchema.TemplateTitle, ContentProductionReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(13),
            new[] { ContentRowLegacy("عميل قديم", "25", "20", "5") });

        var report = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(13)}&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل قديم", row.Client);
        Assert.Equal(string.Empty, row.Project);   // عمود المشروع خارج الحدود ⇒ فارغ
        Assert.Equal(0m, row.WorkHours);           // عمود الساعات خارج الحدود ⇒ 0
        Assert.Equal(20m, row.ContentPieces);      // الأعمدة القديمة تُقرأ كما هي
        Assert.Equal(0m, row.ProductivityIndicators.ContentPerHour); // قسمة آمنة على 0
    }

    // ===== (13) القراءة التاريخية لتسليم مُغلَق (Closed) — ساعات العمل تُقرأ =====
    [Fact]
    public async Task ClosedSubmission_HistoricalRead_WorkHoursRead()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // تسليم تاريخيّ اكتمل مساره سابقًا (Closed) — يقرؤه المحرّك (المُستبعَد هو Draft فقط).
        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            ContentProductionReportSchema.TemplateTitle, ContentProductionReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(14),
            new[] { ContentRowWU("عميل اعتماد", "10", "8", "1", "مشروع اعتماد", "5") },
            status: SubmissionStatus.Closed);

        var pods = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(14)}&employeeId={empId}");
        var row = Assert.Single(pods.Rows);
        Assert.Equal(8m, row.ContentPieces);
        Assert.Equal(5m, row.WorkHours);
        Assert.Equal("مشروع اعتماد", row.Project);
    }

    // ===== (14) القوالب القديمة غير مكسورة — صفوف بالترتيب القديم لا تزال تُجمَع بفهارسها الأصلية =====
    [Fact]
    public async Task OldTemplates_HistoricalRead_NotBroken_LegacyIndicesStable()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // صفّ بالترتيب القديم: العميل(0)/معتمد(6)/متأخر(7) — إلحاق الأعمدة الجديدة لم يُزِح هذه الفهارس.
        await LegacyExecutionFixture.SeedLegacyHistoricalGridAsync(_factory,
            ContentProductionReportSchema.TemplateTitle, ContentProductionReportSchema.MainTableLabel,
            empId, null, TestCalendar.Cycle(15),
            new[] { ContentRowLegacy("عميل استقرار", "40", "30", "4") });

        var report = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(15)}&employeeId={empId}");
        var row = Assert.Single(report.Rows);
        Assert.Equal("عميل استقرار", row.Client);
        Assert.Equal(30m, row.ContentPieces);                     // فهرس المعتمد (6) ثابت
        Assert.Equal(4m, row.DelayedItems);                       // فهرس المتأخر (7) ثابت
        Assert.Equal(10.0m, row.ProductivityIndicators.DelayRate); // 4 / 40 = 10% (متأخر ÷ مطلوب)
    }

    // ============================================================================
    //  (ج) عدم التأثّر — Phase 4 (B2C/B2B) نشطة عبر الـAPI الطبيعي
    // ============================================================================

    // ===== (15) Phase 4 (B2C/B2B) غير متأثّرة =====
    [Fact]
    public async Task Phase4_B2cB2b_Unaffected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var b2c = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var b2cGrid = GridField(b2c);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // B2C: [الدورة، ساعات، Leads، Contacted، Qualified، Follow، Sales، Revenue، Lost، السبب]
        await SubmitGridAsync(emp, b2c.Id, b2cGrid.Id, TestCalendar.Cycle(16),
            new[] { new[] { "دورة وحدة العمل", "40", "100", "80", "50", "10", "25", "8000", "10", "" } });

        var b2cReport = (await (await admin.GetAsync($"/api/reporting/aggregation/b2c?periodKey={TestCalendar.Cycle(16)}&employeeId={empId}"))
            .ReadAsync<B2cAggregationReport>())!;
        Assert.Single(b2cReport.Rows);
        Assert.Equal("دورة وحدة العمل", b2cReport.Rows[0].Course);
        Assert.Equal(40m, b2cReport.Rows[0].WorkHours);

        // محرّك Phase 5 لا يرى قالب B2C ⇒ لا صفوف.
        var pods = await AggPodsAsync(admin, $"periodKey={TestCalendar.Cycle(16)}&employeeId={empId}");
        Assert.Empty(pods.Rows);
    }
}
