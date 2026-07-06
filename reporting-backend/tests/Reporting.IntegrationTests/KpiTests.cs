using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Directory;
using Reporting.Application.Kpi;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class KpiTests
{
    private readonly CustomWebApplicationFactory _factory;

    public KpiTests(CustomWebApplicationFactory factory) => _factory = factory;

    /// <summary>ينشئ قالب KPI منشورًا بمؤشرين (يدوي 50، آلي 50) ويعيد المعرّفات.</summary>
    private static async Task<(Guid TemplateId, Guid ManualMetricId, Guid AutoMetricId)> PublishKpiAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"مؤشرات {Guid.NewGuid():N}", null, null, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var manual = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الالتزام", null, 50m, null, null, KpiCalcMethod.Manual, null)))
            .ReadAsync<KpiMetricDto>();
        var auto = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الإنجاز", null, 50m, 100m, "%", KpiCalcMethod.Auto, null)))
            .ReadAsync<KpiMetricDto>();

        var publishRes = await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishRes.StatusCode);

        return (created.Id, manual!.Id, auto!.Id);
    }

    [Fact]
    public async Task PublishWithWeightsNot100_IsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest("أوزان خاطئة", null, null, KpiCadence.Quarterly)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("مؤشر", null, 40m, null, null, KpiCalcMethod.Manual, null));

        var publishRes = await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.Conflict, publishRes.StatusCode);
    }

    [Fact]
    public async Task Evaluation_ComputesWeightedScore_AndTrend()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        // الفترة الأولى — درجة 50
        var ev1 = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W01")))
            .ReadAsync<KpiEvaluationDto>();
        await manager.PutAsJsonAsync($"/api/kpi-evaluations/{ev1!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 50m, null),
                new KpiResultInput(autoId, 50m, null, null)
            }));
        var sub1 = await (await manager.PostAsync($"/api/kpi-evaluations/{ev1.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(50m, sub1!.TotalScore);
        Assert.Equal(KpiTrend.Unknown, sub1.Trend);
        Assert.True(sub1.IsBelowTarget);

        // الفترة الثانية — درجة 65 ⇒ اتجاه صاعد
        var ev2 = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W02")))
            .ReadAsync<KpiEvaluationDto>();
        await manager.PutAsJsonAsync($"/api/kpi-evaluations/{ev2!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 80m, null),
                new KpiResultInput(autoId, 50m, null, null)
            }));
        var sub2 = await (await manager.PostAsync($"/api/kpi-evaluations/{ev2.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(65m, sub2!.TotalScore);
        Assert.Equal(KpiTrend.Up, sub2.Trend);
        Assert.False(sub2.IsBelowTarget);

        // الاعتماد
        var approved = await (await manager.PostAsync($"/api/kpi-evaluations/{ev2.Id}/approve", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.Approved, approved!.Status);
    }

    [Fact]
    public async Task Employee_CannotCreateEvaluation_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _, _) = await PublishKpiAsync(admin);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var res = await employee.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W03"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Subject_CanViewOwnEvaluation_OtherEmployeeCannot()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _, _) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (subjectClient, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (otherEmployee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var ev = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W04")))
            .ReadAsync<KpiEvaluationDto>();

        var ownRes = await subjectClient.GetAsync($"/api/kpi-evaluations/{ev!.Id}");
        Assert.Equal(HttpStatusCode.OK, ownRes.StatusCode);

        var otherRes = await otherEmployee.GetAsync($"/api/kpi-evaluations/{ev.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, otherRes.StatusCode);
    }

    [Fact]
    public async Task CreateEvaluation_IsIdempotent_PerPeriod()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _, _) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var first = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W05")))
            .ReadAsync<KpiEvaluationDto>();
        var second = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W05")))
            .ReadAsync<KpiEvaluationDto>();

        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task Anonymous_CannotListKpiTemplates_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/kpi-templates");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ===== نطاق إنشاء التقييم (Addendum) — المرؤوسون المباشرون فقط =====

    [Fact]
    public async Task Manager_CannotCreateEvaluation_ForNonDirectReport_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _, _) = await PublishKpiAsync(admin);

        // مدير، وموظّف تابع لمدير آخر (ليس مرؤوسًا مباشرًا للمدير الأول)
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, otherManagerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, foreignSubjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", otherManagerId);

        var res = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, foreignSubjectId, PeriodType.Weekly, "2026-W06"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateEvaluation_ForAnySubject_Override()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _, _) = await PublishKpiAsync(admin);

        // موظّف لا تربطه أيّ علاقة إشراف بالأدمن
        var (_, lonerManagerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", lonerManagerId);

        var res = await admin.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W07"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task EvaluatableSubjects_ReturnsOnlyDirectReports_ForManager()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, directReportId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        // مرؤوس غير مباشر: تابع لقائد فريق تابع للمدير (يجب ألّا يظهر)
        var (_, teamLeaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader", managerId);
        var (_, nestedMemberId) = await TestAuth.CreateUserAsync(_factory, "Employee", teamLeaderId);

        var dto = await (await manager.GetAsync("/api/kpi-evaluations/evaluatable-subjects"))
            .ReadAsync<EvaluatableSubjectsDto>();

        Assert.NotNull(dto);
        Assert.False(dto!.IsAdminOverride);
        var ids = dto.Subjects.Select(s => s.Id).ToHashSet();
        Assert.Contains(directReportId, ids);
        Assert.Contains(teamLeaderId, ids); // قائد الفريق مرؤوس مباشر للمدير
        Assert.DoesNotContain(nestedMemberId, ids); // العضو المتداخل ليس مباشرًا
    }

    [Fact]
    public async Task EvaluatableSubjects_AdminGetsOverride()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var dto = await (await admin.GetAsync("/api/kpi-evaluations/evaluatable-subjects"))
            .ReadAsync<EvaluatableSubjectsDto>();

        Assert.NotNull(dto);
        Assert.True(dto!.IsAdminOverride);
    }

    [Fact]
    public async Task EvaluatableSubjects_Employee_403()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await employee.GetAsync("/api/kpi-evaluations/evaluatable-subjects");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== حارس الدورية (UAT Phase 3 — البند 7): تقييم KPI أسبوعي فقط =====

    [Fact]
    public async Task CreateEvaluation_NonWeeklyPeriod_IsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _, _) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var res = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Quarterly, "2026-Q1"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task CreateEvaluation_Weekly_IsAccepted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _, _) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var res = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W08"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== صيغة مفتاح الفترة (UAT KPI UI): يجب أن تكون YYYY-Www حتى لا تُحفظ قيَم حرّة غير مفهومة =====

    [Theory]
    [InlineData("الاسبوع الاول من يوليو")]
    [InlineData("٩٨٧غفقيبلا")]
    [InlineData("2026-25")]
    [InlineData("W25-2026")]
    public async Task CreateEvaluation_MalformedWeekKey_IsRejected(string badKey)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _, _) = await PublishKpiAsync(admin);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var res = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, badKey));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== Phase 1 — KPI Stabilization (بدون Migration) =====

    /// <summary>ينشئ قالب KPI منشورًا بمؤشّر واحد وزنه 100 بطريقة الاحتساب المطلوبة، ويعيد (TemplateId, MetricId).</summary>
    private static async Task<(Guid TemplateId, Guid MetricId)> PublishSingleMetricAsync(
        HttpClient admin, KpiCalcMethod method, decimal? target, Guid? jobRoleId = null)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"مؤشر واحد {Guid.NewGuid():N}", null, jobRoleId, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var metric = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("المؤشّر", null, 100m, target, target.HasValue ? "%" : null, method, null)))
            .ReadAsync<KpiMetricDto>();
        var publishRes = await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishRes.StatusCode);
        return (created.Id, metric!.Id);
    }

    private static async Task<Guid> CreateJobRoleAsync(HttpClient admin)
    {
        var role = await (await admin.PostAsJsonAsync("/api/directory/job-roles",
            new CreateJobRoleRequest($"دور {Guid.NewGuid():N}", null, $"R{Guid.NewGuid():N}", null)))
            .ReadAsync<JobRoleDto>();
        return role!.Id;
    }

    // (1) سبب صفر النتيجة: الإرسال دون حفظ نتائج يُنتج صفرًا (يوثّق الجذر الذي تعالجه الواجهة بالحفظ-قبل-الإرسال).
    [Fact]
    public async Task Submit_WithoutSavingResults_YieldsZero()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishSingleMetricAsync(admin, KpiCalcMethod.Auto, 100m);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var ev = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W20")))
            .ReadAsync<KpiEvaluationDto>();
        var sub = await (await manager.PostAsync($"/api/kpi-evaluations/{ev!.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(0m, sub!.TotalScore);
    }

    // (1) عند الحفظ قبل الإرسال تكون النتيجة غير صفرية (السلوك بعد إصلاح الواجهة).
    [Fact]
    public async Task Submit_AfterSavingResults_YieldsNonZero()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, metricId) = await PublishSingleMetricAsync(admin, KpiCalcMethod.Auto, 100m);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var ev = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W21")))
            .ReadAsync<KpiEvaluationDto>();
        await manager.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[] { new KpiResultInput(metricId, 80m, null, null) }));
        var sub = await (await manager.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(80m, sub!.TotalScore);
    }

    // (2) Auto: Target=100, Raw=80 ⇒ Score=80.
    [Fact]
    public async Task AutoMetric_ComputesScoreFromRawAndTarget()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, metricId) = await PublishSingleMetricAsync(admin, KpiCalcMethod.Auto, 100m);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var ev = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W22")))
            .ReadAsync<KpiEvaluationDto>();
        await manager.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[] { new KpiResultInput(metricId, 80m, null, null) }));
        var sub = await (await manager.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(80m, sub!.TotalScore);
        Assert.Equal(80m, sub.Results.Single().Score);
    }

    // (3) Manual: الدرجة المُدخَلة هي المعتمدة، والقيمة الخام تُتجاهَل.
    [Fact]
    public async Task ManualMetric_UsesScore_IgnoresRawValue()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, metricId) = await PublishSingleMetricAsync(admin, KpiCalcMethod.Manual, null);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var ev = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W23")))
            .ReadAsync<KpiEvaluationDto>();
        // قيمة خام 99 (يجب تجاهلها) + درجة 70 (المعتمدة)
        await manager.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[] { new KpiResultInput(metricId, 99m, 70m, null) }));
        var sub = await (await manager.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(70m, sub!.TotalScore);
    }

    // (4) Hybrid: بلا درجة يدوية ⇒ يُحتسب من الخام/المستهدف.
    [Fact]
    public async Task HybridMetric_RawOnly_ComputesFromTarget()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, metricId) = await PublishSingleMetricAsync(admin, KpiCalcMethod.Hybrid, 100m);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var ev = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W24")))
            .ReadAsync<KpiEvaluationDto>();
        await manager.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[] { new KpiResultInput(metricId, 80m, null, null) }));
        var sub = await (await manager.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(80m, sub!.TotalScore);
    }

    // (4) Hybrid: إدخال الدرجة اليدوية يطغى على الاحتساب من الخام.
    [Fact]
    public async Task HybridMetric_ManualScoreWinsOverRaw()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, metricId) = await PublishSingleMetricAsync(admin, KpiCalcMethod.Hybrid, 100m);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var ev = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W25")))
            .ReadAsync<KpiEvaluationDto>();
        // خام 80 (=80% لو احتُسب) + درجة يدوية 60 (المعتمدة)
        await manager.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[] { new KpiResultInput(metricId, 80m, 60m, null) }));
        var sub = await (await manager.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(60m, sub!.TotalScore);
    }

    // (5) منتقي المسمّى الوظيفي — إنشاء مربوط، تعديل لمسمّى آخر، ثم إلى عام (null).
    [Fact]
    public async Task JobRolePicker_Create_Update_AndClear()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleA = await CreateJobRoleAsync(admin);
        var roleB = await CreateJobRoleAsync(admin);

        // إنشاء مربوط بـ roleA
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"قالب مسمّى {Guid.NewGuid():N}", null, roleA, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        Assert.Equal(roleA, created!.JobRoleId);

        // تعديل إلى roleB
        var updated = await (await admin.PutAsJsonAsync($"/api/kpi-templates/{created.Id}",
            new UpdateKpiTemplateRequest(created.Title, null, roleB, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        Assert.Equal(roleB, updated!.JobRoleId);

        // تحويل إلى عام (بدون مسمّى)
        var cleared = await (await admin.PutAsJsonAsync($"/api/kpi-templates/{created.Id}",
            new UpdateKpiTemplateRequest(created.Title, null, null, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        Assert.Null(cleared!.JobRoleId);
    }

    // (5) الإنشاء بلا مسمّى = قالب عام.
    [Fact]
    public async Task CreateKpiTemplate_NullJobRole_IsGeneral()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"عام {Guid.NewGuid():N}", null, null, KpiCadence.WeeklyPulse)))
            .ReadAsync<KpiTemplateDetailDto>();
        Assert.Null(created!.JobRoleId);
    }

    // (6) إعادة التفعيل — أرشفة ثم إعادة تفعيل ⇒ يعود غير مؤرشف ونشطًا.
    [Fact]
    public async Task ReactivateKpiTemplate_AfterArchive_RestoresActive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishSingleMetricAsync(admin, KpiCalcMethod.Auto, 100m);

        var archiveRes = await admin.DeleteAsync($"/api/kpi-templates/{templateId}");
        Assert.Equal(HttpStatusCode.OK, archiveRes.StatusCode);
        var archived = await (await admin.GetAsync($"/api/kpi-templates/{templateId}"))
            .ReadAsync<KpiTemplateDetailDto>();
        Assert.Equal(TemplateStatus.Archived, archived!.Status);

        var reactivateRes = await admin.PostAsync($"/api/kpi-templates/{templateId}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, reactivateRes.StatusCode);
        var reactivated = await (await admin.GetAsync($"/api/kpi-templates/{templateId}"))
            .ReadAsync<KpiTemplateDetailDto>();
        Assert.NotEqual(TemplateStatus.Archived, reactivated!.Status);
        Assert.True(reactivated.IsActive);
    }

    // (6) إعادة تفعيل قالب غير مؤرشف ⇒ 409.
    [Fact]
    public async Task ReactivateKpiTemplate_NotArchived_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishSingleMetricAsync(admin, KpiCalcMethod.Auto, 100m);
        var res = await admin.PostAsync($"/api/kpi-templates/{templateId}/reactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // (6) RBAC — موظّف لا يملك صلاحية إعادة التفعيل ⇒ 403.
    [Fact]
    public async Task ReactivateKpiTemplate_Employee_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishSingleMetricAsync(admin, KpiCalcMethod.Auto, 100m);
        await admin.DeleteAsync($"/api/kpi-templates/{templateId}");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await employee.PostAsync($"/api/kpi-templates/{templateId}/reactivate", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (7) التقييمات القديمة لا تتغيّر بعد تعديل قالب KPI (لقطة النسخة محفوظة).
    [Fact]
    public async Task OldEvaluation_Unchanged_AfterTemplateEdit()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, metricId) = await PublishSingleMetricAsync(admin, KpiCalcMethod.Auto, 100m);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, subjectId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var ev = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, "2026-W26")))
            .ReadAsync<KpiEvaluationDto>();
        await manager.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[] { new KpiResultInput(metricId, 80m, null, null) }));
        var sub = await (await manager.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(80m, sub!.TotalScore);
        var oldMetricName = sub.Results.Single().MetricName;

        // تعديل بيانات القالب + إصدار جديد منشور بمؤشّر مختلف
        await admin.PutAsJsonAsync($"/api/kpi-templates/{templateId}",
            new UpdateKpiTemplateRequest("عنوان معدّل", "وصف جديد", null, KpiCadence.WeeklyPulse));
        var draft = await (await admin.PostAsync($"/api/kpi-templates/{templateId}/versions", null))
            .ReadAsync<KpiTemplateVersionDto>();
        await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{draft!.Id}/metrics",
            new UpsertKpiMetricRequest("مؤشّر مختلف تمامًا", null, 100m, 50m, "%", KpiCalcMethod.Auto, null));
        await admin.PostAsync($"/api/kpi-templates/versions/{draft.Id}/publish", null);

        // التقييم القديم يجب أن يبقى كما هو
        var refetched = await (await manager.GetAsync($"/api/kpi-evaluations/{ev.Id}"))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(80m, refetched!.TotalScore);
        Assert.Single(refetched.Results);
        Assert.Equal(oldMetricName, refetched.Results.Single().MetricName);
    }
}
