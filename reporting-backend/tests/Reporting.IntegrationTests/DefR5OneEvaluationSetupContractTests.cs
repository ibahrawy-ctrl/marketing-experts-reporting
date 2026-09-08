using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Kpi;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// R5 — قرار مالك المنتج: <b>DEF-R5-001</b> (رحلة إنشاء التقييم) و<b>DEVIATION-01</b> (صحّة اختيار القالب)
/// و<b>DEVIATION-02</b> (حارس تطابق المسارين) — مقيسة على واجهة HTTP فعليّة وقاعدة معزولة.
/// <list type="bullet">
/// <item>الواجهة لا تختار تواترًا ولا تفترضه: «الإعداد الفعّال» يُعلن المسار ومصدره ونوع الفترة ومفتاحها الجاري.</item>
/// <item>الخادم هو الحاسم النهائيّ: تعديل طلب الواجهة (قالب غير مُسنَد أو نوع فترة من المسار الآخر) يُرفَض برمز مسمًّى.</item>
/// <item>غياب القالب الصالح حالة مسمّاة معروضة، لا اختيار صامت ولا طلب إنشاء غير صالح.</item>
/// <item>لا سقوط إلى قالب عامّ مع وجود إسناد أخصّ، ولا قالب من المسار الآخر داخل قوائم المسار.</item>
/// <item>نتائج المسارين لا تختلط في الحساب ولا في التفصيل.</item>
/// </list>
/// </summary>
[Collection("DecOneIsolated")]
public class DefR5OneEvaluationSetupContractTests
{
    private readonly DecOneIsolatedFactory _factory;

    public DefR5OneEvaluationSetupContractTests(DecOneIsolatedFactory factory) => _factory = factory;

    private const string Q = "2026-Q2";

    // ===================== أدوات مساعدة =====================

    private static async Task<(Guid TemplateId, Guid ManualId, Guid AutoId)> PublishAsync(
        HttpClient admin, KpiCadence cadence, Guid? jobRoleId = null)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"قالب DEF-R5-001 {Guid.NewGuid():N}", null, jobRoleId, cadence)))
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
    {
        var res = await admin.PostAsJsonAsync($"/api/kpi-templates/{templateId}/assignments",
            new CreateKpiAssignmentRequest(scope, scopeId, TemplateAssignmentKind.Include, null, null, null));
        res.EnsureSuccessStatusCode();
    }

    /// <summary>ما تراه الواجهة فعلًا قبل أن ترسم الشاشة: مصدر الحقيقة الوحيد لرحلة الإنشاء.</summary>
    private static async Task<KpiEvaluationSetupDto> SetupAsync(HttpClient c, Guid subjectId)
    {
        var res = await c.GetAsync($"/api/kpi-evaluations/effective-setup?subjectUserId={subjectId}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiEvaluationSetupDto>())!;
    }

    /// <summary>
    /// مسار بعينه من «الإعداد الفعّال». التسمية صريحة لأنّ العقد يُعيد <b>المسارين معًا دائمًا</b>
    /// (OBS-R5-01): لا حقل مسطّح واحد يمكنه ابتلاع أحدهما، ولا اختبار يقرأ «التواتر الفعّال» مجرَّدًا.
    /// </summary>
    private static KpiEvaluationTrackDto Track(KpiEvaluationSetupDto setup, KpiCadence cadence)
        => setup.Tracks.Single(t => t.Cadence == cadence);

    private static async Task<string[]> WeekKeysAsync(HttpClient client, string type, string periodKey)
    {
        var res = await client.GetAsync($"/api/kpi/periods/resolve?type={type}&periodKey={periodKey}");
        res.EnsureSuccessStatusCode();
        using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
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

    /// <summary>قوالب البذر العامّة إسنادٌ فعّال (المستوى الخامس)؛ تعطيلها يجعل «لا إعداد» حالةً قابلة للبلوغ.</summary>
    private async Task DeactivateGeneralTemplatesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.KpiTemplates.Where(t => t.JobRoleId == null && t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false));
    }

    // ===================== DEF-R5-001 — الواجهة تعرض ولا تختار =====================

    [Fact]
    public async Task الإعداد_الفعّال_يحسم_المسار_الربعيّ_وفترته_الجارية_بلا_سؤال()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, employee) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var foreign = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"R5_S1_{Guid.NewGuid():N}");
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, foreign);
        await AssignAsync(admin, quarterly, TemplateAssignmentScope.Employee, employee);

        var setup = await SetupAsync(manager, employee);

        var track = Track(setup, KpiCadence.Quarterly);
        Assert.True(setup.IsConfigured);
        Assert.Null(setup.BlockingReason);
        Assert.True(track.IsConfigured);
        Assert.Equal(KpiCadenceSources.EmployeeAssignment, track.CadenceSource);
        // نوع الفترة ومفتاحها يأتيان من الخادم — لا تشتقّهما الواجهة ولا تسأل عنهما المستخدم.
        Assert.Equal(PeriodType.Quarterly, track.PeriodType);
        Assert.Matches(@"^\d{4}-Q[1-4]$", track.CurrentPeriodKey);
        Assert.Contains(track.Templates, t => t.Id == quarterly);
        Assert.All(track.Templates, t => Assert.False(string.IsNullOrWhiteSpace(t.Name)));
    }

    [Fact]
    public async Task الإعداد_الفعّال_يحسم_مسار_نبض_الأسبوع_ومفتاح_دورته_الجارية()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, employee) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, employee);
        var foreign = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"R5_S2_{Guid.NewGuid():N}");
        var (weekly, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse, foreign);
        await AssignAsync(admin, weekly, TemplateAssignmentScope.Team, teamId);

        var setup = await SetupAsync(manager, employee);

        var track = Track(setup, KpiCadence.WeeklyPulse);
        Assert.True(setup.IsConfigured);
        Assert.True(track.IsConfigured);
        Assert.Equal(KpiCadenceSources.TeamAssignment, track.CadenceSource);
        Assert.Equal(PeriodType.Weekly, track.PeriodType);
        Assert.Matches(@"^\d{4}-W\d{2}$", track.CurrentPeriodKey);
        Assert.Contains(track.Templates, t => t.Id == weekly);
    }

    [Fact]
    public async Task بلا_إسناد_فعّال_يعلن_الإعداد_حالة_مسمّاة_ولا_يعرض_قوالب()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, orphan) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        await DeactivateGeneralTemplatesAsync();
        var foreign = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"R5_S3_{Guid.NewGuid():N}");
        await PublishAsync(admin, KpiCadence.WeeklyPulse, foreign);

        var setup = await SetupAsync(manager, orphan);

        // DEC-01/5 — حالة مسمّاة معروضة، لا صمت ولا سقوط افتراضيّ إلى الأسبوعيّ.
        // OBS-R5-01/3 — وحين يغيب المساران معًا يُعلَن كلٌّ منهما بسببه الخاصّ، ويبقى المسارَان
        // ظاهرَين بفترتهما ونوعها (المعرفة الزمنيّة ليست تهيئة) — فلا يختفي أحدهما بسبب الآخر.
        Assert.False(setup.IsConfigured);
        Assert.False(string.IsNullOrWhiteSpace(setup.BlockingReason));
        Assert.Equal(2, setup.Tracks.Count);
        Assert.All(setup.Tracks, t =>
        {
            Assert.False(t.IsConfigured);
            Assert.Equal(KpiCadenceSources.NotConfigured, t.CadenceSource);
            Assert.Empty(t.Templates);
            Assert.False(string.IsNullOrWhiteSpace(t.BlockingReason));
        });
    }

    /// <summary>
    /// R6/§5.4 — كان الاختبار يثبت نجاح الإنشاء في **المسارين معًا**. بقرار المالك تقاعد مسار الكتابة
    /// الربعيّة، فانقلبت نتيجة الشقّ الربعيّ وحده. المحور محفوظ كما هو: نفس الحلقة، ونفس الموظّفَين،
    /// ونفس المبدأ المقيس («ما ترسله الواجهة = ما أعلنه الخادم حرفيًّا») — وما تغيّر هو أنّ إعلان
    /// الخادم للمسار الربعيّ صار إعلان بابٍ مغلق، ويجب أن يُغلَق بالرمز المسمّى لا بخطأ غامض.
    /// وبقاء المسار الربعيّ **معلَنًا في الإعداد** مقصود: الصفوف التاريخيّة تُقرأ ولا تُنكَر (WS-2)،
    /// والمقفَل هو الكتابة عليها.
    /// </summary>
    [Fact]
    public async Task إنشاء_التقييم_من_الإعداد_الفعّال_ينجح_للأسبوعيّ_ويُرفَض_للربعيّ_المتقاعد()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, quarterlySubject) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (_, weeklySubject) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var foreign = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"R5_S4_{Guid.NewGuid():N}");
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, foreign);
        var (weekly, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse, foreign);
        await AssignAsync(admin, quarterly, TemplateAssignmentScope.Employee, quarterlySubject);
        await AssignAsync(admin, weekly, TemplateAssignmentScope.Employee, weeklySubject);

        // الشقّ الأسبوعيّ — مصدر الحقيقة الوحيد بعد R6: يُقبَل كما كان بلا أيّ تخفيف.
        var weeklyTrack = Track(await SetupAsync(manager, weeklySubject), KpiCadence.WeeklyPulse);
        Assert.True(weeklyTrack.IsConfigured);
        Assert.Contains(weeklyTrack.Templates, t => t.Id == weekly);

        // ما ترسله الواجهة = ما أعلنه الخادم حرفيًّا: قالب من قائمته، ونوع فترة ومفتاحًا من عنده.
        var weeklyRes = await manager.PostAsJsonAsync("/api/kpi-evaluations", new CreateKpiEvaluationRequest(
            weekly, weeklySubject, weeklyTrack.PeriodType, weeklyTrack.CurrentPeriodKey));
        weeklyRes.EnsureSuccessStatusCode();

        var ev = (await weeklyRes.ReadAsync<KpiEvaluationDto>())!;
        Assert.Equal(weeklySubject, ev.SubjectUserId);
        Assert.Equal(PeriodType.Weekly, ev.PeriodType);
        Assert.Equal(weeklyTrack.CurrentPeriodKey, ev.PeriodKey);

        // الشقّ الربعيّ — الإعداد ما زال يعلنه (الصفوف التاريخيّة تُقرأ)، لكنّ الكتابة عليه مُقفَلة.
        var quarterlyTrack = Track(await SetupAsync(manager, quarterlySubject), KpiCadence.Quarterly);
        Assert.True(quarterlyTrack.IsConfigured);
        Assert.Contains(quarterlyTrack.Templates, t => t.Id == quarterly);

        var quarterlyRes = await manager.PostAsJsonAsync("/api/kpi-evaluations", new CreateKpiEvaluationRequest(
            quarterly, quarterlySubject, quarterlyTrack.PeriodType, quarterlyTrack.CurrentPeriodKey));
        Assert.Equal(HttpStatusCode.BadRequest, quarterlyRes.StatusCode);
        Assert.Contains("legacy_quarterly_write_disabled", await quarterlyRes.Content.ReadAsStringAsync());
    }

    // ===================== DEF-R5-001 — الخادم هو الحاسم لا الواجهة =====================

    [Fact]
    public async Task التلاعب_بطلب_الواجهة_بقالب_غير_مُسنَد_يُرفَض_برمز_مسمًّى()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, employee) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var mine = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"R5_S5A_{Guid.NewGuid():N}");
        var (assigned, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, mine);
        await AssignAsync(admin, assigned, TemplateAssignmentScope.Employee, employee);

        // قالب منشور بالتواتر نفسه لكنّه مربوط بمسمًّى لا يخصّ هذا الموظّف ولا مُسنَد له.
        var foreign = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"R5_S5B_{Guid.NewGuid():N}");
        var (notAssigned, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, foreign);

        var track = Track(await SetupAsync(manager, employee), KpiCadence.Quarterly);
        Assert.DoesNotContain(track.Templates, t => t.Id == notAssigned);

        var res = await manager.PostAsJsonAsync("/api/kpi-evaluations", new CreateKpiEvaluationRequest(
            notAssigned, employee, PeriodType.Quarterly, track.CurrentPeriodKey));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("kpi_eval.template_not_assigned", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task التلاعب_بنوع_الفترة_لخلط_المسارين_يُرفَض_برمز_مسمًّى()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_S6_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);
        var (weekly, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);
        var weeks = await WeekKeysAsync(manager, "Quarter", Q);

        // DEVIATION-02 — نبض أسبوع على قالب ربعيّ: خلط المسارين مرفوض برمز مسمًّى لا برفض غامض.
        var pulseOnQuarterly = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(quarterly, employee, PeriodType.Weekly, weeks[0]));
        Assert.Equal(HttpStatusCode.BadRequest, pulseOnQuarterly.StatusCode);
        Assert.Contains("kpi_eval.period_type_not_supported", await pulseOnQuarterly.Content.ReadAsStringAsync());

        // والعكس: تقييم ربعيّ رسميّ على قالب نبض أسبوعيّ.
        var quarterOnWeekly = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(weekly, employee, PeriodType.Quarterly, Q));
        Assert.Equal(HttpStatusCode.BadRequest, quarterOnWeekly.StatusCode);
        Assert.Contains("kpi_eval.period_type_not_supported", await quarterOnWeekly.Content.ReadAsStringAsync());

        // الضابط الموجب («الحارس يمنع الخلط لا الرحلة») **محفوظ**، لكنّه نُقل إلى المسار الذي بقي
        // مفتوحًا: قالب نبض أسبوعيّ بنوع فترته الصحيح يُقبَل. لولا هذا الضابط لَجاز أن يمرّ تنفيذٌ
        // يرفض كلّ شيء فتبدو الاختبارات السالبة ناجحة بينما الرحلة كلّها معطّلة.
        var correct = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(weekly, employee, PeriodType.Weekly, weeks[0]));
        correct.EnsureSuccessStatusCode();

        // R6/§5.4 — والقالب الربعيّ بنوع فترته الصحيح لم يعد يُقبَل، لكن برمز **الإقفال** لا برمز
        // الخلط: التمييز بين الرمزين هو ما يفرّق بين «أخطأتَ الطلب» و«هذا الباب أُغلق».
        var retired = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(quarterly, employee, PeriodType.Quarterly, Q));
        Assert.Equal(HttpStatusCode.BadRequest, retired.StatusCode);
        Assert.Contains("legacy_quarterly_write_disabled", await retired.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task الدوريّات_التي_لا_تواتر_يقابلها_مرفوضة_برمز_مسمًّى()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_S7_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (weekly, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        var cases = new (PeriodType Type, string Key)[]
        {
            (PeriodType.Monthly, "2026-06"),
            (PeriodType.Yearly, "2026"),
            (PeriodType.AdHoc, "2026-06-01"),
            (PeriodType.Daily, "2026-06-01")
        };

        foreach (var (type, key) in cases)
        {
            var res = await manager.PostAsJsonAsync("/api/kpi-evaluations",
                new CreateKpiEvaluationRequest(weekly, employee, type, key));
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Contains("kpi_eval.period_type_not_supported", await res.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task الإعداد_الفعّال_محكوم_بنطاق_التقييم_المباشر_لا_بنطاق_العرض()
    {
        var (_, managerAId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (managerB, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, employeeOfA) = await TestAuth.CreateUserAsync(_factory, "Employee", managerAId);

        var res = await managerB.GetAsync($"/api/kpi-evaluations/effective-setup?subjectUserId={employeeOfA}");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Contains("auth.forbidden", await res.Content.ReadAsStringAsync());
    }

    // ===================== DEVIATION-01 — صحّة اختيار القالب =====================

    [Fact]
    public async Task لا_سقوط_إلى_قالب_عامّ_مع_وجود_إسناد_أخصّ_صالح()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_S9_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);

        // قالب عامّ (بلا مسمًّى) بالتواتر نفسه — مطابِق للجميع في المستوى الخامس.
        var (general, _, _) = await PublishAsync(admin, KpiCadence.Quarterly);
        // وقالب أخصّ مربوط بمسمّى هذا الموظّف — المستوى الثاني.
        var (specific, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);

        var track = Track(await SetupAsync(manager, employee), KpiCadence.Quarterly);

        // «الأخصّ يطغى» غير تراكميّ **داخل المسار الواحد**: القالب العامّ الربعيّ لا يظهر أصلًا،
        // ولا يُقبل عند الإنشاء — والمسار الأسبوعيّ لا يتأثّر بهذا الحسم إطلاقًا (OBS-R5-01/2).
        Assert.Equal(KpiCadenceSources.JobRole, track.CadenceSource);
        Assert.Contains(track.Templates, t => t.Id == specific);
        Assert.DoesNotContain(track.Templates, t => t.Id == general);

        var res = await manager.PostAsJsonAsync("/api/kpi-evaluations", new CreateKpiEvaluationRequest(
            general, employee, PeriodType.Quarterly, track.CurrentPeriodKey));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("kpi_eval.template_not_assigned", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task لا_يُعرَض_قالب_من_المسار_الآخر_ضمن_قوالب_الإعداد()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, employee) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var foreign = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"R5_S10_{Guid.NewGuid():N}");

        // إسنادان صريحان للموظّف نفسه بالمستوى نفسه، أحدهما أسبوعيّ والآخر ربعيّ.
        var (weekly, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse, foreign);
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, foreign);
        await AssignAsync(admin, weekly, TemplateAssignmentScope.Employee, employee);
        await AssignAsync(admin, quarterly, TemplateAssignmentScope.Employee, employee);

        var setup = await SetupAsync(manager, employee);
        var weeklyTrack = Track(setup, KpiCadence.WeeklyPulse);
        var quarterlyTrack = Track(setup, KpiCadence.Quarterly);

        // OBS-R5-01/1+5 — المساران مُهيّآن معًا لهذا الموظّف (لا أحدهما يُقصي الآخر)، ومع ذلك
        // تبقى قوالب كلّ مسار داخل مساره وحده: لا خلط ولا خيار تقنيّ للمستخدم.
        Assert.True(weeklyTrack.IsConfigured);
        Assert.True(quarterlyTrack.IsConfigured);

        Assert.Equal(PeriodType.Quarterly, quarterlyTrack.PeriodType);
        Assert.Contains(quarterlyTrack.Templates, t => t.Id == quarterly);
        Assert.DoesNotContain(quarterlyTrack.Templates, t => t.Id == weekly);

        Assert.Equal(PeriodType.Weekly, weeklyTrack.PeriodType);
        Assert.Contains(weeklyTrack.Templates, t => t.Id == weekly);
        Assert.DoesNotContain(weeklyTrack.Templates, t => t.Id == quarterly);
    }

    // ===================== DEVIATION-02 — لا اختلاط بين نتائج المسارين =====================

    /// <summary>
    /// R6/§4 — <b>بديل عقديّ</b> لاختبار «نتيجة نبض أسبوعيّ لا تدخل حساب المسار الربعيّ ولا تفصيله».
    /// العقد المقيس سابقًا كان يفترض أنّ للربع تقييمًا ربعيًّا مستقلًّا يملأ مقامه بمفرده، وأنّ النبض
    /// الأسبوعيّ دخيل عليه. R6 يعكس هذا الافتراض نصًّا: مصدر الحقيقة الوحيد هو
    /// <c>PeriodType=Weekly ∧ Cadence=WeeklyPulse ∧ Status=Approved ∧ !IsDeleted</c>، و«الربع» حبيبة
    /// <b>قراءة</b> مشتقّة لا نوع تقييم يُكتب. فما كان يُقاس هنا (عزل المسارين) لم يعد له وجود؛
    /// والمقيس بديلًا — وهو مطلب المالك «Quarterly output is derived from Weekly Approved» — أنّ
    /// القراءة الربعيّة تُبنى من النبض المعتمَد نفسه: مقامها دورات الربع، وبسطها ما اعتُمد منها.
    /// وجود قالب ربعيّ منشور بجوارها لا يحوّل المسار ولا يُنشئ مقامًا موازيًا.
    /// </summary>
    [Fact]
    public async Task القراءة_الربعيّة_مشتقّة_من_النبض_الأسبوعيّ_المعتمَد_لا_من_تقييم_ربعيّ()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_S11_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);

        // قالب ربعيّ منشور بجوار قالب النبض: النشر يبقى مقبولًا (WS-1 يقفل إنشاء التقييم لا القالب)،
        // والمقصود إثبات أنّ مجرّد وجوده لا يخلق مسارًا موازيًا ولا يغيّر مصدر القراءة الربعيّة.
        await PublishAsync(admin, KpiCadence.Quarterly, role);
        var (weekly, manualId, autoId) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);
        var weeks = await WeekKeysAsync(manager, "Quarter", Q);

        var ev = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(weekly, employee, PeriodType.Weekly, weeks[0])))
            .ReadAsync<KpiEvaluationDto>();
        Assert.NotNull(ev);
        await manager.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 95m, null),
                new KpiResultInput(autoId, 95m, null, null)
            }));
        await manager.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null);
        var ceo = await TestAuth.LoginAsRoleAsync(_factory, "CEO");
        (await ceo.PostAsync($"/api/kpi-evaluations/{ev.Id}/approve", null)).EnsureSuccessStatusCode();

        var row = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);

        // المسار واحد لا اثنان: النبض الأسبوعيّ، ووجود قالب ربعيّ لا يحوّله.
        Assert.Equal(KpiCadence.WeeklyPulse, row.EffectiveCadence);

        // والمقام دورات الربع كلّها لا دورة ربعيّة واحدة — «الربع» نافذة قراءة فوق أسابيع.
        Assert.Equal(weeks.Length, row.Measure.ExpectedEvaluationCount);
        Assert.Equal(weeks.Length, row.Measure.AdjustedExpectedCount);

        // والبسط هو النبض المعتمَد وحده: أسبوع معتمَد واحد ⇒ قيمة مشتقّة منه، والباقي نقص معلن.
        Assert.Equal(1, row.Measure.EligibleEvaluationCount);
        Assert.Equal(weeks.Length - 1, row.Measure.MissingCount);
        Assert.Equal(95m, row.Measure.Value);

        var drillRes = await manager.GetAsync(
            $"/api/kpi/drilldown?periodType=Quarter&periodKey={Q}&subjectUserId={employee}");
        drillRes.EnsureSuccessStatusCode();
        var drill = (await drillRes.ReadAsync<KpiDrilldownDto>())!;

        // والتفصيل يعلن نَسَب الرقم: مفاتيح أسابيع الربع، لا المفتاح الربعيّ الإرثيّ.
        Assert.NotNull(drill.SourcePeriods);
        var periodKeys = drill.SourcePeriods!.Select(p => p.PeriodKey).ToArray();
        Assert.Equal(weeks, periodKeys);
        Assert.DoesNotContain(Q, periodKeys);

        // والأسبوع المعتمَد وحده مكتمل؛ البقيّة مُعلَنة ناقصة لا مطويّة.
        Assert.Equal(new[] { weeks[0] },
            drill.SourcePeriods!.Where(p => p.IsCompleted).Select(p => p.PeriodKey).ToArray());
    }
}
