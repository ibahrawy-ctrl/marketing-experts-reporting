using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class ReportsTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب تقرير {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    [Fact]
    public async Task SubmissionCompleteness_AggregatesForExecutive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishTemplateAsync(admin);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W60"));

        var report = await (await admin.GetAsync("/api/reports/submission-completeness?periodKey=2026-W60"))
            .ReadAsync<SubmissionCompletenessReport>();
        Assert.NotNull(report);
        Assert.True(report!.Total >= 1);
        Assert.Contains(report.ByStatus, s => s.Status == SubmissionStatus.Draft);
    }

    [Fact]
    public async Task GovernanceSummary_AccessibleToExecutive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var report = await (await admin.GetAsync("/api/reports/governance-summary"))
            .ReadAsync<GovernanceSummaryReport>();
        Assert.NotNull(report);
        Assert.True(report!.OpenRisks >= 0);
    }

    [Fact]
    public async Task KpiSummary_AccessibleToManagement()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var report = await (await manager.GetAsync("/api/reports/kpi-summary?periodKey=2026-W60"))
            .ReadAsync<KpiSummaryReport>();
        Assert.NotNull(report);
        Assert.True(report!.Evaluated >= 0);
    }

    [Fact]
    public async Task SubmissionsExport_ReturnsCsv()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.GetAsync("/api/reports/submissions/export?periodKey=2026-W60");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/csv", res.Content.Headers.ContentType?.MediaType);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length >= 3);
        // BOM لدعم العربية في Excel
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
    }

    [Fact]
    public async Task Reports_ForbiddenToEmployee_403()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await employee.GetAsync("/api/reports/submission-completeness");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CeoSupport_CanViewCompleteness()
    {
        var (support, _) = await TestAuth.CreateUserAsync(_factory, "CeoSupport");
        var res = await support.GetAsync("/api/reports/submission-completeness");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Reports_Anonymous_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/reports/governance-summary");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ===== Business-1A — تجميع مبيعات B2C =====

    /// <summary>يجد القالب المبذور «تقرير مندوب مبيعات B2C الفردي» ويعيد (معرّف الإصدار، معرّفات حقول الأرقام).</summary>
    private static async Task<(Guid VersionId, Guid LeadsFieldId, Guid RegFieldId)> ResolveB2cTemplateAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();
        var b2c = list!.Single(t => t.Title == B2cReportSchema.TemplateTitle);
        var detail = await (await admin.GetAsync($"/api/report-templates/{b2c.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        var version = detail!.Versions.Single(v => v.IsPublished);
        var leads = version.Fields.Single(f => f.Label == B2cReportSchema.Leads).Id;
        var reg = version.Fields.Single(f => f.Label == B2cReportSchema.Registrations).Id;
        return (version.Id, leads, reg);
    }

    [Fact]
    public async Task B2cRollup_Employee_SeesOwnNumbersOnly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, leadsFieldId, regFieldId) = await ResolveB2cTemplateAsync(admin);

        // إيجاد معرّف القالب من القائمة (لإنشاء التسليم).
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var templateId = list!.Single(t => t.Title == B2cReportSchema.TemplateTitle).Id;

        var (rep, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W71";

        var draft = await (await rep.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period)))
            .ReadAsync<SubmissionDto>();
        await rep.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(leadsFieldId, null, 100m, null, null, null),
                new FieldValueInput(regFieldId, null, 20m, null, null, null),
            }));
        await rep.PostAsync($"/api/submissions/{draft.Id}/submit", null);

        // المندوب يرى أرقامه (200، صفّ واحد، معدل تحويل 20٪).
        var mine = await (await rep.GetAsync($"/api/reports/b2c-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<B2cRollupReport>();
        Assert.NotNull(mine);
        Assert.Equal(1, mine!.Reporters);
        Assert.Equal(100m, mine.TotalLeads);
        Assert.Equal(20m, mine.TotalRegistrations);
        Assert.Equal(20m, mine.OverallConversionRate);

        // موظف آخر (خارج النطاق، بلا تسليم) يرى صفرًا — تأكيد عزل النطاق.
        var (other, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var theirs = await (await other.GetAsync($"/api/reports/b2c-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<B2cRollupReport>();
        Assert.Equal(0, theirs!.Reporters);
    }

    [Fact]
    public async Task B2cRollup_TeamLeader_AggregatesDirectReports()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, leadsFieldId, regFieldId) = await ResolveB2cTemplateAsync(admin);
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var templateId = list!.Single(t => t.Title == B2cReportSchema.TemplateTitle).Id;

        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (rep1, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        var (rep2, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        const string period = "2026-W72";

        async Task SubmitAsync(HttpClient c, decimal leads, decimal reg)
        {
            var d = await (await c.PostAsJsonAsync("/api/submissions",
                new CreateSubmissionRequest(templateId, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();
            await c.PutAsJsonAsync($"/api/submissions/{d!.Id}/values",
                new SaveFieldValuesRequest(new[]
                {
                    new FieldValueInput(leadsFieldId, null, leads, null, null, null),
                    new FieldValueInput(regFieldId, null, reg, null, null, null),
                }));
            await c.PostAsync($"/api/submissions/{d.Id}/submit", null);
        }

        await SubmitAsync(rep1, 100m, 30m);
        await SubmitAsync(rep2, 100m, 10m);

        var rollup = await (await tl.GetAsync($"/api/reports/b2c-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<B2cRollupReport>();
        Assert.NotNull(rollup);
        Assert.Equal(2, rollup!.Reporters);
        Assert.Equal(200m, rollup.TotalLeads);
        Assert.Equal(40m, rollup.TotalRegistrations);
        Assert.Equal(20m, rollup.OverallConversionRate);
        Assert.NotNull(rollup.Best);
        Assert.NotNull(rollup.Worst);
        // الأفضل أعلى تسجيلات، والأضعف أقل تحويلًا يحتاج متابعة.
        Assert.Equal(30m, rollup.Best!.Registrations);
        Assert.Contains(rollup.Rows, r => r.NeedsFollowUp);
    }

    /// <summary>ينشئ تسليم B2C واحدًا بأرقام محددة في فترة معطاة (بواسطة موظف جديد).</summary>
    private async Task SeedB2cSubmissionAsync(HttpClient admin, string period, decimal leads, decimal reg)
    {
        var (_, leadsFieldId, regFieldId) = await ResolveB2cTemplateAsync(admin);
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var templateId = list!.Single(t => t.Title == B2cReportSchema.TemplateTitle).Id;

        var (rep, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var draft = await (await rep.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();
        await rep.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(leadsFieldId, null, leads, null, null, null),
                new FieldValueInput(regFieldId, null, reg, null, null, null),
            }));
        await rep.PostAsync($"/api/submissions/{draft.Id}/submit", null);
    }

    [Fact]
    public async Task B2cRollup_Ceo_ReturnsExecutiveSummaryWithoutRepRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        const string period = "2026-W73";
        await SeedB2cSubmissionAsync(admin, period, 100m, 25m);

        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var rollup = await (await ceo.GetAsync($"/api/reports/b2c-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<B2cRollupReport>();

        Assert.NotNull(rollup);
        // الرئيس التنفيذي يرى الملخص التنفيذي فقط: إجماليات نعم، تفاصيل المندوبين لا (تقليل بيانات خادمي).
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
        Assert.True(rollup.TotalLeads >= 100m);
    }

    [Fact]
    public async Task B2cRollup_GeneralManager_ReturnsSummaryWithoutRepRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        const string period = "2026-W74";
        await SeedB2cSubmissionAsync(admin, period, 80m, 16m);

        var (gm, _) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var rollup = await (await gm.GetAsync($"/api/reports/b2c-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<B2cRollupReport>();

        Assert.NotNull(rollup);
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
        Assert.True(rollup.TotalLeads >= 80m);
    }

    // ===== Business-1A — تقييد قالب B2C الفردي حسب الوظيفة =====

    private async Task SetUserJobRoleAsync(Guid userId, Guid? jobRoleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = await db.Users.FirstAsync(x => x.Id == userId);
        u.JobRoleId = jobRoleId;
        await db.SaveChangesAsync();
    }

    private async Task SetTemplateJobRoleAsync(Guid templateId, Guid? jobRoleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t = await db.ReportTemplates.FirstAsync(x => x.Id == templateId);
        t.JobRoleId = jobRoleId;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ReportTemplates_JobRoleBound_VisibleOnlyToMatchingJobRole()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishTemplateAsync(admin);
        var b2cJobRole = Guid.NewGuid();
        var seoJobRole = Guid.NewGuid();
        await SetTemplateJobRoleAsync(templateId, b2cJobRole);

        var (b2cRep, b2cRepId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetUserJobRoleAsync(b2cRepId, b2cJobRole);
        var (seoLeader, seoLeaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        await SetUserJobRoleAsync(seoLeaderId, seoJobRole);

        async Task<bool> SeesTemplateAsync(HttpClient c) =>
            (await (await c.GetAsync("/api/report-templates?status=Published&isActive=true"))
                .ReadAsync<List<ReportTemplateDto>>())!.Any(t => t.Id == templateId);

        // مندوب الدور المطابق يراه؛ قائد فريق دور آخر لا يراه؛ الأدمن يدير الكل.
        Assert.True(await SeesTemplateAsync(b2cRep));
        Assert.False(await SeesTemplateAsync(seoLeader));
        Assert.True(await SeesTemplateAsync(admin));
    }

    [Fact]
    public async Task ReportTemplates_Generic_VisibleToAllRoles()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishTemplateAsync(admin); // بلا وظيفة (عام)

        var (emp, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var list = await (await emp.GetAsync("/api/report-templates?status=Published&isActive=true"))
            .ReadAsync<List<ReportTemplateDto>>();
        Assert.Contains(list!, t => t.Id == templateId);
    }

    // ===== Business-1B — تجميع أداء الإعلانات (Media Buyer) =====

    /// <summary>يجد القالب المبذور «تقرير النمو والأداء — Media Buyer» ويعيد معرّفات حقول الأرقام.</summary>
    private static async Task<(Guid TemplateId, Guid SpendFieldId, Guid LeadsFieldId, Guid CtrFieldId, Guid ConversionFieldId)>
        ResolveMediaBuyerTemplateAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var mb = list!.Single(t => t.Title == MediaBuyerReportSchema.TemplateTitle);
        var detail = await (await admin.GetAsync($"/api/report-templates/{mb.Id}")).ReadAsync<ReportTemplateDetailDto>();
        var version = detail!.Versions.Single(v => v.IsPublished);
        var spend = version.Fields.Single(f => f.Label == MediaBuyerReportSchema.Spend).Id;
        var leads = version.Fields.Single(f => f.Label == MediaBuyerReportSchema.Leads).Id;
        var ctr = version.Fields.Single(f => f.Label == MediaBuyerReportSchema.Ctr).Id;
        var conversion = version.Fields.Single(f => f.Label == MediaBuyerReportSchema.ConversionRate).Id;
        return (mb.Id, spend, leads, ctr, conversion);
    }

    private async Task SubmitMediaBuyerAsync(HttpClient c, Guid templateId, Guid spendF, Guid leadsF, Guid ctrF, Guid convF,
        string period, decimal spend, decimal leads, decimal ctr, decimal conversion)
    {
        var d = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{d!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(spendF, null, spend, null, null, null),
                new FieldValueInput(leadsF, null, leads, null, null, null),
                new FieldValueInput(ctrF, null, ctr, null, null, null),
                new FieldValueInput(convF, null, conversion, null, null, null),
            }));
        await c.PostAsync($"/api/submissions/{d.Id}/submit", null);
    }

    [Fact]
    public async Task MediaBuyerRollup_Employee_SeesOwnNumbersWithAutoCpl()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, spendF, leadsF, ctrF, convF) = await ResolveMediaBuyerTemplateAsync(admin);

        var (buyer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W81";
        await SubmitMediaBuyerAsync(buyer, templateId, spendF, leadsF, ctrF, convF, period, 5000m, 200m, 2.5m, 20m);

        var mine = await (await buyer.GetAsync($"/api/reports/media-buyer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<MediaBuyerRollupReport>();
        Assert.NotNull(mine);
        Assert.Equal("self", mine!.ViewLevel);
        Assert.True(mine.CanViewRows);
        Assert.Equal(1, mine.Reporters);
        Assert.Equal(5000m, mine.TotalSpend);
        Assert.Equal(200m, mine.TotalLeads);
        // CPL يُحتسب آليًا = 5000/200 = 25.
        Assert.Equal(25m, mine.OverallCpl);
        Assert.Equal(2.5m, mine.AverageCtr);
        Assert.Equal(20m, mine.AverageConversionRate);

        // مشترٍ آخر بلا تسليم يرى صفرًا — عزل النطاق.
        var (other, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var theirs = await (await other.GetAsync($"/api/reports/media-buyer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<MediaBuyerRollupReport>();
        Assert.Equal(0, theirs!.Reporters);
    }

    [Fact]
    public async Task MediaBuyerRollup_Manager_AggregatesReportsBestWorst()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, spendF, leadsF, ctrF, convF) = await ResolveMediaBuyerTemplateAsync(admin);

        var (mgr, mgrId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (b1, _) = await TestAuth.CreateUserAsync(_factory, "Employee", mgrId);
        var (b2, _) = await TestAuth.CreateUserAsync(_factory, "Employee", mgrId);
        const string period = "2026-W82";

        // b1 أكفأ (CPL=20)، b2 أضعف (CPL=50).
        await SubmitMediaBuyerAsync(b1, templateId, spendF, leadsF, ctrF, convF, period, 2000m, 100m, 3m, 25m);
        await SubmitMediaBuyerAsync(b2, templateId, spendF, leadsF, ctrF, convF, period, 5000m, 100m, 1m, 10m);

        var rollup = await (await mgr.GetAsync($"/api/reports/media-buyer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<MediaBuyerRollupReport>();
        Assert.NotNull(rollup);
        Assert.Equal("department", rollup!.ViewLevel);
        Assert.True(rollup.CanViewRows);
        Assert.Equal(2, rollup.Reporters);
        Assert.Equal(7000m, rollup.TotalSpend);
        Assert.Equal(200m, rollup.TotalLeads);
        Assert.Equal(35m, rollup.OverallCpl); // 7000/200
        Assert.NotNull(rollup.Best);
        Assert.NotNull(rollup.Worst);
        Assert.Equal(20m, rollup.Best!.Cpl);  // الأكفأ = أقل CPL
        Assert.Equal(50m, rollup.Worst!.Cpl); // الأضعف = أعلى CPL
        Assert.Contains(rollup.Rows, x => x.NeedsIntervention);
    }

    [Fact]
    public async Task MediaBuyerRollup_Ceo_ReturnsExecutiveSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, spendF, leadsF, ctrF, convF) = await ResolveMediaBuyerTemplateAsync(admin);
        const string period = "2026-W83";
        var (buyer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitMediaBuyerAsync(buyer, templateId, spendF, leadsF, ctrF, convF, period, 4000m, 100m, 2m, 15m);

        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var rollup = await (await ceo.GetAsync($"/api/reports/media-buyer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<MediaBuyerRollupReport>();

        Assert.NotNull(rollup);
        // الرئيس التنفيذي: ملخّص تنفيذي فقط — إجماليات نعم، صفوف المشترين لا (تقليل بيانات خادمي).
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
        Assert.True(rollup.TotalSpend >= 4000m);
        Assert.True(rollup.OverallCpl > 0);
    }

    [Fact]
    public async Task MediaBuyerRollup_GeneralManager_ReturnsSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, spendF, leadsF, ctrF, convF) = await ResolveMediaBuyerTemplateAsync(admin);
        const string period = "2026-W84";
        var (buyer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitMediaBuyerAsync(buyer, templateId, spendF, leadsF, ctrF, convF, period, 3000m, 60m, 2m, 12m);

        var (gm, _) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var rollup = await (await gm.GetAsync($"/api/reports/media-buyer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<MediaBuyerRollupReport>();

        Assert.NotNull(rollup);
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
        Assert.True(rollup.TotalSpend >= 3000m);
    }

    [Fact]
    public async Task MediaBuyerRollup_OutOfScopeUser_SeesNoAdData()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, spendF, leadsF, ctrF, convF) = await ResolveMediaBuyerTemplateAsync(admin);
        const string period = "2026-W85";
        var (buyer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitMediaBuyerAsync(buyer, templateId, spendF, leadsF, ctrF, convF, period, 9000m, 300m, 2m, 18m);

        // موظف غير مرتبط (نطاق own، بلا تسليم) لا يرى أي بيانات إعلانات.
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var rollup = await (await stranger.GetAsync($"/api/reports/media-buyer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<MediaBuyerRollupReport>();
        Assert.Equal(0, rollup!.Reporters);
        Assert.Empty(rollup.Rows);
        Assert.Equal(0m, rollup.TotalSpend);
    }

    // ===== Business-1C — تجميع أداء SEO (دمج قالبَي الفريق + المقالات) =====

    /// <summary>يجد القالبين المبذورين «🔍 تقرير فريق SEO» و«متابعة مقالات SEO» ويعيد معرّفات الحقول المطلوبة للتجميع.</summary>
    private static async Task<(Guid TeamTemplateId, Guid ImprovedF, Guid DeclinedF, Guid TasksF, Guid IssuesF,
        Guid ArticlesTemplateId, Guid PlannedF, Guid PublishedF, Guid LateF)>
        ResolveSeoTemplatesAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();

        var team = list!.Single(t => t.Title == SeoReportSchema.TeamTemplateTitle);
        var teamDetail = await (await admin.GetAsync($"/api/report-templates/{team.Id}")).ReadAsync<ReportTemplateDetailDto>();
        var teamVersion = teamDetail!.Versions.Single(v => v.IsPublished);
        var improved = teamVersion.Fields.Single(f => f.Label == SeoReportSchema.ImprovedKeywords).Id;
        var declined = teamVersion.Fields.Single(f => f.Label == SeoReportSchema.DeclinedKeywords).Id;
        var tasks = teamVersion.Fields.Single(f => f.Label == SeoReportSchema.TasksDone).Id;
        var issues = teamVersion.Fields.Single(f => f.Label == SeoReportSchema.TechnicalIssues).Id;

        var articles = list!.Single(t => t.Title == SeoReportSchema.ArticlesTemplateTitle);
        var articlesDetail = await (await admin.GetAsync($"/api/report-templates/{articles.Id}")).ReadAsync<ReportTemplateDetailDto>();
        var articlesVersion = articlesDetail!.Versions.Single(v => v.IsPublished);
        var planned = articlesVersion.Fields.Single(f => f.Label == SeoReportSchema.ArticlesPlanned).Id;
        var published = articlesVersion.Fields.Single(f => f.Label == SeoReportSchema.ArticlesPublished).Id;
        var late = articlesVersion.Fields.Single(f => f.Label == SeoReportSchema.ArticlesLate).Id;

        return (team.Id, improved, declined, tasks, issues, articles.Id, planned, published, late);
    }

    /// <summary>يُسلّم تقرير فريق SEO (كلمات/مهام/مشاكل) لعضوٍ ما في فترة معطاة.</summary>
    private async Task SubmitSeoTeamAsync(HttpClient c, Guid templateId, Guid improvedF, Guid declinedF, Guid tasksF, Guid issuesF,
        string period, decimal improved, decimal declined, decimal tasks, decimal issues)
    {
        var d = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{d!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(improvedF, null, improved, null, null, null),
                new FieldValueInput(declinedF, null, declined, null, null, null),
                new FieldValueInput(tasksF, null, tasks, null, null, null),
                new FieldValueInput(issuesF, null, issues, null, null, null),
            }));
        await c.PostAsync($"/api/submissions/{d.Id}/submit", null);
    }

    /// <summary>يُسلّم تقرير متابعة مقالات SEO (مخطّط/منشور/متأخر) لعضوٍ ما في فترة معطاة.</summary>
    private async Task SubmitSeoArticlesAsync(HttpClient c, Guid templateId, Guid plannedF, Guid publishedF, Guid lateF,
        string period, decimal planned, decimal published, decimal late)
    {
        var d = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{d!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(plannedF, null, planned, null, null, null),
                new FieldValueInput(publishedF, null, published, null, null, null),
                new FieldValueInput(lateF, null, late, null, null, null),
            }));
        await c.PostAsync($"/api/submissions/{d.Id}/submit", null);
    }

    [Fact]
    public async Task SeoRollup_Employee_MergesTwoTemplatesWithAutoNetKeywords()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (teamId, improvedF, declinedF, tasksF, issuesF, articlesId, plannedF, publishedF, lateF)
            = await ResolveSeoTemplatesAsync(admin);

        var (spec, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W91";
        await SubmitSeoTeamAsync(spec, teamId, improvedF, declinedF, tasksF, issuesF, period, 50m, 10m, 12m, 3m);
        await SubmitSeoArticlesAsync(spec, articlesId, plannedF, publishedF, lateF, period, 8m, 6m, 1m);

        var mine = await (await spec.GetAsync($"/api/reports/seo-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SeoRollupReport>();
        Assert.NotNull(mine);
        Assert.Equal("self", mine!.ViewLevel);
        Assert.True(mine.CanViewRows);
        // عضو واحد رغم تسليمين (يُدمجان حسب المُسلِّم).
        Assert.Equal(1, mine.Reporters);
        Assert.Equal(50m, mine.TotalImprovedKeywords);
        Assert.Equal(10m, mine.TotalDeclinedKeywords);
        // صافي حركة الكلمات يُحتسب آليًا = 50 − 10 = 40.
        Assert.Equal(40m, mine.NetKeywordMovement);
        Assert.Equal(12m, mine.TotalTasksDone);
        Assert.Equal(3m, mine.TotalTechnicalIssues);
        Assert.Equal(8m, mine.TotalArticlesPlanned);
        Assert.Equal(6m, mine.TotalArticlesPublished);
        // معدّل تسليم المحتوى = 6/8 = 75٪.
        Assert.Equal(75m, mine.ContentDeliveryRate);
        Assert.Single(mine.Rows);
        Assert.Equal(40m, mine.Rows[0].NetKeywords);

        // عضو آخر بلا تسليم يرى صفرًا — عزل النطاق.
        var (other, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var theirs = await (await other.GetAsync($"/api/reports/seo-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SeoRollupReport>();
        Assert.Equal(0, theirs!.Reporters);
    }

    [Fact]
    public async Task SeoRollup_TeamLeader_AggregatesBestWorstAndFollowup()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (teamId, improvedF, declinedF, tasksF, issuesF, _, _, _, _)
            = await ResolveSeoTemplatesAsync(admin);

        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (s1, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        var (s2, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        const string period = "2026-W92";

        // s1 صافي موجب (+30)؛ s2 صافي سالب (−15) → يحتاج متابعة.
        await SubmitSeoTeamAsync(s1, teamId, improvedF, declinedF, tasksF, issuesF, period, 40m, 10m, 15m, 2m);
        await SubmitSeoTeamAsync(s2, teamId, improvedF, declinedF, tasksF, issuesF, period, 5m, 20m, 8m, 6m);

        var rollup = await (await tl.GetAsync($"/api/reports/seo-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SeoRollupReport>();
        Assert.NotNull(rollup);
        Assert.Equal("team", rollup!.ViewLevel);
        Assert.True(rollup.CanViewRows);
        Assert.Equal(2, rollup.Reporters);
        Assert.Equal(45m, rollup.TotalImprovedKeywords);
        Assert.Equal(30m, rollup.TotalDeclinedKeywords);
        Assert.Equal(15m, rollup.NetKeywordMovement); // 45 − 30
        Assert.NotNull(rollup.Best);
        Assert.NotNull(rollup.Worst);
        Assert.Equal(30m, rollup.Best!.NetKeywords);   // الأفضل = أعلى صافي
        Assert.Equal(-15m, rollup.Worst!.NetKeywords); // الأحوج = أدنى صافي (سالب)
        Assert.Contains(rollup.Rows, r => r.NeedsFollowup);
    }

    [Fact]
    public async Task SeoRollup_Ceo_ReturnsExecutiveSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (teamId, improvedF, declinedF, tasksF, issuesF, _, _, _, _)
            = await ResolveSeoTemplatesAsync(admin);
        const string period = "2026-W93";
        var (spec, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitSeoTeamAsync(spec, teamId, improvedF, declinedF, tasksF, issuesF, period, 30m, 5m, 10m, 2m);

        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var rollup = await (await ceo.GetAsync($"/api/reports/seo-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SeoRollupReport>();

        Assert.NotNull(rollup);
        // الرئيس التنفيذي: ملخّص تنفيذي فقط — إجماليات نعم، صفوف الأعضاء لا (تقليل بيانات خادمي).
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
        Assert.True(rollup.NetKeywordMovement >= 25m);
    }

    [Fact]
    public async Task SeoRollup_GeneralManager_ReturnsSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (teamId, improvedF, declinedF, tasksF, issuesF, _, _, _, _)
            = await ResolveSeoTemplatesAsync(admin);
        const string period = "2026-W94";
        var (spec, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitSeoTeamAsync(spec, teamId, improvedF, declinedF, tasksF, issuesF, period, 20m, 4m, 9m, 1m);

        var (gm, _) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var rollup = await (await gm.GetAsync($"/api/reports/seo-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SeoRollupReport>();

        Assert.NotNull(rollup);
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
    }

    [Fact]
    public async Task SeoRollup_OutOfScopeUser_SeesNoSeoData()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (teamId, improvedF, declinedF, tasksF, issuesF, _, _, _, _)
            = await ResolveSeoTemplatesAsync(admin);
        const string period = "2026-W95";
        var (spec, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitSeoTeamAsync(spec, teamId, improvedF, declinedF, tasksF, issuesF, period, 60m, 5m, 14m, 2m);

        // موظف غير مرتبط (نطاق own، بلا تسليم SEO) لا يرى أي بيانات SEO.
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var rollup = await (await stranger.GetAsync($"/api/reports/seo-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SeoRollupReport>();
        Assert.Equal(0, rollup!.Reporters);
        Assert.Empty(rollup.Rows);
        Assert.Equal(0m, rollup.TotalImprovedKeywords);
        Assert.Equal(0m, rollup.NetKeywordMovement);
    }

    // ===== Business-1D-1 — تجميع أداء كاتب المحتوى =====

    /// <summary>يجد قالب «تقرير كاتب المحتوى الأسبوعي» المبذور ويعيد معرّفات الحقول الرقمية المطلوبة للتجميع.</summary>
    private static async Task<(Guid TemplateId, Guid RequiredF, Guid DeliveredF, Guid ApprovedF, Guid LateF, Guid AchievementF)>
        ResolveContentWriterTemplateAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var tpl = list!.Single(t => t.Title == ContentWriterReportSchema.TemplateTitle);
        var detail = await (await admin.GetAsync($"/api/report-templates/{tpl.Id}")).ReadAsync<ReportTemplateDetailDto>();
        var version = detail!.Versions.Single(v => v.IsPublished);
        var required = version.Fields.Single(f => f.Label == ContentWriterReportSchema.RequiredPieces).Id;
        var delivered = version.Fields.Single(f => f.Label == ContentWriterReportSchema.DeliveredPieces).Id;
        var approved = version.Fields.Single(f => f.Label == ContentWriterReportSchema.ApprovedFirstTime).Id;
        var late = version.Fields.Single(f => f.Label == ContentWriterReportSchema.LatePieces).Id;
        var achievement = version.Fields.Single(f => f.Label == ContentWriterReportSchema.OutputAchievement).Id;
        return (tpl.Id, required, delivered, approved, late, achievement);
    }

    /// <summary>يُسلّم تقرير كاتب المحتوى (مطلوبة/مسلَّمة/معتمدة من أول مرة/متأخرة/نسبة المخرجات) لكاتبٍ ما في فترة معطاة.</summary>
    private async Task SubmitContentWriterAsync(HttpClient c, Guid templateId,
        Guid requiredF, Guid deliveredF, Guid approvedF, Guid lateF, Guid achievementF,
        string period, decimal required, decimal delivered, decimal approved, decimal late, decimal achievement)
    {
        var d = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{d!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(requiredF, null, required, null, null, null),
                new FieldValueInput(deliveredF, null, delivered, null, null, null),
                new FieldValueInput(approvedF, null, approved, null, null, null),
                new FieldValueInput(lateF, null, late, null, null, null),
                new FieldValueInput(achievementF, null, achievement, null, null, null),
            }));
        await c.PostAsync($"/api/submissions/{d.Id}/submit", null);
    }

    [Fact]
    public async Task ContentWriterRollup_Employee_ComputesDerivedRates()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requiredF, deliveredF, approvedF, lateF, achievementF)
            = await ResolveContentWriterTemplateAsync(admin);

        var (writer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W81";
        await SubmitContentWriterAsync(writer, templateId, requiredF, deliveredF, approvedF, lateF, achievementF,
            period, 10m, 8m, 6m, 1m, 80m);

        var mine = await (await writer.GetAsync($"/api/reports/content-writer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<ContentWriterRollupReport>();
        Assert.NotNull(mine);
        Assert.Equal("self", mine!.ViewLevel);
        Assert.True(mine.CanViewRows);
        Assert.Equal(1, mine.Reporters);
        Assert.Equal(10m, mine.TotalRequired);
        Assert.Equal(8m, mine.TotalDelivered);
        Assert.Equal(6m, mine.TotalApprovedFirstTime);
        // المعادة للتعديل تُحتسب آليًا = 8 − 6 = 2.
        Assert.Equal(2m, mine.TotalRevised);
        // تسليم المحتوى = 8/10 = 80٪.
        Assert.Equal(80m, mine.ContentDeliveryRate);
        // الاعتماد من أول مرة = 6/8 = 75٪.
        Assert.Equal(75m, mine.FirstApprovalRate);
        // نسبة التعديلات = 2/8 = 25٪.
        Assert.Equal(25m, mine.RevisionRate);
        Assert.Equal(80m, mine.AvgPlanAdherence);
        Assert.Single(mine.Rows);
        Assert.Equal(75m, mine.Rows[0].FirstApprovalRate);

        // كاتب آخر بلا تسليم يرى صفرًا — عزل النطاق.
        var (other, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var theirs = await (await other.GetAsync($"/api/reports/content-writer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<ContentWriterRollupReport>();
        Assert.Equal(0, theirs!.Reporters);
    }

    [Fact]
    public async Task ContentWriterRollup_TeamLeader_AggregatesBestWorstAndFollowup()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requiredF, deliveredF, approvedF, lateF, achievementF)
            = await ResolveContentWriterTemplateAsync(admin);

        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (w1, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        var (w2, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        const string period = "2026-W82";

        // w1 اعتماد من أول مرة 90٪ بلا تأخير؛ w2 اعتماد 50٪ + تأخير → يحتاج متابعة.
        await SubmitContentWriterAsync(w1, templateId, requiredF, deliveredF, approvedF, lateF, achievementF,
            period, 10m, 10m, 9m, 0m, 95m);
        await SubmitContentWriterAsync(w2, templateId, requiredF, deliveredF, approvedF, lateF, achievementF,
            period, 10m, 10m, 5m, 2m, 60m);

        var rollup = await (await tl.GetAsync($"/api/reports/content-writer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<ContentWriterRollupReport>();
        Assert.NotNull(rollup);
        Assert.Equal("team", rollup!.ViewLevel);
        Assert.True(rollup.CanViewRows);
        Assert.Equal(2, rollup.Reporters);
        Assert.Equal(20m, rollup.TotalRequired);
        Assert.Equal(20m, rollup.TotalDelivered);
        Assert.Equal(14m, rollup.TotalApprovedFirstTime);
        // الاعتماد من أول مرة إجمالًا = 14/20 = 70٪.
        Assert.Equal(70m, rollup.FirstApprovalRate);
        Assert.NotNull(rollup.Best);
        Assert.NotNull(rollup.Worst);
        Assert.Equal(90m, rollup.Best!.FirstApprovalRate);   // الأفضل = أعلى اعتماد من أول مرة
        Assert.Equal(50m, rollup.Worst!.FirstApprovalRate);  // الأحوج = أدناها
        Assert.Contains(rollup.Rows, r => r.NeedsFollowup);
    }

    [Fact]
    public async Task ContentWriterRollup_Ceo_ReturnsExecutiveSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requiredF, deliveredF, approvedF, lateF, achievementF)
            = await ResolveContentWriterTemplateAsync(admin);
        const string period = "2026-W83";
        var (writer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitContentWriterAsync(writer, templateId, requiredF, deliveredF, approvedF, lateF, achievementF,
            period, 10m, 8m, 7m, 1m, 85m);

        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var rollup = await (await ceo.GetAsync($"/api/reports/content-writer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<ContentWriterRollupReport>();

        Assert.NotNull(rollup);
        // الرئيس التنفيذي: ملخّص تنفيذي فقط — إجماليات نعم، صفوف الكتّاب لا (تقليل بيانات خادمي).
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
        Assert.True(rollup.TotalDelivered >= 8m);
    }

    [Fact]
    public async Task ContentWriterRollup_GeneralManager_ReturnsSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requiredF, deliveredF, approvedF, lateF, achievementF)
            = await ResolveContentWriterTemplateAsync(admin);
        const string period = "2026-W84";
        var (writer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitContentWriterAsync(writer, templateId, requiredF, deliveredF, approvedF, lateF, achievementF,
            period, 6m, 5m, 4m, 0m, 90m);

        var (gm, _) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var rollup = await (await gm.GetAsync($"/api/reports/content-writer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<ContentWriterRollupReport>();

        Assert.NotNull(rollup);
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
    }

    [Fact]
    public async Task ContentWriterRollup_OutOfScopeUser_SeesNoData()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requiredF, deliveredF, approvedF, lateF, achievementF)
            = await ResolveContentWriterTemplateAsync(admin);
        const string period = "2026-W85";
        var (writer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitContentWriterAsync(writer, templateId, requiredF, deliveredF, approvedF, lateF, achievementF,
            period, 9m, 8m, 7m, 1m, 88m);

        // موظف غير مرتبط (نطاق own، بلا تسليم محتوى) لا يرى أي بيانات محتوى.
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var rollup = await (await stranger.GetAsync($"/api/reports/content-writer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<ContentWriterRollupReport>();
        Assert.Equal(0, rollup!.Reporters);
        Assert.Empty(rollup.Rows);
        Assert.Equal(0m, rollup.TotalDelivered);
        Assert.Equal(0m, rollup.FirstApprovalRate);
    }

    // ===== Business-1D-2 — تجميع أداء فريق التصميم =====

    /// <summary>يجد قالب «تقرير فريق التصميم» المبذور ويعيد معرّفات الحقول الرقمية المطلوبة للتجميع.</summary>
    private static async Task<(Guid TemplateId, Guid RequestedF, Guid DeliveredF, Guid ApprovedF, Guid LateF, Guid PendingF, Guid RevisedF, Guid AchievementF)>
        ResolveDesignerTemplateAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var tpl = list!.Single(t => t.Title == DesignerReportSchema.TemplateTitle);
        var detail = await (await admin.GetAsync($"/api/report-templates/{tpl.Id}")).ReadAsync<ReportTemplateDetailDto>();
        var version = detail!.Versions.Single(v => v.IsPublished);
        var requested = version.Fields.Single(f => f.Label == DesignerReportSchema.RequestedDesigns).Id;
        var delivered = version.Fields.Single(f => f.Label == DesignerReportSchema.DeliveredDesigns).Id;
        var approved = version.Fields.Single(f => f.Label == DesignerReportSchema.ApprovedFirstTime).Id;
        var late = version.Fields.Single(f => f.Label == DesignerReportSchema.LateDesigns).Id;
        var pending = version.Fields.Single(f => f.Label == DesignerReportSchema.PendingReview).Id;
        var revised = version.Fields.Single(f => f.Label == DesignerReportSchema.RevisedDesigns).Id;
        var achievement = version.Fields.Single(f => f.Label == DesignerReportSchema.OutputAchievement).Id;
        return (tpl.Id, requested, delivered, approved, late, pending, revised, achievement);
    }

    /// <summary>يُسلّم تقرير فريق التصميم (مطلوبة/مسلَّمة/معتمدة من أول مرة/متأخرة/بانتظار/معادة/نسبة المخرجات) لمصمّمٍ ما في فترة معطاة.</summary>
    private async Task SubmitDesignerAsync(HttpClient c, Guid templateId,
        Guid requestedF, Guid deliveredF, Guid approvedF, Guid lateF, Guid pendingF, Guid revisedF, Guid achievementF,
        string period, decimal requested, decimal delivered, decimal approved, decimal late, decimal pending, decimal revised, decimal achievement)
    {
        var d = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{d!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(requestedF, null, requested, null, null, null),
                new FieldValueInput(deliveredF, null, delivered, null, null, null),
                new FieldValueInput(approvedF, null, approved, null, null, null),
                new FieldValueInput(lateF, null, late, null, null, null),
                new FieldValueInput(pendingF, null, pending, null, null, null),
                new FieldValueInput(revisedF, null, revised, null, null, null),
                new FieldValueInput(achievementF, null, achievement, null, null, null),
            }));
        await c.PostAsync($"/api/submissions/{d.Id}/submit", null);
    }

    [Fact]
    public async Task DesignerRollup_Employee_ComputesDirectRevisedAndRates()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF)
            = await ResolveDesignerTemplateAsync(admin);

        var (designer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W91";
        // مطلوبة 12، مسلَّمة 10، معتمدة 7، متأخرة 1، بانتظار 1، معادة 3 (حقل مباشر)، نسبة المخرجات 85.
        await SubmitDesignerAsync(designer, templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF,
            period, 12m, 10m, 7m, 1m, 1m, 3m, 85m);

        var mine = await (await designer.GetAsync($"/api/reports/designer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<DesignerRollupReport>();
        Assert.NotNull(mine);
        Assert.Equal("self", mine!.ViewLevel);
        Assert.True(mine.CanViewRows);
        Assert.Equal(1, mine.Reporters);
        Assert.Equal(12m, mine.TotalRequested);
        Assert.Equal(10m, mine.TotalDelivered);
        Assert.Equal(7m, mine.TotalApprovedFirstTime);
        Assert.Equal(1m, mine.TotalPendingReview);
        // المعادة للتعديل تُقرأ مباشرة من الحقل = 3 (لا تُشتق).
        Assert.Equal(3m, mine.TotalRevised);
        // التسليم = 10/12 = 83.3٪.
        Assert.Equal(83.3m, mine.DeliveryRate);
        // الاعتماد من أول مرة = 7/10 = 70٪.
        Assert.Equal(70m, mine.FirstApprovalRate);
        // نسبة التعديلات = 3/10 = 30٪.
        Assert.Equal(30m, mine.RevisionRate);
        // الالتزام بالمواعيد = (10−1)/10 = 90٪.
        Assert.Equal(90m, mine.OnTimeRate);
        Assert.Equal(85m, mine.AvgPlanAdherence);
        Assert.Single(mine.Rows);
        Assert.Equal(3m, mine.Rows[0].RevisedDesigns);

        // مصمّم آخر بلا تسليم يرى صفرًا — عزل النطاق.
        var (other, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var theirs = await (await other.GetAsync($"/api/reports/designer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<DesignerRollupReport>();
        Assert.Equal(0, theirs!.Reporters);
    }

    [Fact]
    public async Task DesignerRollup_TeamLeader_AggregatesBestWorstAndFollowup()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF)
            = await ResolveDesignerTemplateAsync(admin);

        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (d1, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        var (d2, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        const string period = "2026-W92";

        // d1 اعتماد من أول مرة 90٪ بلا تأخير؛ d2 اعتماد 50٪ + تأخير → يحتاج متابعة.
        await SubmitDesignerAsync(d1, templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF,
            period, 10m, 10m, 9m, 0m, 0m, 1m, 95m);
        await SubmitDesignerAsync(d2, templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF,
            period, 10m, 10m, 5m, 2m, 0m, 5m, 60m);

        var rollup = await (await tl.GetAsync($"/api/reports/designer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<DesignerRollupReport>();
        Assert.NotNull(rollup);
        Assert.Equal("team", rollup!.ViewLevel);
        Assert.True(rollup.CanViewRows);
        Assert.Equal(2, rollup.Reporters);
        Assert.Equal(20m, rollup.TotalRequested);
        Assert.Equal(20m, rollup.TotalDelivered);
        Assert.Equal(14m, rollup.TotalApprovedFirstTime);
        // الاعتماد من أول مرة إجمالًا = 14/20 = 70٪.
        Assert.Equal(70m, rollup.FirstApprovalRate);
        // الالتزام بالمواعيد إجمالًا = (20−2)/20 = 90٪.
        Assert.Equal(90m, rollup.OnTimeRate);
        Assert.NotNull(rollup.Best);
        Assert.NotNull(rollup.Worst);
        Assert.Equal(90m, rollup.Best!.FirstApprovalRate);   // الأفضل = أعلى اعتماد من أول مرة
        Assert.Equal(50m, rollup.Worst!.FirstApprovalRate);  // الأحوج = أدناها
        Assert.Contains(rollup.Rows, r => r.NeedsFollowup);
    }

    [Fact]
    public async Task DesignerRollup_Ceo_ReturnsExecutiveSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF)
            = await ResolveDesignerTemplateAsync(admin);
        const string period = "2026-W93";
        var (designer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitDesignerAsync(designer, templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF,
            period, 10m, 8m, 7m, 1m, 0m, 1m, 85m);

        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var rollup = await (await ceo.GetAsync($"/api/reports/designer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<DesignerRollupReport>();

        Assert.NotNull(rollup);
        // الرئيس التنفيذي: ملخّص تنفيذي فقط — إجماليات نعم، صفوف المصمّمين لا (تقليل بيانات خادمي).
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
        Assert.True(rollup.TotalDelivered >= 8m);
    }

    [Fact]
    public async Task DesignerRollup_GeneralManager_ReturnsSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF)
            = await ResolveDesignerTemplateAsync(admin);
        const string period = "2026-W94";
        var (designer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitDesignerAsync(designer, templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF,
            period, 6m, 5m, 4m, 0m, 0m, 1m, 90m);

        var (gm, _) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var rollup = await (await gm.GetAsync($"/api/reports/designer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<DesignerRollupReport>();

        Assert.NotNull(rollup);
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
    }

    [Fact]
    public async Task DesignerRollup_OutOfScopeUser_SeesNoData()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF)
            = await ResolveDesignerTemplateAsync(admin);
        const string period = "2026-W95";
        var (designer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitDesignerAsync(designer, templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF,
            period, 9m, 8m, 7m, 1m, 0m, 1m, 88m);

        // موظف غير مرتبط (نطاق own، بلا تسليم تصميم) لا يرى أي بيانات تصميم.
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var rollup = await (await stranger.GetAsync($"/api/reports/designer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<DesignerRollupReport>();
        Assert.Equal(0, rollup!.Reporters);
        Assert.Empty(rollup.Rows);
        Assert.Equal(0m, rollup.TotalDelivered);
        Assert.Equal(0m, rollup.FirstApprovalRate);
    }

    // ===== Business-1D-3 — تجميع أداء فريق الفيديو =====

    /// <summary>يجد قالب «تقرير فريق الفيديو» المبذور ويعيد معرّفات الحقول الرقمية المطلوبة للتجميع.</summary>
    private static async Task<(Guid TemplateId, Guid RequestedF, Guid DeliveredF, Guid ApprovedF, Guid LateF, Guid PendingF, Guid RevisedF, Guid AchievementF)>
        ResolveVideoTemplateAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var tpl = list!.Single(t => t.Title == VideoReportSchema.TemplateTitle);
        var detail = await (await admin.GetAsync($"/api/report-templates/{tpl.Id}")).ReadAsync<ReportTemplateDetailDto>();
        var version = detail!.Versions.Single(v => v.IsPublished);
        var requested = version.Fields.Single(f => f.Label == VideoReportSchema.RequestedVideos).Id;
        var delivered = version.Fields.Single(f => f.Label == VideoReportSchema.DeliveredVideos).Id;
        var approved = version.Fields.Single(f => f.Label == VideoReportSchema.ApprovedFirstTime).Id;
        var late = version.Fields.Single(f => f.Label == VideoReportSchema.LateVideos).Id;
        var pending = version.Fields.Single(f => f.Label == VideoReportSchema.PendingReview).Id;
        var revised = version.Fields.Single(f => f.Label == VideoReportSchema.RevisedVideos).Id;
        var achievement = version.Fields.Single(f => f.Label == VideoReportSchema.OutputAchievement).Id;
        return (tpl.Id, requested, delivered, approved, late, pending, revised, achievement);
    }

    /// <summary>يُسلّم تقرير فريق الفيديو (مطلوبة/مسلَّمة/معتمدة من أول مرة/متأخرة/بانتظار/معادة/نسبة المخرجات) لعضوٍ ما في فترة معطاة.</summary>
    private async Task SubmitVideoAsync(HttpClient c, Guid templateId,
        Guid requestedF, Guid deliveredF, Guid approvedF, Guid lateF, Guid pendingF, Guid revisedF, Guid achievementF,
        string period, decimal requested, decimal delivered, decimal approved, decimal late, decimal pending, decimal revised, decimal achievement)
    {
        var d = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{d!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(requestedF, null, requested, null, null, null),
                new FieldValueInput(deliveredF, null, delivered, null, null, null),
                new FieldValueInput(approvedF, null, approved, null, null, null),
                new FieldValueInput(lateF, null, late, null, null, null),
                new FieldValueInput(pendingF, null, pending, null, null, null),
                new FieldValueInput(revisedF, null, revised, null, null, null),
                new FieldValueInput(achievementF, null, achievement, null, null, null),
            }));
        await c.PostAsync($"/api/submissions/{d.Id}/submit", null);
    }

    [Fact]
    public async Task VideoRollup_Employee_ComputesDirectRevisedAndRates()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF)
            = await ResolveVideoTemplateAsync(admin);

        var (editor, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W96";
        // مطلوبة 12، مسلَّمة 10، معتمدة 7، متأخرة 1، بانتظار 1، معادة 3 (حقل مباشر)، نسبة المخرجات 85.
        await SubmitVideoAsync(editor, templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF,
            period, 12m, 10m, 7m, 1m, 1m, 3m, 85m);

        var mine = await (await editor.GetAsync($"/api/reports/video-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<VideoRollupReport>();
        Assert.NotNull(mine);
        Assert.Equal("self", mine!.ViewLevel);
        Assert.True(mine.CanViewRows);
        Assert.Equal(1, mine.Reporters);
        Assert.Equal(12m, mine.TotalRequested);
        Assert.Equal(10m, mine.TotalDelivered);
        Assert.Equal(7m, mine.TotalApprovedFirstTime);
        Assert.Equal(1m, mine.TotalPendingReview);
        // المعادة للتعديل تُقرأ مباشرة من الحقل = 3 (لا تُشتق).
        Assert.Equal(3m, mine.TotalRevised);
        // التسليم = 10/12 = 83.3٪.
        Assert.Equal(83.3m, mine.DeliveryRate);
        // الاعتماد من أول مرة = 7/10 = 70٪.
        Assert.Equal(70m, mine.FirstApprovalRate);
        // نسبة التعديلات = 3/10 = 30٪.
        Assert.Equal(30m, mine.RevisionRate);
        // الالتزام بالمواعيد = (10−1)/10 = 90٪.
        Assert.Equal(90m, mine.OnTimeRate);
        Assert.Equal(85m, mine.AvgPlanAdherence);
        Assert.Single(mine.Rows);
        Assert.Equal(3m, mine.Rows[0].RevisedVideos);

        // عضو فيديو آخر بلا تسليم يرى صفرًا — عزل النطاق.
        var (other, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var theirs = await (await other.GetAsync($"/api/reports/video-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<VideoRollupReport>();
        Assert.Equal(0, theirs!.Reporters);
    }

    [Fact]
    public async Task VideoRollup_TeamLeader_AggregatesBestWorstAndFollowup()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF)
            = await ResolveVideoTemplateAsync(admin);

        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (v1, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        var (v2, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        const string period = "2026-W97";

        // v1 اعتماد من أول مرة 90٪ بلا تأخير؛ v2 اعتماد 50٪ + تأخير → يحتاج متابعة.
        await SubmitVideoAsync(v1, templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF,
            period, 10m, 10m, 9m, 0m, 0m, 1m, 95m);
        await SubmitVideoAsync(v2, templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF,
            period, 10m, 10m, 5m, 2m, 0m, 5m, 60m);

        var rollup = await (await tl.GetAsync($"/api/reports/video-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<VideoRollupReport>();
        Assert.NotNull(rollup);
        Assert.Equal("team", rollup!.ViewLevel);
        Assert.True(rollup.CanViewRows);
        Assert.Equal(2, rollup.Reporters);
        Assert.Equal(20m, rollup.TotalRequested);
        Assert.Equal(20m, rollup.TotalDelivered);
        Assert.Equal(14m, rollup.TotalApprovedFirstTime);
        // الاعتماد من أول مرة إجمالًا = 14/20 = 70٪.
        Assert.Equal(70m, rollup.FirstApprovalRate);
        // الالتزام بالمواعيد إجمالًا = (20−2)/20 = 90٪.
        Assert.Equal(90m, rollup.OnTimeRate);
        Assert.NotNull(rollup.Best);
        Assert.NotNull(rollup.Worst);
        Assert.Equal(90m, rollup.Best!.FirstApprovalRate);   // الأفضل = أعلى اعتماد من أول مرة
        Assert.Equal(50m, rollup.Worst!.FirstApprovalRate);  // الأحوج = أدناها
        Assert.Contains(rollup.Rows, r => r.NeedsFollowup);
    }

    [Fact]
    public async Task VideoRollup_Ceo_ReturnsExecutiveSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF)
            = await ResolveVideoTemplateAsync(admin);
        const string period = "2026-W98";
        var (editor, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitVideoAsync(editor, templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF,
            period, 10m, 8m, 7m, 1m, 0m, 1m, 85m);

        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var rollup = await (await ceo.GetAsync($"/api/reports/video-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<VideoRollupReport>();

        Assert.NotNull(rollup);
        // الرئيس التنفيذي: ملخّص تنفيذي فقط — إجماليات نعم، صفوف الأعضاء لا (تقليل بيانات خادمي).
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
        Assert.True(rollup.TotalDelivered >= 8m);
    }

    [Fact]
    public async Task VideoRollup_GeneralManager_ReturnsSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF)
            = await ResolveVideoTemplateAsync(admin);
        const string period = "2026-W99";
        var (editor, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitVideoAsync(editor, templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF,
            period, 6m, 5m, 4m, 0m, 0m, 1m, 90m);

        var (gm, _) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var rollup = await (await gm.GetAsync($"/api/reports/video-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<VideoRollupReport>();

        Assert.NotNull(rollup);
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
    }

    [Fact]
    public async Task VideoRollup_OutOfScopeUser_SeesNoData()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF)
            = await ResolveVideoTemplateAsync(admin);
        const string period = "2026-W100";
        var (editor, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitVideoAsync(editor, templateId, requestedF, deliveredF, approvedF, lateF, pendingF, revisedF, achievementF,
            period, 9m, 8m, 7m, 1m, 0m, 1m, 88m);

        // موظف غير مرتبط (نطاق own، بلا تسليم فيديو) لا يرى أي بيانات فيديو.
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var rollup = await (await stranger.GetAsync($"/api/reports/video-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<VideoRollupReport>();
        Assert.Equal(0, rollup!.Reporters);
        Assert.Empty(rollup.Rows);
        Assert.Equal(0m, rollup.TotalDelivered);
        Assert.Equal(0m, rollup.FirstApprovalRate);
    }

    // ===== Business-1D-4 — تجميع أداء المودريشن =====

    /// <summary>يجد قالب «تقرير المديرشن الأسبوعي» المبذور ويعيد معرّفات الحقول الرقمية المطلوبة للتجميع (مع الحقول المُضافة تراكميًا).</summary>
    private static async Task<(Guid TemplateId, Guid IncomingF, Guid AnsweredF, Guid ResponseMinF, Guid ProblematicF, Guid EscalationsF, Guid ComplaintsF, Guid ConvertedF)>
        ResolveModerationTemplateAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var tpl = list!.Single(t => t.Title == ModerationReportSchema.TemplateTitle);
        var detail = await (await admin.GetAsync($"/api/report-templates/{tpl.Id}")).ReadAsync<ReportTemplateDetailDto>();
        var version = detail!.Versions.Single(v => v.IsPublished);
        var incoming = version.Fields.Single(f => f.Label == ModerationReportSchema.IncomingMessages).Id;
        var answered = version.Fields.Single(f => f.Label == ModerationReportSchema.AnsweredMessages).Id;
        var responseMin = version.Fields.Single(f => f.Label == ModerationReportSchema.AvgResponseMinutes).Id;
        var problematic = version.Fields.Single(f => f.Label == ModerationReportSchema.ProblematicComments).Id;
        var escalations = version.Fields.Single(f => f.Label == ModerationReportSchema.Escalations).Id;
        var complaints = version.Fields.Single(f => f.Label == ModerationReportSchema.Complaints).Id;
        var converted = version.Fields.Single(f => f.Label == ModerationReportSchema.ConvertedOpportunities).Id;
        return (tpl.Id, incoming, answered, responseMin, problematic, escalations, complaints, converted);
    }

    /// <summary>يُسلّم تقرير المديرشن (واردة/مُجاب عليها/متوسط زمن الرد/إشكالية/مصعّدة/شكاوى/فرص محوَّلة) لمودريترٍ ما في فترة معطاة.</summary>
    private async Task SubmitModerationAsync(HttpClient c, Guid templateId,
        Guid incomingF, Guid answeredF, Guid responseMinF, Guid problematicF, Guid escalationsF, Guid complaintsF, Guid convertedF,
        string period, decimal incoming, decimal answered, decimal responseMin, decimal problematic, decimal escalations, decimal complaints, decimal converted)
    {
        var d = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{d!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(incomingF, null, incoming, null, null, null),
                new FieldValueInput(answeredF, null, answered, null, null, null),
                new FieldValueInput(responseMinF, null, responseMin, null, null, null),
                new FieldValueInput(problematicF, null, problematic, null, null, null),
                new FieldValueInput(escalationsF, null, escalations, null, null, null),
                new FieldValueInput(complaintsF, null, complaints, null, null, null),
                new FieldValueInput(convertedF, null, converted, null, null, null),
            }));
        await c.PostAsync($"/api/submissions/{d.Id}/submit", null);
    }

    [Fact]
    public async Task ModerationRollup_Employee_ComputesResponseRateAndReadsEscalations()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, incomingF, answeredF, responseMinF, problematicF, escalationsF, complaintsF, convertedF)
            = await ResolveModerationTemplateAsync(admin);

        var (mod, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W101";
        // واردة 200، مُجاب عليها 180، متوسط زمن الرد 12د، إشكالية 5، مصعّدة 3، شكاوى 0، فرص محوَّلة 7.
        await SubmitModerationAsync(mod, templateId, incomingF, answeredF, responseMinF, problematicF, escalationsF, complaintsF, convertedF,
            period, 200m, 180m, 12m, 5m, 3m, 0m, 7m);

        var mine = await (await mod.GetAsync($"/api/reports/moderation-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<ModerationRollupReport>();
        Assert.NotNull(mine);
        Assert.Equal("self", mine!.ViewLevel);
        Assert.True(mine.CanViewRows);
        Assert.Equal(1, mine.Reporters);
        Assert.Equal(200m, mine.TotalIncoming);
        Assert.Equal(180m, mine.TotalAnswered);
        // غير المعالجة = max(0, 200−180) = 20.
        Assert.Equal(20m, mine.TotalUnhandled);
        Assert.Equal(3m, mine.TotalEscalations);
        Assert.Equal(0m, mine.TotalComplaints);
        Assert.Equal(7m, mine.TotalConverted);
        // نسبة الرد = 180/200 = 90٪.
        Assert.Equal(90m, mine.ResponseRate);
        Assert.Equal(12m, mine.AvgResponseMinutes);
        Assert.Single(mine.Rows);
        Assert.Equal(3m, mine.Rows[0].Escalations);
        Assert.Equal(90m, mine.Rows[0].ResponseRate);

        // مودريتر آخر بلا تسليم يرى صفرًا — عزل النطاق.
        var (other, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var theirs = await (await other.GetAsync($"/api/reports/moderation-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<ModerationRollupReport>();
        Assert.Equal(0, theirs!.Reporters);
    }

    [Fact]
    public async Task ModerationRollup_TeamLeader_AggregatesBestWorstAndFollowup()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, incomingF, answeredF, responseMinF, problematicF, escalationsF, complaintsF, convertedF)
            = await ResolveModerationTemplateAsync(admin);

        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (m1, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        var (m2, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        const string period = "2026-W102";

        // m1 نسبة رد 95٪ بلا شكاوى؛ m2 نسبة رد 70٪ + شكوى → يحتاج متابعة.
        await SubmitModerationAsync(m1, templateId, incomingF, answeredF, responseMinF, problematicF, escalationsF, complaintsF, convertedF,
            period, 100m, 95m, 8m, 2m, 1m, 0m, 5m);
        await SubmitModerationAsync(m2, templateId, incomingF, answeredF, responseMinF, problematicF, escalationsF, complaintsF, convertedF,
            period, 100m, 70m, 20m, 8m, 4m, 3m, 1m);

        var rollup = await (await tl.GetAsync($"/api/reports/moderation-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<ModerationRollupReport>();
        Assert.NotNull(rollup);
        Assert.Equal("team", rollup!.ViewLevel);
        Assert.True(rollup.CanViewRows);
        Assert.Equal(2, rollup.Reporters);
        Assert.Equal(200m, rollup.TotalIncoming);
        Assert.Equal(165m, rollup.TotalAnswered);
        // نسبة الرد الإجمالية = 165/200 = 82.5٪.
        Assert.Equal(82.5m, rollup.ResponseRate);
        Assert.Equal(3m, rollup.TotalComplaints);
        Assert.NotNull(rollup.Best);
        Assert.NotNull(rollup.Worst);
        Assert.Equal(95m, rollup.Best!.ResponseRate);   // الأفضل = أعلى نسبة رد
        Assert.Equal(70m, rollup.Worst!.ResponseRate);  // الأحوج = أدناها
        Assert.Contains(rollup.Rows, r => r.NeedsFollowup);
    }

    [Fact]
    public async Task ModerationRollup_Ceo_ReturnsExecutiveSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, incomingF, answeredF, responseMinF, problematicF, escalationsF, complaintsF, convertedF)
            = await ResolveModerationTemplateAsync(admin);
        const string period = "2026-W103";
        var (mod, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitModerationAsync(mod, templateId, incomingF, answeredF, responseMinF, problematicF, escalationsF, complaintsF, convertedF,
            period, 150m, 140m, 10m, 4m, 2m, 1m, 6m);

        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var rollup = await (await ceo.GetAsync($"/api/reports/moderation-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<ModerationRollupReport>();

        Assert.NotNull(rollup);
        // الرئيس التنفيذي: ملخّص تنفيذي فقط — إجماليات نعم، صفوف الأعضاء لا (تقليل بيانات خادمي).
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
        Assert.True(rollup.TotalAnswered >= 140m);
    }

    [Fact]
    public async Task ModerationRollup_GeneralManager_ReturnsSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, incomingF, answeredF, responseMinF, problematicF, escalationsF, complaintsF, convertedF)
            = await ResolveModerationTemplateAsync(admin);
        const string period = "2026-W104";
        var (mod, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitModerationAsync(mod, templateId, incomingF, answeredF, responseMinF, problematicF, escalationsF, complaintsF, convertedF,
            period, 80m, 76m, 9m, 1m, 1m, 0m, 3m);

        var (gm, _) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var rollup = await (await gm.GetAsync($"/api/reports/moderation-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<ModerationRollupReport>();

        Assert.NotNull(rollup);
        Assert.Equal("summary", rollup!.ViewLevel);
        Assert.False(rollup.CanViewRows);
        Assert.Empty(rollup.Rows);
        Assert.Null(rollup.Best);
        Assert.Null(rollup.Worst);
        Assert.True(rollup.Reporters >= 1);
    }

    [Fact]
    public async Task ModerationRollup_OutOfScopeUser_SeesNoData()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, incomingF, answeredF, responseMinF, problematicF, escalationsF, complaintsF, convertedF)
            = await ResolveModerationTemplateAsync(admin);
        const string period = "2026-W105";
        var (mod, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SubmitModerationAsync(mod, templateId, incomingF, answeredF, responseMinF, problematicF, escalationsF, complaintsF, convertedF,
            period, 120m, 110m, 11m, 3m, 2m, 1m, 4m);

        // موظف غير مرتبط (نطاق own، بلا تسليم مودريشن) لا يرى أي بيانات.
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var rollup = await (await stranger.GetAsync($"/api/reports/moderation-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<ModerationRollupReport>();
        Assert.Equal(0, rollup!.Reporters);
        Assert.Empty(rollup.Rows);
        Assert.Equal(0m, rollup.TotalAnswered);
        Assert.Equal(0m, rollup.ResponseRate);
    }

    // ===== Business-1D-5 — ملخّص تشغيل السوشيال ميديا الموحّد =====

    /// <summary>يُسلّم تقارير المسارات الأربعة (محتوى/تصميم/فيديو/مودريشن) لعضوٍ واحد في فترة معطاة — لاختبار التجميع الموحّد.</summary>
    private async Task SeedAllSocialTracksAsync(HttpClient admin, HttpClient member, string period,
        decimal cwApproveRate, decimal designLate, decimal videoLate, decimal modComplaints, decimal modEscalations,
        decimal modIncoming = 200m, decimal modAnswered = 180m, decimal modResponseMin = 12m)
    {
        var (cwTpl, cwReq, cwDel, cwApp, cwLate, cwAch) = await ResolveContentWriterTemplateAsync(admin);
        var (dTpl, dReq, dDel, dApp, dLate, dPend, dRev, dAch) = await ResolveDesignerTemplateAsync(admin);
        var (vTpl, vReq, vDel, vApp, vLate, vPend, vRev, vAch) = await ResolveVideoTemplateAsync(admin);
        var (mTpl, mIn, mAns, mRes, mProb, mEsc, mComp, mConv) = await ResolveModerationTemplateAsync(admin);

        // محتوى: 10 مطلوبة، 10 مسلَّمة، اعتماد أول مرة = cwApproveRate (من 10)، بلا متأخرة.
        await SubmitContentWriterAsync(member, cwTpl, cwReq, cwDel, cwApp, cwLate, cwAch,
            period, 10m, 10m, cwApproveRate, 0m, 100m);
        // تصميم: 12 مطلوبة، 12 مسلَّمة، 10 اعتماد، designLate متأخرة، 0 بانتظار، 1 معادة.
        await SubmitDesignerAsync(member, dTpl, dReq, dDel, dApp, dLate, dPend, dRev, dAch,
            period, 12m, 12m, 10m, designLate, 0m, 1m, 100m);
        // فيديو: 8 مطلوبة، 8 مسلَّمة، 7 اعتماد، videoLate متأخرة، 0 بانتظار، 1 معادة.
        await SubmitVideoAsync(member, vTpl, vReq, vDel, vApp, vLate, vPend, vRev, vAch,
            period, 8m, 8m, 7m, videoLate, 0m, 1m, 100m);
        // مودريشن.
        await SubmitModerationAsync(member, mTpl, mIn, mAns, mRes, mProb, mEsc, mComp, mConv,
            period, modIncoming, modAnswered, modResponseMin, 4m, modEscalations, modComplaints, 6m);
    }

    [Fact]
    public async Task SocialOpsRollup_Employee_AggregatesFourTracks()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (member, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W201";
        await SeedAllSocialTracksAsync(admin, member, period,
            cwApproveRate: 9m, designLate: 2m, videoLate: 1m, modComplaints: 0m, modEscalations: 1m);

        var s = await (await member.GetAsync($"/api/reports/social-ops-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SocialOpsRollupReport>();

        Assert.NotNull(s);
        Assert.Equal("self", s!.ViewLevel);
        Assert.True(s.CanViewRows);
        // كل مسار يحسب مُبلِّغًا واحدًا → الإجمالي 4.
        Assert.Equal(4, s.TotalReporters);
        // محتوى.
        Assert.Equal(1, s.Content.Reporters);
        Assert.Equal(10m, s.Content.Required);
        Assert.Equal(10m, s.Content.Delivered);
        Assert.Equal(90m, s.Content.FirstApprovalRate); // 9/10
        // تصميم.
        Assert.Equal(12m, s.Design.Delivered);
        Assert.Equal(2m, s.Design.Late);
        // فيديو.
        Assert.Equal(8m, s.Video.Delivered);
        Assert.Equal(1m, s.Video.Late);
        // مودريشن.
        Assert.Equal(200m, s.Moderation.Incoming);
        Assert.Equal(180m, s.Moderation.Answered);
        Assert.Equal(90m, s.Moderation.ResponseRate);
        // مؤشرات الخطر/التوصية موجودة.
        Assert.False(string.IsNullOrWhiteSpace(s.TopRisk));
        Assert.False(string.IsNullOrWhiteSpace(s.Recommendation));
        Assert.False(string.IsNullOrWhiteSpace(s.HealthLabel));
        Assert.True(s.HealthScore > 0m);
    }

    [Fact]
    public async Task SocialOpsRollup_TeamLeader_AggregatesTeamAndFlagsRisk()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (m1, _) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        const string period = "2026-W202";
        // شكاوى + تصعيد في المودريشن → أعلى خطر يجب أن يشير للمودريشن.
        await SeedAllSocialTracksAsync(admin, m1, period,
            cwApproveRate: 8m, designLate: 3m, videoLate: 0m, modComplaints: 3m, modEscalations: 4m);

        var s = await (await tl.GetAsync($"/api/reports/social-ops-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SocialOpsRollupReport>();

        Assert.NotNull(s);
        Assert.Equal("team", s!.ViewLevel);
        Assert.True(s.CanViewRows);
        Assert.Equal(4, s.TotalReporters);
        Assert.Equal(3m, s.Moderation.Complaints);
        Assert.Equal(4m, s.Moderation.Escalations);
        // أعلى خطر = شكاوى/تصعيد المودريشن.
        Assert.Contains("المودريشن", s.TopRisk);
        Assert.Equal("المودريشن", s.MostComplaintsTrack);
        // أكثر تأخرًا = التصميم (3 متأخرة).
        Assert.Equal("التصميم", s.MostDelayedTrack);
    }

    [Fact]
    public async Task SocialOpsRollup_Ceo_ReturnsExecutiveSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        const string period = "2026-W203";
        var (member, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedAllSocialTracksAsync(admin, member, period,
            cwApproveRate: 9m, designLate: 1m, videoLate: 0m, modComplaints: 0m, modEscalations: 0m);

        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var s = await (await ceo.GetAsync($"/api/reports/social-ops-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SocialOpsRollupReport>();

        Assert.NotNull(s);
        // الرئيس التنفيذي: ملخّص تنفيذي فقط — إجماليات نعم، بلا تفاصيل أعضاء.
        Assert.Equal("summary", s!.ViewLevel);
        Assert.False(s.CanViewRows);
        Assert.True(s.TotalReporters >= 4);
        // الإجماليات لا تزال محسوبة (تقليل البيانات على الصفوف فقط).
        Assert.True(s.Moderation.Answered >= 180m);
        Assert.False(string.IsNullOrWhiteSpace(s.HealthLabel));
        Assert.False(string.IsNullOrWhiteSpace(s.TopRisk));
    }

    [Fact]
    public async Task SocialOpsRollup_GeneralManager_ReturnsSummaryWithoutRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        const string period = "2026-W204";
        var (member, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedAllSocialTracksAsync(admin, member, period,
            cwApproveRate: 8m, designLate: 0m, videoLate: 2m, modComplaints: 1m, modEscalations: 1m);

        var (gm, _) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var s = await (await gm.GetAsync($"/api/reports/social-ops-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SocialOpsRollupReport>();

        Assert.NotNull(s);
        Assert.Equal("summary", s!.ViewLevel);
        Assert.False(s.CanViewRows);
        Assert.True(s.TotalReporters >= 4);
        Assert.False(string.IsNullOrWhiteSpace(s.Recommendation));
    }

    [Fact]
    public async Task SocialOpsRollup_OutOfScopeUser_SeesNoData()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        const string period = "2026-W205";
        var (member, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedAllSocialTracksAsync(admin, member, period,
            cwApproveRate: 9m, designLate: 1m, videoLate: 1m, modComplaints: 0m, modEscalations: 0m);

        // موظف غير مرتبط (نطاق own، بلا أي تسليم سوشيال) لا يرى أي بيانات.
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var s = await (await stranger.GetAsync($"/api/reports/social-ops-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SocialOpsRollupReport>();

        Assert.NotNull(s);
        Assert.Equal(0, s!.TotalReporters);
        Assert.Equal(0m, s.Content.Delivered);
        Assert.Equal(0m, s.Design.Delivered);
        Assert.Equal(0m, s.Video.Delivered);
        Assert.Equal(0m, s.Moderation.Answered);
        Assert.Equal("لا توجد بيانات", s.HealthLabel);
    }
}
