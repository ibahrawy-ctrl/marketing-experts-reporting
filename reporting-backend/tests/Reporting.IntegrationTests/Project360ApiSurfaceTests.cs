using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// سطح الـAPI لـProject 360 (CPW-R3 · W5) — **عبر HTTP حقيقيّ** بتوكن حقيقيّ لكلّ هويّة،
/// لأنّ ما تختبره W5 هو المسار والحالة والعقد لا منطق الأعمال (ذاك اختُبر في W4).
///
/// <para>
/// **قاعدة الاختبار مشتركة دائمة وتتراكم** ⟹ كلّ تجهيزة تبني عميلها ومشروعها وفريقها ومستخدميها
/// بوسم فريد، ولا يعتمد أيّ تأكيد على عدّ عالميّ لصفوف الجدول.
/// </para>
/// </summary>
[Collection("Integration")]
public class Project360ApiSurfaceTests
{
    /// <summary>
    /// نفس مصدر الحقيقة المستعمَل في <see cref="CustomWebApplicationFactory"/>: يقرأ
    /// <c>TEST_DB_CONNECTION</c> ليعمل على قاعدة معزولة نظيفة بلا تعديل الشيفرة،
    /// ويسقط افتراضيًّا إلى <c>reporting_test</c> المشتركة بلا تغيير سلوك.
    /// </summary>
    private static readonly string TestConnectionString =
        System.Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? "Host=localhost;Database=reporting_test;Username=ibrahimelbahrawi";

    /// <summary>
    /// <c>OVERVIEW_SQL_QUERY_COUNT</c> — عدد أوامر SQL لرحلة لوحة واحدة. **ثابت** لا يتناسب مع عدد
    /// الأهداف أو المؤشّرات؛ مثبَّت هنا رقمًا صريحًا كي يفشل أيّ انحدار يُدخل N+1 خِلسة.
    ///
    /// <para>
    /// **12 ⟵ 14 في P360-WF-R2**: استعلامان ثابتان أُضيفا عمدًا — أسماء المسنَدين (§11) وخريطة
    /// القدرات (§12). كلاهما لا يتناسب مع عدد الأهداف، والدليل أنّ مشروع الهدف الواحد ومشروع
    /// العشرين هدفًا ما زالا متساويَين في الاختبار أدناه.
    /// </para>
    /// </summary>
    private const int OverviewSqlQueryCount = 14;

    private readonly CustomWebApplicationFactory _factory;

    public Project360ApiSurfaceTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ==================================================================
    // §25 — Catalog Bootstrap: حتميّ · Idempotent · آمن لإعادة التشغيل
    // ==================================================================

    /// <summary>
    /// DEC-W4-01 — تشغيلتان متتاليتان: الأولى تُنشئ 38 قيمة (6 أقسام + 14 حقلًا + 18 مخرَجًا)،
    /// والثانية <c>Created = 0</c> و<c>Updated = 0</c> بتطابق لقطة كاملة (المعرّف والاسم والترتيب).
    ///
    /// <para>
    /// **لماذا معاملة تُلغى**: البذر يجري أصلًا عند إقلاع التطبيق، فلا سبيل لإثبات «التشغيلة الأولى»
    /// على قاعدة مشتركة دائمة إلّا بمحو المجالات الثلاثة داخل معاملة ثمّ التراجع عنها بالكامل.
    /// لا كتابة باقية على أيّ قاعدة، ولا مساس بمجالات الكتالوج الأخرى الـ19.
    /// </para>
    /// </summary>
    [Fact]
    public async Task CatalogBootstrap_SecondRun_CreatesNothing_AndUpdatesNothing()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var domains = new[]
        {
            Project360CatalogDomains.StrategySection,
            Project360CatalogDomains.StrategyField,
            Project360CatalogDomains.ContractDeliverable,
        };

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await db.ExecutionTaxonomyValues.Where(v => domains.Contains(v.Domain)).ExecuteDeleteAsync();
            Assert.Equal(0, await db.ExecutionTaxonomyValues.CountAsync(v => domains.Contains(v.Domain)));

            await ExecutionTaxonomySeeder.SeedAsync(scope.ServiceProvider);
            var run1 = await SnapshotCatalogAsync(db, domains);

            await ExecutionTaxonomySeeder.SeedAsync(scope.ServiceProvider);
            var run2 = await SnapshotCatalogAsync(db, domains);

            // Run #1 — الحصيلة المتوقَّعة حرفيًّا من R2 §9-7 وW1-A بند 3.
            Assert.Equal(38, run1.Count);
            Assert.Equal(6, run1.Count(v => v.Domain == Project360CatalogDomains.StrategySection));
            Assert.Equal(14, run1.Count(v => v.Domain == Project360CatalogDomains.StrategyField));
            Assert.Equal(18, run1.Count(v => v.Domain == Project360CatalogDomains.ContractDeliverable));

            // Run #2 — Created = 0 (نفس العدد) و Updated = 0 (نفس اللقطة حقلًا بحقل).
            Assert.Equal(run1.Count, run2.Count);
            Assert.Equal(run1, run2);

            // حتميّة الترتيب: عدّاد مستقلّ لكلّ مجال يبدأ من 10 ويتصاعد بـ10.
            var sections = run1.Where(v => v.Domain == Project360CatalogDomains.StrategySection)
                .OrderBy(v => v.SortOrder).Select(v => v.SortOrder).ToArray();
            Assert.Equal(new[] { 10, 20, 30, 40, 50, 60 }, sections);

            // كلّ رمز حقل يحمل بادئة قسم موجود فعلًا في الكتالوج ⟹ لا حقل يتيم بلا قسم.
            var sectionCodes = run1.Where(v => v.Domain == Project360CatalogDomains.StrategySection)
                .Select(v => v.Code).ToHashSet(StringComparer.Ordinal);
            Assert.All(run1.Where(v => v.Domain == Project360CatalogDomains.StrategyField),
                v => Assert.Contains(ProjectStrategyApplicability.SectionOf(v.Code)!, sectionCodes));
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ==================================================================
    // §4 + DEC-W4-02 — مخطَّط الاستراتيجيّة مقود بالبيانات لا بشرط لكلّ خدمة
    // ==================================================================

    [Fact]
    public async Task StrategySchema_IsServiceScoped_ByCatalogDataOnly()
    {
        var fx = await NewFixtureAsync();
        var seo = await NewProjectAsync(fx, ServiceType.Seo);
        var social = await NewProjectAsync(fx, ServiceType.Social);

        var seoSchema = await GetAsync<ProjectStrategySchemaDto>(fx.Admin, $"/api/projects/{seo}/strategy/schema");
        var socialSchema = await GetAsync<ProjectStrategySchemaDto>(fx.Admin, $"/api/projects/{social}/strategy/schema");

        // الأقسام العامّة تظهر للجميع، والأقسام المرتبطة بخدمة تظهر لخدمتها وحدها.
        foreach (var schema in new[] { seoSchema, socialSchema })
        {
            Assert.Contains(schema.Sections, s => s.Code == "business");
            Assert.Contains(schema.Sections, s => s.Code == "audience");
            Assert.Contains(schema.Sections, s => s.Code == "brand");
            Assert.Equal(ProjectStrategyCoreFields.Count, schema.CoreFields.Count);
        }

        Assert.Contains(seoSchema.Sections, s => s.Code == "seo");
        Assert.DoesNotContain(seoSchema.Sections, s => s.Code == "ads" || s.Code == "social");
        Assert.Contains(seoSchema.DynamicFields, f => f.FieldCode == "seo.keywords");
        Assert.DoesNotContain(seoSchema.DynamicFields, f => f.FieldCode == "ads.budget");

        Assert.Contains(socialSchema.Sections, s => s.Code == "social");
        Assert.DoesNotContain(socialSchema.Sections, s => s.Code == "seo" || s.Code == "ads");
        Assert.Contains(socialSchema.DynamicFields, f => f.FieldCode == "social.brand_voice");
        Assert.DoesNotContain(socialSchema.DynamicFields, f => f.FieldCode == "seo.keywords");

        // اسم القسم مرفَق بكلّ حقل مُقسَّم ⟹ الواجهة تبني المجموعات بلا أيّ رمز مكتوب يدويًّا.
        // الترشيح على الحقول الحاملة لبادئة قسم: قاعدة الاختبار المشتركة تحوي رموزًا بلا بادئة
        // من تجهيزات اختبارات أخرى، وهي عامّة بحكم التصميم (بلا قسم ⟹ تنطبق على كلّ الخدمات).
        Assert.All(seoSchema.DynamicFields.Where(f => f.FieldCode.Contains('.')), f =>
        {
            Assert.NotNull(f.SectionCode);
            Assert.NotNull(f.SectionNameAr);
            Assert.Contains(seoSchema.Sections, s => s.Code == f.SectionCode);
        });
    }

    [Fact]
    public async Task StrategyUpsert_FieldOutsideProjectService_IsRejectedWith400()
    {
        var fx = await NewFixtureAsync();
        var seo = await NewProjectAsync(fx, ServiceType.Seo);

        var applicable = await fx.Admin.PutAsJsonAsync($"/api/projects/{seo}/strategy",
            new UpsertProjectStrategyRequest(Vision: "رؤية",
                Attributes: new[] { new UpsertProjectStrategyAttributeRequest("seo.keywords", "seo", "كلمات") }));
        Assert.Equal(HttpStatusCode.OK, applicable.StatusCode);

        var notApplicable = await fx.Admin.PutAsJsonAsync($"/api/projects/{seo}/strategy",
            new UpsertProjectStrategyRequest(Vision: "رؤية",
                Attributes: new[] { new UpsertProjectStrategyAttributeRequest("ads.budget", "ads", "1000") }));

        Assert.Equal(HttpStatusCode.BadRequest, notApplicable.StatusCode);
        Assert.Contains(Project360ErrorCodes.StrategyFieldNotApplicable, await notApplicable.Content.ReadAsStringAsync());
    }

    // ==================================================================
    // §11 — PROJECT_LEVEL_KPI_CREATE_ROUTE = NONE
    // ==================================================================

    /// <summary>
    /// المسار المسطَّح للمؤشّرات قراءة فقط: لا يوجد <c>POST</c> نظير له في جدول التوجيه إطلاقًا،
    /// فمحاولة الإنشاء المباشرة عليه تُردّ بـ405 قبل بلوغ أيّ خدمة ⟹ استحالة مؤشّر يتيم بلا هدف.
    /// </summary>
    [Fact]
    public async Task ProjectLevelKpiRoute_IsReadOnly_NoCreateRouteExists()
    {
        var fx = await NewFixtureAsync();

        var read = await fx.Admin.GetAsync($"/api/projects/{fx.ProjectId}/kpis");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var create = await fx.Admin.PostAsJsonAsync($"/api/projects/{fx.ProjectId}/kpis",
            new CreateProjectKpiRequest("مؤشّر يتيم", TargetValue: 100m));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, create.StatusCode);

        // والمسار المتداخل تحت الهدف هو المدخل الوحيد للإنشاء.
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف", weight: 100m);
        var nested = await fx.Admin.PostAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/objectives/{objective}/kpis",
            new CreateProjectKpiRequest("مؤشّر تحت هدف", TargetValue: 100m));
        Assert.Equal(HttpStatusCode.OK, nested.StatusCode);
    }

    // ==================================================================
    // §14 + §28 — الترجيح على مستويين مثبَتًا من API النظرة العامّة
    // ==================================================================

    /// <summary>
    /// DEC-W4-03 — مثال المالك الحَكَم: هدف أ (وزن 70) بمؤشّرين متساويي الوزن 100% و0% ⟹ 50،
    /// وهدف ب (وزن 30) بمؤشّر 100% ⟹ 100. النتيجة **65** لا 66.67 (المتوسّط المسطَّح للمؤشّرات الثلاثة).
    /// </summary>
    [Fact]
    public async Task Overview_TwoLevelWeightedAggregation_Returns65_NotFlatAverage()
    {
        var fx = await NewFixtureAsync();

        var objectiveA = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف أ", weight: 70m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objectiveA, "أ-1", weight: 50m, value: 100m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objectiveA, "أ-2", weight: 50m, value: 0m);

        var objectiveB = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف ب", weight: 30m);
        await CreateKpiWithReadingAsync(fx.Admin, fx.ProjectId, objectiveB, "ب-1", weight: 50m, value: 100m);

        var overview = await GetAsync<ProjectOverviewDto>(fx.Admin, $"/api/projects/{fx.ProjectId}/overview");

        // المستوى الداخليّ — نتيجة كلّ هدف مرجَّحة بأوزان مؤشّراته.
        var a = overview.Objectives.Items.Single(o => o.Id == objectiveA);
        var b = overview.Objectives.Items.Single(o => o.Id == objectiveB);
        Assert.Equal(50m, a.ProgressPercent);
        Assert.Equal(100m, b.ProgressPercent);

        // المستوى الخارجيّ — ترجيح نتائج الأهداف بأوزان الأهداف.
        Assert.Equal(65m, overview.Kpis.AverageAchievementPercent);
        Assert.NotEqual(66.67m, overview.Kpis.AverageAchievementPercent);

        // رقم واحد للمفهوم الواحد عبر اللوحة كلّها — لا حقل يعرض متوسّطًا مسطَّحًا.
        Assert.Equal(65m, overview.Objectives.AverageAchievementPercent);
        Assert.Equal(65m, overview.Health.KpiScore);
        Assert.Equal(3, overview.Kpis.Total);
    }

    // ==================================================================
    // §29 — عدّاد الاستعلامات: OVERVIEW_SQL_QUERY_COUNT ثابت
    // ==================================================================

    /// <summary>
    /// عدد استعلامات لوحة النظرة العامّة **ثابت** مهما بلغ عدد الأهداف والمؤشّرات:
    /// مشروع بهدف واحد ومشروع بعشرين هدفًا يستهلكان العدد نفسه بالضبط ⟹ صفر N+1.
    /// </summary>
    [Fact]
    public async Task Overview_SqlQueryCount_IsConstant_RegardlessOfObjectiveCount()
    {
        var fx = await NewFixtureAsync();

        var small = await NewProjectAsync(fx, ServiceType.Other);
        await SeedObjectivesWithKpisAsync(fx.Admin, small, 1);

        var large = await NewProjectAsync(fx, ServiceType.Other);
        await SeedObjectivesWithKpisAsync(fx.Admin, large, 20);

        var smallCount = await CountOverviewQueriesAsync(fx.AdminUserId, small);
        var largeCount = await CountOverviewQueriesAsync(fx.AdminUserId, large);

        Assert.True(smallCount > 0, "لم يُلتقَط أيّ استعلام — المعترِض غير موصول.");
        Assert.Equal(smallCount, largeCount);                     // ثبات العدد بين 1 و20 هدفًا
        Assert.Equal(OverviewSqlQueryCount, smallCount);           // والقيمة نفسها مثبَّتة ضدّ الانحدار
        Assert.Equal(OverviewSqlQueryCount, largeCount);
    }

    // ==================================================================
    // §26 — مصفوفة التخويل: عشر هويّات × موارد W5 الستّة
    // ==================================================================

    [Fact]
    public async Task AuthorizationMatrix_ReadTier_AcrossAllSixResources()
    {
        var fx = await NewFixtureAsync();
        var paths = ReadPaths(fx.ProjectId);

        // ملاحظة عقد: القراءة المسموحة تُرجِع 2xx — و`GET /strategy` تحديدًا تُرجِع **204**
        // على مشروع بلا استراتيجيّة بعد (Ok(null) ⟹ NoContent)، وهو نجاح لا منع.
        // رؤية واسعة (governance/company) — تقرأ كلّ الموارد.
        foreach (var client in new[] { fx.Admin, fx.Ceo, fx.GeneralManager })
            foreach (var path in paths)
                Assert.True((await client.GetAsync(path)).IsSuccessStatusCode, path);

        // داخل النطاق بالانتماء لا بالدور: قائد الفريق المالك، مدير الحساب، وموظّف الفريق.
        foreach (var client in new[] { fx.OwnerTeamLeader, fx.AccountManager, fx.TeamEmployee })
            foreach (var path in paths)
                Assert.True((await client.GetAsync(path)).IsSuccessStatusCode, path);

        // خارج النطاق — مدير إدارة أخرى، قائد فريق آخر، مُطّلِع بلا انتماء.
        // FINDING-W6-04: **404 لا 403** — من لا يرى المشروع لا يُخبَر بوجوده.
        foreach (var client in new[] { fx.ForeignManager, fx.ForeignTeamLeader, fx.Viewer })
            foreach (var path in paths)
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);

        // بلا توكن — 401 قبل أيّ منطق.
        foreach (var path in paths)
            Assert.Equal(HttpStatusCode.Unauthorized, (await fx.Anonymous.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task AuthorizationMatrix_StructuralWrite_IsManagementOnly()
    {
        var fx = await NewFixtureAsync();

        // بنيويّ = إنشاء هدف / إنشاء مخرَج تعاقديّ — أدوار الإدارة داخل النطاق حصرًا.
        foreach (var client in new[] { fx.Admin, fx.Ceo, fx.GeneralManager })
        {
            var created = await client.PostAsJsonAsync($"/api/projects/{fx.ProjectId}/objectives",
                new CreateProjectObjectiveRequest($"هدف {Guid.NewGuid():N}"));
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        foreach (var client in new[] { fx.OwnerTeamLeader, fx.AccountManager, fx.TeamEmployee })
        {
            var objective = await client.PostAsJsonAsync($"/api/projects/{fx.ProjectId}/objectives",
                new CreateProjectObjectiveRequest("هدف ممنوع"));
            Assert.Equal(HttpStatusCode.Forbidden, objective.StatusCode);

            var deliverable = await client.PostAsJsonAsync($"/api/projects/{fx.ProjectId}/contract-deliverables",
                new CreateProjectContractDeliverableRequest("monthly_report"));
            Assert.Equal(HttpStatusCode.Forbidden, deliverable.StatusCode);
        }
    }

    [Fact]
    public async Task AuthorizationMatrix_OperationalWrite_ReachesOwnersButNotPlainEmployee()
    {
        var fx = await NewFixtureAsync();
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف تشغيليّ", weight: 100m);

        var deliverableRes = await fx.Admin.PostAsJsonAsync($"/api/projects/{fx.ProjectId}/contract-deliverables",
            new CreateProjectContractDeliverableRequest("monthly_report", PlannedQuantity: 12));
        Assert.Equal(HttpStatusCode.OK, deliverableRes.StatusCode);
        var deliverable = (await deliverableRes.ReadAsync<ProjectContractDeliverableDto>())!.Id;

        // D-07 — قائد الفريق ومدير الحساب المسؤولان يحدّثان الحالة والتقدّم دون امتلاك البنية.
        var tlStatus = await fx.OwnerTeamLeader.PatchAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/objectives/{objective}/status",
            new UpdateProjectObjectiveStatusRequest(ProjectObjectiveStatus.InProgress));
        Assert.Equal(HttpStatusCode.OK, tlStatus.StatusCode);

        var amProgress = await fx.AccountManager.PatchAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/contract-deliverables/{deliverable}/progress",
            new UpdateProjectContractDeliverableProgressRequest(ProjectDeliverableStatus.InProgress, 25m));
        Assert.Equal(HttpStatusCode.OK, amProgress.StatusCode);

        // موظّف الفريق يقرأ ولا يكتب في أيّ مستوى.
        var employeeStatus = await fx.TeamEmployee.PatchAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/objectives/{objective}/status",
            new UpdateProjectObjectiveStatusRequest(ProjectObjectiveStatus.Completed));
        Assert.Equal(HttpStatusCode.Forbidden, employeeStatus.StatusCode);
    }

    // ==================================================================
    // §27 — IDOR/BOLA على سطح الـHTTP
    // ==================================================================

    /// <summary>
    /// مشروع موجود خارج النطاق ومشروع غير موجود إطلاقًا يُردّان بـ**404** وبجسم استجابة
    /// **متطابق حرفيًّا** (نفس الرمز ونفس الرسالة ونفس الشكل)، فلا يستطيع المهاجم عدّ المشاريع
    /// ولا التفريق بين «غير موجود» و«موجود لكن ممنوع» — قرار المالك FINDING-W6-04.
    /// </summary>
    [Fact]
    public async Task Idor_ExistingOutOfScopeAndNonExistent_AreIndistinguishable()
    {
        var fx = await NewFixtureAsync();
        var ghost = Guid.NewGuid();

        foreach (var path in ReadPaths(fx.ProjectId).Zip(ReadPaths(ghost)))
        {
            var outOfScope = await fx.ForeignTeamLeader.GetAsync(path.First);
            var nonExistent = await fx.ForeignTeamLeader.GetAsync(path.Second);

            Assert.Equal(HttpStatusCode.NotFound, outOfScope.StatusCode);
            Assert.Equal(outOfScope.StatusCode, nonExistent.StatusCode);

            // تطابق الجسم كاملًا بعد إسقاط traceId وحده (معرّف طلب يتغيّر بطبيعته ولا يصف موردًا)
            // — لا رسالة ولا حقل ولا فارق شكليّ واحد يفرّق الحالتين.
            var outOfScopeBody = await ErrorShapeAsync(outOfScope);
            Assert.Contains(Project360ErrorCodes.ProjectNotFound, outOfScopeBody);
            Assert.Equal(outOfScopeBody, await ErrorShapeAsync(nonExistent));
        }
    }

    /// <summary>معرّف مورد تابع لمشروع آخر لا يُقرأ عبر مشروع مرئيّ — الترشيح بالمشروع لا بالمعرّف وحده.</summary>
    [Fact]
    public async Task Idor_ForeignChildResource_IsNotReachableThroughVisibleProject()
    {
        var fx = await NewFixtureAsync();
        var other = await NewProjectAsync(fx, ServiceType.Other);
        var foreignObjective = await CreateObjectiveAsync(fx.Admin, other, "هدف مشروع آخر", weight: 100m);

        var probe = await fx.Admin.GetAsync($"/api/projects/{fx.ProjectId}/objectives/{foreignObjective}");
        Assert.Equal(HttpStatusCode.NotFound, probe.StatusCode);

        // كتابة عابرة للحدود داخل مشروع مخوَّل ⟹ 409 لا 404 (RM-01 المعتمد): المستخدم مخوَّل هنا،
        // والخطأ منطقيّ لا تخويليّ، ولا يُعاد أيّ حقل من المشروع الآخر.
        var write = await fx.Admin.PostAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/objectives/{foreignObjective}/kpis",
            new CreateProjectKpiRequest("مؤشّر", TargetValue: 10m));
        Assert.Equal(HttpStatusCode.Conflict, write.StatusCode);
        Assert.Contains(Project360ErrorCodes.KpiObjectiveMismatch, await write.Content.ReadAsStringAsync());
    }

    // ==================================================================
    // §22 — جرد رموز الأخطاء بحالات HTTP مُثبَتة
    // ==================================================================

    [Fact]
    public async Task ErrorCodes_MapToProvenHttpStatuses()
    {
        var fx = await NewFixtureAsync();
        var objective = await CreateObjectiveAsync(fx.Admin, fx.ProjectId, "هدف", weight: 100m);

        // 400 — تحقّق مدخلات.
        var badTarget = await fx.Admin.PostAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/objectives/{objective}/kpis",
            new CreateProjectKpiRequest("مؤشّر بلا مستهدَف", TargetValue: 0m));
        await AssertErrorAsync(badTarget, HttpStatusCode.BadRequest, Project360ErrorCodes.KpiTargetInvalid);

        var badType = await fx.Admin.PostAsJsonAsync($"/api/projects/{fx.ProjectId}/contract-deliverables",
            new CreateProjectContractDeliverableRequest("type_that_does_not_exist"));
        await AssertErrorAsync(badType, HttpStatusCode.BadRequest, Project360ErrorCodes.DeliverableTypeInvalid);

        // 404 — مورد ابن غير موجود داخل مشروع مرئيّ.
        var missing = await fx.Admin.GetAsync($"/api/projects/{fx.ProjectId}/objectives/{Guid.NewGuid()}");
        await AssertErrorAsync(missing, HttpStatusCode.NotFound, Project360ErrorCodes.ObjectiveNotFound);

        // 409 — تعارض حالة: حذف هدف يملك مؤشّرات.
        var kpi = await fx.Admin.PostAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/objectives/{objective}/kpis",
            new CreateProjectKpiRequest("مؤشّر", TargetValue: 100m));
        Assert.Equal(HttpStatusCode.OK, kpi.StatusCode);

        var deleteWithKpis = await fx.Admin.DeleteAsync($"/api/projects/{fx.ProjectId}/objectives/{objective}");
        await AssertErrorAsync(deleteWithKpis, HttpStatusCode.Conflict, Project360ErrorCodes.ObjectiveHasKpis);

        // 404 — خارج النطاق (لا 403 · FINDING-W6-04). 403 — يرى ولا يملك القدرة. 401 — بلا توكن.
        var outOfScope = await fx.ForeignTeamLeader.GetAsync($"/api/projects/{fx.ProjectId}/overview");
        await AssertErrorAsync(outOfScope, HttpStatusCode.NotFound, Project360ErrorCodes.ProjectNotFound);

        var lacksCapability = await fx.TeamEmployee.PostAsJsonAsync(
            $"/api/projects/{fx.ProjectId}/objectives", new CreateProjectObjectiveRequest("هدف ممنوع"));
        await AssertErrorAsync(lacksCapability, HttpStatusCode.Forbidden, "auth.forbidden");

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fx.Anonymous.GetAsync($"/api/projects/{fx.ProjectId}/overview")).StatusCode);
    }

    // ==================================================================
    // §15 + §24 — المخرَجات التعاقديّة ومسارات الحوكمة لا تصطدم بالقائم
    // ==================================================================

    /// <summary>
    /// <c>contract-deliverables</c> مصطلح مقصود ومسار مستقلّ: مسار مخرَجات تيّار العمل القائم
    /// <c>workstreams/{wid}/deliverables</c> لم يُمَسّ ولا يتقاطع معه، والكتالوجان منفصلان.
    /// </summary>
    [Fact]
    public async Task ContractDeliverables_HaveTheirOwnRouteAndCatalog_WithoutTouchingWorkstreamDeliverables()
    {
        var fx = await NewFixtureAsync();

        var types = await GetAsync<List<ProjectCatalogOptionDto>>(
            fx.Admin, $"/api/projects/{fx.ProjectId}/contract-deliverables/types");

        // قاعدة الاختبار مشتركة دائمة وتتراكم (تُضاف إليها رموز تجهيزات اختبارات أخرى في المجال نفسه)
        // ⟹ التأكيد على **احتواء** الثمانية عشر رمزًا القانونيّ لا على عدّ مطلق للصفوف.
        var canonical = new[]
        {
            "monthly_content_plan", "monthly_calendar", "posts_package", "reels_package", "stories_package",
            "monthly_report", "keyword_research_doc", "technical_audit", "monthly_seo_report",
            "onpage_optimization", "brand_strategy", "brand_identity", "logo", "brand_guidelines",
            "campaign_structure", "creatives_package", "landing_page_delivery", "performance_report",
        };
        var returned = types.Select(t => t.Code).ToHashSet(StringComparer.Ordinal);
        Assert.All(canonical, code => Assert.Contains(code, returned));
        Assert.Equal(types.OrderBy(t => t.SortOrder).Select(t => t.Code), types.Select(t => t.Code));
        Assert.All(types, t => Assert.False(string.IsNullOrWhiteSpace(t.NameAr)));

        // المسار القائم لمخرَجات تيّار العمل ما زال حيًّا على حاله (صفر إزالة/تغيير).
        var workstreams = await fx.Admin.GetAsync($"/api/projects/{fx.ProjectId}/workstreams");
        Assert.Equal(HttpStatusCode.OK, workstreams.StatusCode);
    }

    [Fact]
    public async Task GovernanceRead_IsProjectFiltered_AndWriteless()
    {
        var fx = await NewFixtureAsync();
        var other = await NewProjectAsync(fx, ServiceType.Other);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Risks.Add(new Reporting.Domain.Entities.Governance.Risk
            {
                Title = "خطر هذا المشروع",
                ProjectId = fx.ProjectId,
                OwnerId = fx.AdminUserId,
                Status = RiskStatus.Open,
            });
            db.Risks.Add(new Reporting.Domain.Entities.Governance.Risk
            {
                Title = "خطر مشروع آخر",
                ProjectId = other,
                OwnerId = fx.AdminUserId,
                Status = RiskStatus.Open,
            });
            await db.SaveChangesAsync();
        }

        var risks = await GetAsync<List<ProjectRiskListItemDto>>(fx.Admin, $"/api/projects/{fx.ProjectId}/risks");
        Assert.Contains(risks, r => r.Title == "خطر هذا المشروع");
        Assert.DoesNotContain(risks, r => r.Title == "خطر مشروع آخر");

        // صفر كتابة على الحوكمة من هذه المسارات — لا نظير POST لأيّ منها.
        foreach (var path in new[] { "risks", "decisions", "notes" })
        {
            var write = await fx.Admin.PostAsJsonAsync($"/api/projects/{fx.ProjectId}/{path}", new { title = "محاولة" });
            Assert.Equal(HttpStatusCode.MethodNotAllowed, write.StatusCode);
        }
    }

    /// <summary>
    /// UAT-DEF-02 — مسار الكتابة الأصليّ للقرار يقبل <c>ProjectId</c>، فيمتلئ سطح
    /// <c>/api/projects/{id}/decisions</c> بدل أن يبقى فارغًا أبدًا. والترشيح يبقى بالمشروع.
    /// </summary>
    [Fact]
    public async Task Decision_CreatedWithProjectId_AppearsOnThatProjectOnly()
    {
        var fx = await NewFixtureAsync();
        var other = await NewProjectAsync(fx, ServiceType.Other);
        var title = $"قرار {Guid.NewGuid().ToString("N")[..8]}";

        var created = await fx.Admin.PostAsJsonAsync("/api/decisions", new
        {
            title,
            description = "قرار مرتبط بمشروع",
            projectId = fx.ProjectId,
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var mine = await GetAsync<List<ProjectDecisionListItemDto>>(
            fx.Admin, $"/api/projects/{fx.ProjectId}/decisions");
        Assert.Contains(mine, d => d.Title == title);

        var theirs = await GetAsync<List<ProjectDecisionListItemDto>>(
            fx.Admin, $"/api/projects/{other}/decisions");
        Assert.DoesNotContain(theirs, d => d.Title == title);
    }

    /// <summary>UAT-DEF-02 — مشروع غير موجود يُرفض بدل أن يُخزَّن ارتباط معلَّق.</summary>
    [Fact]
    public async Task Decision_WithUnknownProjectId_IsRejected()
    {
        var fx = await NewFixtureAsync();
        var res = await fx.Admin.PostAsJsonAsync("/api/decisions", new
        {
            title = $"قرار {Guid.NewGuid().ToString("N")[..8]}",
            projectId = Guid.NewGuid(),
        });
        await AssertErrorAsync(res, HttpStatusCode.NotFound, "decision.project.not_found");
    }

    /// <summary>
    /// UAT-DEF-01 — الملاحظة الإداريّة تقبل نوعَي <c>Project</c> و<c>Client</c>، فيمتلئ سطح
    /// <c>/api/projects/{id}/notes</c> بدل أن يُرفض الإنشاء بـ<c>note.entity.not_found</c>.
    /// </summary>
    [Fact]
    public async Task ManagementNote_OnProject_IsAccepted_AndAppearsOnThatProjectOnly()
    {
        var fx = await NewFixtureAsync();
        var other = await NewProjectAsync(fx, ServiceType.Other);
        var body = $"ملاحظة {Guid.NewGuid().ToString("N")[..8]}";

        var created = await fx.Admin.PostAsJsonAsync("/api/management-notes", new
        {
            entityType = nameof(ManagementNoteEntityType.Project),
            entityId = fx.ProjectId,
            noteType = nameof(ManagementNoteType.FollowUp),
            body,
            requiresAction = true,
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var mine = await GetAsync<List<ProjectNoteListItemDto>>(
            fx.Admin, $"/api/projects/{fx.ProjectId}/notes");
        Assert.Contains(mine, n => n.Body == body);

        var theirs = await GetAsync<List<ProjectNoteListItemDto>>(
            fx.Admin, $"/api/projects/{other}/notes");
        Assert.DoesNotContain(theirs, n => n.Body == body);
    }

    /// <summary>UAT-DEF-01 — العميل مقبول أيضًا، والكيان المجهول يبقى مرفوضًا كما كان.</summary>
    [Fact]
    public async Task ManagementNote_OnClient_IsAccepted_ButUnknownEntityStaysRejected()
    {
        var fx = await NewFixtureAsync();

        var onClient = await fx.Admin.PostAsJsonAsync("/api/management-notes", new
        {
            entityType = nameof(ManagementNoteEntityType.Client),
            entityId = fx.ClientId,
            noteType = nameof(ManagementNoteType.FollowUp),
            body = $"ملاحظة عميل {Guid.NewGuid().ToString("N")[..8]}",
        });
        Assert.Equal(HttpStatusCode.OK, onClient.StatusCode);

        var ghost = await fx.Admin.PostAsJsonAsync("/api/management-notes", new
        {
            entityType = nameof(ManagementNoteEntityType.Project),
            entityId = Guid.NewGuid(),
            noteType = nameof(ManagementNoteType.FollowUp),
            body = "ملاحظة على مشروع غير موجود",
        });
        await AssertErrorAsync(ghost, HttpStatusCode.NotFound, "note.entity.not_found");
    }

    /// <summary>
    /// UAT-DEF-01 — الكتابة على ملاحظات المشروع تبقى مقصورة على أصحاب صلاحيّة الحوكمة:
    /// من يرى المشروع لا يكتسب بذلك حقّ الكتابة عليه.
    /// </summary>
    [Fact]
    public async Task ManagementNote_OnProject_IsRejectedForNonGovernanceRoles()
    {
        var fx = await NewFixtureAsync();

        foreach (var client in new[] { fx.OwnerTeamLeader, fx.AccountManager, fx.TeamEmployee, fx.Viewer })
        {
            var res = await client.PostAsJsonAsync("/api/management-notes", new
            {
                entityType = nameof(ManagementNoteEntityType.Project),
                entityId = fx.ProjectId,
                noteType = nameof(ManagementNoteType.FollowUp),
                body = "محاولة كتابة",
            });
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
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
        };

    /// <summary>جسم الخطأ بعد إسقاط <c>traceId</c> — معرّف طلب يتغيّر لكلّ نداء ولا يصف موردًا.</summary>
    private static async Task<string> ErrorShapeAsync(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        return System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\"\\s*:\\s*\"[^\"]*\"", "\"traceId\":\"*\"");
    }

    private static async Task AssertErrorAsync(HttpResponseMessage res, HttpStatusCode expected, string errorCode)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.Equal(expected, res.StatusCode);
        Assert.Contains(errorCode, body);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        var res = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<T>())!;
    }

    private static async Task<Guid> CreateObjectiveAsync(HttpClient client, Guid projectId, string name, decimal weight = 0m)
    {
        var res = await client.PostAsJsonAsync($"/api/projects/{projectId}/objectives",
            new CreateProjectObjectiveRequest(name, Weight: weight));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<ProjectObjectiveDto>())!.Id;
    }

    private static async Task CreateKpiWithReadingAsync(
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
    }

    private static async Task SeedObjectivesWithKpisAsync(HttpClient client, Guid projectId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var objective = await CreateObjectiveAsync(client, projectId, $"هدف {i}", weight: 10m);
            await CreateKpiWithReadingAsync(client, projectId, objective, $"مؤشّر {i}", weight: 1m, value: 50m);
        }
    }

    /// <summary>
    /// يعدّ أوامر SQL الفعليّة لرحلة لوحة واحدة عبر معترِض أوامر على سياق مبنيّ يدويًّا —
    /// أدقّ من قياس زمنيّ ولا يتأثّر بأيّ استعلام آخر في الطلب.
    /// </summary>
    private static async Task<int> CountOverviewQueriesAsync(Guid actorId, Guid projectId)
    {
        var counter = new SqlCommandCounter();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnectionString)
            .AddInterceptors(counter)
            .Options;

        await using var db = new AppDbContext(options);
        var currentUser = new FixedCurrentUser(actorId, Roles.Admin);
        var access = new ClientProjectAccess(db, new ScopeResolver(db, currentUser), currentUser);
        var auth = new Project360Authorization(db, currentUser, access);
        var objectives = new ProjectObjectiveService(db, currentUser, auth, new AuditService(db), new ProjectHealthService(db, auth));
        var overview = new ProjectOverviewService(db, auth, objectives);

        counter.Reset();
        var result = await overview.GetProjectOverviewAsync(projectId);
        Assert.True(result.Succeeded, result.Error);
        return counter.Count;
    }

    private static async Task<List<CatalogRow>> SnapshotCatalogAsync(AppDbContext db, string[] domains) =>
        await db.ExecutionTaxonomyValues
            .Where(v => domains.Contains(v.Domain))
            .OrderBy(v => v.Domain).ThenBy(v => v.SortOrder)
            .Select(v => new CatalogRow(v.Id, v.Domain, v.Code, v.NameAr, v.NameEn, v.SortOrder, v.IsActive))
            .ToListAsync();

    private sealed record CatalogRow(
        Guid Id, string Domain, string Code, string NameAr, string? NameEn, int SortOrder, bool IsActive);

    private sealed class SqlCommandCounter : DbCommandInterceptor
    {
        private int _count;

        public int Count => _count;

        public void Reset() => Interlocked.Exchange(ref _count, 0);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Interlocked.Increment(ref _count);
            return base.ScalarExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Interlocked.Increment(ref _count);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class FixedCurrentUser : ICurrentUser
    {
        private readonly HashSet<string> _roles;

        public FixedCurrentUser(Guid userId, params string[] roles)
        {
            UserId = userId;
            _roles = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
        }

        public Guid? UserId { get; }
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => _roles;
        public bool IsInRole(string role) => _roles.Contains(role);
        public bool IsInAnyRole(params string[] roles) => roles.Any(_roles.Contains);
    }

    private sealed record Fixture(
        Guid ClientId,
        Guid ProjectId,
        Guid OwnerTeamId,
        Guid AdminUserId,
        Guid OwnerTeamLeaderId,
        Guid AccountManagerId,
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
    /// عميل ومشروع وفريق مالك ومستخدمون بعشر هويّات مختلفة — وهي بالضبط المداخل التي تفرّق بينها
    /// بوّابة التخويل: رؤية واسعة، انتماء بقيادة الفريق، انتماء بإدارة الحساب، عضويّة فريق، وخارج النطاق.
    /// </summary>
    private async Task<Fixture> NewFixtureAsync()
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

        Guid adminId, clientId, projectId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            adminId = await db.Users.Where(u => u.Email == "admin@marketingexperts.local")
                .Select(u => u.Id).FirstAsync();

            var tag = Guid.NewGuid().ToString("N")[..8];
            var client = new Client { Name = $"عميل W5 {tag}", Status = ClientStatus.Active };
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            clientId = client.Id;

            var project = new Project
            {
                ClientId = clientId,
                Name = $"مشروع W5 {tag}",
                ServiceType = ServiceType.Other,
                Status = ProjectStatus.Active,
                OwnerTeamId = ownerTeam,
                TeamLeaderId = ownerTl.UserId,
                AccountManagerId = accountManager.UserId,
                ProgressPercent = 50m,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31),
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            projectId = project.Id;
        }

        return new Fixture(
            clientId, projectId, ownerTeam, adminId, ownerTl.UserId, accountManager.UserId,
            admin, ceo.Client, gm.Client, foreignManager.Client,
            ownerTl.Client, foreignTl.Client, accountManager.Client, employee.Client, viewer.Client,
            _factory.CreateClient());
    }

    /// <summary>مشروع إضافيّ لنفس العميل والفريق المالك — يُستعمل لاختبارات نوع الخدمة وحدود المشروع.</summary>
    private async Task<Guid> NewProjectAsync(Fixture fx, ServiceType serviceType)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project
        {
            ClientId = fx.ClientId,
            Name = $"مشروع {serviceType} {Guid.NewGuid().ToString("N")[..8]}",
            ServiceType = serviceType,
            Status = ProjectStatus.Active,
            OwnerTeamId = fx.OwnerTeamId,
            TeamLeaderId = fx.OwnerTeamLeaderId,
            AccountManagerId = fx.AccountManagerId,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }
}
