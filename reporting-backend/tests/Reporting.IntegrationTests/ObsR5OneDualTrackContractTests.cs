using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Entities.System;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// <b>OBS-R5-01 — Dual Track</b> (قرار مالك المنتج): «النبض الأسبوعيّ» و«التقييم الربعيّ الرسميّ»
/// مساران <b>متزامنان لا متبادلان</b> لنفس الموظّف.
/// <para>
/// <b>العيب المُصلَح:</b> كان حسم التواتر يأخذ <c>Min</c> على سلّم الأولويّة <b>عبر التواترين معًا</b>
/// ثمّ يفصل التعادل لصالح الربعيّ ⟹ إسنادٌ ربعيّ أخصّ (مسمّى وظيفيّ) كان <b>يبتلع مسار النبض كلّيًّا</b>
/// فلا يظهر ولا يُحسب ولا يُنشأ. العلاج: التجميع حسب المسار أوّلًا، ثمّ تطبيق السلّم <b>داخل كلّ مسار</b>.
/// </para>
/// كلّ اختبار هنا يقيس نتيجة أعمال على واجهة HTTP فعليّة وقاعدة معزولة، ويغطّي بنود القبول الاثني
/// عشر: (2) و(3) و(8) و(9) و(10) و(11) و(12) كاملةً هنا، و(1) و(4) و(5) خادميًّا (ونظيرها في
/// الواجهة في <c>KpiDualTrackJourney.test.tsx</c>)، و(6) و(7) في الواجهة لأنّهما عن الطلب المُرسَل.
/// </summary>
[Collection("DecOneIsolated")]
public class ObsR5OneDualTrackContractTests
{
    private readonly DecOneIsolatedFactory _factory;

    public ObsR5OneDualTrackContractTests(DecOneIsolatedFactory factory) => _factory = factory;

    private const string Q = "2026-Q2";

    // ===================== أدوات مساعدة =====================

    private static async Task<(Guid TemplateId, Guid ManualId, Guid AutoId)> PublishAsync(
        HttpClient admin, KpiCadence cadence, Guid? jobRoleId = null)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"قالب OBS-R5-01 {Guid.NewGuid():N}", null, jobRoleId, cadence)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var manual = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الالتزام", null, 50m, null, null, KpiCalcMethod.Manual, null)))
            .ReadAsync<KpiMetricDto>();
        var auto = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الإنجاز", null, 50m, 100m, "%", KpiCalcMethod.Auto, null)))
            .ReadAsync<KpiMetricDto>();

        (await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null))
            .EnsureSuccessStatusCode();
        return (created.Id, manual!.Id, auto!.Id);
    }

    private static async Task AssignAsync(
        HttpClient admin, Guid templateId, TemplateAssignmentScope scope, Guid scopeId)
        => (await admin.PostAsJsonAsync($"/api/kpi-templates/{templateId}/assignments",
                new CreateKpiAssignmentRequest(scope, scopeId, TemplateAssignmentKind.Include, null, null, null)))
            .EnsureSuccessStatusCode();

    private static async Task<KpiEvaluationSetupDto> SetupAsync(HttpClient c, Guid subjectId)
    {
        var res = await c.GetAsync($"/api/kpi-evaluations/effective-setup?subjectUserId={subjectId}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiEvaluationSetupDto>())!;
    }

    private static KpiEvaluationTrackDto Track(KpiEvaluationSetupDto setup, KpiCadence cadence)
        => setup.Tracks.Single(t => t.Cadence == cadence);

    private static async Task<string[]> WeekKeysAsync(HttpClient client, string type, string periodKey)
    {
        var res = await client.GetAsync($"/api/kpi/periods/resolve?type={type}&periodKey={periodKey}");
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("weekKeys").EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    private static async Task<KpiPerformanceDto> PerfAsync(HttpClient c, string query)
    {
        var res = await c.GetAsync($"/api/kpi/performance?{query}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiPerformanceDto>())!;
    }

    private static async Task<KpiEmployeeScoreDto> RowAsync(HttpClient c, string query, Guid userId)
        => (await PerfAsync(c, query)).Employees.Single(e => e.UserId == userId);

    private static async Task<KpiDrilldownDto> DrilldownAsync(HttpClient c, string query)
    {
        var res = await c.GetAsync($"/api/kpi/drilldown?{query}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiDrilldownDto>())!;
    }

    /// <summary>ينشئ تقييمًا ويُدخل درجته ويرسله ويعتمده — «نتيجة معتمَدة» فعليّة لا صفّ مزروع.</summary>
    private async Task ApprovedEvaluationAsync(
        HttpClient evaluator, Guid templateId, Guid subjectId,
        Guid manualId, Guid autoId, PeriodType periodType, string periodKey, decimal score)
    {
        var ev = await (await evaluator.PostAsJsonAsync("/api/kpi-evaluations",
                new CreateKpiEvaluationRequest(templateId, subjectId, periodType, periodKey)))
            .ReadAsync<KpiEvaluationDto>();
        await evaluator.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, score, null),
                new KpiResultInput(autoId, score, null, null)
            }));
        await evaluator.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null);

        var ceo = await TestAuth.LoginAsRoleAsync(_factory, "CEO");
        var approved = await (await ceo.PostAsync($"/api/kpi-evaluations/{ev.Id}/approve", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.Approved, approved!.Status);
    }

    /// <summary>
    /// R6/§5.4 (WS-2) — يبذر صفّ تقييم <b>ربعيّ المسار ومعتمَد</b> مباشرةً في القاعدة. لا يمرّ عبر الـAPI
    /// لأنّ مسار الكتابة الربعيّة مُقفَل برمز <c>legacy_quarterly_write_disabled</c>؛ والغرض تمثيل
    /// السجلّات <b>القائمة</b> التي تبقى في القاعدة بلا حذف ولا هجرة، لإثبات استبعادها من القراءات
    /// التشغيليّة. لا بديل عن البذر المباشر: بغيره لا يمكن قياس الاستبعاد أصلًا.
    /// </summary>
    private async Task<Guid> SeedLegacyApprovedQuarterlyRowAsync(
        Guid quarterlyTemplateId, Guid subjectId, string periodKey, decimal score)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var versionId = await db.KpiTemplateVersions.Where(v => v.KpiTemplateId == quarterlyTemplateId)
            .OrderByDescending(v => v.VersionNumber).Select(v => v.Id).FirstAsync();
        var subject = await db.Users.AsNoTracking()
            .Where(u => u.Id == subjectId).Select(u => new { u.DepartmentId, u.TeamId }).FirstAsync();
        var row = new KpiEvaluation
        {
            KpiTemplateVersionId = versionId,
            SubjectUserId = subjectId,
            DepartmentId = subject.DepartmentId,
            TeamId = subject.TeamId,
            PeriodType = PeriodType.Quarterly,
            PeriodKey = periodKey,
            Status = KpiEvaluationStatus.Approved,
            TotalScore = score,
            ReviewedAtUtc = DateTime.UtcNow
        };
        db.KpiEvaluations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private async Task DeactivateGeneralTemplatesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.KpiTemplates.Where(t => t.JobRoleId == null && t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false));
    }

    /// <summary>
    /// قاعدة المجموعة مشتركة، واختبارات شقيقة تُعطّل القوالب العامّة عمدًا لتصنع حالة «لا إسناد».
    /// قبول (11) يقيس أثر <b>القوالب المبذورة</b> تحديدًا، فيجب أن يبدأ من حالة البذر لا من أثر
    /// اختبار سابق — وإلّا صار نتيجته رهينة ترتيب التشغيل لا رهينة الكود المقيس.
    /// </summary>
    private async Task ReactivateSeededGeneralTemplatesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.KpiTemplates.Where(t => SeededTitles.Contains(t.Title) && !t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, true));
    }

    private const string GeneralWeeklyPulseTitle = "النبض الأسبوعي العام";

    private static readonly string[] SeededTitles =
    {
        GeneralWeeklyPulseTitle, "مؤشرات مندوب المبيعات", "مؤشرات مشتري الإعلانات"
    };

    // ===================== قبول (1) — المساران معًا لموظّف واحد =====================

    [Fact]
    public async Task قبول01_موظّف_له_أسبوعيّ_وربعيّ_يُعيد_الخادم_المسارين_معًا_بمصدرَي_حسم_مستقلَّين()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"OBS_T1_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);

        // ربعيّ بمسمّاه (المستوى 2) + أسبوعيّ بإسناد صريح له (المستوى 1).
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);
        var (weekly, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse);
        await AssignAsync(admin, weekly, TemplateAssignmentScope.Employee, employee);

        var setup = await SetupAsync(manager, employee);
        var weeklyTrack = Track(setup, KpiCadence.WeeklyPulse);
        var quarterlyTrack = Track(setup, KpiCadence.Quarterly);

        // المساران **معًا** مُهيّآن: قبل الإصلاح كان المستوى الأدنى (1: إسناد الموظّف) يفوز عبر
        // التواترين مجتمعَين فيُخفي المسار الربعيّ كلّيًّا — والآن لكلّ مسار حسمه ومصدره.
        Assert.True(weeklyTrack.IsConfigured);
        Assert.True(quarterlyTrack.IsConfigured);
        Assert.Equal(KpiCadenceSources.EmployeeAssignment, weeklyTrack.CadenceSource);
        Assert.Equal(KpiCadenceSources.JobRole, quarterlyTrack.CadenceSource);
        Assert.Contains(weeklyTrack.Templates, t => t.Id == weekly);
        Assert.Contains(quarterlyTrack.Templates, t => t.Id == quarterly);
        Assert.Null(setup.BlockingReason);
    }

    // ===================== قبول (2) — الأخصّ الربعيّ لا يُخفي قالب النبض =====================

    [Fact]
    public async Task قبول02_قالب_ربعيّ_أضيق_لا_يُخفي_قالب_النبض_الأسبوعيّ_الأوسع()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"OBS_T2_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);

        // نبض **عامّ** (المستوى 5، الأوسع) + ربعيّ **بمسمّى** (المستوى 2، الأضيق).
        var (generalWeekly, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse);
        var (roleQuarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);

        var setup = await SetupAsync(manager, employee);
        var weeklyTrack = Track(setup, KpiCadence.WeeklyPulse);

        // هذه هي عيّنة العيب بعينها: 2 < 5 عالميًّا ⟹ كان النبض يختفي. الآن يبقى مسارًا كامل الصلاحيّة.
        Assert.True(weeklyTrack.IsConfigured);
        Assert.Equal(KpiCadenceSources.GeneralTemplate, weeklyTrack.CadenceSource);
        Assert.Contains(weeklyTrack.Templates, t => t.Id == generalWeekly);
        Assert.True(Track(setup, KpiCadence.Quarterly).IsConfigured);
        Assert.Contains(Track(setup, KpiCadence.Quarterly).Templates, t => t.Id == roleQuarterly);

        // ودليل العمل لا العرض: إنشاء نبض الأسبوع على هذا الموظّف ينجح فعلًا.
        var weeks = await WeekKeysAsync(manager, "Quarter", Q);
        var res = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(generalWeekly, employee, PeriodType.Weekly, weeks[0]));
        res.EnsureSuccessStatusCode();
    }

    // ===================== قبول (3) — السلّم يُطبَّق داخل كلّ مسار على حدة =====================

    [Fact]
    public async Task قبول03_سلّم_الأولويّة_يُطبَّق_داخل_كلّ_مسار_على_حدة_لا_عبر_المسارين()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"OBS_T3_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);

        // داخل المسار الأسبوعيّ: عامّ (5) مقابل إسناد موظّف (1) ⟹ يفوز الإسناد.
        var (generalWeekly, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse);
        var (employeeWeekly, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse);
        await AssignAsync(admin, employeeWeekly, TemplateAssignmentScope.Employee, employee);

        // وداخل المسار الربعيّ: عامّ (5) مقابل مسمّى (2) ⟹ يفوز المسمّى — بمعزل تامّ عن الأسبوعيّ.
        var (generalQuarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly);
        var (roleQuarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);

        var setup = await SetupAsync(manager, employee);
        var weeklyTrack = Track(setup, KpiCadence.WeeklyPulse);
        var quarterlyTrack = Track(setup, KpiCadence.Quarterly);

        // مستويان فائزان **مختلفان** في آنٍ واحد — وهو ما يستحيل مع حسم مفرد عبر المسارين.
        Assert.Equal(KpiCadenceSources.EmployeeAssignment, weeklyTrack.CadenceSource);
        Assert.Equal(KpiCadenceSources.JobRole, quarterlyTrack.CadenceSource);

        // و«الأخصّ يطغى» غير تراكميّ داخل كلّ مسار: الخاسر لا يظهر في قوائم مساره.
        Assert.Contains(weeklyTrack.Templates, t => t.Id == employeeWeekly);
        Assert.DoesNotContain(weeklyTrack.Templates, t => t.Id == generalWeekly);
        Assert.Contains(quarterlyTrack.Templates, t => t.Id == roleQuarterly);
        Assert.DoesNotContain(quarterlyTrack.Templates, t => t.Id == generalQuarterly);
    }

    // ===================== قبول (4) و(5) — غياب مسار لا يكسر المسار المقابل =====================

    [Fact]
    public async Task قبول04_غياب_القالب_الأسبوعيّ_لا_يكسر_المسار_الربعيّ_ويُعلَن_سببه_وحده()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"OBS_T4_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);

        await DeactivateGeneralTemplatesAsync();
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);

        var setup = await SetupAsync(manager, employee);
        var weeklyTrack = Track(setup, KpiCadence.WeeklyPulse);
        var quarterlyTrack = Track(setup, KpiCadence.Quarterly);

        Assert.False(weeklyTrack.IsConfigured);
        Assert.Equal(KpiCadenceSources.NotConfigured, weeklyTrack.CadenceSource);
        Assert.False(string.IsNullOrWhiteSpace(weeklyTrack.BlockingReason));
        Assert.Empty(weeklyTrack.Templates);

        // والربعيّ لا يُصاب بغياب جاره: مُهيّأ، وله قوالبه، وسببه الحاجب فارغ.
        Assert.True(quarterlyTrack.IsConfigured);
        Assert.True(setup.IsConfigured);
        Assert.Null(setup.BlockingReason);

        // R6/§5.4 — كان الدليل على «عمل المسار كاملًا» هو قبول الإنشاء، وقد تقاعدت الكتابة الربعيّة
        // بقرار المالك. الدليل الجديد أدقّ على المحور المقيس نفسه (**عزل سبب الرفض**): الرفض يأتي
        // برمز الإقفال وحده، لا بـ`kpi_eval.template_not_assigned` ولا برمز «غير مُهيّأ». فلو تسرّب
        // غيابُ المسار الأسبوعيّ إلى المسار الربعيّ لَظهر ذلك هنا رمزًا مختلفًا — وهو بالضبط العيب
        // الذي وُضع هذا الاختبار لرصده.
        var res = await manager.PostAsJsonAsync("/api/kpi-evaluations", new CreateKpiEvaluationRequest(
            quarterly, employee, quarterlyTrack.PeriodType, quarterlyTrack.CurrentPeriodKey));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("legacy_quarterly_write_disabled", body);
        Assert.DoesNotContain("kpi_eval.template_not_assigned", body);
    }

    [Fact]
    public async Task قبول05_غياب_القالب_الربعيّ_لا_يكسر_مسار_النبض_ويُعلَن_سببه_وحده()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"OBS_T5_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);

        await DeactivateGeneralTemplatesAsync();
        var (weekly, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        var setup = await SetupAsync(manager, employee);
        var weeklyTrack = Track(setup, KpiCadence.WeeklyPulse);
        var quarterlyTrack = Track(setup, KpiCadence.Quarterly);

        Assert.False(quarterlyTrack.IsConfigured);
        Assert.Equal(KpiCadenceSources.NotConfigured, quarterlyTrack.CadenceSource);
        Assert.False(string.IsNullOrWhiteSpace(quarterlyTrack.BlockingReason));
        Assert.Empty(quarterlyTrack.Templates);

        Assert.True(weeklyTrack.IsConfigured);
        Assert.True(setup.IsConfigured);
        var weeks = await WeekKeysAsync(manager, "Quarter", Q);
        (await manager.PostAsJsonAsync("/api/kpi-evaluations",
                new CreateKpiEvaluationRequest(weekly, employee, PeriodType.Weekly, weeks[0])))
            .EnsureSuccessStatusCode();
    }

    // ===================== قبول (8) — لا تقييم بقالب من المسار الآخر =====================

    [Fact]
    public async Task قبول08_لا_يُقبَل_تقييم_بقالب_من_مسار_مختلف_عن_نوع_فترته()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"OBS_T8_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);
        var (weekly, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);
        var weeks = await WeekKeysAsync(manager, "Quarter", Q);

        // إتاحة المسارين معًا لا تعني إباحة خلطهما: الحارس يبقى قائمًا في الاتّجاهين.
        var pulseOnQuarterly = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(quarterly, employee, PeriodType.Weekly, weeks[0]));
        Assert.Equal(HttpStatusCode.BadRequest, pulseOnQuarterly.StatusCode);
        Assert.Contains("kpi_eval.period_type_not_supported", await pulseOnQuarterly.Content.ReadAsStringAsync());

        var quarterOnWeekly = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(weekly, employee, PeriodType.Quarterly, Q));
        Assert.Equal(HttpStatusCode.BadRequest, quarterOnWeekly.StatusCode);
        Assert.Contains("kpi_eval.period_type_not_supported", await quarterOnWeekly.Content.ReadAsStringAsync());

        // R6/§5.4 (WS-1) — المسار الأسبوعيّ في مساره يُقبَل: الحارس يمنع الخلط لا الرحلة.
        (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(weekly, employee, PeriodType.Weekly, weeks[0]))).EnsureSuccessStatusCode();

        // أمّا «الربعيّ في مساره» فلم يعد يُقبَل: مسار الكتابة الربعيّة متقاعد برمز مستقلّ،
        // ويجب أن يكون الرمز رمزَ التقاعد لا رمزَ الخلط — وإلّا اختلط عقدان مختلفان.
        var retired = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(quarterly, employee, PeriodType.Quarterly, Q));
        Assert.Equal(HttpStatusCode.BadRequest, retired.StatusCode);
        var retiredBody = await retired.Content.ReadAsStringAsync();
        Assert.Contains("legacy_quarterly_write_disabled", retiredBody);
        Assert.DoesNotContain("kpi_eval.period_type_not_supported", retiredBody);
    }

    // ===================== قبول (9) — عدّادات النبض مستقلّة، و(10) بعد R6: لا مسار ربعيّ يُقرأ =====================

    /// <summary>
    /// R6/§4+§5.3 — بند القبول (9) <b>محفوظ كما هو</b>: مقام النبض عدد دورات الربع لا 1، وقيمته من
    /// تقييماته وحدها. أمّا بند (10) — «المسار الربعيّ الرسميّ يستهلك تقييمه الربعيّ وحده» — فقد سقط
    /// موضوعه: لم يعد ثمّة مسار ربعيّ يُكتب (WS-1) ولا يُقرأ (§5.3/NF-08). فيُقاس بدلًا منه ما طلبه
    /// المالك نصًّا: أنّ <b>النبض الأسبوعيّ المعتمَد هو مصدر التجميع الوحيد</b>، وأنّ صفًّا ربعيًّا
    /// إرثيًّا <b>قائمًا ومعتمَدًا</b> في القاعدة لا يدخل أيّ قراءة تشغيليّة ولا يحرّك رقمًا، وأنّ طلب
    /// المسار الربعيّ صراحةً يُردّ برمز مسمًّى بدل لوحة صفريّة صامتة.
    /// </summary>
    [Fact]
    public async Task قبول09و10_عدّادات_النبض_مستقلّة_والمسار_الربعيّ_مُقفَل_قراءةً_ولا_يحرّك_رقمًا()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"OBS_T9_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);

        await DeactivateGeneralTemplatesAsync();
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);
        var (weekly, wManual, wAuto) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);
        var weeks = await WeekKeysAsync(manager, "Quarter", Q);

        // نتيجة نبض معتمَدة واحدة (60)، وبجوارها صفّ ربعيّ إرثيّ **معتمَد** (90) لنفس الموظّف وداخل
        // نفس الربع. الصفّ الإرثيّ مبذور مباشرةً لأنّ مسار كتابته مُقفَل (WS-1) وهو باقٍ بلا مسّ (WS-2).
        await ApprovedEvaluationAsync(manager, weekly, employee, wManual, wAuto, PeriodType.Weekly, weeks[0], 60m);
        var legacyId = await SeedLegacyApprovedQuarterlyRowAsync(quarterly, employee, Q, 90m);

        var q = $"periodType=Quarter&periodKey={Q}&subjectUserId={employee}";

        // (10-بديل) طلب المسار الربعيّ صراحةً مردود برمزه على نقاط القراءة التحليليّة — لا 200 صفريّة.
        var quarterlyPerf = await manager.GetAsync($"/api/kpi/performance?{q}&cadence=Quarterly");
        Assert.Equal(HttpStatusCode.BadRequest, quarterlyPerf.StatusCode);
        Assert.Contains("legacy_cadence_disabled", await quarterlyPerf.Content.ReadAsStringAsync());

        var quarterlyDrill = await manager.GetAsync($"/api/kpi/drilldown?{q}&cadence=Quarterly");
        Assert.Equal(HttpStatusCode.BadRequest, quarterlyDrill.StatusCode);
        Assert.Contains("legacy_cadence_disabled", await quarterlyDrill.Content.ReadAsStringAsync());

        // (9) وعدّادات النبض مستقلّة تمامًا: مقامها عدد دورات الربع، لا 1.
        var weeklyRow = await RowAsync(manager, $"{q}&cadence=WeeklyPulse", employee);
        Assert.Equal(60m, weeklyRow.Measure.Value);
        Assert.Equal(1, weeklyRow.Measure.EligibleEvaluationCount);
        Assert.Equal(weeks.Length, weeklyRow.Measure.AdjustedExpectedCount);
        Assert.Equal(weeks.Length - 1, weeklyRow.Measure.MissingCount);

        // ودرجة الصفّ الإرثيّ (90) لا تحرّك الرقم ولا تُحتسب: القراءة الافتراضيّة تطابق قراءة النبض.
        var defaultRow = await RowAsync(manager, q, employee);
        Assert.Equal(KpiCadence.WeeklyPulse, defaultRow.EffectiveCadence);
        Assert.Equal(60m, defaultRow.Measure.Value);
        Assert.Equal(1, defaultRow.Measure.EligibleEvaluationCount);

        // والتفصيل يحفظ نقاء المصدر: كلّ صفوفه من النبض، ولا أثر للصفّ الإرثيّ ولا لمفتاحه الربعيّ.
        var weeklyRows = (await DrilldownAsync(manager, $"{q}&cadence=WeeklyPulse")).Rows;
        Assert.NotEmpty(weeklyRows);
        Assert.All(weeklyRows, r => Assert.Equal(KpiCadence.WeeklyPulse, r.Cadence));
        Assert.DoesNotContain(weeklyRows, r => r.EvaluationId == legacyId);
        Assert.DoesNotContain(weeklyRows, r => r.PeriodKey == Q);
    }

    // ===================== قبول (11) — موظّفو القوالب المبذورة لا يفقدون النبض =====================

    [Theory]
    [InlineData("SALES_B2B")]
    [InlineData("MEDIA_BUYER")]
    public async Task قبول11_موظّفو_القوالب_الربعيّة_المبذورة_يحتفظون_بمسار_النبض_الأسبوعيّ(string roleCode)
    {
        await ReactivateSeededGeneralTemplatesAsync();

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(
            _factory, "Employee", roleCode, managerId);

        // R6.3/§3 — كان الشرط المسبق يُصنَع عبر `OrgSeeder` الذي يربط **قالبًا ربعيًّا مبذورًا**
        // بمسمّاه. بعد إقفال بذر الربعيّ لم يعد ذلك القالب يُنشأ على قاعدة نظيفة، فبقي الشرط بلا
        // مُنتِج والاختبار يقيس العدم. والسيناريو نفسه **ما زال قائمًا في قواعد الإنتاج** (WS-2:
        // القوالب التاريخيّة لم تُحذف ولا تُهاجَر) ⇒ التغطية تُصان بصناعة الشرط صراحةً بدل
        // الاعتماد على الباذر: قالب ربعيّ منشور مربوط بالمسمّى نفسه. لا تأكيد حُذف ولا أُضعِف.
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, roleCode);
        await PublishAsync(admin, KpiCadence.Quarterly, role);

        var setup = await SetupAsync(manager, employee);
        var weeklyTrack = Track(setup, KpiCadence.WeeklyPulse);
        var quarterlyTrack = Track(setup, KpiCadence.Quarterly);

        // الشرط المسبق للسيناريو: الربعيّ المربوط بالمسمّى فاز بالمستوى الأخصّ فعلًا.
        Assert.Equal(KpiCadenceSources.JobRole, quarterlyTrack.CadenceSource);

        // ومع ذلك «النبض الأسبوعي العام» (المستوى العامّ) يبقى قائمًا في مساره.
        // قبل الإصلاح كان الأخصّ الربعيّ يبتلع النبض كلّيًّا فيفقد الموظّف المسار.
        Assert.True(weeklyTrack.IsConfigured);
        Assert.Equal(KpiCadenceSources.GeneralTemplate, weeklyTrack.CadenceSource);
        Assert.Contains(weeklyTrack.Templates, t => t.Name == GeneralWeeklyPulseTitle);
        Assert.Equal(PeriodType.Weekly, weeklyTrack.PeriodType);
        Assert.Null(weeklyTrack.BlockingReason);
    }

    // ===================== قبول (12) — بذر متكرّر بلا تكرار ولا تغيير حالة صامت =====================

    [Fact]
    public async Task قبول12_إعادة_البذر_لا_تُنشئ_قالبًا_مكرّرًا_ولا_تغيّر_حالة_قالب_قائم_بلا_سجلّ()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        async Task<Dictionary<string, (Guid Id, TemplateStatus Status, KpiCadence Cadence, Guid? JobRoleId)>> SnapshotAsync()
            => await db.KpiTemplates.AsNoTracking()
                .ToDictionaryAsync(t => t.Title, t => (t.Id, t.Status, t.Cadence, t.JobRoleId));

        const string BindingAction = "KpiTemplateSeedBinding.JobRoleAssigned";
        Task<List<AuditLog>> BindingAuditsAsync()
            => db.AuditLogs.AsNoTracking().Where(a => a.Action == BindingAction).ToListAsync();

        // R6.3/§3 — ضمان وجود مسمّى المسبار صراحةً: `OrgSeeder.SeedJobRolesAsync` يخرج مبكّرًا
        // إن وُجد أيّ مسمّى في القاعدة المشتركة، فربطُ القالب كان يقع أو لا يقع بحسب ترتيب
        // تشغيل الاختبارات الشقيقة. الإنشاء الصريح يجعل الشرط المسبق ملكًا للاختبار لا للترتيب.
        await TestAuth.GetOrCreateJobRoleAsync(_factory, "SALES_B2C");

        // ندفع القاعدة إلى حالة التقارب أوّلًا. `OrgSeeder` لا يعمل إلّا في Development فقد لا يكون
        // عمل داخل مصنع الاختبار ⟹ لو قِسنا مباشرةً لخلطنا «التقارب الأوّل» بـ«إعادة البذر».
        await TemplateSeeder.SeedAsync(scope.ServiceProvider);
        await OrgSeeder.SeedAsync(scope.ServiceProvider);

        // ثمّ نفكّ ربط قالب مبذور بعينه عمدًا لنُعيد خلق حالة «ما قبل الربط» — فيصير القياس مستقلًّا
        // عن كون القاعدة نظيفة أو مبذورة سلفًا، لا رهينة ترتيب التشغيل.
        //
        // R6.3/§3 — كان المسبار «مؤشرات مندوب المبيعات» (ربعيّ). بعد إقفال بذر الربعيّ لم يعد
        // يُنشأ على قاعدة نظيفة، فبُدِّل إلى قالب **ما زال يُبذَر ويُربَط** بمسمّاه في القائمة نفسها
        // (`OrgSeeder.kpiTitleToRole`). المقيس لم يتغيّر: تسجيل أثر صريح عند إعادة الربط، ثمّ
        // ثبات البذر (لا قالب جديد ولا معرّف ولا حالة ولا أثر إضافيّ). التغطية كما هي بالكامل.
        const string BoundTitle = B2cReportSchema.KpiTitle;
        var probe = await db.KpiTemplates.FirstAsync(t => t.Title == BoundTitle);
        var expectedRoleId = probe.JobRoleId;
        Assert.NotNull(expectedRoleId);
        probe.JobRoleId = null;
        await db.SaveChangesAsync();

        var auditsBeforeRebind = await BindingAuditsAsync();
        await OrgSeeder.SeedAsync(scope.ServiceProvider);

        // إعادة الربط تغيير حالة على قالب قائم — والعقد يبيحه **بسجلّ واضح** لا صمتًا.
        var auditsAfterRebind = await BindingAuditsAsync();
        var rebindAudit = Assert.Single(
            auditsAfterRebind.Where(a => auditsBeforeRebind.All(b => b.Id != a.Id)));
        Assert.Equal(nameof(KpiTemplate), rebindAudit.EntityType);
        Assert.Equal(probe.Id, rebindAudit.EntityId);
        var payload = JsonDocument.Parse(rebindAudit.DataJson!).RootElement;
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("before").ValueKind);
        // الأثر ليس نصًّا تجميليًّا: قيمته «بعد» تطابق الحالة المخزَّنة فعلًا لنفس القالب.
        Assert.Equal(expectedRoleId, payload.GetProperty("after").GetGuid());
        Assert.Equal(expectedRoleId,
            await db.KpiTemplates.AsNoTracking().Where(t => t.Id == probe.Id)
                .Select(t => t.JobRoleId).SingleAsync());

        var before = await SnapshotAsync();
        Assert.NotEmpty(before);

        // إعادة تشغيل البذر كاملًا — نفس ما يحدث عند كلّ إقلاع تالٍ للتطبيق.
        await TemplateSeeder.SeedAsync(scope.ServiceProvider);
        await OrgSeeder.SeedAsync(scope.ServiceProvider);

        var after = await SnapshotAsync();

        // لا قالب جديد، ولا معرّف تغيّر، ولا حالة انقلبت صمتًا: البذر Idempotent فعلًا لا ادّعاءً.
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(before.Keys.OrderBy(k => k), after.Keys.OrderBy(k => k));
        foreach (var (title, snapshot) in before)
        {
            Assert.Equal(snapshot.Id, after[title].Id);
            Assert.Equal(snapshot.Status, after[title].Status);
            Assert.Equal(snapshot.Cadence, after[title].Cadence);
            Assert.Equal(snapshot.JobRoleId, after[title].JobRoleId);
        }

        // ولا أثر ربط جديد في الجولة التالية — فلا «تغيير صامت» ولا حتّى ضجيج سجلّ بلا تغيير.
        Assert.Equal(auditsAfterRebind.Count, (await BindingAuditsAsync()).Count);

        // وحارس النشر ليس متجاوَزًا. والعهد المقيس هنا **لكلّ إصدار منشور على حدة** لا لكلّ قالب:
        // `KpiTemplateService.PublishVersionAsync` يرفض نشر إصدار مجموع أوزانه ≠ 100، ولا يُلغي نشر
        // الإصدارات السابقة — فتعدّد الإصدارات المنشورة للقالب الواحد حالة سليمة مقصودة (التقييمات
        // التاريخيّة تبقى مقروءة بإصداراتها). تسطيح المؤشّرات عبر إصدارات القالب كان يجمع 100+100=200
        // فيقرأ حالةً سليمة عيبًا — خطأ نطاق في القياس لا خرقٌ لحارس النشر.
        //
        // R6.5 — توفيق: مرشّحان مستقلّان أصلحا هذا العيب نفسه، واتّفقا حرفيًّا على تأكيد «مجموع أوزان
        // كلّ إصدار منشور = 100»، واختلفا في صياغة القضيّة المرافقة «كلّ قالب منشور له إصدار منشور»:
        // أحدهما عدَّ الإصدارات المنشورة للقالب، والآخر تحقّق من احتواء عنوان القالب في قائمة الإصدارات
        // المنشورة. القضيّتان متكافئتان منطقيًّا على المصدر نفسه (المرشِّحان يقرآن الجدولين ذاتهما بشرطَي
        // `IsPublished` و`Status == Published`)، فأُبقيت صياغة العدّ لأنّها **الأقوى تشخيصًا**: تنصّ على
        // العتبة رقمًا وتُسمّي القالب المخالف في رسالة الفشل. المحذوف تكرارٌ لا تغطية.
        var publishedTemplates = await db.KpiTemplates.AsNoTracking()
            .Where(t => t.Status == TemplateStatus.Published)
            .Select(t => new { t.Title, PublishedVersions = t.Versions.Count(v => v.IsPublished) })
            .ToListAsync();
        Assert.NotEmpty(publishedTemplates);
        Assert.All(publishedTemplates, t =>
            Assert.True(t.PublishedVersions >= 1, $"القالب المنشور «{t.Title}» بلا إصدار منشور."));

        var publishedVersions = await db.KpiTemplateVersions.AsNoTracking()
            .Where(v => v.IsPublished && v.KpiTemplate!.Status == TemplateStatus.Published)
            .Select(v => new
            {
                v.KpiTemplate!.Title,
                v.VersionNumber,
                Weights = v.Metrics.Select(m => m.Weight).ToList()
            })
            .ToListAsync();
        Assert.NotEmpty(publishedVersions);
        Assert.All(publishedVersions, v =>
        {
            Assert.NotEmpty(v.Weights);
            Assert.Equal(100m, v.Weights.Sum());
        });
    }

    // ============ المسار الأوّليّ بلا طلب صريح — أخصّ الإسنادين لا نوع المسار ============

    /// <summary>
    /// عيب مقيس أثناء انحدار OBS-R5-01: مستهلكو التحليلات القدامى ينادون <c>/api/kpi/performance</c>
    /// <b>بلا</b> <c>cadence</c>. حين كان المسار الأوّليّ يُنتقى بالنوع («الربعيّ إن وُجد») صار وجودُ
    /// قالبٍ ربعيّ <b>عامّ</b> يبتلع نبضًا أسبوعيًّا مُسنَدًا إلى مسمّى الموظّف نفسه: مقام الربع صار 1
    /// بدل 13 أسبوعًا، فتنقلب تغطية الموظّف ومقامه بسبب قالب عامّ لم يُقصَد به.
    ///
    /// القاعدة التي كانت مقيسة: الأوّليّ هو الأخصّ إسنادًا، وعند التساوي يفوز الربعيّ الرسميّ.
    ///
    /// R6/§4+§5.5 — <b>العيب المقيس باقٍ ومقيس، وقاعدة كسر التعادل هي التي سقطت</b>: لم يعد ثمّة
    /// «مسار ربعيّ» يفوز بشيء. الغياب يعني «النبض الأسبوعيّ» صراحةً لا «الأخصّ أيًّا كان»، فيستحيل
    /// على قالب ربعيّ — عامًّا كان أو بالمسمّى — أن يقلب المقام من 13 أسبوعًا إلى دورة واحدة.
    /// وهذا يقيس الحماية نفسها بضمانة أقوى: لا مساحة تعادل أصلًا. وطلب المسار الربعيّ صراحةً
    /// لم يعد قراءةً بديلة بل ردًّا مسمًّى (§5.3/NF-08).
    /// </summary>
    [Fact]
    public async Task المسار_الأوّليّ_بلا_طلب_صريح_هو_النبض_الأسبوعيّ_ولا_يقلبه_قالب_ربعيّ()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"OBS_PRIM_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);

        // أسبوعيّ مُسنَد إلى مسمّاه (مستوى 3) مقابل ربعيّ عامّ (مستوى 5).
        await PublishAsync(admin, KpiCadence.WeeklyPulse, role);
        await PublishAsync(admin, KpiCadence.Quarterly);

        var setup = await SetupAsync(manager, employee);
        Assert.Equal(KpiCadenceSources.JobRole, Track(setup, KpiCadence.WeeklyPulse).CadenceSource);
        Assert.Equal(KpiCadenceSources.GeneralTemplate, Track(setup, KpiCadence.Quarterly).CadenceSource);

        var weeks = await WeekKeysAsync(manager, "Quarter", Q);
        var auto = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);

        // المقام يتبع المسار الأخصّ: 13 دورة أسبوعيّة لا دورة ربعيّة واحدة.
        Assert.Equal(KpiCadence.WeeklyPulse, auto.EffectiveCadence);
        Assert.Equal(KpiCadenceSources.JobRole, auto.CadenceSource);
        Assert.Equal(weeks.Length, auto.Measure.ExpectedEvaluationCount);

        // والربعيّ العامّ لم يعد قراءةً بديلة: طلبه صراحةً يُردّ برمزه بدل لوحة موازية.
        var quarterlyRes = await manager.GetAsync(
            $"/api/kpi/performance?cadence=Quarterly&periodType=Quarter&periodKey={Q}&subjectUserId={employee}");
        Assert.Equal(HttpStatusCode.BadRequest, quarterlyRes.StatusCode);
        Assert.Contains("legacy_cadence_disabled", await quarterlyRes.Content.ReadAsStringAsync());

        // وحتّى حين يصير الربعيّ **بالمسمّى** (أخصّ إسناد ممكن) لا يقلب المقام ولا المسار الأوّليّ:
        // لا قاعدة كسر تعادل تُطبَّق، لأنّ المسار الوحيد هو النبض الأسبوعيّ.
        await PublishAsync(admin, KpiCadence.Quarterly, role);
        var tie = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);
        Assert.Equal(KpiCadence.WeeklyPulse, tie.EffectiveCadence);
        Assert.Equal(KpiCadenceSources.JobRole, tie.CadenceSource);
        Assert.Equal(weeks.Length, tie.Measure.ExpectedEvaluationCount);
    }
}
