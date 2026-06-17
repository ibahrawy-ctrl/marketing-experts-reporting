using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Kpi;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// UAT Fix Pack — Phase 4 (حوكمة القوالب وKPI داخل الإدارة/الحوكمة):
/// • §2 الصلاحيات: إدارة القوالب/الـKPI متاحة للمستوى الإداري الأعلى (Admin/CEO/GM) فقط،
///   وممنوعة (403) على المدير الأدنى من العام/قائد الفريق/الموظّف — مفروضة خادميًّا.
/// • §9 دورية KPI: تقييم KPI أسبوعي فقط — لا يُنشَر قالب KPI بدورية غير أسبوعية.
/// • §4 منع ازدواج التقرير الأساسي: لا تقريران أساسيّان مطلوبان لنفس الفترة؛
///   التكميلي/الاختياري لا يُحتسب تقريرًا أساسيًا ثانيًا.
/// </summary>
[Collection("Integration")]
public class Phase4TemplateGovernanceTests
{
    private readonly CustomWebApplicationFactory _factory;

    public Phase4TemplateGovernanceTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== §2 صلاحيات حوكمة قوالب التقارير =====

    [Fact]
    public async Task Ceo_CanCreateReportTemplate_200()
    {
        var ceo = await TestAuth.LoginAsRoleAsync(_factory, "CEO");
        var res = await ceo.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب CEO {Guid.NewGuid():N}", null, null, PeriodType.Weekly));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GeneralManager_CanCreateReportTemplate_200()
    {
        var gm = await TestAuth.LoginAsRoleAsync(_factory, "GeneralManager");
        var res = await gm.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب GM {Guid.NewGuid():N}", null, null, PeriodType.Weekly));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Manager_CannotCreateReportTemplate_403()
    {
        var manager = await TestAuth.LoginAsRoleAsync(_factory, "Manager");
        var res = await manager.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest("غير مصرّح", null, null, PeriodType.Weekly));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task TeamLeader_CannotCreateReportTemplate_403()
    {
        var teamLeader = await TestAuth.LoginAsRoleAsync(_factory, "TeamLeader");
        var res = await teamLeader.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest("غير مصرّح", null, null, PeriodType.Weekly));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== §2 صلاحيات حوكمة قوالب KPI =====

    [Fact]
    public async Task Ceo_CanCreateKpiTemplate_200()
    {
        var ceo = await TestAuth.LoginAsRoleAsync(_factory, "CEO");
        var res = await ceo.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"KPI CEO {Guid.NewGuid():N}", null, null, KpiCadence.WeeklyPulse));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Manager_CannotCreateKpiTemplate_403()
    {
        var manager = await TestAuth.LoginAsRoleAsync(_factory, "Manager");
        var res = await manager.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest("غير مصرّح", null, null, KpiCadence.WeeklyPulse));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task TeamLeader_CannotCreateKpiTemplate_403()
    {
        var teamLeader = await TestAuth.LoginAsRoleAsync(_factory, "TeamLeader");
        var res = await teamLeader.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest("غير مصرّح", null, null, KpiCadence.WeeklyPulse));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== §9 دورية KPI أسبوعية فقط =====

    [Fact]
    public async Task PublishNonWeeklyKpiTemplate_IsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"KPI ربع سنوي {Guid.NewGuid():N}", null, null, KpiCadence.Quarterly)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        // وزن صحيح 100% — لكن الدورية ربع سنوية ⇒ يُرفَض النشر بسبب الدورية.
        await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("مؤشر", null, 100m, null, null, KpiCalcMethod.Manual, null));

        var publishRes = await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.Conflict, publishRes.StatusCode);
    }

    [Fact]
    public async Task PublishWeeklyKpiTemplate_WithValidWeights_IsAccepted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"KPI أسبوعي {Guid.NewGuid():N}", null, null, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("مؤشر", null, 100m, null, null, KpiCalcMethod.Manual, null));

        var publishRes = await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishRes.StatusCode);
    }

    // ===== §4 منع ازدواج التقرير الأساسي =====

    [Fact]
    public async Task TwoPrimaryWeeklyTemplates_SamePeriod_SecondIsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var primary1 = await PublishWeeklyAsync(admin, TemplateClassification.Primary);
        var primary2 = await PublishWeeklyAsync(admin, TemplateClassification.Primary);

        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W30";

        var first = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(primary1, PeriodType.Weekly, period));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(primary2, PeriodType.Weekly, period));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task PrimaryPlusSupplementary_SamePeriod_BothAccepted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var primary = await PublishWeeklyAsync(admin, TemplateClassification.Primary);
        var supplementary = await PublishWeeklyAsync(admin, TemplateClassification.Supplementary);

        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W31";

        var first = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(primary, PeriodType.Weekly, period));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // التكميلي لا يُحتسب تقريرًا أساسيًا ثانيًا ⇒ مقبول رغم وجود تقرير أساسي لنفس الفترة.
        var second = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(supplementary, PeriodType.Weekly, period));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task SamePrimaryTemplate_SamePeriod_IsIdempotent()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var primary = await PublishWeeklyAsync(admin, TemplateClassification.Primary);

        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W32";

        var first = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(primary, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();
        var second = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(primary, PeriodType.Weekly, period))).ReadAsync<SubmissionDto>();

        // نفس القالب لنفس الفترة = نفس المسوّدة (idempotent) وليس ازدواجًا.
        Assert.Equal(first!.Id, second!.Id);
    }

    private static async Task<Guid> PublishWeeklyAsync(HttpClient admin, TemplateClassification classification)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, null, PeriodType.Weekly, classification)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return created.Id;
    }
}
