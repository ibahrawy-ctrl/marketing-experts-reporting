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
/// RC-3 Task 2A — قالب «تقرير مبيعات B2B — حسب مصدر البيانات» (جدولان مستقلّان: New Leads + Data Scraping) + تجميع مفصول المصدر.
/// يتحقّق من: بذر القالب بجدولين اختياريين، منتقي «الخدمة» من الكتالوج في الجدولين، إرسال مصدر واحد فقط أو كليهما،
/// رفض القيم غير المنطقية، فصل التجميع (New/Data/Legacy) وجمع الإجمالي، وDrill-down لكل موظّف — مع عدم كسر القالب القديم أحادي الجدول ولا B2C ولا كتالوج الخدمات.
/// أسماء خدمات فريدة لكل تشغيل لعزل قاعدة الاختبار المشتركة المتراكمة.
/// </summary>
[Collection("Integration")]
public class B2bBySourceTests
{
    private readonly CustomWebApplicationFactory _factory;

    public B2bBySourceTests(CustomWebApplicationFactory factory) => _factory = factory;

    private record GridConfig(string[] Columns);

    private static string Uniq(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static async Task<ReportTemplateDetailDto> GetTemplateByTitleAsync(HttpClient admin, string title)
    {
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == title));
        return (await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>())!;
    }

    private static TemplateVersionDto PublishedVersion(ReportTemplateDetailDto t)
        => t.Versions.Single(v => v.IsPublished);

    private static TemplateFieldDto GridByLabel(ReportTemplateDetailDto t, string label)
        => PublishedVersion(t).Fields.Single(f => f.FieldType == FieldType.TableGrid && f.Label == label);

    private static string[] ConfigColumns(TemplateFieldDto grid)
        => JsonSerializer.Deserialize<GridConfig>(grid.ConfigJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Columns;

    // صفّ New Leads: [الخدمة، ساعات العمل، New Leads، Contacted، Meetings، Proposals، Negotiation، Won، Revenue] (9 أعمدة)
    private static string[] NewRow(string service, string work, string newLeads, string contacted, string meetings,
        string proposals, string negotiation, string won, string revenue)
        => new[] { service, work, newLeads, contacted, meetings, proposals, negotiation, won, revenue };

    // صفّ Data Scraping: [الخدمة، ساعات العمل، Scraped Leads، Valid Leads، Contacted، Meetings، Proposals، Negotiation، Won، Revenue] (10 أعمدة)
    private static string[] DataRow(string service, string work, string scraped, string valid, string contacted,
        string meetings, string proposals, string negotiation, string won, string revenue)
        => new[] { service, work, scraped, valid, contacted, meetings, proposals, negotiation, won, revenue };

    /// <summary>يُنشئ موظّفًا، مسودّة على القالب الجديد، يعبّئ الجدولين (يجوز تمرير صفوف فارغة لأحدهما)، ثم يُرسِل.</summary>
    private async Task<Guid> SubmitSourcesAsync(Guid managerId, ReportTemplateDetailDto tpl, string periodKey,
        string[][] newRows, string[][] dataRows)
    {
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var newGrid = GridByLabel(tpl, B2bBySourceReportSchema.NewLeadsTableLabel);
        var dataGrid = GridByLabel(tpl, B2bBySourceReportSchema.DataScrapingTableLabel);

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(tpl.Id, PeriodType.Weekly, periodKey))).ReadAsync<SubmissionDto>();

        var save = await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(newGrid.Id, null, null, null, null, JsonSerializer.Serialize(newRows)),
                new FieldValueInput(dataGrid.Id, null, null, null, null, JsonSerializer.Serialize(dataRows)),
            }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var submitted = await (await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);
        return empId;
    }

    private static async Task<B2bSourceReport> AggBySourceAsync(HttpClient client, string query)
        => (await (await client.GetAsync($"/api/reporting/aggregation/b2b/by-source?{query}")).ReadAsync<B2bSourceReport>())!;

    // قيم أساس صالحة السلسلة (تُعاد استخدامها عبر عدّة اختبارات).
    private static string[][] StandardNew(string service)
        => new[] { NewRow(service, "40", "100", "80", "50", "20", "10", "8", "8000") };

    private static string[][] StandardData(string service)
        => new[] { DataRow(service, "20", "200", "120", "60", "30", "10", "5", "4", "4000") };

    private static readonly string[][] EmptyRows = Array.Empty<string[]>();

    // ===== (1) القالب الجديد فيه جدول New Leads بأعمدته المتوقّعة (اختياري) =====
    [Fact]
    public async Task NewTemplate_HasNewLeadsTable_WithExpectedColumns()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        Assert.Equal(TemplateStatus.Published, tpl.Status);

        var newGrid = GridByLabel(tpl, B2bBySourceReportSchema.NewLeadsTableLabel);
        Assert.False(newGrid.IsRequired); // جدول اختياري (يجوز إرسال مصدر واحد فقط)
        Assert.Equal(B2bBySourceReportSchema.NewLeadsColumns, ConfigColumns(newGrid));
    }

    // ===== (2) القالب الجديد فيه جدول Data Scraping بأعمدته المتوقّعة (اختياري) =====
    [Fact]
    public async Task NewTemplate_HasDataScrapingTable_WithExpectedColumns()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);

        var grids = PublishedVersion(tpl).Fields.Where(f => f.FieldType == FieldType.TableGrid).ToList();
        Assert.Equal(2, grids.Count);

        var dataGrid = GridByLabel(tpl, B2bBySourceReportSchema.DataScrapingTableLabel);
        Assert.False(dataGrid.IsRequired);
        Assert.Equal(B2bBySourceReportSchema.DataScrapingColumns, ConfigColumns(dataGrid));
    }

    // ===== (3) عمود «الخدمة» (منتقي من الكتالوج) هو العمود الأول في كلا الجدولين + الكتالوج متاح كمصدر للمنسدل =====
    [Fact]
    public async Task ServiceColumn_IsFirstInBothTables_AndCatalogAvailable()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);

        var newCols = ConfigColumns(GridByLabel(tpl, B2bBySourceReportSchema.NewLeadsTableLabel));
        var dataCols = ConfigColumns(GridByLabel(tpl, B2bBySourceReportSchema.DataScrapingTableLabel));
        Assert.Equal(B2bBySourceReportSchema.ColService, newCols[0]);
        Assert.Equal(B2bBySourceReportSchema.ColService, dataCols[0]);

        // مصدر المنسدل = كتالوج الخدمات (قابل للقراءة).
        var resp = await admin.GetAsync("/api/services");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.NotNull(await resp.Content.ReadFromJsonAsync<List<JsonElement>>());
    }

    // ===== (4) إرسال New Leads فقط (Data Scraping فارغ) ينجح =====
    [Fact]
    public async Task Submit_NewLeadsOnly_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var service = Uniq("خدمة");

        var empId = await SubmitSourcesAsync(ceoId, tpl, "2026-W10", StandardNew(service), EmptyRows);

        var r = await AggBySourceAsync(admin, $"periodKey=2026-W10&service={Uri.EscapeDataString(service)}");
        Assert.Equal(100m, r.NewLeadsTotals.Leads);
        Assert.Equal(0m, r.DataScrapingTotals.Leads);
    }

    // ===== (5) إرسال Data Scraping فقط (New Leads فارغ) ينجح =====
    [Fact]
    public async Task Submit_DataScrapingOnly_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var service = Uniq("خدمة");

        await SubmitSourcesAsync(ceoId, tpl, "2026-W11", EmptyRows, StandardData(service));

        var r = await AggBySourceAsync(admin, $"periodKey=2026-W11&service={Uri.EscapeDataString(service)}");
        Assert.Equal(0m, r.NewLeadsTotals.Leads);
        Assert.Equal(200m, r.DataScrapingTotals.Leads);
        Assert.Equal(120m, r.DataScrapingTotals.ValidLeads);
    }

    // ===== (6) إرسال الجدولين معًا يُخزّن القيم كما هي لكلا الجدولين =====
    [Fact]
    public async Task Submit_BothTables_StoresTabularValues()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var service = Uniq("خدمة");
        var newRows = StandardNew(service);
        var dataRows = StandardData(service);

        var (emp, _) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);
        var newGrid = GridByLabel(tpl, B2bBySourceReportSchema.NewLeadsTableLabel);
        var dataGrid = GridByLabel(tpl, B2bBySourceReportSchema.DataScrapingTableLabel);
        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(tpl.Id, PeriodType.Weekly, "2026-W12"))).ReadAsync<SubmissionDto>();
        var save = await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(newGrid.Id, null, null, null, null, JsonSerializer.Serialize(newRows)),
                new FieldValueInput(dataGrid.Id, null, null, null, null, JsonSerializer.Serialize(dataRows)),
            }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var submitted = await (await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);

        var list = await (await emp.GetAsync("/api/submissions?period=2026-W12")).ReadAsync<List<SubmissionDto>>();
        var sub = Assert.Single(list!.Where(s => s.TemplateTitle == tpl.Title));
        var full = await (await emp.GetAsync($"/api/submissions/{sub.Id}")).ReadAsync<SubmissionDto>();
        var storedNew = Assert.Single(full!.FieldValues.Where(v => v.TemplateFieldId == newGrid.Id)).ValueJson;
        var storedData = Assert.Single(full.FieldValues.Where(v => v.TemplateFieldId == dataGrid.Id)).ValueJson;
        Assert.Equal(newRows, JsonSerializer.Deserialize<string[][]>(storedNew!));
        Assert.Equal(dataRows, JsonSerializer.Deserialize<string[][]>(storedData!));
    }

    // ===== (7) قيم غير منطقية (Contacted > New Leads) ⇒ 400 (رفض) =====
    [Fact]
    public async Task Submit_IllogicalValues_Rejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var newGrid = GridByLabel(tpl, B2bBySourceReportSchema.NewLeadsTableLabel);
        var dataGrid = GridByLabel(tpl, B2bBySourceReportSchema.DataScrapingTableLabel);
        var service = Uniq("خدمة");

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(tpl.Id, PeriodType.Weekly, "2026-W13"))).ReadAsync<SubmissionDto>();

        // Contacted(200) > New Leads(100) ⇒ سلسلة مخالفة.
        var badNew = new[] { NewRow(service, "40", "100", "200", "50", "20", "10", "8", "8000") };
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(newGrid.Id, null, null, null, null, JsonSerializer.Serialize(badNew)),
                new FieldValueInput(dataGrid.Id, null, null, null, null, JsonSerializer.Serialize(EmptyRows)),
            }));

        var submit = await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
    }

    // ===== (8) التجميع يفصل المصدرين بمؤشرات مستقلّة =====
    [Fact]
    public async Task Aggregation_DistinguishesSources()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var service = Uniq("خدمة");

        await SubmitSourcesAsync(ceoId, tpl, "2026-W14", StandardNew(service), StandardData(service));

        var r = await AggBySourceAsync(admin, $"periodKey=2026-W14&service={Uri.EscapeDataString(service)}");
        var row = Assert.Single(r.Services);
        Assert.Equal(service, row.Service);

        // دلو New Leads (Leads = New Leads).
        Assert.Equal(100m, row.NewLeads.Leads);
        Assert.Equal(0m, row.NewLeads.ValidLeads);
        Assert.Equal(8m, row.NewLeads.Won);
        Assert.Equal(8000m, row.NewLeads.Revenue);
        Assert.Equal(40.0m, row.NewLeads.WinRate);        // Won/Proposals = 8/20
        Assert.Equal(200.00m, row.NewLeads.RevenuePerHour); // 8000/40

        // دلو Data Scraping (Leads = Scraped، مع Valid Leads).
        Assert.Equal(200m, row.DataScraping.Leads);
        Assert.Equal(120m, row.DataScraping.ValidLeads);
        Assert.Equal(4m, row.DataScraping.Won);
        Assert.Equal(4000m, row.DataScraping.Revenue);
    }

    // ===== (9) التجميع يجمع الإجمالي (Total = New + Data + Legacy) بصورة صحيحة =====
    [Fact]
    public async Task Aggregation_SumsTotalCorrectly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var service = Uniq("خدمة");

        await SubmitSourcesAsync(ceoId, tpl, "2026-W15", StandardNew(service), StandardData(service));

        var r = await AggBySourceAsync(admin, $"periodKey=2026-W15&service={Uri.EscapeDataString(service)}");
        var row = Assert.Single(r.Services);

        // Total = New(40h,100 leads,8 won,8000) + Data(20h,200 leads,4 won,4000).
        Assert.Equal(60m, row.Total.WorkHours);
        Assert.Equal(300m, row.Total.Leads);
        Assert.Equal(12m, row.Total.Won);
        Assert.Equal(12000m, row.Total.Revenue);
        Assert.Equal(200.00m, row.Total.RevenuePerHour); // 12000/60

        // الإجماليات على مستوى التقرير تعكس الفصل والجمع.
        Assert.Equal(100m, r.NewLeadsTotals.Leads);
        Assert.Equal(200m, r.DataScrapingTotals.Leads);
        Assert.Equal(0m, r.LegacyTotals.Leads);
        Assert.Equal(300m, r.Totals.Leads);
    }

    // ===== (10) Drill-down: تفصيل الخدمة لكل موظّف يفصل المصدرين =====
    [Fact]
    public async Task Aggregation_DrillDownBySource()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var service = Uniq("خدمة");

        var empId = await SubmitSourcesAsync(ceoId, tpl, "2026-W16", StandardNew(service), StandardData(service));

        var r = await AggBySourceAsync(admin,
            $"periodKey=2026-W16&employeeId={empId}&service={Uri.EscapeDataString(service)}");
        var row = Assert.Single(r.Services);
        var emp = Assert.Single(row.Employees);
        Assert.Equal(empId, emp.EmployeeId);
        Assert.Equal(100m, emp.NewLeads.Leads);
        Assert.Equal(200m, emp.DataScraping.Leads);
        Assert.Equal(12m, emp.Total.Won);
    }

    // ===== (11) لوحة B2B — فلتر All / New Leads / Data Scraping (الدلاء الثلاثة متاحة) =====
    [Fact]
    public async Task Dashboard_FilterAll_New_Data_BucketsPresent()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var tpl = await GetTemplateByTitleAsync(admin, B2bBySourceReportSchema.TemplateTitle);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var service = Uniq("خدمة");

        await SubmitSourcesAsync(ceoId, tpl, "2026-W17", StandardNew(service), StandardData(service));

        var r = await AggBySourceAsync(admin, $"periodKey=2026-W17&service={Uri.EscapeDataString(service)}");
        // فلتر All = الإجمالي، New = دلو New Leads، Data = دلو Data Scraping — كلها يقدّمها الخادم للوحة.
        Assert.Equal(300m, r.Totals.Leads);          // All
        Assert.Equal(100m, r.NewLeadsTotals.Leads);  // New Leads
        Assert.Equal(200m, r.DataScrapingTotals.Leads); // Data Scraping
    }

    // ===== (12) القالب القديم أحادي الجدول (Legacy) لم يُكسَر: يُقرأ ويُجمَّع ضمن دلو Legacy =====
    [Fact]
    public async Task LegacyB2b_NotBroken()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var legacy = await GetTemplateByTitleAsync(admin, B2bByServiceReportSchema.TemplateTitle);
        Assert.Equal(TemplateStatus.Published, legacy.Status);
        var grid = PublishedVersion(legacy).Fields.Single(f => f.FieldType == FieldType.TableGrid);
        Assert.Equal(B2bByServiceReportSchema.Columns, ConfigColumns(grid));

        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var service = Uniq("خدمة قديمة");
        // أعمدة Legacy: [الخدمة، ساعات العمل، Leads، Meetings، Proposals، Negotiation، Won، Lost، Revenue، Next Step].
        var legacyRows = new[] { new[] { service, "30", "70", "40", "20", "10", "6", "5", "6000", "" } };
        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(legacy.Id, PeriodType.Weekly, "2026-W18"))).ReadAsync<SubmissionDto>();
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(grid.Id, null, null, null, null, JsonSerializer.Serialize(legacyRows)) }));
        var submitted = await (await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);

        var r = await AggBySourceAsync(admin,
            $"periodKey=2026-W18&employeeId={empId}&service={Uri.EscapeDataString(service)}");
        var row = Assert.Single(r.Services);
        // Legacy يُطوى داخل Total، ولا يظهر في New/Data.
        Assert.Equal(70m, r.LegacyTotals.Leads);
        Assert.Equal(70m, row.Total.Leads);
        Assert.Equal(0m, row.NewLeads.Leads);
        Assert.Equal(0m, row.DataScraping.Leads);
    }

    // ===== (13) B2C لم يُكسَر: تجميع New/Old لا يزال يعمل والقالب القديم أحادي الجدول يُغذّي دلو New =====
    [Fact]
    public async Task B2c_NotBroken()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var legacy = await GetTemplateByTitleAsync(admin, B2cByCourseReportSchema.TemplateTitle);
        var legacyGrid = PublishedVersion(legacy).Fields.Single(f => f.FieldType == FieldType.TableGrid);
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var course = Uniq("دورة");

        var legacyRows = new[] { new[] { course, "35", "70", "60", "40", "9", "14", "7000", "7", "السعر" } };
        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(legacy.Id, PeriodType.Weekly, "2026-W19"))).ReadAsync<SubmissionDto>();
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(legacyGrid.Id, null, null, null, null, JsonSerializer.Serialize(legacyRows)) }));
        var submitted = await (await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);

        var report = (await (await admin.GetAsync(
            $"/api/reporting/aggregation/b2c/new-old?periodKey=2026-W19&employeeId={empId}&course={Uri.EscapeDataString(course)}"))
            .ReadAsync<B2cNewOldReport>())!;
        var row = Assert.Single(report.Courses);
        Assert.Equal(70m, row.New.Leads);
        Assert.Equal(14m, row.New.Sales);
        Assert.Equal(0m, row.Old.Leads);
    }

    // ===== (14) كتالوج الخدمات لم يُكسَر: نقطة القراءة تعمل وتُرجِع قائمة =====
    [Fact]
    public async Task ServiceCatalog_NotBroken()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var resp = await admin.GetAsync("/api/services");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var services = await resp.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(services);
    }
}
