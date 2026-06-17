using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Kpi;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// تجميع KPI الدوري (Phase 5 §8/§14): الأسبوع وحدة الأساس، والمتوسط شهري/ربع سنوي/سنوي
/// يُحتسب من متوسط نتائج الأسابيع داخل المدى — مقيَّدًا خادميًّا بنطاق المستخدم (ScopeResolver).
/// كلّ الحالات قائمة على الدور والنطاق، لا على اسم مستخدم بعينه.
/// </summary>
[Collection("Integration")]
public class KpiAggregationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public KpiAggregationTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<(Guid TemplateId, Guid ManualMetricId, Guid AutoMetricId)> PublishKpiAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"مؤشرات تجميع {Guid.NewGuid():N}", null, null, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var manual = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الالتزام", null, 50m, null, null, KpiCalcMethod.Manual, null)))
            .ReadAsync<KpiMetricDto>();
        var auto = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الإنجاز", null, 50m, 100m, "%", KpiCalcMethod.Auto, null)))
            .ReadAsync<KpiMetricDto>();

        await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        return (created.Id, manual!.Id, auto!.Id);
    }

    /// <summary>ينشئ تقييمًا أسبوعيًّا للموظّف بدرجة محدَّدة (manual=auto=score) ويُرسله ⇒ TotalScore=score.</summary>
    private static async Task SubmitWeeklyScoreAsync(
        HttpClient evaluator, Guid templateId, Guid subjectId, Guid manualId, Guid autoId,
        string weekKey, decimal score)
    {
        var ev = await (await evaluator.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, weekKey)))
            .ReadAsync<KpiEvaluationDto>();
        await evaluator.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, score, null),
                new KpiResultInput(autoId, score, null, null)
            }));
        var submitted = await (await evaluator.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(score, submitted!.TotalScore);
    }

    // §14.14 — الأسبوع وحدة الأساس: التجميع يقرأ التقييمات الأسبوعية.
    // §14.15 — متوسط الشهر يُحتسب من أسابيع الشهر.
    [Fact]
    public async Task MonthlyAggregate_AveragesWeeksWithinMonth()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        // أسابيع خميسها داخل يونيو 2026: W23 (خميس 04/06) و W25 (خميس 18/06).
        await SubmitWeeklyScoreAsync(manager, templateId, subjectId, manualId, autoId, "2026-W23", 60m);
        await SubmitWeeklyScoreAsync(manager, templateId, subjectId, manualId, autoId, "2026-W25", 80m);

        var dto = await (await manager.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Monthly&periodKey=2026-06&subjectUserId={subjectId}"))
            .ReadAsync<KpiAggregateDto>();

        Assert.NotNull(dto);
        Assert.Equal("Monthly", dto!.Granularity);
        Assert.Equal(2, dto.WeeksCount);
        Assert.Equal(70m, dto.Average);   // (60 + 80) / 2
        Assert.True(dto.CanViewRows);
        Assert.Equal(2, dto.Weeks.Count);
    }

    // §14.16 — متوسط الربع من أسابيع الربع (Q2 = أبريل..يونيو).
    [Fact]
    public async Task QuarterlyAggregate_AveragesWeeksWithinQuarter()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        await SubmitWeeklyScoreAsync(manager, templateId, subjectId, manualId, autoId, "2026-W18", 40m); // أبريل
        await SubmitWeeklyScoreAsync(manager, templateId, subjectId, manualId, autoId, "2026-W25", 60m); // يونيو

        var dto = await (await manager.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Quarterly&periodKey=2026-Q2&subjectUserId={subjectId}"))
            .ReadAsync<KpiAggregateDto>();

        Assert.Equal(2, dto!.WeeksCount);
        Assert.Equal(50m, dto.Average);
    }

    // §14.17 — متوسط السنة من أسابيع العام؛ أسبوع خارج العام لا يُحتسب.
    [Fact]
    public async Task YearlyAggregate_ExcludesWeeksOutsideYear()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        await SubmitWeeklyScoreAsync(manager, templateId, subjectId, manualId, autoId, "2026-W25", 90m);
        await SubmitWeeklyScoreAsync(manager, templateId, subjectId, manualId, autoId, "2025-W25", 10m);

        var dto = await (await manager.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Yearly&periodKey=2026&subjectUserId={subjectId}"))
            .ReadAsync<KpiAggregateDto>();

        Assert.Equal(1, dto!.WeeksCount);   // 2025-W25 مستبعد
        Assert.Equal(90m, dto.Average);
    }

    // §14.18/19 — التجميع يحترم النطاق: الموظّف يرى نفسه فقط، ولا يرى موظفًا خارج نطاقه (403).
    [Fact]
    public async Task Aggregate_Employee_SeesSelf_ButForbiddenOutOfScope()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (_, strangerId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitWeeklyScoreAsync(manager, templateId, employeeId, manualId, autoId, "2026-W25", 75m);

        // الموظّف يرى تجميع نفسه.
        var self = await (await employee.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Monthly&periodKey=2026-06&subjectUserId={employeeId}"))
            .ReadAsync<KpiAggregateDto>();
        Assert.Equal(75m, self!.Average);
        Assert.Equal("own", self.ScopeType);

        // الموظّف لا يستطيع تجميع موظّف خارج نطاقه.
        var forbidden = await employee.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Monthly&periodKey=2026-06&subjectUserId={strangerId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    // §14.20/21 — قائد الفريق/المدير يرى تجميع نطاقه فقط (مرؤوسوه)، لا موظفًا خارج النطاق.
    [Fact]
    public async Task Aggregate_TeamLeader_SeesTeamScopeOnly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (_, memberId) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        var (_, outsiderId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await SubmitWeeklyScoreAsync(admin, templateId, memberId, manualId, autoId, "2026-W25", 88m);

        var inScope = await (await tl.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Monthly&periodKey=2026-06&subjectUserId={memberId}"))
            .ReadAsync<KpiAggregateDto>();
        Assert.Equal(88m, inScope!.Average);
        Assert.Equal("team", inScope.ScopeType);

        var outOfScope = await tl.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Monthly&periodKey=2026-06&subjectUserId={outsiderId}");
        Assert.Equal(HttpStatusCode.Forbidden, outOfScope.StatusCode);
    }

    // صيغ الفترة غير الصحيحة تُرفض (400) — لا تجميع على مفاتيح حرّة.
    [Theory]
    [InlineData("Monthly", "2026-6")]
    [InlineData("Quarterly", "2026-Q9")]
    [InlineData("Yearly", "غير صحيح")]
    [InlineData("Unknown", "2026-06")]
    public async Task Aggregate_MalformedRequest_IsRejected(string granularity, string periodKey)
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity={granularity}&periodKey={periodKey}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // المدى المخصّص يتطلّب بداية ونهاية.
    [Fact]
    public async Task Aggregate_Custom_RequiresRange()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.GetAsync("/api/kpi-evaluations/aggregate?granularity=Custom");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Aggregate_Anonymous_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/kpi-evaluations/aggregate?granularity=Monthly&periodKey=2026-06");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
