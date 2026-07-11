using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Clients;
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

    // ===== أدوات قسم المشاريع المتكرّر (Project-First) للقوالب المبذورة =====
    // قوالب Media Buyer/SEO المبذورة تحوي قسم مشاريع متكرّر مطلوب (projectRequired=true, minProjects=1).
    // لذا يجب أن يحمل كل تسليم عنصر مشروع صالحًا واحدًا على الأقل بمشروع ضمن نطاق رؤية المُسلِّم،
    // وإلا يرفض الخادم الإرسال بـ submission.repeatable_section_invalid فيبقى التقرير Draft (مُستبعَد من التجميع).

    /// <summary>
    /// ينشئ (بالمسؤول) عميلًا ومشروعًا يكون <paramref name="accountManagerId"/> مدير حسابه،
    /// فيصبح المشروع ضمن نطاق رؤية ذلك المُسلِّم (own scope) لاختياره داخل قسم PRS. يعيد معرّف المشروع.
    /// </summary>
    private async Task<Guid> CreateOwnedProjectAsync(HttpClient admin, Guid accountManagerId)
    {
        var client = (await (await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest($"عميل {Guid.NewGuid():N}", null))).ReadAsync<ClientDto>())!;
        var project = (await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, $"مشروع {Guid.NewGuid():N}", ServiceType.MediaBuying,
                AccountManagerId: accountManagerId))).ReadAsync<ProjectDto>())!;
        return project.Id;
    }

    /// <summary>عنصر PRS صالح واحد بمشروع مُختار (بلا حقول فرعية — لأن الحقول الفرعية لقوالب Media Buyer/SEO غير مطلوبة).</summary>
    private static string ProjectSectionValue(Guid projectId)
        => $"[{{\"projectId\":\"{projectId}\",\"answers\":{{}}}}]";

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

    /// <summary>يجد القالب المبذور «تقرير النمو والأداء — Media Buyer» ويعيد معرّفات حقول الأرقام + قسم المشاريع المتكرّر.</summary>
    private static async Task<(Guid TemplateId, Guid SpendFieldId, Guid LeadsFieldId, Guid CtrFieldId, Guid ConversionFieldId, Guid ProjectSectionFieldId)>
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
        var projectSection = version.Fields.Single(f => f.FieldType == FieldType.ProjectRepeatableSection).Id;
        return (mb.Id, spend, leads, ctr, conversion, projectSection);
    }

    /// <summary>
    /// يُسلّم تقرير Media Buyer صالحًا: القيم المسطّحة + عنصر قسم مشاريع صالح (مشروع ضمن نطاق المُسلِّم)،
    /// ثم يتحقّق أن الإرسال نجح وأن التقرير لم يعُد مسودّة (يُحتسَب في التجميع).
    /// </summary>
    private async Task SubmitMediaBuyerAsync(HttpClient c, Guid templateId, Guid spendF, Guid leadsF, Guid ctrF, Guid convF,
        Guid projectSectionF, Guid projectId,
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
                new FieldValueInput(projectSectionF, null, null, null, null, ProjectSectionValue(projectId)),
            }));
        var submitted = await (await c.PostAsync($"/api/submissions/{d.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.NotNull(submitted);
        Assert.NotEqual(SubmissionStatus.Draft, submitted!.Status);
    }

    [Fact]
    public async Task MediaBuyerRollup_Employee_SeesOwnNumbersWithAutoCpl()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, spendF, leadsF, ctrF, convF, prsF) = await ResolveMediaBuyerTemplateAsync(admin);

        var (buyer, buyerId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var buyerProject = await CreateOwnedProjectAsync(admin, buyerId);
        const string period = "2026-W81";
        await SubmitMediaBuyerAsync(buyer, templateId, spendF, leadsF, ctrF, convF, prsF, buyerProject, period, 5000m, 200m, 2.5m, 20m);

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
        var (templateId, spendF, leadsF, ctrF, convF, prsF) = await ResolveMediaBuyerTemplateAsync(admin);

        var (mgr, mgrId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (b1, b1Id) = await TestAuth.CreateUserAsync(_factory, "Employee", mgrId);
        var (b2, b2Id) = await TestAuth.CreateUserAsync(_factory, "Employee", mgrId);
        var p1 = await CreateOwnedProjectAsync(admin, b1Id);
        var p2 = await CreateOwnedProjectAsync(admin, b2Id);
        const string period = "2026-W82";

        // b1 أكفأ (CPL=20)، b2 أضعف (CPL=50).
        await SubmitMediaBuyerAsync(b1, templateId, spendF, leadsF, ctrF, convF, prsF, p1, period, 2000m, 100m, 3m, 25m);
        await SubmitMediaBuyerAsync(b2, templateId, spendF, leadsF, ctrF, convF, prsF, p2, period, 5000m, 100m, 1m, 10m);

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
        var (templateId, spendF, leadsF, ctrF, convF, prsF) = await ResolveMediaBuyerTemplateAsync(admin);
        const string period = "2026-W83";
        var (buyer, buyerId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var buyerProject = await CreateOwnedProjectAsync(admin, buyerId);
        await SubmitMediaBuyerAsync(buyer, templateId, spendF, leadsF, ctrF, convF, prsF, buyerProject, period, 4000m, 100m, 2m, 15m);

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
        var (templateId, spendF, leadsF, ctrF, convF, prsF) = await ResolveMediaBuyerTemplateAsync(admin);
        const string period = "2026-W84";
        var (buyer, buyerId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var buyerProject = await CreateOwnedProjectAsync(admin, buyerId);
        await SubmitMediaBuyerAsync(buyer, templateId, spendF, leadsF, ctrF, convF, prsF, buyerProject, period, 3000m, 60m, 2m, 12m);

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
        var (templateId, spendF, leadsF, ctrF, convF, prsF) = await ResolveMediaBuyerTemplateAsync(admin);
        const string period = "2026-W85";
        var (buyer, buyerId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var buyerProject = await CreateOwnedProjectAsync(admin, buyerId);
        await SubmitMediaBuyerAsync(buyer, templateId, spendF, leadsF, ctrF, convF, prsF, buyerProject, period, 9000m, 300m, 2m, 18m);

        // موظف غير مرتبط (نطاق own، بلا تسليم) لا يرى أي بيانات إعلانات.
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var rollup = await (await stranger.GetAsync($"/api/reports/media-buyer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<MediaBuyerRollupReport>();
        Assert.Equal(0, rollup!.Reporters);
        Assert.Empty(rollup.Rows);
        Assert.Equal(0m, rollup.TotalSpend);
    }

    // ===== Business-1C — تجميع أداء SEO (دمج قالبَي الفريق + المقالات) =====

    /// <summary>
    /// يجد القالبين المبذورين «🔍 تقرير فريق SEO» و«متابعة مقالات SEO» ويعيد معرّفات الحقول المطلوبة للتجميع
    /// + معرّف قسم المشاريع المتكرّر في كلٍّ منهما (لأن كليهما يشترط عنصر مشروع صالحًا عند الإرسال).
    /// </summary>
    private static async Task<(Guid TeamTemplateId, Guid ImprovedF, Guid DeclinedF, Guid TasksF, Guid IssuesF, Guid TeamProjectSectionF,
        Guid ArticlesTemplateId, Guid PlannedF, Guid PublishedF, Guid LateF, Guid ArticlesProjectSectionF)>
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
        var teamProjectSection = teamVersion.Fields.Single(f => f.FieldType == FieldType.ProjectRepeatableSection).Id;

        var articles = list!.Single(t => t.Title == SeoReportSchema.ArticlesTemplateTitle);
        var articlesDetail = await (await admin.GetAsync($"/api/report-templates/{articles.Id}")).ReadAsync<ReportTemplateDetailDto>();
        // «تقرير متابعة مقالات SEO» قالب تكميلي فعليًّا في الإنتاج (صنّفه TemplateBinder Supplementary)،
        // فيُسمح للموظّف بتسليمه إلى جانب تقرير فريق SEO الأساسي لنفس الفترة. نضبطه هنا عبر واجهة الأدمن
        // المعتمَدة كي يتطابق الاختبار مع السلوك الإنتاجي — بلا مساس بالـSeeder ولا تعديل قاعدة يدويّ.
        if (articlesDetail!.Classification != TemplateClassification.Supplementary)
            await admin.PutAsJsonAsync($"/api/report-templates/{articles.Id}",
                new UpdateTemplateRequest(articlesDetail.Title, articlesDetail.Description,
                    articlesDetail.JobRoleId, articlesDetail.DefaultPeriodType, TemplateClassification.Supplementary));
        var articlesVersion = articlesDetail!.Versions.Single(v => v.IsPublished);
        var planned = articlesVersion.Fields.Single(f => f.Label == SeoReportSchema.ArticlesPlanned).Id;
        var published = articlesVersion.Fields.Single(f => f.Label == SeoReportSchema.ArticlesPublished).Id;
        var late = articlesVersion.Fields.Single(f => f.Label == SeoReportSchema.ArticlesLate).Id;
        var articlesProjectSection = articlesVersion.Fields.Single(f => f.FieldType == FieldType.ProjectRepeatableSection).Id;

        return (team.Id, improved, declined, tasks, issues, teamProjectSection,
            articles.Id, planned, published, late, articlesProjectSection);
    }

    /// <summary>يُسلّم تقرير فريق SEO (كلمات/مهام/مشاكل) صالحًا مع عنصر قسم مشاريع، ويتحقّق أنه لم يعُد مسودّة.</summary>
    private async Task SubmitSeoTeamAsync(HttpClient c, Guid templateId, Guid improvedF, Guid declinedF, Guid tasksF, Guid issuesF,
        Guid projectSectionF, Guid projectId,
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
                new FieldValueInput(projectSectionF, null, null, null, null, ProjectSectionValue(projectId)),
            }));
        var submitted = await (await c.PostAsync($"/api/submissions/{d.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.NotNull(submitted);
        Assert.NotEqual(SubmissionStatus.Draft, submitted!.Status);
    }

    /// <summary>يُسلّم تقرير متابعة مقالات SEO (مخطّط/منشور/متأخر) صالحًا مع عنصر قسم مشاريع، ويتحقّق أنه لم يعُد مسودّة.</summary>
    private async Task SubmitSeoArticlesAsync(HttpClient c, Guid templateId, Guid plannedF, Guid publishedF, Guid lateF,
        Guid projectSectionF, Guid projectId,
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
                new FieldValueInput(projectSectionF, null, null, null, null, ProjectSectionValue(projectId)),
            }));
        var submitted = await (await c.PostAsync($"/api/submissions/{d.Id}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.NotNull(submitted);
        Assert.NotEqual(SubmissionStatus.Draft, submitted!.Status);
    }

    [Fact]
    public async Task SeoRollup_Employee_MergesTwoTemplatesWithAutoNetKeywords()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (teamId, improvedF, declinedF, tasksF, issuesF, teamPrsF, articlesId, plannedF, publishedF, lateF, articlesPrsF)
            = await ResolveSeoTemplatesAsync(admin);

        var (spec, specId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var specProject = await CreateOwnedProjectAsync(admin, specId);
        const string period = "2026-W91";
        await SubmitSeoTeamAsync(spec, teamId, improvedF, declinedF, tasksF, issuesF, teamPrsF, specProject, period, 50m, 10m, 12m, 3m);
        await SubmitSeoArticlesAsync(spec, articlesId, plannedF, publishedF, lateF, articlesPrsF, specProject, period, 8m, 6m, 1m);

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
        var (teamId, improvedF, declinedF, tasksF, issuesF, teamPrsF, _, _, _, _, _)
            = await ResolveSeoTemplatesAsync(admin);

        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (s1, s1Id) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        var (s2, s2Id) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        var p1 = await CreateOwnedProjectAsync(admin, s1Id);
        var p2 = await CreateOwnedProjectAsync(admin, s2Id);
        const string period = "2026-W92";

        // s1 صافي موجب (+30)؛ s2 صافي سالب (−15) → يحتاج متابعة.
        await SubmitSeoTeamAsync(s1, teamId, improvedF, declinedF, tasksF, issuesF, teamPrsF, p1, period, 40m, 10m, 15m, 2m);
        await SubmitSeoTeamAsync(s2, teamId, improvedF, declinedF, tasksF, issuesF, teamPrsF, p2, period, 5m, 20m, 8m, 6m);

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
        var (teamId, improvedF, declinedF, tasksF, issuesF, teamPrsF, _, _, _, _, _)
            = await ResolveSeoTemplatesAsync(admin);
        const string period = "2026-W93";
        var (spec, specId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var specProject = await CreateOwnedProjectAsync(admin, specId);
        await SubmitSeoTeamAsync(spec, teamId, improvedF, declinedF, tasksF, issuesF, teamPrsF, specProject, period, 30m, 5m, 10m, 2m);

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
        var (teamId, improvedF, declinedF, tasksF, issuesF, teamPrsF, _, _, _, _, _)
            = await ResolveSeoTemplatesAsync(admin);
        const string period = "2026-W94";
        var (spec, specId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var specProject = await CreateOwnedProjectAsync(admin, specId);
        await SubmitSeoTeamAsync(spec, teamId, improvedF, declinedF, tasksF, issuesF, teamPrsF, specProject, period, 20m, 4m, 9m, 1m);

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
        var (teamId, improvedF, declinedF, tasksF, issuesF, teamPrsF, _, _, _, _, _)
            = await ResolveSeoTemplatesAsync(admin);
        const string period = "2026-W95";
        var (spec, specId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var specProject = await CreateOwnedProjectAsync(admin, specId);
        await SubmitSeoTeamAsync(spec, teamId, improvedF, declinedF, tasksF, issuesF, teamPrsF, specProject, period, 60m, 5m, 14m, 2m);

        // موظف غير مرتبط (نطاق own، بلا تسليم SEO) لا يرى أي بيانات SEO.
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var rollup = await (await stranger.GetAsync($"/api/reports/seo-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SeoRollupReport>();
        Assert.Equal(0, rollup!.Reporters);
        Assert.Empty(rollup.Rows);
        Assert.Equal(0m, rollup.TotalImprovedKeywords);
        Assert.Equal(0m, rollup.NetKeywordMovement);
    }

    // ===== اختبارات سلبيّة لكل عائلة: بلا عنصر مشروع ⇒ رفض الإرسال ⇒ يبقى Draft ⇒ مُستبعَد من التجميع =====
    // تُثبت أنّ حارس قسم المشاريع الإلزاميّ (projectRequired=true, minProjects=1) لم يُضعَّف: تسليم بلا عنصر مشروع
    // يُرفَض بالكود الرسميّ submission.repeatable_section_invalid ويبقى مسودّةً فلا يدخل تجميع Media Buyer/SEO.

    [Fact]
    public async Task MediaBuyerRollup_SubmitWithoutProjectSection_StaysDraft_NotAggregated()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, spendF, leadsF, ctrF, convF, _) = await ResolveMediaBuyerTemplateAsync(admin);

        var (buyer, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W86";

        // مسودّة بالقيم المسطّحة فقط دون أيّ عنصر مشروع في القسم الإلزاميّ.
        var draft = await (await buyer.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();
        await buyer.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(spendF, null, 4000m, null, null, null),
                new FieldValueInput(leadsF, null, 100m, null, null, null),
                new FieldValueInput(ctrF, null, 2m, null, null, null),
                new FieldValueInput(convF, null, 15m, null, null, null),
            }));

        // الإرسال يُرفَض بالكود الرسميّ (لا إضعاف لـ minProjects=1).
        var submit = await buyer.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
        Assert.Contains("repeatable_section_invalid", await submit.Content.ReadAsStringAsync());

        // يبقى مسودّة (لم يتحوّل إلى Submitted).
        var after = await (await buyer.GetAsync($"/api/submissions/{draft.Id}")).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Draft, after!.Status);

        // مُستبعَد من تجميع Media Buyer لتلك الفترة.
        var rollup = await (await buyer.GetAsync($"/api/reports/media-buyer-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<MediaBuyerRollupReport>();
        Assert.Equal(0, rollup!.Reporters);
        Assert.Empty(rollup.Rows);
        Assert.Equal(0m, rollup.TotalSpend);
    }

    [Fact]
    public async Task SeoRollup_SubmitWithoutProjectSection_StaysDraft_NotAggregated()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (teamId, improvedF, declinedF, tasksF, issuesF, _, _, _, _, _, _)
            = await ResolveSeoTemplatesAsync(admin);

        var (spec, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W96";

        // مسودّة تقرير فريق SEO بالقيم المسطّحة فقط دون أيّ عنصر مشروع في القسم الإلزاميّ.
        var draft = await (await spec.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(teamId, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();
        await spec.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[]
            {
                new FieldValueInput(improvedF, null, 30m, null, null, null),
                new FieldValueInput(declinedF, null, 5m, null, null, null),
                new FieldValueInput(tasksF, null, 10m, null, null, null),
                new FieldValueInput(issuesF, null, 2m, null, null, null),
            }));

        var submit = await spec.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
        Assert.Contains("repeatable_section_invalid", await submit.Content.ReadAsStringAsync());

        var after = await (await spec.GetAsync($"/api/submissions/{draft.Id}")).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Draft, after!.Status);

        var rollup = await (await spec.GetAsync($"/api/reports/seo-rollup?periodType=Weekly&periodKey={period}"))
            .ReadAsync<SeoRollupReport>();
        Assert.Equal(0, rollup!.Reporters);
        Assert.Empty(rollup.Rows);
        Assert.Equal(0m, rollup.TotalImprovedKeywords);
    }

}
