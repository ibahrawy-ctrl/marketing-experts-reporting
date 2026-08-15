using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Enums;
using Reporting.Domain.Projects360;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// **إغلاق عائقَي W6** (CPW-R3 · قرارا المالك FINDING-W6-03 و FINDING-W6-04) — عبر HTTP حقيقيّ.
///
/// <para>
/// **ما يُثبَت هنا شيئان لا ثالث لهما**: (1) أنّ الصحّة **تُخزَّن فعلًا** في الأعمدة الثلاثة وأنّ
/// الأسباب **لا تُخزَّن**؛ (2) أنّ «غير موجود» و«موجود خارج نطاقي» لا يُفرَّق بينهما بأيّ وسيلة.
/// </para>
///
/// <para>
/// **قاعدة الاختبار مشتركة دائمة وتتراكم** ⟹ كلّ تجهيزة تبني عميلها ومشروعها بوسم فريد، ولا
/// يعتمد أيّ تأكيد على عدّ عالميّ لصفوف الجدول.
/// </para>
/// </summary>
[Collection("Integration")]
public class Project360HealthAndAuthorizationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public Project360HealthAndAuthorizationTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ==================================================================
    // FINDING-W6-03 — الصحّة المخزَّنة
    // ==================================================================

    /// <summary>
    /// مشروع بلا أهداف ولا مؤشّرات ولا تواريخ: المكوّنان المستبعَدان يصدران سببيهما صراحةً
    /// (<c>health.kpi.excluded</c> و<c>health.schedule.excluded</c>) ويُعاد توزيع وزنيهما،
    /// ولا يُعامَل الغياب صفرًا.
    ///
    /// <para>
    /// **لماذا يبقى المشروع مقيَّمًا رغم ذلك؟** لأنّ <c>Project.ProgressPercent</c> عمود
    /// **غير قابل للإفراغ** — فلا سبيل لتمثيل «تقدّم غير معلَن» في النموذج القائم، ومكوّن التقدّم
    /// متاح دائمًا بحكم المخطَّط. حالة «لم يُقيَّم» (الختم <c>null</c>) تبقى مسارًا دفاعيًّا في
    /// <c>ProjectHealthPolicy</c> لا يبلغه المشروع ما دام العمود على حاله. تغيير ذلك يستلزم
    /// هجرة على العمود — **خارج نطاق هذه الحزمة** بالنصّ.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Recompute_ProjectWithoutKpisOrDates_ExcludesBothComponents_WithoutTreatingThemAsZero()
    {
        var fx = await NewFixtureAsync(progressPercent: null, withDates: false);

        var health = await RecomputeAsync(fx.Admin, fx.ProjectId);

        Assert.Null(health.KpiScore);
        Assert.Null(health.ScheduleScore);
        Assert.Contains(ProjectHealthReasonCodes.KpiComponentExcluded, health.ReasonCodes);
        Assert.Contains(ProjectHealthReasonCodes.ScheduleComponentExcluded, health.ReasonCodes);

        // مكوّن التقدّم وحده متاح ⟹ النتيجة تساويه بعد إعادة توزيع الأوزان، لا متوسّطًا مخفَّضًا.
        Assert.Equal(health.ProgressPercent, health.Score);

        var (percent, status, stamp) = await ReadStoredHealthAsync(fx.ProjectId);
        Assert.NotNull(stamp);
        Assert.Equal(health.Score, percent);
        Assert.Equal(health.Status, status);
    }

    /// <summary>
    /// أوّل مجموعة مؤشّرات فعليّة: الأعمدة الثلاثة تُكتب، والرقم المخزَّن **يطابق** الرقم المعاد
    /// في العقد — فلا يعرض المشروع الواحد رقمين متناقضين بين المسارين.
    /// </summary>
    [Fact]
    public async Task Recompute_WritesTheThreeColumns_MatchingTheReturnedContract()
    {
        var fx = await NewFixtureAsync();
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف", weight: 100m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objective, "مؤشّر", weight: 100m, value: 80m);

        var health = await RecomputeAsync(fx.Admin, fx.ProjectId);
        Assert.NotNull(health.Score);
        Assert.NotNull(health.Status);
        Assert.NotNull(health.LastEvaluatedAtUtc);

        var (percent, status, stamp) = await ReadStoredHealthAsync(fx.ProjectId);
        Assert.Equal(health.Score, percent);
        Assert.Equal(health.Status, status);
        Assert.NotNull(stamp);
        Assert.Equal(stamp, health.PersistedAtUtc);
    }

    /// <summary>
    /// **الأسباب لا تُخزَّن أبدًا** (W1-A بند 1): تعود في الاستجابة، ولا يوجد لها عمود ولا جدول
    /// ولا أثر في الصفّ المخزَّن — يُثبَت بأنّ أعمدة المشروع الثلاثة هي كلّ ما تغيّر.
    /// </summary>
    [Fact]
    public async Task Recompute_DerivesReasons_ButNeverPersistsThem()
    {
        var fx = await NewFixtureAsync();
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف", weight: 100m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objective, "مؤشّر", weight: 100m, value: 10m);

        var health = await RecomputeAsync(fx.Admin, fx.ProjectId);
        Assert.NotEmpty(health.ReasonCodes);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // لا عمود يحمل الأسباب على الكيان — الفحص على مستوى النموذج لا على مستوى قيمة صفّ واحد.
        var projectProperties = db.Model.FindEntityType(typeof(Project))!
            .GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(projectProperties, n => n.Contains("Reason", StringComparison.OrdinalIgnoreCase));

        // ولا جدول للأسباب في النموذج كلّه.
        Assert.DoesNotContain(db.Model.GetEntityTypes(),
            e => e.ClrType.Name.Contains("HealthReason", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// **Idempotent وظيفيًّا**: مدخلات لم تتغيّر ⟹ نفس النتيجة ونفس الحالة ونفس الأسباب.
    /// الختم وحده يتقدّم لأنّه يسجّل «متى حُسِب» لا «ما النتيجة».
    /// </summary>
    [Fact]
    public async Task Recompute_IsIdempotent_WhenInputsUnchanged()
    {
        var fx = await NewFixtureAsync();
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف", weight: 100m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objective, "مؤشّر", weight: 100m, value: 80m);

        var first = await RecomputeAsync(fx.Admin, fx.ProjectId);
        var second = await RecomputeAsync(fx.Admin, fx.ProjectId);

        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.KpiScore, second.KpiScore);
        Assert.Equal(first.ScheduleScore, second.ScheduleScore);
        Assert.Equal(first.ReasonCodes, second.ReasonCodes);
        Assert.True(second.LastEvaluatedAtUtc >= first.LastEvaluatedAtUtc);
    }

    /// <summary>
    /// **الطفرة تحدّث الصحّة في نفس وحدة العمل**: تسجيل قراءة يدويّة أفضل يرفع الرقم المخزَّن
    /// **بلا** استدعاء إعادة احتساب صريحة — وهو جوهر «لا طفرة تُحفَظ بصحّة بائتة».
    /// </summary>
    [Fact]
    public async Task ManualReading_UpdatesStoredHealth_WithoutExplicitRecompute()
    {
        var fx = await NewFixtureAsync();
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف", weight: 100m);
        var kpi = await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objective, "مؤشّر", weight: 100m, value: 20m);

        var (lowPercent, _, lowStamp) = await ReadStoredHealthAsync(fx.ProjectId);
        Assert.NotNull(lowStamp);

        var better = await fx.Admin.PostAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/objectives/{objective}/kpis/{kpi}/readings",
            new CreateProjectKpiReadingRequest(new DateOnly(2026, 4, 1), 95m));
        Assert.Equal(HttpStatusCode.OK, better.StatusCode);

        var (highPercent, _, highStamp) = await ReadStoredHealthAsync(fx.ProjectId);
        Assert.True(highPercent > lowPercent, $"{highPercent} يجب أن يفوق {lowPercent}");
        Assert.True(highStamp >= lowStamp);
    }

    /// <summary>تعديل مستهدَف المؤشّر مدخَل حقيقيّ للـPolicy ⟹ يعيد الاحتساب ويكتب رقمًا مختلفًا.</summary>
    [Fact]
    public async Task EditingKpiTarget_RecomputesStoredHealth()
    {
        var fx = await NewFixtureAsync();
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف", weight: 100m);
        var kpi = await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objective, "مؤشّر", weight: 100m, value: 50m);

        var (before, _, _) = await ReadStoredHealthAsync(fx.ProjectId);

        // مضاعفة المستهدَف تنصّف الإنجاز ⟹ صحّة أدنى حتمًا.
        var res = await fx.Admin.PutAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/objectives/{objective}/kpis/{kpi}",
            new UpdateProjectKpiRequest("مؤشّر", TargetValue: 200m, Weight: 100m));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var (after, _, _) = await ReadStoredHealthAsync(fx.ProjectId);
        Assert.True(after < before, $"{after} يجب أن يقلّ عن {before}");
    }

    /// <summary>
    /// **الترجيح على مستويين** (DEC-W4-03) لا متوسّطًا مسطَّحًا: هدف وزنه 90 بمؤشّر واحد ضعيف
    /// وهدف وزنه 10 بمؤشّرين ممتازين ⟹ النتيجة تميل للهدف الثقيل لا لعدد المؤشّرات.
    /// </summary>
    [Fact]
    public async Task Recompute_UsesTwoLevelWeighting_NotFlatKpiAverage()
    {
        var fx = await NewFixtureAsync();

        var heavy = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف ثقيل", weight: 90m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, heavy, "ضعيف", weight: 100m, value: 20m);

        var light = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف خفيف", weight: 10m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, light, "ممتاز ١", weight: 50m, value: 100m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, light, "ممتاز ٢", weight: 50m, value: 100m);

        var health = await RecomputeAsync(fx.Admin, fx.ProjectId);

        // المتوسّط المسطَّح على المؤشّرات = (20+100+100)/3 ≈ 73.3 — والترجيح الصحيح = 0.9×20 + 0.1×100 = 28.
        Assert.NotNull(health.KpiScore);
        Assert.Equal(28m, Math.Round(health.KpiScore!.Value, 2));
    }

    /// <summary>الصحّة المعروضة في اللوحة هي **نفسها** المخزَّنة — مصدر واحد لا مساران.</summary>
    [Fact]
    public async Task Overview_ReportsSameHealth_AsPersistedColumns()
    {
        var fx = await NewFixtureAsync();
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف", weight: 100m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objective, "مؤشّر", weight: 100m, value: 70m);

        var overview = await GetAsync<ProjectOverviewDto>(fx.Admin, $"/api/projects/{fx.ProjectId}/overview");
        var (percent, status, stamp) = await ReadStoredHealthAsync(fx.ProjectId);

        Assert.Equal(percent, overview.Health.Score);
        Assert.Equal(status, overview.Health.Status);
        Assert.Equal(stamp, overview.Health.PersistedAtUtc);
    }

    /// <summary>**صفر كتابة في مسار قراءة**: طلب اللوحة لا يغيّر الختم مهما تكرّر.</summary>
    [Fact]
    public async Task Overview_IsWriteless_AndDoesNotTouchTheHealthStamp()
    {
        var fx = await NewFixtureAsync();
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف", weight: 100m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objective, "مؤشّر", weight: 100m, value: 70m);

        var (_, _, before) = await ReadStoredHealthAsync(fx.ProjectId);
        for (var i = 0; i < 3; i++)
            await GetAsync<ProjectOverviewDto>(fx.Admin, $"/api/projects/{fx.ProjectId}/overview");
        var (_, _, after) = await ReadStoredHealthAsync(fx.ProjectId);

        Assert.Equal(before, after);
    }

    /// <summary>
    /// **الأعمدة المخزَّنة ثلاثة لا رابع لها**: إعادة الاحتساب لا تمسّ <c>ProgressPercent</c>
    /// ولا الحالة ولا التواريخ — فالصحّة مشتقّة من الحالة ولا تعيد كتابتها.
    /// </summary>
    [Fact]
    public async Task Recompute_TouchesHealthColumnsOnly_LeavingProjectStateIntact()
    {
        var fx = await NewFixtureAsync();
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف", weight: 100m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objective, "مؤشّر", weight: 100m, value: 70m);

        var before = await ReadProjectStateAsync(fx.ProjectId);
        await RecomputeAsync(fx.Admin, fx.ProjectId);
        var after = await ReadProjectStateAsync(fx.ProjectId);

        Assert.Equal(before, after);
    }

    // ===== تخويل مسار إعادة الاحتساب =====

    /// <summary>المستوى التشغيليّ (D-07): قائد الفريق ومدير الحساب المسؤولان يعيدان الاحتساب.</summary>
    [Fact]
    public async Task Recompute_IsAllowedFor_ManagementAndOwningTeamLeaderAndAccountManager()
    {
        var fx = await NewFixtureAsync();
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف", weight: 100m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objective, "مؤشّر", weight: 100m, value: 70m);

        foreach (var client in new[] { fx.Admin, fx.Ceo, fx.GeneralManager, fx.OwnerTeamLeader, fx.AccountManager })
        {
            var res = await client.PostAsync($"/api/projects/{fx.ProjectId}/health/recompute", null);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
    }

    /// <summary>
    /// من يرى المشروع ولا يملك القدرة التشغيليّة ⟹ **403** (لا 404): لا تسريب لأنّه يعرف
    /// المشروع أصلًا. وبلا توكن ⟹ **401** قبل أيّ منطق.
    /// </summary>
    [Fact]
    public async Task Recompute_ReaderWithoutCapability_Is403_AndAnonymousIs401()
    {
        var fx = await NewFixtureAsync();

        var employee = await fx.TeamEmployee.PostAsync($"/api/projects/{fx.ProjectId}/health/recompute", null);
        Assert.Equal(HttpStatusCode.Forbidden, employee.StatusCode);
        Assert.Contains("auth.forbidden", await employee.Content.ReadAsStringAsync());

        var anonymous = await fx.Anonymous.PostAsync($"/api/projects/{fx.ProjectId}/health/recompute", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    /// <summary>
    /// FINDING-W6-04 على مسار الكتابة أيضًا: خارج النطاق وغير الموجود يعودان بـ404 وبجسم متطابق
    /// حرفيًّا — فلا يُستعمَل مسار الطفرة نفسه أداةَ تعداد.
    /// </summary>
    [Fact]
    public async Task Recompute_OutOfScopeAndMissing_AreIndistinguishable()
    {
        var fx = await NewFixtureAsync();
        var ghost = Guid.NewGuid();

        var outOfScope = await fx.ForeignTeamLeader.PostAsync($"/api/projects/{fx.ProjectId}/health/recompute", null);
        var missing = await fx.ForeignTeamLeader.PostAsync($"/api/projects/{ghost}/health/recompute", null);

        Assert.Equal(HttpStatusCode.NotFound, outOfScope.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var outOfScopeBody = await ErrorShapeAsync(outOfScope);
        Assert.Contains(Project360ErrorCodes.ProjectNotFound, outOfScopeBody);
        Assert.Equal(outOfScopeBody, await ErrorShapeAsync(missing));
    }

    // ==================================================================
    // FINDING-W6-04 — منع التعداد على كامل سطح Project 360
    // ==================================================================

    /// <summary>
    /// المصفوفة الكاملة: كلّ هويّة خارج النطاق × كلّ مسار قراءة × (مشروع قائم مقابل معرّف وهميّ)
    /// ⟹ **404** وجسم متطابق في الحالتين. هذا هو الاختبار الذي يمنع عودة الانحراف بأيّ مسار جديد.
    /// </summary>
    [Fact]
    public async Task AllReadPaths_AreIndistinguishable_ForEveryOutOfScopeIdentity()
    {
        var fx = await NewFixtureAsync();
        var ghost = Guid.NewGuid();

        foreach (var client in new[] { fx.ForeignManager, fx.ForeignTeamLeader, fx.Viewer })
            foreach (var (existing, missing) in ReadPaths(fx.ProjectId).Zip(ReadPaths(ghost)))
            {
                var existingRes = await client.GetAsync(existing);
                var missingRes = await client.GetAsync(missing);

                Assert.Equal(HttpStatusCode.NotFound, existingRes.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, missingRes.StatusCode);
                Assert.Equal(await ErrorShapeAsync(existingRes), await ErrorShapeAsync(missingRes));
            }
    }

    /// <summary>
    /// المسارات المتداخلة لا تفتح نافذة جانبيّة: معرّف هدف/مؤشّر صحيح **تحت مشروع خارج النطاق**
    /// يعود بنفس عقد «المشروع غير موجود» قبل أن يُستعلَم عن الابن أصلًا.
    /// </summary>
    [Fact]
    public async Task NestedPaths_DoNotLeakChildExistence_AcrossProjectBoundary()
    {
        var fx = await NewFixtureAsync();
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف", weight: 100m);
        var kpi = await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objective, "مؤشّر", weight: 100m, value: 70m);

        var realChild = await fx.ForeignTeamLeader.GetAsync(
            $"/api/projects/{fx.ProjectId}/objectives/{objective}/kpis/{kpi}");
        var fakeChild = await fx.ForeignTeamLeader.GetAsync(
            $"/api/projects/{fx.ProjectId}/objectives/{Guid.NewGuid()}/kpis/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, realChild.StatusCode);
        Assert.Equal(await ErrorShapeAsync(realChild), await ErrorShapeAsync(fakeChild));
    }

    /// <summary>
    /// مسارات الكتابة أيضًا: خارج النطاق ⟹ 404 لا 403، وإلّا صار زرّ الكتابة كاشفًا لوجود
    /// مشاريع لا يراها المستخدم.
    /// </summary>
    [Fact]
    public async Task WritePaths_OutOfScope_Return404_NotForbidden()
    {
        var fx = await NewFixtureAsync();

        var objective = await fx.ForeignTeamLeader.PostAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/objectives", new CreateProjectObjectiveRequest("هدف متسلّل"));
        Assert.Equal(HttpStatusCode.NotFound, objective.StatusCode);

        var deliverable = await fx.ForeignTeamLeader.PostAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/contract-deliverables",
            new CreateProjectContractDeliverableRequest("monthly_report"));
        Assert.Equal(HttpStatusCode.NotFound, deliverable.StatusCode);
    }

    // ==================================================================
    // العقود القائمة — لا يُنقَض شيء بحزمة الإغلاق
    // ==================================================================

    /// <summary>لا مسار إنشاء مؤشّر يتيم على مستوى المشروع (D-02) — الأب إلزاميّ ويبقى كذلك.</summary>
    [Fact]
    public async Task NoProjectLevelKpiCreateRoute_Exists()
    {
        var fx = await NewFixtureAsync();

        var orphan = await fx.Admin.PostAsJsonAsync($"/api/projects/{fx.ProjectId}/kpis",
            new CreateProjectKpiRequest("مؤشّر يتيم", TargetValue: 100m));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, orphan.StatusCode);
    }

    /// <summary>مسارات المشاريع القائمة قبل CPW-R3 ما زالت حيّة على حالها (صفر إزالة/تغيير).</summary>
    [Fact]
    public async Task PreExistingProjectRoutes_RemainIntact()
    {
        var fx = await NewFixtureAsync();

        foreach (var path in new[]
                 {
                     $"/api/projects/{fx.ProjectId}",
                     $"/api/projects/{fx.ProjectId}/workstreams",
                 })
            Assert.Equal(HttpStatusCode.OK, (await fx.Admin.GetAsync(path)).StatusCode);
    }

    // ==================================================================
    // تجهيزات
    // ==================================================================

    private static string[] ReadPaths(Guid projectId) =>
        new[]
        {
            $"/api/projects/{projectId}/overview",
            $"/api/projects/{projectId}/strategy",
            $"/api/projects/{projectId}/objectives",
            $"/api/projects/{projectId}/kpis",
            $"/api/projects/{projectId}/contract-deliverables",
            $"/api/projects/{projectId}/risks",
            $"/api/projects/{projectId}/decisions",
            $"/api/projects/{projectId}/notes",
        };

    /// <summary>
    /// جسم الخطأ **بعد إسقاط <c>traceId</c>** — فهو معرّف طلب يتغيّر لكلّ نداء بطبيعته ولا يحمل
    /// معلومة عن المورد، فإبقاؤه يجعل أيّ استجابتين مختلفتين حتمًا ويُفرِغ اختبار عدم التمييز من معناه.
    /// كلّ ما عداه (الحالة، الرمز، الرسالة، الحقول) يجب أن يتطابق حرفيًّا.
    /// </summary>
    private static async Task<string> ErrorShapeAsync(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        return System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\"\\s*:\\s*\"[^\"]*\"", "\"traceId\":\"*\"");
    }

    private static async Task<ProjectHealthDto> RecomputeAsync(HttpClient client, Guid projectId)
    {
        var res = await client.PostAsync($"/api/projects/{projectId}/health/recompute", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<ProjectHealthDto>())!;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        var res = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<T>())!;
    }

    /// <summary>قراءة الأعمدة الثلاثة من القاعدة مباشرة — لا عبر أيّ عقد قد يعيد الاحتساب.</summary>
    private async Task<(decimal Percent, ProjectHealthStatus Status, DateTime? Stamp)> ReadStoredHealthAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.HealthPercent, p.HealthStatus, p.HealthComputedAtUtc })
            .FirstAsync();
        return (row.HealthPercent, row.HealthStatus, row.HealthComputedAtUtc);
    }

    private async Task<object> ReadProjectStateAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Name, p.Status, p.ProgressPercent, p.StartDate, p.EndDate, p.ServiceType })
            .FirstAsync();
    }

    private static async Task<Guid> CreateObjectiveAsync(HttpClient client, Guid projectId, string name, decimal weight)
    {
        var res = await client.PostAsJsonAsync($"/api/projects/{projectId}/objectives",
            new CreateProjectObjectiveRequest(name, Weight: weight));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<ProjectObjectiveDto>())!.Id;
    }

    private static async Task<Guid> CreateKpiWithReadingAsync(
        HttpClient client, Guid projectId, Guid objectiveId, string name, decimal weight, decimal value)
    {
        var created = await client.PostAsJsonAsync($"/api/projects/{projectId}/objectives/{objectiveId}/kpis",
            new CreateProjectKpiRequest(name, TargetValue: 100m, Weight: weight));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var kpiId = (await created.ReadAsync<ProjectKpiDto>())!.Id;

        var reading = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/objectives/{objectiveId}/kpis/{kpiId}/readings",
            new CreateProjectKpiReadingRequest(new DateOnly(2026, 3, 1), value));
        Assert.Equal(HttpStatusCode.OK, reading.StatusCode);
        return kpiId;
    }

    private sealed record Fixture(
        Guid ClientId,
        Guid ProjectId,
        HttpClient Admin,
        HttpClient Ceo,
        HttpClient GeneralManager,
        HttpClient ForeignManager,
        HttpClient OwnerTeamLeader,
        HttpClient ForeignTeamLeader,
        HttpClient AccountManager,
        HttpClient TeamEmployee,
        HttpClient Viewer,
        HttpClient Anonymous);

    /// <summary>
    /// عميل ومشروع وفريق مالك وتسع هويّات — وهي بالضبط المداخل التي تفرّق بينها بوّابة التخويل.
    /// <paramref name="progressPercent"/> و<paramref name="withDates"/> يسمحان ببناء مشروع
    /// **بلا أيّ مدخل للـPolicy** لإثبات حالة «لم يُقيَّم».
    /// </summary>
    private async Task<Fixture> NewFixtureAsync(decimal? progressPercent = 50m, bool withDates = true)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var ceo = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var foreignManager = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var ownerTl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var foreignTl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var accountManager = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var employee = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Viewer);

        var ownerTeam = await TestAuth.CreateTeamWithLeaderAsync(_factory, ownerTl.UserId, employee.UserId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, foreignTl.UserId);

        Guid clientId, projectId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var tag = Guid.NewGuid().ToString("N")[..8];
            var client = new Client { Name = $"عميل صحّة {tag}", Status = ClientStatus.Active };
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            clientId = client.Id;

            var project = new Project
            {
                ClientId = clientId,
                Name = $"مشروع صحّة {tag}",
                ServiceType = ServiceType.Other,
                Status = ProjectStatus.Active,
                OwnerTeamId = ownerTeam,
                TeamLeaderId = ownerTl.UserId,
                AccountManagerId = accountManager.UserId,
                ProgressPercent = progressPercent ?? 0m,
                StartDate = withDates ? new DateOnly(2026, 1, 1) : null,
                EndDate = withDates ? new DateOnly(2026, 12, 31) : null,
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            projectId = project.Id;
        }

        return new Fixture(
            clientId, projectId,
            admin, ceo.Client, gm.Client, foreignManager.Client,
            ownerTl.Client, foreignTl.Client, accountManager.Client, employee.Client, viewer.Client,
            _factory.CreateClient());
    }
}
