using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Entities.ExecutionTaxonomy;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Projects360;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// طبقة أعمال Project 360 (CPW-R3 · W4 · §20) — أهداف/مؤشّرات/قراءات/استراتيجيّة/مخرَجات تعاقديّة/لوحة.
///
/// <para>
/// **لماذا لا HTTP هنا**: W4 طبقة أعمال بلا Controller بنصّ التذكرة، فتُستدعى الخدمات مباشرةً
/// عبر نطاق DI حقيقيّ فوق قاعدة الاختبار الفعليّة (لا In-Memory ولا Mock للـDbContext).
/// المستخدم الحالي وحده مُزيَّف — وهو بالضبط المتغيّر الذي تختبره حالات التخويل.
/// </para>
///
/// <para>
/// **الكتالوج**: W4 لا يبذر شيئًا في الإنتاج (§12)، فالقيم اللازمة تُدرَج هنا في **قاعدة الاختبار فقط**
/// بأسلوب get-or-create، لأنّ قاعدة الاختبار مشتركة دائمة وتتراكم عبر التشغيلات.
/// </para>
/// </summary>
[Collection("Integration")]
public class Project360FoundationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public Project360FoundationTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ==================================================================
    // §3 — الاستراتيجيّة
    // ==================================================================

    [Fact]
    public async Task Strategy_Upsert_Then_Get_RoundTrips_And_SyncsAttributesDifferentially()
    {
        using var ctx = await NewProjectAsync();

        var first = await ctx.Admin.Strategy.UpsertAsync(ctx.ProjectId, new UpsertProjectStrategyRequest(
            Vision: "رؤية المشروع",
            TargetAudience: "الجمهور",
            Attributes: new[]
            {
                new UpsertProjectStrategyAttributeRequest(FieldA, SectionA, "قيمة أ", 1),
                new UpsertProjectStrategyAttributeRequest(FieldB, SectionA, "قيمة ب", 2),
            }));

        Assert.True(first.Succeeded, first.Error);
        Assert.Equal("رؤية المشروع", first.Value!.Vision);
        Assert.Equal(2, first.Value.Attributes.Count);
        Assert.Equal(SectionNameAr, first.Value.Attributes.First(a => a.FieldCode == FieldA).SectionNameAr);

        // المزامنة التفاضليّة: السمة غير المرسَلة تُحذف، والمرسَلة تُحدَّث — لا تُكدَّس نسخ.
        var second = await ctx.Admin.Strategy.UpsertAsync(ctx.ProjectId, new UpsertProjectStrategyRequest(
            Vision: "رؤية محدَّثة",
            Attributes: new[] { new UpsertProjectStrategyAttributeRequest(FieldA, SectionA, "قيمة أ محدَّثة", 1) }));

        Assert.True(second.Succeeded, second.Error);
        Assert.Equal("رؤية محدَّثة", second.Value!.Vision);
        Assert.Null(second.Value.TargetAudience);
        var attribute = Assert.Single(second.Value.Attributes);
        Assert.Equal(FieldA, attribute.FieldCode);
        Assert.Equal("قيمة أ محدَّثة", attribute.ValueText);

        var fetched = await ctx.Admin.Strategy.GetAsync(ctx.ProjectId);
        Assert.True(fetched.Succeeded);
        Assert.Equal(second.Value.Id, fetched.Value!.Id);

        // استراتيجيّة نشطة واحدة لكلّ مشروع — فهرس فريد جزئيّ لا سطر مكرّر.
        Assert.Equal(1, await ctx.Db.ProjectStrategies.CountAsync(s => s.ProjectId == ctx.ProjectId && s.IsActive));
    }

    [Fact]
    public async Task Strategy_UnknownFieldCode_IsRejected()
    {
        using var ctx = await NewProjectAsync();

        var result = await ctx.Admin.Strategy.UpsertAsync(ctx.ProjectId, new UpsertProjectStrategyRequest(
            Attributes: new[] { new UpsertProjectStrategyAttributeRequest("field_that_does_not_exist") }));

        Assert.False(result.Succeeded);
        Assert.Equal(Project360ErrorCodes.StrategyFieldCodeInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Strategy_UnknownSectionCode_IsRejected()
    {
        using var ctx = await NewProjectAsync();

        var result = await ctx.Admin.Strategy.UpsertAsync(ctx.ProjectId, new UpsertProjectStrategyRequest(
            Attributes: new[] { new UpsertProjectStrategyAttributeRequest(FieldA, "section_that_does_not_exist") }));

        Assert.False(result.Succeeded);
        Assert.Equal(Project360ErrorCodes.StrategySectionCodeInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Strategy_DuplicateFieldCode_IsConflict()
    {
        using var ctx = await NewProjectAsync();

        var result = await ctx.Admin.Strategy.UpsertAsync(ctx.ProjectId, new UpsertProjectStrategyRequest(
            Attributes: new[]
            {
                new UpsertProjectStrategyAttributeRequest(FieldA, ValueText: "أولى"),
                new UpsertProjectStrategyAttributeRequest(FieldA, ValueText: "ثانية"),
            }));

        Assert.False(result.Succeeded);
        Assert.Equal(Project360ErrorCodes.StrategyDuplicateField, result.ErrorCode);
    }

    [Fact]
    public async Task Strategy_Schema_ExposesElevenCoreFields_AndCatalogDrivenDynamicFields()
    {
        using var ctx = await NewProjectAsync();

        var schema = await ctx.Admin.Strategy.GetSchemaAsync(ctx.ProjectId);

        Assert.True(schema.Succeeded, schema.Error);
        Assert.Equal(ProjectStrategyCoreFields.Count, schema.Value!.CoreFields.Count);
        Assert.All(schema.Value.CoreFields, f => Assert.True(f.IsCore));
        Assert.Contains(schema.Value.DynamicFields, f => f.FieldCode == FieldA);
        Assert.Contains(schema.Value.Sections, s => s.Code == SectionA);
    }

    // ==================================================================
    // §4 — الأهداف
    // ==================================================================

    [Fact]
    public async Task Objective_Crud_RoundTrips_AndProgressIsDerivedNotStored()
    {
        using var ctx = await NewProjectAsync();

        var created = await ctx.Admin.Objectives.CreateAsync(ctx.ProjectId,
            new CreateProjectObjectiveRequest("هدف النموّ", Weight: 40m, SortOrder: 1));
        Assert.True(created.Succeeded, created.Error);
        Assert.Null(created.Value!.ProgressPercent); // بلا مؤشّرات ⟹ غير محتسَب لا صفر
        Assert.Equal(0, created.Value.KpiCount);

        var updated = await ctx.Admin.Objectives.UpdateAsync(ctx.ProjectId, created.Value.Id,
            new UpdateProjectObjectiveRequest("هدف النموّ المعدَّل", Weight: 60m, SortOrder: 2));
        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal("هدف النموّ المعدَّل", updated.Value!.Name);
        Assert.Equal(60m, updated.Value.Weight);

        var deactivated = await ctx.Admin.Objectives.SetActiveAsync(ctx.ProjectId, created.Value.Id, false);
        Assert.True(deactivated.Succeeded);
        Assert.False(deactivated.Value!.IsActive);

        var listed = await ctx.Admin.Objectives.ListAsync(ctx.ProjectId, includeInactive: false);
        Assert.True(listed.Succeeded);
        Assert.DoesNotContain(listed.Value!, o => o.Id == created.Value.Id);

        var deleted = await ctx.Admin.Objectives.DeleteAsync(ctx.ProjectId, created.Value.Id);
        Assert.True(deleted.Succeeded, deleted.Error);
    }

    [Fact]
    public async Task Objective_DeleteWithKpis_IsRejectedAsConflict()
    {
        using var ctx = await NewProjectAsync();
        var objective = await CreateObjectiveAsync(ctx, "هدف له مؤشّر");
        await CreateKpiAsync(ctx, objective, "مؤشّر");

        var result = await ctx.Admin.Objectives.DeleteAsync(ctx.ProjectId, objective);

        Assert.False(result.Succeeded);
        Assert.Equal(Project360ErrorCodes.ObjectiveHasKpis, result.ErrorCode);
        Assert.EndsWith(".conflict", result.ErrorCode); // اللاحقة جزء من العقد ⟹ 409
    }

    // ==================================================================
    // §5 + §6 — المؤشّرات والقراءات
    // ==================================================================

    [Fact]
    public async Task Kpi_IsAlwaysCreatedUnderObjective_WithMatchingProjectId()
    {
        using var ctx = await NewProjectAsync();
        var objective = await CreateObjectiveAsync(ctx, "هدف");

        var kpi = await ctx.Admin.Kpis.CreateAsync(ctx.ProjectId, objective,
            new CreateProjectKpiRequest("عدد العملاء المحتملين", TargetValue: 200m, Weight: 1m));

        Assert.True(kpi.Succeeded, kpi.Error);
        Assert.Equal(objective, kpi.Value!.ObjectiveId);
        Assert.Equal(ctx.ProjectId, kpi.Value.ProjectId);
        Assert.Equal(ProjectKpiSourceType.Manual, kpi.Value.SourceType);
        Assert.Null(kpi.Value.AchievementPercent); // لا قراءة بعد ⟹ غير محتسَب

        // لا يوجد مؤشّر يتيم في قاعدة البيانات أصلًا — ObjectiveId غير قابل للعدم.
        Assert.False(await ctx.Db.ProjectKpis.AnyAsync(k => k.ProjectId == ctx.ProjectId && k.ObjectiveId == Guid.Empty));
    }

    [Fact]
    public async Task Kpi_TargetValueNotPositive_IsRejected()
    {
        using var ctx = await NewProjectAsync();
        var objective = await CreateObjectiveAsync(ctx, "هدف");

        var result = await ctx.Admin.Kpis.CreateAsync(ctx.ProjectId, objective,
            new CreateProjectKpiRequest("مؤشّر بلا مستهدَف", TargetValue: 0m));

        Assert.False(result.Succeeded);
        Assert.Equal(Project360ErrorCodes.KpiTargetInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Kpi_ObjectiveFromAnotherProject_IsConflict()
    {
        using var ctx = await NewProjectAsync();
        using var other = await NewProjectAsync();
        var foreignObjective = await CreateObjectiveAsync(other, "هدف مشروع آخر");

        var result = await ctx.Admin.Kpis.CreateAsync(ctx.ProjectId, foreignObjective,
            new CreateProjectKpiRequest("مؤشّر", TargetValue: 10m));

        Assert.False(result.Succeeded);
        // العقد المنفَّذ (ProjectKpiService §299-308): هدف غير موجود إطلاقًا ⟹ not_found،
        // وهدف موجود لكنّه يتبع مشروعًا آخر ⟹ conflict (409) — تمييز مقصود بين خطأ الإدخال
        // ومحاولة عبور حدود المشروع. لا تسرُّب بيانات: لا يُعاد أيّ حقل من المشروع الآخر.
        Assert.Equal(Project360ErrorCodes.KpiObjectiveMismatch, result.ErrorCode);
        Assert.EndsWith(".conflict", result.ErrorCode);
    }

    [Fact]
    public async Task Reading_Added_UpdatesSnapshot_AndRejectsDuplicateDate()
    {
        using var ctx = await NewProjectAsync();
        var objective = await CreateObjectiveAsync(ctx, "هدف");
        var kpi = await CreateKpiAsync(ctx, objective, "مؤشّر", target: 100m);
        var date = new DateOnly(2026, 3, 1);

        var reading = await ctx.Admin.Kpis.AddReadingAsync(ctx.ProjectId, objective, kpi,
            new CreateProjectKpiReadingRequest(date, 80m));

        Assert.True(reading.Succeeded, reading.Error);
        Assert.Equal(100m, reading.Value!.TargetSnapshot);      // لقطة المستهدَف وقت القراءة
        Assert.Equal(80m, reading.Value.AchievementSnapshot);   // 80 / 100 × 100

        var refreshed = await ctx.Admin.Kpis.GetAsync(ctx.ProjectId, objective, kpi);
        Assert.Equal(80m, refreshed.Value!.CurrentValue);
        Assert.Equal(date, refreshed.Value.LastReadingDate);
        Assert.Equal(80m, refreshed.Value.AchievementPercent);
        Assert.Equal(-20m, refreshed.Value.Variance);

        var duplicate = await ctx.Admin.Kpis.AddReadingAsync(ctx.ProjectId, objective, kpi,
            new CreateProjectKpiReadingRequest(date, 90m));
        Assert.False(duplicate.Succeeded);
        Assert.Equal(Project360ErrorCodes.ReadingDuplicateDate, duplicate.ErrorCode);

        // التصحيح يقع بالتحديث لا بقراءة ثانية لنفس التاريخ.
        var corrected = await ctx.Admin.Kpis.UpdateReadingAsync(ctx.ProjectId, objective, kpi, reading.Value.Id,
            new UpdateProjectKpiReadingRequest(90m));
        Assert.True(corrected.Succeeded, corrected.Error);
        Assert.Equal(1, await ctx.Db.ProjectKpiReadings.CountAsync(r => r.ProjectKpiId == kpi));

        var afterCorrection = await ctx.Admin.Kpis.GetAsync(ctx.ProjectId, objective, kpi);
        Assert.Equal(90m, afterCorrection.Value!.CurrentValue);
    }

    [Fact]
    public async Task Reading_OnNonManualKpi_IsConflict()
    {
        using var ctx = await NewProjectAsync();
        var objective = await CreateObjectiveAsync(ctx, "هدف");
        var kpi = await CreateKpiAsync(ctx, objective, "مؤشّر مُدمَج", target: 50m);

        // محاكاة تدرّج المصدر (D-06): مؤشّر صار مصدره تكاملًا خارجيًّا لا إدخالًا يدويًّا.
        var entity = await ctx.Db.ProjectKpis.FirstAsync(k => k.Id == kpi);
        entity.SourceType = ProjectKpiSourceType.Integration;
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Admin.Kpis.AddReadingAsync(ctx.ProjectId, objective, kpi,
            new CreateProjectKpiReadingRequest(new DateOnly(2026, 3, 2), 10m));

        Assert.False(result.Succeeded);
        Assert.Equal(Project360ErrorCodes.KpiSourceNotManual, result.ErrorCode);
    }

    // ==================================================================
    // §11 — المخرَجات التعاقديّة
    // ==================================================================

    [Fact]
    public async Task ContractDeliverable_Crud_RoundTrips_AndValidatesTypeCode()
    {
        using var ctx = await NewProjectAsync();

        var invalid = await ctx.Admin.Deliverables.CreateAsync(ctx.ProjectId,
            new CreateProjectContractDeliverableRequest("type_that_does_not_exist"));
        Assert.False(invalid.Succeeded);
        Assert.Equal(Project360ErrorCodes.DeliverableTypeInvalid, invalid.ErrorCode);

        var created = await ctx.Admin.Deliverables.CreateAsync(ctx.ProjectId,
            new CreateProjectContractDeliverableRequest(DeliverableType, PlannedQuantity: 12));
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(DeliverableTypeNameAr, created.Value!.Name);           // الاسم يتراجع إلى اسم النوع
        Assert.Equal(DeliverableTypeNameAr, created.Value.DeliverableTypeNameAr);
        Assert.Equal(ProjectDeliverableStatus.NotStarted, created.Value.Status);

        var updated = await ctx.Admin.Deliverables.UpdateAsync(ctx.ProjectId, created.Value.Id,
            new UpdateProjectContractDeliverableRequest("تقرير شهريّ", PlannedQuantity: 12));
        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal("تقرير شهريّ", updated.Value!.Name);
        Assert.Equal(DeliverableType, updated.Value.DeliverableTypeCode); // لقطة النوع لا تتغيّر بالتعديل

        var overQuantity = await ctx.Admin.Deliverables.UpdateProgressAsync(ctx.ProjectId, created.Value.Id,
            new UpdateProjectContractDeliverableProgressRequest(ProjectDeliverableStatus.InProgress, 50m, CompletedQuantity: 99));
        Assert.False(overQuantity.Succeeded);
        Assert.Equal(Project360ErrorCodes.DeliverableQuantityInvalid, overQuantity.ErrorCode);

        var progressed = await ctx.Admin.Deliverables.UpdateProgressAsync(ctx.ProjectId, created.Value.Id,
            new UpdateProjectContractDeliverableProgressRequest(ProjectDeliverableStatus.InProgress, 50m, CompletedQuantity: 6));
        Assert.True(progressed.Succeeded, progressed.Error);
        Assert.Equal(50m, progressed.Value!.ProgressPercent);
        Assert.Equal(6, progressed.Value.CompletedQuantity);
    }

    [Fact]
    public async Task ContractDeliverable_ObjectiveFromAnotherProject_IsConflict()
    {
        using var ctx = await NewProjectAsync();
        using var other = await NewProjectAsync();
        var foreignObjective = await CreateObjectiveAsync(other, "هدف مشروع آخر");

        var result = await ctx.Admin.Deliverables.CreateAsync(ctx.ProjectId,
            new CreateProjectContractDeliverableRequest(DeliverableType, ObjectiveId: foreignObjective));

        Assert.False(result.Succeeded);
        Assert.Equal(Project360ErrorCodes.DeliverableObjectiveMismatch, result.ErrorCode);
    }

    // ==================================================================
    // §16 + §17 — التخويل: قائد الفريق ومدير الحساب والموظّف والـIDOR
    // ==================================================================

    [Fact]
    public async Task TeamLeader_CanUpdateOperationalState_ButCannotManageStructure()
    {
        using var ctx = await NewProjectAsync();
        var objective = await CreateObjectiveAsync(ctx, "هدف");

        var structural = await ctx.TeamLeader.Objectives.CreateAsync(ctx.ProjectId,
            new CreateProjectObjectiveRequest("هدف من قائد الفريق"));
        Assert.False(structural.Succeeded);
        Assert.Equal("auth.forbidden", structural.ErrorCode);

        var operational = await ctx.TeamLeader.Objectives.UpdateStatusAsync(ctx.ProjectId, objective,
            new UpdateProjectObjectiveStatusRequest(ProjectObjectiveStatus.InProgress));
        Assert.True(operational.Succeeded, operational.Error);
        Assert.Equal(ProjectObjectiveStatus.InProgress, operational.Value!.Status);

        var read = await ctx.TeamLeader.Objectives.ListAsync(ctx.ProjectId, includeInactive: false);
        Assert.True(read.Succeeded);
    }

    [Fact]
    public async Task AccountManager_CanUpdateOperationalState_ButCannotManageStructure()
    {
        using var ctx = await NewProjectAsync();
        var deliverable = await CreateDeliverableAsync(ctx);

        var structural = await ctx.AccountManager.Deliverables.CreateAsync(ctx.ProjectId,
            new CreateProjectContractDeliverableRequest(DeliverableType));
        Assert.False(structural.Succeeded);
        Assert.Equal("auth.forbidden", structural.ErrorCode);

        var operational = await ctx.AccountManager.Deliverables.UpdateProgressAsync(ctx.ProjectId, deliverable,
            new UpdateProjectContractDeliverableProgressRequest(ProjectDeliverableStatus.InProgress, 25m));
        Assert.True(operational.Succeeded, operational.Error);
        Assert.Equal(25m, operational.Value!.ProgressPercent);
    }

    [Fact]
    public async Task Employee_InOwnerTeam_CanRead_ButCannotWriteAnyTier()
    {
        using var ctx = await NewProjectAsync();
        var objective = await CreateObjectiveAsync(ctx, "هدف");

        var read = await ctx.Employee.Objectives.ListAsync(ctx.ProjectId, includeInactive: false);
        Assert.True(read.Succeeded, read.Error);

        var structural = await ctx.Employee.Objectives.CreateAsync(ctx.ProjectId,
            new CreateProjectObjectiveRequest("هدف من موظّف"));
        Assert.False(structural.Succeeded);
        Assert.Equal("auth.forbidden", structural.ErrorCode);

        var operational = await ctx.Employee.Objectives.UpdateStatusAsync(ctx.ProjectId, objective,
            new UpdateProjectObjectiveStatusRequest(ProjectObjectiveStatus.Completed));
        Assert.False(operational.Succeeded);
        Assert.Equal("auth.forbidden", operational.ErrorCode);
    }

    /// <summary>
    /// IDOR/BOLA (FINDING-W6-04): مشروع خارج النطاق يُرفَض بـ<c>project.not_found</c> **قبل** أيّ
    /// استعلام وجود، بنفس الرمز ونفس الرسالة الحرفيّة لحالة «غير موجود إطلاقًا» — فلا يفرّق
    /// المهاجم بين الحالتين ولا يعدّ المشاريع.
    /// </summary>
    [Fact]
    public async Task ProjectOutsideVisibility_IsIndistinguishableFromNonExistent()
    {
        using var ctx = await NewProjectAsync();
        using var other = await NewProjectAsync();

        var existingButUnrelated = await other.TeamLeader.Objectives.ListAsync(ctx.ProjectId, includeInactive: false);
        Assert.False(existingButUnrelated.Succeeded);
        Assert.Equal(Project360ErrorCodes.ProjectNotFound, existingButUnrelated.ErrorCode);

        var nonExistent = await other.TeamLeader.Objectives.ListAsync(Guid.NewGuid(), includeInactive: false);
        Assert.False(nonExistent.Succeeded);
        Assert.Equal(Project360ErrorCodes.ProjectNotFound, nonExistent.ErrorCode);

        // نفس الرسالة حرفيًّا ⟹ صفر تسريب حتّى عبر نصّ الخطأ.
        Assert.Equal(existingButUnrelated.Error, nonExistent.Error);
    }

    // ==================================================================
    // §14 — لوحة النظرة العامّة
    // ==================================================================

    [Fact]
    public async Task Overview_AggregatesEverySection_AndReflectsComputedHealth()
    {
        using var ctx = await NewProjectAsync();

        var objective = await CreateObjectiveAsync(ctx, "هدف اللوحة", weight: 100m);
        var kpi = await CreateKpiAsync(ctx, objective, "مؤشّر اللوحة", target: 100m, weight: 1m);
        await ctx.Admin.Kpis.AddReadingAsync(ctx.ProjectId, objective, kpi,
            new CreateProjectKpiReadingRequest(new DateOnly(2026, 3, 5), 90m));

        var deliverable = await CreateDeliverableAsync(ctx);
        await ctx.Admin.Deliverables.UpdateProgressAsync(ctx.ProjectId, deliverable,
            new UpdateProjectContractDeliverableProgressRequest(ProjectDeliverableStatus.Delivered, 100m));

        await ctx.Admin.Strategy.UpsertAsync(ctx.ProjectId, new UpsertProjectStrategyRequest(
            Vision: "رؤية", StrategySummary: "ملخّص",
            Attributes: new[] { new UpsertProjectStrategyAttributeRequest(FieldA, SectionA, "قيمة") }));

        ctx.Db.Risks.Add(new Risk { Title = "خطر", ProjectId = ctx.ProjectId, OwnerId = ctx.AdminId, Status = RiskStatus.Open });
        ctx.Db.Decisions.Add(new Decision { Title = "قرار", ProjectId = ctx.ProjectId, MadeById = ctx.AdminId, Status = DecisionStatus.Proposed });
        ctx.Db.ManagementNotes.Add(new ManagementNote
        {
            EntityType = ManagementNoteEntityType.Project,
            EntityId = ctx.ProjectId,
            AuthorId = ctx.AdminId,
            Body = "ملاحظة",
            Status = ManagementNoteStatus.Open,
        });
        await ctx.Db.SaveChangesAsync();

        var overview = await ctx.Admin.Overview.GetProjectOverviewAsync(ctx.ProjectId);

        Assert.True(overview.Succeeded, overview.Error);
        var dto = overview.Value!;

        Assert.Equal(ctx.ProjectId, dto.Project.Id);
        Assert.Equal(1, dto.Objectives.Total);
        Assert.Equal(90m, dto.Objectives.AverageAchievementPercent);

        Assert.Equal(1, dto.Kpis.Total);
        Assert.Equal(1, dto.Kpis.OnTrack);
        Assert.Equal(0, dto.Kpis.WithoutReadings);
        Assert.Equal(90m, dto.Kpis.AverageAchievementPercent);
        Assert.Equal(new DateOnly(2026, 3, 5), dto.Kpis.LastReadingDate);

        Assert.Equal(1, dto.Deliverables.Total);
        Assert.Equal(1, dto.Deliverables.Completed);
        // رقم التقدّم الوحيد على اللوحة هو تقدّم المشروع نفسه — لا نسبة ثانية داخل بطاقة المخرَجات.
        Assert.Equal(100m, dto.Project.ProgressPercent);

        Assert.Equal(1, dto.Governance.RisksTotal);
        Assert.Equal(1, dto.Governance.RisksOpen);
        Assert.Equal(1, dto.Governance.DecisionsPending);
        Assert.Equal(1, dto.Governance.NotesOpen);

        Assert.True(dto.Strategy.Exists);
        Assert.Equal(2, dto.Strategy.FilledCoreFields);
        Assert.Equal(ProjectStrategyCoreFields.Count, dto.Strategy.TotalCoreFields);
        Assert.Equal(1, dto.Strategy.AttributesCount);

        // الصحّة محتسَبة من نفس المحرّك — واللوحة **استعلام عرض لا يغيّر حالة**.
        Assert.NotNull(dto.Health.Score);
        Assert.Equal(90m, dto.Health.KpiScore);
        Assert.NotEmpty(dto.Health.ReasonCodes);

        // FINDING-W6-03 (مغلق): الأعمدة الثلاثة صارت تُكتب في نفس وحدة عمل الطفرة، فالمخزَّن
        // **يطابق** المعروض. قبل الإغلاق كان الختم يبقى فارغًا أبدًا — وهو ما وثّقه W6 عيبًا.
        var stored = await ctx.Db.Projects.AsNoTracking().FirstAsync(p => p.Id == ctx.ProjectId);
        Assert.NotNull(stored.HealthComputedAtUtc);
        Assert.Equal(dto.Health.Score, stored.HealthPercent);
        Assert.Equal(dto.Health.Status, stored.HealthStatus);
    }

    [Fact]
    public async Task Overview_ProjectWithoutAnyComponent_IsNotEvaluated_NotZero()
    {
        using var ctx = await NewProjectAsync(withDates: false);

        var overview = await ctx.Admin.Overview.GetProjectOverviewAsync(ctx.ProjectId);

        Assert.True(overview.Succeeded, overview.Error);
        Assert.Equal(0, overview.Value!.Kpis.Total);
        Assert.Null(overview.Value.Kpis.AverageAchievementPercent);
        // مشروع لم يُقَس قطّ: صفر **موسوم بـ`NotCalculated`** لا صفرًا مبهمًا يُقرأ تقدّمًا معدومًا.
        Assert.Equal(0, overview.Value.Deliverables.Total);
        Assert.Equal(ProjectProgressMode.NotCalculated, overview.Value.Project.ProgressMode);
        Assert.Null(overview.Value.Project.ProgressCalculatedAtUtc);
        Assert.False(overview.Value.Strategy.Exists);
    }

    // ==================================================================
    // تجهيزات — أدوات البناء
    // ==================================================================

    private const string FieldA = "w4_test_field_a";
    private const string FieldB = "w4_test_field_b";
    private const string SectionA = "w4_test_section_a";
    private const string SectionNameAr = "قسم اختبار W4";
    private const string DeliverableType = "w4_test_monthly_report";
    private const string DeliverableTypeNameAr = "تقرير شهريّ (اختبار W4)";

    private async Task<Guid> CreateObjectiveAsync(Ctx ctx, string name, decimal weight = 0m)
    {
        var result = await ctx.Admin.Objectives.CreateAsync(ctx.ProjectId,
            new CreateProjectObjectiveRequest(name, Weight: weight));
        Assert.True(result.Succeeded, result.Error);
        return result.Value!.Id;
    }

    private async Task<Guid> CreateKpiAsync(Ctx ctx, Guid objectiveId, string name, decimal target = 100m, decimal weight = 1m)
    {
        var result = await ctx.Admin.Kpis.CreateAsync(ctx.ProjectId, objectiveId,
            new CreateProjectKpiRequest(name, TargetValue: target, Weight: weight));
        Assert.True(result.Succeeded, result.Error);
        return result.Value!.Id;
    }

    private async Task<Guid> CreateDeliverableAsync(Ctx ctx)
    {
        var result = await ctx.Admin.Deliverables.CreateAsync(ctx.ProjectId,
            new CreateProjectContractDeliverableRequest(DeliverableType, PlannedQuantity: 12));
        Assert.True(result.Succeeded, result.Error);
        return result.Value!.Id;
    }

    /// <summary>
    /// مشروع معزول بفريق مالك يقوده قائد الفريق، ومدير حساب، وموظّف عضو في الفريق —
    /// وهي المداخل الأربعة التي تفرّق بينها بوّابة التخويل.
    /// </summary>
    private async Task<Ctx> NewProjectAsync(bool withDates = true)
    {
        var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await EnsureTaxonomyAsync(db);

        var tag = Guid.NewGuid().ToString("N")[..8];
        var admin = await CreateUserAsync(db, $"admin-{tag}");
        var teamLeader = await CreateUserAsync(db, $"tl-{tag}");
        var accountManager = await CreateUserAsync(db, $"am-{tag}");
        var employee = await CreateUserAsync(db, $"emp-{tag}");

        var department = new Department { NameAr = $"إدارة {tag}", IsActive = true };
        db.Departments.Add(department);
        await db.SaveChangesAsync();

        var team = new Team { NameAr = $"فريق {tag}", DepartmentId = department.Id, TeamLeaderId = teamLeader.Id, IsActive = true };
        db.Teams.Add(team);
        await db.SaveChangesAsync();

        employee.TeamId = team.Id;
        await db.SaveChangesAsync();

        var client = new Client { Name = $"عميل {tag}", Status = ClientStatus.Active };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id,
            Name = $"مشروع {tag}",
            ServiceType = ServiceType.Other,
            Status = ProjectStatus.Active,
            OwnerTeamId = team.Id,
            TeamLeaderId = teamLeader.Id,
            AccountManagerId = accountManager.Id,
            ProgressPercent = withDates ? 50m : 0m,
            StartDate = withDates ? new DateOnly(2026, 1, 1) : null,
            EndDate = withDates ? new DateOnly(2026, 12, 31) : null,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        return new Ctx(scope, db, project.Id, admin.Id)
        {
            Admin = Services.For(db, admin.Id, Roles.Admin),
            TeamLeader = Services.For(db, teamLeader.Id, Roles.TeamLeader),
            AccountManager = Services.For(db, accountManager.Id, Roles.Employee),
            Employee = Services.For(db, employee.Id, Roles.Employee),
        };
    }

    private static async Task<ApplicationUserRow> CreateUserAsync(AppDbContext db, string tag)
    {
        var user = new Reporting.Infrastructure.Identity.ApplicationUser
        {
            UserName = $"{tag}@w4.local",
            NormalizedUserName = $"{tag}@W4.LOCAL",
            Email = $"{tag}@w4.local",
            NormalizedEmail = $"{tag}@W4.LOCAL",
            FullName = $"مستخدم {tag}",
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new ApplicationUserRow(user.Id, user);
    }

    private sealed record ApplicationUserRow(Guid Id, Reporting.Infrastructure.Identity.ApplicationUser Entity)
    {
        public Guid? TeamId
        {
            get => Entity.TeamId;
            set => Entity.TeamId = value;
        }
    }

    /// <summary>قيم الكتالوج اللازمة — get-or-create لأنّ قاعدة الاختبار مشتركة دائمة وتتراكم.</summary>
    private static async Task EnsureTaxonomyAsync(AppDbContext db)
    {
        var wanted = new (string Domain, string Code, string NameAr)[]
        {
            (Project360CatalogDomains.StrategyField, FieldA, "حقل اختبار أ"),
            (Project360CatalogDomains.StrategyField, FieldB, "حقل اختبار ب"),
            (Project360CatalogDomains.StrategySection, SectionA, SectionNameAr),
            (Project360CatalogDomains.ContractDeliverable, DeliverableType, DeliverableTypeNameAr),
        };

        var codes = wanted.Select(w => w.Code).ToList();
        var existing = await db.ExecutionTaxonomyValues
            .Where(v => codes.Contains(v.Code))
            .Select(v => v.Code)
            .ToListAsync();

        var missing = wanted.Where(w => !existing.Contains(w.Code)).ToList();
        if (missing.Count == 0) return;

        foreach (var (domain, code, nameAr) in missing)
            db.ExecutionTaxonomyValues.Add(new ExecutionTaxonomyValue
            {
                Domain = domain,
                Code = code,
                NameAr = nameAr,
                IsActive = true,
            });

        await db.SaveChangesAsync();
    }

    /// <summary>مستخدم حاليّ مُزيَّف — المتغيّر الوحيد بين حالات التخويل.</summary>
    private sealed class FakeCurrentUser : ICurrentUser
    {
        private readonly HashSet<string> _roles;

        public FakeCurrentUser(Guid? userId, params string[] roles)
        {
            UserId = userId;
            _roles = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
        }

        public Guid? UserId { get; }
        public bool IsAuthenticated => UserId is not null;
        public IReadOnlyCollection<string> Roles => _roles;
        public bool IsInRole(string role) => _roles.Contains(role);
        public bool IsInAnyRole(params string[] roles) => roles.Any(_roles.Contains);
    }

    /// <summary>رسم الخدمات كما يبنيه الحاوي في الطلب الحقيقيّ — بلا Mock لأيّ طبقة بيانات.</summary>
    private sealed record Services(
        IProjectStrategyService Strategy,
        IProjectObjectiveService Objectives,
        IProjectKpiService Kpis,
        IProjectContractDeliverableService Deliverables,
        IProjectOverviewService Overview)
    {
        public static Services For(AppDbContext db, Guid userId, params string[] roles)
        {
            var currentUser = new FakeCurrentUser(userId, roles);
            var access = new ClientProjectAccess(db, new ScopeResolver(db, currentUser), currentUser);
            var auth = new Project360Authorization(db, currentUser, access);
            var audit = new AuditService(db);
            var health = new ProjectHealthService(db, auth);
            var objectives = new ProjectObjectiveService(db, currentUser, auth, audit, health);

            return new Services(
                new ProjectStrategyService(db, currentUser, auth, audit),
                objectives,
                new ProjectKpiService(db, currentUser, auth, audit, health),
                new ProjectContractDeliverableService(db, currentUser, auth, audit, health),
                new ProjectOverviewService(db, auth, objectives));
        }
    }

    private sealed class Ctx : IDisposable
    {
        private readonly IServiceScope _scope;

        public Ctx(IServiceScope scope, AppDbContext db, Guid projectId, Guid adminId)
        {
            _scope = scope;
            Db = db;
            ProjectId = projectId;
            AdminId = adminId;
        }

        public AppDbContext Db { get; }
        public Guid ProjectId { get; }
        public Guid AdminId { get; }

        public Services Admin { get; init; } = null!;
        public Services TeamLeader { get; init; } = null!;
        public Services AccountManager { get; init; } = null!;
        public Services Employee { get; init; } = null!;

        public void Dispose() => _scope.Dispose();
    }
}
