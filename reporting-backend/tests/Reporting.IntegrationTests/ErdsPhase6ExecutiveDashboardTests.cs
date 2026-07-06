using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Common;
using Reporting.Application.Dashboard;
using Reporting.Application.Reports;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// ERDS Phase 6 — اللوحة التنفيذية (Preview). طبقة عرض قراءة-فقط فوق محرّكَي التجميع
/// (Phase 4 مبيعات B2C/B2B، Phase 5/5.5 تنفيذ Pods/عملاء/مشاريع). تتحقّق هذه الاختبارات من:
/// السبع لوحات (overview/sales/pods/clients/projects/workload/risks)، أنّ اللوحة تركّب أرقام المحرّك
/// كما هي (لا إعادة حساب)، أنّ النطاق (Scope) محكوم (موظّف يرى بياناته فقط بينما الأدمن يرى الكل)،
/// وأنّ نقاط التجميع القائمة غير متأثّرة. كل اختبار يستخدم موظّفًا فريدًا (GUID) وفترة/عميلًا فريدَين
/// (وسم فريد) لعزل قاعدة الاختبار المشتركة.
/// </summary>
[Collection("Integration")]
public class ErdsPhase6ExecutiveDashboardTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ErdsPhase6ExecutiveDashboardTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== أدوات مشتركة (مأخوذة بنفس نمط Phase 5.5) =====

    private static async Task<ReportTemplateDetailDto> GetTemplateByTitleAsync(HttpClient admin, string title)
    {
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == title));
        return (await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>())!;
    }

    private static TemplateFieldDto GridField(ReportTemplateDetailDto t)
        => t.Versions.Single(v => v.IsPublished).Fields.Single(f => f.FieldType == FieldType.TableGrid);

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

    // بُناة صفوف (نفس ترتيب Schema المُلحق بأعمدة Work Unit في نهاية القوالب).
    private static string[] ContentRowWU(string client, string required, string approved, string late, string project, string hours)
        => new[] { client, required, "0", "0", "0", "0", approved, late, "", project, hours, "" };

    private static string[] MediaRowWU(string client, string platform, string spend, string leads, string purchases, string revenue, string project, string hours)
        => new[] { client, platform, spend, leads, "0", purchases, "0", revenue, "0", "", project, hours, "" };

    private static string[] ProjectRowWU(string client, string project, string hours, string planned, string done,
        string late, string blocked, string progress, string risk)
        => new[] { client, project, hours, planned, done, late, blocked, progress, risk, "", "" };

    // B2C (10 أعمدة): [الدورة، ساعات، Leads، Contacted، Qualified، Follow، Sales، Revenue، Lost، السبب]
    // قيود التحقّق: Contacted≤Leads، Qualified≤Contacted، Sales≤Qualified، Lost≤Leads ⇒ نجعل القمع مساويًا لـLeads.
    private static string[] B2cRow(string course, string hours, string leads, string sales, string revenue)
        => new[] { course, hours, leads, leads, leads, "0", sales, revenue, "0", "" };

    // B2B (10 أعمدة): [الخدمة، ساعات، Leads، Meetings، Proposals، Negotiation، Won، Lost، Revenue، Next]
    private static string[] B2bRow(string service, string hours, string leads, string won, string revenue)
        => new[] { service, hours, leads, "0", "0", "0", won, "0", revenue, "" };

    private static async Task<T> GetDashboardAsync<T>(HttpClient client, string path, string query)
        => (await (await client.GetAsync($"/api/dashboard/{path}?{query}")).ReadAsync<T>())!;

    // ===== (1) Overview — يركّب مجاميع التنفيذ + المبيعات =====
    [Fact]
    public async Task Overview_ComposesExecutionAndSales()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var content = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var b2c = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var b2b = await GetTemplateByTitleAsync(admin, B2bByServiceReportSchema.TemplateTitle);
        // ثلاثة قوالب أساسية ⇒ ثلاثة موظّفين (لا تقريران أساسيان لموظّف/فترة واحدة).
        var (e1, id1) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (e2, id2) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (e3, id3) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(e1, content.Id, GridField(content).Id, "2029-W01",
            new[] { ContentRowWU("عميل نظرة", "25", "20", "0", "مشروع نظرة", "12") });
        await SubmitGridAsync(e2, b2c.Id, GridField(b2c).Id, "2029-W01",
            new[] { B2cRow("دورة نظرة", "40", "100", "25", "8000") });
        await SubmitGridAsync(e3, b2b.Id, GridField(b2b).Id, "2029-W01",
            new[] { B2bRow("خدمة نظرة", "30", "50", "10", "5000") });

        // الأدمن يرى الكل؛ لكن دمج ثلاثة موظّفين يتطلّب استعلامًا بلا employeeId مقيّدًا بالفترة فقط.
        // نعزل عبر الجمع اليدوي: كل واحد على حدة ثم نتحقّق من التركيب على e1 (محتوى) وحده أولًا.
        var ov1 = await GetDashboardAsync<DashboardOverviewDto>(admin, "overview", $"periodKey=2029-W01&employeeId={id1}");
        Assert.Equal(12m, ov1.WorkHours);
        Assert.Equal(1, ov1.Clients);
        Assert.Equal(1, ov1.Projects);
        Assert.Equal(20m, ov1.Content);
        Assert.Equal("summary", ov1.ViewLevel); // الأدمن = governance ⇒ summary

        var ov2 = await GetDashboardAsync<DashboardOverviewDto>(admin, "overview", $"periodKey=2029-W01&employeeId={id2}");
        Assert.Equal(40m, ov2.WorkHours);   // ساعات B2C
        Assert.Equal(100m, ov2.Leads);
        Assert.Equal(25m, ov2.Sales);
        Assert.Equal(8000m, ov2.Revenue);

        var ov3 = await GetDashboardAsync<DashboardOverviewDto>(admin, "overview", $"periodKey=2029-W01&employeeId={id3}");
        Assert.Equal(30m, ov3.WorkHours);   // ساعات B2B
        Assert.Equal(50m, ov3.Leads);
        Assert.Equal(10m, ov3.Sales);       // Won
        Assert.Equal(5000m, ov3.Revenue);
    }

    // ===== (2) Sales — B2C + B2B مع المؤشرات =====
    [Fact]
    public async Task Sales_B2cAndB2b_WithKpis()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var b2c = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var b2b = await GetTemplateByTitleAsync(admin, B2bByServiceReportSchema.TemplateTitle);
        var (e1, id1) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (e2, id2) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(e1, b2c.Id, GridField(b2c).Id, "2029-W02",
            new[] { B2cRow("دورة مبيعات", "40", "100", "25", "8000") });
        await SubmitGridAsync(e2, b2b.Id, GridField(b2b).Id, "2029-W02",
            new[] { B2bRow("خدمة مبيعات", "30", "60", "10", "5000") });

        var s1 = await GetDashboardAsync<DashboardSalesDto>(admin, "sales", $"periodKey=2029-W02&employeeId={id1}");
        var b2cRow = Assert.Single(s1.B2c);
        Assert.Equal("دورة مبيعات", b2cRow.Item);
        Assert.Equal(25m, b2cRow.Sales);
        Assert.Equal(8000m, b2cRow.Revenue);
        Assert.Empty(s1.B2b);
        Assert.Equal(25m, s1.Kpis.B2cSales);
        Assert.Equal(8000m, s1.Kpis.B2cRevenue);

        var s2 = await GetDashboardAsync<DashboardSalesDto>(admin, "sales", $"periodKey=2029-W02&employeeId={id2}");
        var b2bRow = Assert.Single(s2.B2b);
        Assert.Equal("خدمة مبيعات", b2bRow.Item);
        Assert.Equal(10m, b2bRow.Sales);       // Won مطبّع في حقل Sales الموحّد
        Assert.Equal(5000m, b2bRow.Revenue);
        Assert.Equal(10m, s2.Kpis.B2bWon);
        Assert.Equal(5000m, s2.Kpis.B2bRevenue);
        Assert.Equal(60m, s2.Kpis.TotalLeads);
    }

    // ===== (3) Pods — تجميع الفريق + الإنتاجية =====
    [Fact]
    public async Task Pods_GroupingAndProductivity()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var content = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, content.Id, GridField(content).Id, "2029-W03",
            new[] { ContentRowWU("عميل بود", "25", "20", "2", "مشروع بود", "10") });

        var pods = await GetDashboardAsync<DashboardPodsDto>(admin, "pods", $"periodKey=2029-W03&employeeId={empId}");
        var pod = Assert.Single(pods.Pods);
        Assert.Equal(10m, pod.WorkHours);
        Assert.Equal(20m, pod.Content);
        Assert.Equal(2m, pod.Delayed);
        Assert.Equal(2.00m, pod.Productivity); // (محتوى 20 + 0 + 0 + 0) / 10
    }

    // ===== (4) Clients — تجميع لكل عميل (Spend/Revenue) =====
    [Fact]
    public async Task Clients_PerClientAggregation()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var media = await GetTemplateByTitleAsync(admin, MediaBuyerByClientReportSchema.TemplateTitle);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var client = $"عميل لوحة {tag}";

        await SubmitGridAsync(emp, media.Id, GridField(media).Id, "2029-W04",
            new[] { MediaRowWU(client, "Meta", "300", "60", "12", "3000", $"مشروع {tag}", "9") });

        var res = await GetDashboardAsync<DashboardClientsDto>(admin, "clients", $"periodKey=2029-W04&client={client}");
        var row = Assert.Single(res.Clients);
        Assert.Equal(client, row.Client);
        Assert.Equal(9m, row.WorkHours);
        Assert.Equal(300m, row.Spend);
        Assert.Equal(3000m, row.Revenue);
        Assert.Equal(1, row.Projects);
    }

    // ===== (5) Projects — الإيراد مُركّب من تجميع العملاء لكل (عميل/مشروع) =====
    [Fact]
    public async Task Projects_RevenueComposedFromClientAggregation()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var projects = await GetTemplateByTitleAsync(admin, ProjectsByClientReportSchema.TemplateTitle);
        var media = await GetTemplateByTitleAsync(admin, MediaBuyerByClientReportSchema.TemplateTitle);
        var (e1, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (e2, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var client = $"عميل مشروع {tag}";
        var project = $"مشروع {tag}";

        await SubmitGridAsync(e1, projects.Id, GridField(projects).Id, "2029-W05",
            new[] { ProjectRowWU(client, project, "40", "10", "6", "2", "1", "50", "متوسط") });
        // الميديا تعطي إيرادًا على مستوى (العميل/المشروع) يُركّبه محرّك العملاء.
        await SubmitGridAsync(e2, media.Id, GridField(media).Id, "2029-W05",
            new[] { MediaRowWU(client, "Meta", "200", "40", "8", "3000", project, "9") });

        var res = await GetDashboardAsync<DashboardProjectsDto>(admin, "projects", $"periodKey=2029-W05&client={client}");
        var row = Assert.Single(res.Projects);
        Assert.Equal(client, row.Client);
        Assert.Equal(project, row.Project);
        Assert.Equal(40m, row.WorkHours);       // من قالب المشاريع فقط
        Assert.Equal(1m, row.BlockedTasks);
        Assert.Equal(3000m, row.Revenue);       // مُركّب من تجميع العملاء
    }

    // ===== (6) Workload — عبء العمل لكل موظّف (وحدات العمل/الإنتاجية) =====
    [Fact]
    public async Task Workload_PerEmployee()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var content = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, content.Id, GridField(content).Id, "2029-W06",
            new[] { ContentRowWU("عميل عبء", "25", "20", "0", "مشروع عبء", "10") });

        var res = await GetDashboardAsync<DashboardWorkloadDto>(admin, "workload", $"periodKey=2029-W06&employeeId={empId}");
        var e = Assert.Single(res.Employees);
        Assert.Equal(empId, e.EmployeeId);
        Assert.Equal(10m, e.TotalWorkHours);
        Assert.Equal(1, e.ClientsCount);
        Assert.Equal(1, e.WorkUnits);
        Assert.Equal(2.00m, e.Productivity); // 20 / 10
        Assert.Single(res.Teams);
    }

    // ===== (7) Risks — القوائم العليا (مشاريع خطرة/عملاء متأخّرون/معدّل تأخير) =====
    [Fact]
    public async Task Risks_TopLists()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var content = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var projects = await GetTemplateByTitleAsync(admin, ProjectsByClientReportSchema.TemplateTitle);
        var (e1, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (e2, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var client = $"عميل خطر {tag}";
        var project = $"مشروع خطر {tag}";

        // محتوى: متأخرات + مطلوب ⇒ DelayRate = 4/40 = 10% (مؤشّر محرّك جاهز).
        await SubmitGridAsync(e1, content.Id, GridField(content).Id, "2029-W07",
            new[] { ContentRowWU(client, "40", "30", "4", "مشروع محتوى", "10") });
        // مشاريع: خطر مرتفع + متأخّر + متوقّف.
        await SubmitGridAsync(e2, projects.Id, GridField(projects).Id, "2029-W07",
            new[] { ProjectRowWU(client, project, "40", "10", "5", "3", "2", "40", "مرتفع") });

        var res = await GetDashboardAsync<DashboardRisksDto>(admin, "risks", $"periodKey=2029-W07&client={client}");

        var risky = Assert.Single(res.TopRiskyProjects.Where(p => p.Project == project));
        Assert.Equal("مرتفع", risky.RiskLevel);
        Assert.Equal(3m, risky.DelayedTasks);
        Assert.Equal(2m, risky.BlockedTasks);

        var delayedClient = Assert.Single(res.TopDelayedClients.Where(c => c.Client == client));
        Assert.Equal(4m, delayedClient.DelayedItems);

        var blocked = Assert.Single(res.TopBlockedTasks.Where(b => b.Project == project));
        Assert.Equal(2m, blocked.BlockedTasks);

        Assert.Contains(res.TopDelayRate, d => d.Client == client && d.DelayRate == 10.0m);
        Assert.NotEmpty(res.TopPressuredPods);
    }

    // ===== (8) النطاق — الموظّف يرى بياناته فقط بينما الأدمن يرى الكل =====
    [Fact]
    public async Task Scope_EmployeeSeesOnlyOwn_AdminSeesAll()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var content = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var (empA, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (empB, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var client = $"عميل نطاق {tag}";

        // موظّفان يُسلّمان لنفس العميل/الفترة بقيم مختلفة.
        await SubmitGridAsync(empA, content.Id, GridField(content).Id, "2029-W08",
            new[] { ContentRowWU(client, "25", "20", "0", "مشروع أ", "10") });
        await SubmitGridAsync(empB, content.Id, GridField(content).Id, "2029-W08",
            new[] { ContentRowWU(client, "10", "8", "0", "مشروع ب", "5") });

        // الأدمن (SeesAll) يرى مجموع الاثنين: 10 + 5 = 15 ساعة، 20 + 8 = 28 محتوى.
        var adminView = await GetDashboardAsync<DashboardClientsDto>(admin, "clients", $"periodKey=2029-W08&client={client}");
        var adminRow = Assert.Single(adminView.Clients);
        Assert.Equal(15m, adminRow.WorkHours);
        Assert.Equal(28m, adminRow.Content);
        Assert.Equal("summary", adminView.ViewLevel);

        // الموظّف أ (نطاق own) يرى تسليمه فقط: 10 ساعة، 20 محتوى.
        var aView = await GetDashboardAsync<DashboardClientsDto>(empA, "clients", $"periodKey=2029-W08&client={client}");
        var aRow = Assert.Single(aView.Clients);
        Assert.Equal(10m, aRow.WorkHours);
        Assert.Equal(20m, aRow.Content);
        Assert.Equal("self", aView.ViewLevel);
    }

    // ===== (9) عدم إعادة الحساب — أرقام اللوحة = أرقام محرّك التجميع تمامًا =====
    [Fact]
    public async Task NoRecompute_DashboardMatchesAggregationEngine()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var content = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, content.Id, GridField(content).Id, "2029-W09",
            new[] { ContentRowWU("عميل تطابق", "25", "18", "3", "مشروع تطابق", "9") });

        // مصدر الحقيقة = محرّك Phase 5 مباشرة.
        var engine = (await (await admin.GetAsync($"/api/reporting/aggregation/pods?periodKey=2029-W09&employeeId={empId}"))
            .ReadAsync<PodExecutionReport>())!;
        var engineRow = Assert.Single(engine.Rows);

        // اللوحة تُعيد نفس المجاميع دون إعادة حساب.
        var pods = await GetDashboardAsync<DashboardPodsDto>(admin, "pods", $"periodKey=2029-W09&employeeId={empId}");
        var pod = Assert.Single(pods.Pods);
        Assert.Equal(engineRow.WorkHours, pod.WorkHours);
        Assert.Equal(engineRow.ContentPieces, pod.Content);
        Assert.Equal(engineRow.DelayedItems, pod.Delayed);

        var ov = await GetDashboardAsync<DashboardOverviewDto>(admin, "overview", $"periodKey=2029-W09&employeeId={empId}");
        Assert.Equal(engineRow.WorkHours, ov.WorkHours);
        Assert.Equal(engineRow.ContentPieces, ov.Content);
    }

    // ===== (10) عدم التأثير — نقاط التجميع القائمة تعمل كما هي بعد استدعاء اللوحة =====
    [Fact]
    public async Task NoImpact_AggregationEndpointsUnaffected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var content = await GetTemplateByTitleAsync(admin, ContentProductionReportSchema.TemplateTitle);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitGridAsync(emp, content.Id, GridField(content).Id, "2029-W10",
            new[] { ContentRowWU("عميل عدم تأثير", "25", "22", "1", "مشروع", "8") });

        // استدعاء اللوحة أولًا.
        _ = await GetDashboardAsync<DashboardPodsDto>(admin, "pods", $"periodKey=2029-W10&employeeId={empId}");

        // ثم نقطة التجميع القائمة — يجب أن تبقى سليمة تمامًا.
        var engineResp = await admin.GetAsync($"/api/reporting/aggregation/pods?periodKey=2029-W10&employeeId={empId}");
        Assert.Equal(HttpStatusCode.OK, engineResp.StatusCode);
        var engine = (await engineResp.ReadAsync<PodExecutionReport>())!;
        var row = Assert.Single(engine.Rows);
        Assert.Equal("عميل عدم تأثير", row.Client);
        Assert.Equal(8m, row.WorkHours);
        Assert.Equal(22m, row.ContentPieces);
    }
}
