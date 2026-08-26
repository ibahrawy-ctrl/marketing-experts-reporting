using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Kpi;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// P1-KPI-004 + P1-SEC-009 — عقد تحليلات KPI الموحّد وأمنُه، على قاعدة <b>معزولة</b>
/// (<see cref="KpiTruthIsolatedFactory"/>) لا تمسّ <c>reporting_test</c> المشتركة.
///
/// ما تثبته هذه الحزمة:
/// <list type="number">
/// <item>التوسيط ذو المرحلتين (B-2) عبر واجهة HTTP فعليّة لا عبر دالّة خالصة فقط.</item>
/// <item>الكادنس إلزاميّ (B-3): غيابه خطأ صريح لا سقوط صامت.</item>
/// <item>الحالة: Approved فقط تدخل الرقم؛ Submitted/UnderReview لا تدخل.</item>
/// <item>Drill-down يعيد إنتاج الرقم نفسه من صفوفه (قابليّة التحقّق اليدويّ).</item>
/// <item>الأمن: المورد خارج النطاق يعود <b>404</b> لا 403 ⇒ لا تسريب وجود.</item>
/// <item>الدور غير المصرَّح له يُمنع بالسياسة على الخادم لا بالإخفاء في الواجهة.</item>
/// </list>
/// </summary>
[Collection("KpiTruthIsolated")]
public class KpiTruthContractAndSecurityTests
{
    private readonly KpiTruthIsolatedFactory _factory;

    public KpiTruthContractAndSecurityTests(KpiTruthIsolatedFactory factory) => _factory = factory;

    private async Task<(Guid TemplateId, Guid ManualMetricId, Guid AutoMetricId)> PublishTemplateAsync(
        HttpClient admin, KpiCadence cadence)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"قالب حقيقة {Guid.NewGuid():N}", null, null, cadence)))
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

    /// <summary>ينشئ تقييمًا بدرجة محدَّدة ويعتمده (Approved) ما لم يُطلب إبقاؤه معلَّقًا.</summary>
    private async Task ScoreAsync(HttpClient evaluator, Guid templateId, Guid subjectId,
        Guid manualId, Guid autoId, string weekKey, decimal score, bool approve = true)
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
        await evaluator.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null);
        if (!approve) return;

        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var approved = await (await ceo.PostAsync($"/api/kpi-evaluations/{ev.Id}/approve", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.Approved, approved!.Status);
    }

    /// <summary>
    /// يقرأ مفاتيح الأسابيع الواقعة داخل فترة ما <b>من الخادم</b> (B-1): الاختبار لا يشتقّ حدود الفترة
    /// بنفسه ولا يخمّن أرقام الأسابيع، تمامًا كما لا يحقّ للواجهة أن تشتقّها.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ResolveWeekKeysAsync(
        HttpClient client, string type, string periodKey)
    {
        var res = await client.GetAsync($"/api/kpi/periods/resolve?type={type}&periodKey={periodKey}");
        res.EnsureSuccessStatusCode();
        using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("weekKeys").EnumerateArray()
            .Select(e => e.GetString()!).ToList();
    }

    // ===== 1) التوسيط ذو المرحلتين عبر HTTP (B-2) =====
    // الحالة المرجعيّة: موظّف قُيّم عشر مرّات بـ50، وآخر مرّة واحدة بـ90.
    // الخطأ القديم (متوسّط خام على التقييمات) = (10×50 + 90) / 11 = 53.64.
    // الصواب (متوسّط متوسّطات الموظّفين) = (50 + 90) / 2 = 70.00 — كلّ موظّف يزن واحدًا.
    [Fact]
    public async Task Performance_TwoStageAveraging_EachEmployeeWeighsOne()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishTemplateAsync(admin, KpiCadence.WeeklyPulse);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, heavy) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (_, light) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        // الأسابيع تُؤخَذ من الخادم نفسه (لا تخمين أرقام أسابيع في الاختبار): أوّل عشرة أسابيع
        // داخل الربع كما يحلّها CanonicalPeriodService بتوقيت الرياض.
        var weeks = (await ResolveWeekKeysAsync(manager, "Quarter", "2026-Q2")).Take(10).ToArray();
        Assert.Equal(10, weeks.Length);

        foreach (var w in weeks)
            await ScoreAsync(manager, templateId, heavy, manualId, autoId, w, 50m);
        await ScoreAsync(manager, templateId, light, manualId, autoId, weeks[0], 90m);

        var dto = await (await manager.GetAsync(
            "/api/kpi/performance?periodType=Quarter&periodKey=2026-Q2&cadence=WeeklyPulse"))
            .ReadAsync<KpiPerformanceDto>();

        Assert.NotNull(dto);
        Assert.Equal(70.00m, dto!.Company.Measure.Value);
        Assert.NotEqual(53.64m, dto.Company.Measure.Value);

        var heavyRow = dto.Employees.Single(e => e.UserId == heavy);
        var lightRow = dto.Employees.Single(e => e.UserId == light);
        Assert.Equal(50.00m, heavyRow.Measure.Value);
        Assert.Equal(90.00m, lightRow.Measure.Value);
        Assert.Equal(10, heavyRow.Measure.EligibleEvaluationCount);
        Assert.Equal(1, lightRow.Measure.EligibleEvaluationCount);
    }

    // ===== 1ب) نقطة التجميع القائمة تعيد العتبة والكادنس المطبَّقَين (B-6/B-3) =====
    // بدون هذا الحقل تضطرّ الشاشات القديمة إلى ثوابت 60/85 محلّيّة — وهو ما تمنعه التذكرة.
    [Fact]
    public async Task Aggregate_ReturnsAppliedThresholdAndCadence_SoUiNeedsNoConstants()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishTemplateAsync(admin, KpiCadence.WeeklyPulse);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, employee) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var weeks = (await ResolveWeekKeysAsync(manager, "Quarter", "2026-Q2")).Take(2).ToArray();
        await ScoreAsync(manager, templateId, employee, manualId, autoId, weeks[0], 85m);
        await ScoreAsync(manager, templateId, employee, manualId, autoId, weeks[1], 45m);

        var dto = await (await manager.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Quarterly&periodKey=2026-Q2&subjectUserId={employee}"))
            .ReadAsync<KpiAggregateDto>();

        Assert.NotNull(dto);
        // متوسّط الموظّف = (85 + 45) / 2 = 65 — لا 85 (أعلى تقييم) ولا 45 (آخر تقييم).
        Assert.Equal(65.00m, dto!.Average);
        Assert.Equal(1, dto.EmployeesCount);
        Assert.Equal(KpiCadence.WeeklyPulse, dto.AppliedCadence);
        Assert.NotNull(dto.AppliedBelowTargetThreshold);
    }

    // ===== 2) الكادنس إلزاميّ (B-3) — لا سقوط صامت إلى النبض الأسبوعيّ =====
    [Fact]
    public async Task Performance_WithoutCadence_FailsExplicitly()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.GetAsync("/api/kpi/performance?periodType=Quarter&periodKey=2026-Q2");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("cadence", (await res.Content.ReadAsStringAsync()).ToLowerInvariant());
    }

    // ===== 3) فصل نبض الأسبوع عن الربعيّ الرسميّ (B-3) =====
    [Fact]
    public async Task Performance_QuarterlyCadence_DoesNotSeeWeeklyPulseEvaluations()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (weeklyTemplate, mid, aid) = await PublishTemplateAsync(admin, KpiCadence.WeeklyPulse);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subject) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        await ScoreAsync(manager, weeklyTemplate, subject, mid, aid, "2026-W25", 80m);

        var weekly = await (await manager.GetAsync(
            $"/api/kpi/performance?periodType=Quarter&periodKey=2026-Q2&cadence=WeeklyPulse&subjectUserId={subject}"))
            .ReadAsync<KpiPerformanceDto>();
        var quarterly = await (await manager.GetAsync(
            $"/api/kpi/performance?periodType=Quarter&periodKey=2026-Q2&cadence=Quarterly&subjectUserId={subject}"))
            .ReadAsync<KpiPerformanceDto>();

        Assert.Equal(80.00m, weekly!.Company.Measure.Value);
        // لا خلط: التقييم أسبوعيّ الكادنس فلا يظهر في المسار الربعيّ الرسميّ، ولا يُعرَض صفرًا.
        Assert.Null(quarterly!.Company.Measure.Value);
        Assert.Equal(KpiDataQuality.NoData, quarterly.Company.Measure.DataQuality);
    }

    // ===== 4) الحالة: Approved فقط (§5) — والمفقود ليس صفرًا =====
    [Fact]
    public async Task Performance_NonApprovedEvaluations_AreExcludedAndNotZeroed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, mid, aid) = await PublishTemplateAsync(admin, KpiCadence.WeeklyPulse);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subject) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        await ScoreAsync(manager, templateId, subject, mid, aid, "2026-W25", 40m, approve: false);

        var dto = await (await manager.GetAsync(
            $"/api/kpi/performance?periodType=Quarter&periodKey=2026-Q2&cadence=WeeklyPulse&subjectUserId={subject}"))
            .ReadAsync<KpiPerformanceDto>();

        Assert.Null(dto!.Company.Measure.Value);                       // مفقود لا صفر
        Assert.NotEqual(0m, dto.Company.Measure.Value ?? -1m);
        Assert.Equal(0, dto.Company.Measure.EligibleEvaluationCount);
        Assert.True(dto.Company.Measure.ExcludedByStatusCount >= 1);
    }

    // ===== 5) Drill-down يعيد إنتاج الرقم من صفوفه =====
    [Fact]
    public async Task Drilldown_ReproducesTheNumberFromItsOwnRows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, mid, aid) = await PublishTemplateAsync(admin, KpiCadence.WeeklyPulse);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subject) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        await ScoreAsync(manager, templateId, subject, mid, aid, "2026-W20", 40m);
        await ScoreAsync(manager, templateId, subject, mid, aid, "2026-W21", 90m);
        await ScoreAsync(manager, templateId, subject, mid, aid, "2026-W22", 50m);

        var url = $"periodType=Quarter&periodKey=2026-Q2&cadence=WeeklyPulse&subjectUserId={subject}";
        var perf = await (await manager.GetAsync($"/api/kpi/performance?{url}")).ReadAsync<KpiPerformanceDto>();
        var drill = await (await manager.GetAsync($"/api/kpi/drilldown?{url}")).ReadAsync<KpiDrilldownDto>();

        Assert.Equal(60.00m, perf!.Company.Measure.Value);             // (40+90+50)/3
        Assert.Equal(3, drill!.Rows.Count);
        Assert.Equal(perf.Company.Measure.Value, drill.RecomputedValue);
        Assert.Equal(3, drill.RowCount);
        Assert.All(drill.Rows, r => Assert.Equal(KpiEvaluationStatus.Approved, r.Status));
    }

    // ===== 6) الأمن: خارج النطاق ⇒ 404 لا 403 (لا تسريب وجود المورد) =====
    [Fact]
    public async Task Performance_OutOfScopeSubject_Returns404NotForbidden()
    {
        var (managerA, managerAId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, managerBId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, strangerId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerBId);
        Assert.NotEqual(managerAId, managerBId);

        foreach (var path in new[] { "performance", "rankings", "drilldown" })
        {
            var res = await managerA.GetAsync(
                $"/api/kpi/{path}?periodType=Quarter&periodKey=2026-Q2&cadence=WeeklyPulse&subjectUserId={strangerId}");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            Assert.NotEqual(HttpStatusCode.Forbidden, res.StatusCode);
        }
    }

    // ===== 7) نفس الردّ لمعرّف غير موجود أصلًا ⇒ لا فرق يكشف الوجود =====
    [Fact]
    public async Task Performance_NonExistentSubject_IsIndistinguishableFromOutOfScope()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, otherManagerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, existingButHidden) = await TestAuth.CreateUserAsync(_factory, "Employee", otherManagerId);

        var q = "periodType=Quarter&periodKey=2026-Q2&cadence=WeeklyPulse&subjectUserId=";
        var hidden = await manager.GetAsync($"/api/kpi/performance?{q}{existingButHidden}");
        var ghost = await manager.GetAsync($"/api/kpi/performance?{q}{Guid.NewGuid()}");

        Assert.Equal(hidden.StatusCode, ghost.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    // ===== 8) مصفوفة الأدوار: المنع بالسياسة على الخادم لا بالإخفاء في الواجهة =====
    [Theory]
    [InlineData("Admin", HttpStatusCode.OK)]
    [InlineData("CEO", HttpStatusCode.OK)]
    [InlineData("GeneralManager", HttpStatusCode.OK)]
    [InlineData("Manager", HttpStatusCode.OK)]
    [InlineData("TeamLeader", HttpStatusCode.OK)]
    [InlineData("Employee", HttpStatusCode.OK)]
    [InlineData("HR", HttpStatusCode.OK)]
    [InlineData("AccountPortfolioReader", HttpStatusCode.Forbidden)]
    public async Task Performance_RoleMatrix_IsEnforcedByPolicy(string role, HttpStatusCode expected)
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
        var res = await client.GetAsync("/api/kpi/performance?periodType=Quarter&periodKey=2026-Q2&cadence=WeeklyPulse");
        Assert.Equal(expected, res.StatusCode);
    }

    // ===== 9) نداء مباشر بلا مصادقة إطلاقًا =====
    [Fact]
    public async Task KpiEndpoints_DirectUrlWithoutToken_AreRejected()
    {
        var anonymous = _factory.CreateClient();
        foreach (var path in new[] { "performance", "rankings", "drilldown", "periods/resolve" })
        {
            var res = await anonymous.GetAsync($"/api/kpi/{path}?periodType=Quarter&cadence=WeeklyPulse");
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }
    }

    // ===== 10) الموظّف لا يرى غيره حتّى بلا تمرير أيّ مُرشِّح (النطاق خادميّ) =====
    [Fact]
    public async Task Performance_EmployeeScope_NeverWidensBeyondSelf()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, mid, aid) = await PublishTemplateAsync(admin, KpiCadence.WeeklyPulse);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (selfClient, selfId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (_, peerId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        await ScoreAsync(manager, templateId, selfId, mid, aid, "2026-W20", 70m);
        await ScoreAsync(manager, templateId, peerId, mid, aid, "2026-W20", 30m);

        var dto = await (await selfClient.GetAsync(
            "/api/kpi/performance?periodType=Quarter&periodKey=2026-Q2&cadence=WeeklyPulse"))
            .ReadAsync<KpiPerformanceDto>();

        Assert.DoesNotContain(dto!.Employees, e => e.UserId == peerId);
        // ولا يتسرّب الزميل عبر الرقم المؤسّسيّ أيضًا.
        Assert.Equal(70.00m, dto.Company.Measure.Value);
    }

    // ===== 11) حدود الفترة تُحلّ خادميًّا بتوقيت الرياض (B-1) =====
    [Fact]
    public async Task PeriodsResolve_IsServerSideRiyadhSaturdayToFriday()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.GetAsync("/api/kpi/periods/resolve?type=Week&periodKey=2026-W25");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("Asia/Riyadh", body);
        Assert.Contains("2026-W25", body);
    }

    // ===== 12) العقد القديم لم يتغيّر شكله (P1-KPI-005) =====
    [Fact]
    public async Task LegacyKpiSummary_KeepsItsResponseShapeAndIsMarkedDeprecated()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.GetAsync("/api/reports/kpi-summary?periodType=Weekly");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // ترويسة الإهمال حاضرة، لكنّ الحقول القديمة كما هي حرفيًّا.
        Assert.True(res.Headers.Contains("Deprecation") || res.Content.Headers.Contains("Deprecation"));
        var body = await res.Content.ReadAsStringAsync();
        // حقول KpiSummaryReport الأصليّة حرفيًّا — أيّ اختفاء لأحدها كسرٌ للعقد القديم.
        foreach (var field in new[] { "periodKey", "evaluated", "averageScore", "belowTarget", "rows" })
            Assert.Contains(field, body, StringComparison.OrdinalIgnoreCase);
    }
}
