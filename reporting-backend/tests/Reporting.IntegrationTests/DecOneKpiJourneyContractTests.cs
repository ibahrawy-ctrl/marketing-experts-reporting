using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Kpi;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// R5 — عقد المنتج المعتمد <b>DEC-01</b> على واجهة HTTP فعليّة وقاعدة معزولة.
/// كلّ اختبار هنا يقيس <b>نتيجة أعمال</b> يراها المستخدم، لا استدعاء دالّة:
/// <list type="bullet">
/// <item>البند 1: فتح الشاشة على الربع الجاري بلا سؤال.</item>
/// <item>البند 5: حسم التواتر بأولويّة معلَنة، و«التواتر غير مُهيّأ» حالة مسمّاة لا صمت.</item>
/// <item>البند 6: الفترات التاريخيّة لا يُعاد تفسيرها بإعداد سرى بعدها.</item>
/// <item>البنود 7+8: المتوقَّع من التواتر، والمعدَّل بعد الإجازة/الإعفاء/نافذة الخدمة.</item>
/// <item>البنود 10+12+14: المفقود ليس صفرًا، والتغطية معادلة معلنة، ودون 80% النتيجة مؤقّتة.</item>
/// <item>البنود 16+17: متوسّط المؤهّلين فقط، وغير المؤهّلين بأسمائهم لا بالإخفاء.</item>
/// <item>البند 18: Drill-down إلى الفترات المصدريّة بأسمائها ومآلاتها.</item>
/// </list>
/// </summary>
[Collection("DecOneIsolated")]
public class DecOneKpiJourneyContractTests
{
    private readonly DecOneIsolatedFactory _factory;

    public DecOneKpiJourneyContractTests(DecOneIsolatedFactory factory) => _factory = factory;

    private const string Q = "2026-Q2";

    // ===================== أدوات مساعدة =====================

    private async Task<(Guid TemplateId, Guid ManualId, Guid AutoId)> PublishAsync(
        HttpClient admin, KpiCadence cadence, Guid? jobRoleId = null)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"قالب DEC-01 {Guid.NewGuid():N}", null, jobRoleId, cadence)))
            .ReadAsync<KpiTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var manual = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الالتزام", null, 50m, null, null, KpiCalcMethod.Manual, null)))
            .ReadAsync<KpiMetricDto>();
        var auto = await (await admin.PostAsJsonAsync($"/api/kpi-templates/versions/{versionId}/metrics",
            new UpsertKpiMetricRequest("الإنجاز", null, 50m, 100m, "%", KpiCalcMethod.Auto, null)))
            .ReadAsync<KpiMetricDto>();

        var publish = await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        publish.EnsureSuccessStatusCode();
        return (created.Id, manual!.Id, auto!.Id);
    }

    private static async Task<KpiTemplateAssignmentRowDto> AssignAsync(
        HttpClient admin, Guid templateId, TemplateAssignmentScope scope, Guid scopeId,
        TemplateAssignmentKind kind = TemplateAssignmentKind.Include,
        DateOnly? from = null, DateOnly? to = null)
    {
        var res = await admin.PostAsJsonAsync($"/api/kpi-templates/{templateId}/assignments",
            new CreateKpiAssignmentRequest(scope, scopeId, kind, null, from, to));
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiTemplateAssignmentRowDto>())!;
    }

    private async Task ScoreAsync(HttpClient evaluator, Guid templateId, Guid subjectId,
        Guid manualId, Guid autoId, string weekKey, decimal score)
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

        var ceo = await TestAuth.LoginAsRoleAsync(_factory, "CEO");
        var approved = await (await ceo.PostAsync($"/api/kpi-evaluations/{ev.Id}/approve", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.Approved, approved!.Status);
    }

    /// <summary>مفاتيح الأسابيع داخل فترة، من الخادم لا من اشتقاق الاختبار (B-1).</summary>
    private static async Task<string[]> WeekKeysAsync(HttpClient client, string type, string periodKey)
    {
        using var doc = await ResolveAsync(client, type, periodKey);
        return doc.RootElement.GetProperty("weekKeys").EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    private static async Task<(DateOnly Start, DateOnly End)> RangeAsync(
        HttpClient client, string type, string periodKey)
    {
        using var doc = await ResolveAsync(client, type, periodKey);
        var cur = doc.RootElement.GetProperty("current");
        return (Date(cur, "start"), Date(cur, "end"));
    }

    private static async Task<System.Text.Json.JsonDocument> ResolveAsync(
        HttpClient client, string type, string? periodKey)
    {
        var url = $"/api/kpi/periods/resolve?type={type}"
                  + (periodKey is null ? "" : $"&periodKey={periodKey}");
        var res = await client.GetAsync(url);
        res.EnsureSuccessStatusCode();
        return System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
    }

    private static DateOnly Date(System.Text.Json.JsonElement e, string prop) => DateOnly.ParseExact(
        e.GetProperty(prop).GetString()!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private async Task ApproveLeaveAsync(Guid userId, DateOnly from, DateOnly to)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<LeaveRequest>().Add(new LeaveRequest
        {
            RequesterUserId = userId,
            Type = LeaveRequestType.Leave,
            StartDate = from,
            EndDate = to,
            Reason = "إجازة معتمَدة — اختبار DEC-01/8",
            Status = LeaveRequestStatus.HrApproved,
            CurrentStep = LeaveRequestStep.Completed
        });
        await db.SaveChangesAsync();
    }

    private static async Task<KpiPerformanceDto> PerfAsync(HttpClient c, string query)
    {
        var res = await c.GetAsync($"/api/kpi/performance?{query}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiPerformanceDto>())!;
    }

    private static async Task<KpiEmployeeScoreDto> RowAsync(HttpClient c, string query, Guid userId)
        => (await PerfAsync(c, query)).Employees.Single(e => e.UserId == userId);

    private static async Task<KpiEvaluationSetupDto> SetupAsync(HttpClient c, Guid subjectId)
    {
        var res = await c.GetAsync($"/api/kpi-evaluations/effective-setup?subjectUserId={subjectId}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiEvaluationSetupDto>())!;
    }

    /// <summary>
    /// DEC-01/5 — «لا إعداد فعّال» يجب أن يكون حالةً قابلة للبلوغ فعليًّا كي يُختبَر.
    /// قوالب البذر المنشورة بلا مسمًّى وظيفيّ هي «إسناد عامّ» (المستوى الخامس في سلّم الأولويّة)
    /// فتطابق كلّ موظّف — أي أنّها <b>إعداد فعّال</b> لا غيابه. تعطيلها هنا يخلق سيناريو
    /// «لا إسناد على أيّ مستوى» بلا مسّ منطق الخادم ولا إضعاف توكيد.
    /// </summary>
    private async Task DeactivateGeneralTemplatesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.KpiTemplates.Where(t => t.JobRoleId == null && t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false));
    }

    // ===================== البند 1 — الربع الجاري افتراضيًّا =====================

    [Fact]
    public async Task البند01_فتح_الشاشة_بلا_مُرشِّح_يعطي_الربع_الجاري_مفتوحًا_بتوقيت_الرياض()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");

        var dto = await PerfAsync(manager, "");
        var p = dto.PeriodResolved;

        Assert.Matches(@"^\d{4}-Q[1-4]$", p.Key);
        Assert.Equal("Asia/Riyadh", p.Timezone);
        Assert.True(p.IsOpen, "الربع الجاري فترة مفتوحة بطبيعتها — وهذا ما يمنع إعلان نتيجة نهائيّة عليها.");
        Assert.Equal(3, (p.End.Month - p.Start.Month) + 1);
        Assert.Equal(1, p.Start.Day);
    }

    [Fact]
    public async Task البند01_التنقّل_إلى_ربع_تاريخيّ_متاح_ولا_يغيّر_الافتراضيّ()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");

        var historical = await PerfAsync(manager, $"periodType=Quarter&periodKey={Q}");
        Assert.Equal(Q, historical.PeriodResolved.Key);
        Assert.False(historical.PeriodResolved.IsOpen);

        // الطلب التالي بلا مُرشِّح يعود إلى الربع الجاري: التنقّل لا يثبّت حالة على الخادم.
        var current = await PerfAsync(manager, "");
        Assert.NotEqual(Q, current.PeriodResolved.Key);
        Assert.True(current.PeriodResolved.IsOpen);
    }

    // ===================== البند 5 — أولويّة حسم التواتر =====================

    [Fact]
    public async Task البند05_إسناد_الموظّف_يتفوّق_على_إسناد_الفريق()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, employee) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, employee);

        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"R5_A1_{Guid.NewGuid():N}");
        var (weeklyTemplate, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);
        var (quarterlyTemplate, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);

        await AssignAsync(admin, weeklyTemplate, TemplateAssignmentScope.Team, teamId);
        await AssignAsync(admin, quarterlyTemplate, TemplateAssignmentScope.Employee, employee);

        var row = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);

        Assert.Equal(KpiCadence.Quarterly, row.EffectiveCadence);
        Assert.Equal(KpiCadenceSources.EmployeeAssignment, row.CadenceSource);
    }

    [Fact]
    public async Task البند05_إسناد_الفريق_يتفوّق_على_مطابقة_المسمّى_الوظيفيّ()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_A2_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, employee);

        // OBS-R5-01/2 — السلّم يُطبَّق **داخل المسار الواحد** لا عبر المسارين: فمقارنة «فريق أسبوعيّ»
        // بـ«مسمّى ربعيّ» لم تعد مقارنة أولويّة أصلًا بل مسارين متزامنين. لذلك يُقاس التفوّق هنا
        // بقالبين أسبوعيَّين: أحدهما يطابق مسمّى الموظّف، والآخر مُسنَد لفريقه — والفريق أعلى.
        await PublishAsync(admin, KpiCadence.WeeklyPulse, role);
        var otherRole = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"{code}_OTHER");
        var (weeklyTemplate, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse, otherRole);
        await AssignAsync(admin, weeklyTemplate, TemplateAssignmentScope.Team, teamId);

        var row = await RowAsync(manager, $"cadence=WeeklyPulse&periodType=Quarter&periodKey={Q}", employee);

        Assert.Equal(KpiCadence.WeeklyPulse, row.EffectiveCadence);
        Assert.Equal(KpiCadenceSources.TeamAssignment, row.CadenceSource);

        // والقالب الفائز فعلًا هو قالب الفريق لا قالب المسمّى — لا مجرّد تطابق اسم المصدر.
        var setup = await SetupAsync(manager, employee);
        var weeklyTrack = setup.Tracks.Single(t => t.Cadence == KpiCadence.WeeklyPulse);
        Assert.Equal(weeklyTemplate, Assert.Single(weeklyTrack.Templates).Id);
    }

    [Fact]
    public async Task البند05_بلا_قالب_فعّال_تظهر_حالة_مسمّاة_ولا_يُخترَع_مقام()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, orphan) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        // قالب منشور لكنّه مربوط بمسمّى لا يحمله هذا الموظّف ⇒ لا مطابقة ولا سقوط إلى «عامّ».
        await DeactivateGeneralTemplatesAsync();
        var foreign = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"R5_A3_{Guid.NewGuid():N}");
        await PublishAsync(admin, KpiCadence.WeeklyPulse, foreign);

        var row = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", orphan);

        Assert.Null(row.EffectiveCadence);
        Assert.Equal(KpiCadenceSources.NotConfigured, row.CadenceSource);
        Assert.Equal(KpiJourneyState.CadenceNotConfigured, row.Measure.JourneyState);
        Assert.Equal(0, row.Measure.ExpectedEvaluationCount);
        Assert.Equal(0, row.Measure.AdjustedExpectedCount);
        Assert.Equal(0, row.Measure.MissingCount);
        Assert.Null(row.Measure.CoveragePercent);
        Assert.Null(row.Measure.Value);
        Assert.False(row.EligibleForRanking);
    }

    [Fact]
    public async Task البند05_غير_المُهيّأ_يظهر_في_الترتيب_كقائمة_مستقلّة_لا_كناقص_تغطية()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, orphan) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        await DeactivateGeneralTemplatesAsync();
        var foreign = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"R5_A4_{Guid.NewGuid():N}");
        await PublishAsync(admin, KpiCadence.WeeklyPulse, foreign);

        var res = await manager.GetAsync($"/api/kpi/rankings?periodType=Quarter&periodKey={Q}");
        res.EnsureSuccessStatusCode();
        var dto = (await res.ReadAsync<KpiRankingsDto>())!;

        Assert.NotNull(dto.CadenceNotConfiguredEmployees);
        Assert.Contains(dto.CadenceNotConfiguredEmployees!, e => e.UserId == orphan);
        Assert.DoesNotContain(dto.ExcludedEmployees ?? Array.Empty<KpiEmployeeScoreDto>(), e => e.UserId == orphan);
    }

    // ===================== البند 6 — سريان الإعداد لا يعيد تفسير الماضي =====================

    [Fact]
    public async Task البند06_إسناد_سرى_بعد_الربع_لا_يعيد_تفسير_الربع_التاريخيّ()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_B1_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);

        // الإعداد التاريخيّ: أسبوعيّ عبر المسمّى الوظيفيّ.
        await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        // إعداد جديد (ربعيّ) يسري من بداية الربع الرابع فقط.
        var otherRole = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"{code}_NEW");
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, otherRole);
        var q4 = await RangeAsync(manager, "Quarter", "2026-Q4");
        await AssignAsync(admin, quarterly, TemplateAssignmentScope.Employee, employee, from: q4.Start);

        var inQ2 = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);
        var inQ4 = await RowAsync(manager, "periodType=Quarter&periodKey=2026-Q4", employee);

        // الربع الثاني يُقرأ بإعداده الساري حينها، لا بالإعداد الذي وُلد بعده.
        Assert.Equal(KpiCadence.WeeklyPulse, inQ2.EffectiveCadence);
        Assert.Equal(KpiCadenceSources.JobRole, inQ2.CadenceSource);

        Assert.Equal(KpiCadence.Quarterly, inQ4.EffectiveCadence);
        Assert.Equal(KpiCadenceSources.EmployeeAssignment, inQ4.CadenceSource);
    }

    [Fact]
    public async Task البند06_مدى_سريان_مقلوب_يُرفَض_صراحةً_ولا_يُخزَّن_صامتًا()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, employee) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, $"R5_B2_{Guid.NewGuid():N}");
        var (templateId, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        var res = await admin.PostAsJsonAsync($"/api/kpi-templates/{templateId}/assignments",
            new CreateKpiAssignmentRequest(
                TemplateAssignmentScope.Employee, employee, TemplateAssignmentKind.Include, null,
                new DateOnly(2026, 6, 30), new DateOnly(2026, 4, 1)));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===================== البنود 7+8 — المتوقَّع والمتوقَّع المعدَّل =====================

    [Fact]
    public async Task البند07_المتوقَّع_يساوي_عدد_دورات_التواتر_داخل_الربع_لا_رقمًا_عامًّا()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_C1_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        var weeks = await WeekKeysAsync(manager, "Quarter", Q);
        var row = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);

        Assert.Equal(weeks.Length, row.Measure.ExpectedEvaluationCount);
        Assert.Equal(weeks.Length, row.Measure.AdjustedExpectedCount);
        Assert.Equal(weeks.Length, row.Measure.MissingCount);   // لم يبدأ ⇒ كلّها ناقصة
        Assert.Equal(KpiJourneyState.NotStarted, row.Measure.JourneyState);
        Assert.Null(row.Measure.Value);                          // البند 10: مفقود ≠ صفر
    }

    [Fact]
    public async Task البند08_الإجازة_المعتمدة_تخفض_المقام_ولا_تُحسَب_نقصًا()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_C2_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        var weeks = await WeekKeysAsync(manager, "Quarter", Q);
        var first = await RangeAsync(manager, "Week", weeks[0]);
        var second = await RangeAsync(manager, "Week", weeks[1]);
        await ApproveLeaveAsync(employee, first.Start, second.End);

        var row = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);

        Assert.Equal(weeks.Length, row.Measure.ExpectedEvaluationCount);
        Assert.Equal(weeks.Length - 2, row.Measure.AdjustedExpectedCount);
        // الفرق بين الخامّ والمعدَّل هو نفسه دليلُ أنّ الإعفاء خفّض المقام ولم يعاقب.
        Assert.Equal(weeks.Length - 2, row.Measure.MissingCount);

        var drill = await DrilldownAsync(manager, $"periodType=Quarter&periodKey={Q}&subjectUserId={employee}");
        Assert.Equal(2, drill.SourcePeriods!.Count(p => p.ExemptReason == "approvedLeave"));
        Assert.All(drill.SourcePeriods.Where(p => p.IsExempt), p => Assert.Null(p.Score));
    }

    [Fact]
    public async Task البند08_الإعفاء_الإداريّ_المسجَّل_بتاريخَي_سريان_يخفض_المقام()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_C3_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (templateId, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        var weeks = await WeekKeysAsync(manager, "Quarter", Q);
        var first = await RangeAsync(manager, "Week", weeks[0]);
        await AssignAsync(admin, templateId, TemplateAssignmentScope.Employee, employee,
            TemplateAssignmentKind.Exclude, first.Start, first.End);

        var row = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);

        // الاستثناء مؤقَّت ومنتهٍ قبل نهاية الربع ⇒ التواتر ما زال محسومًا، والمقام وحده انخفض.
        Assert.Equal(KpiCadence.WeeklyPulse, row.EffectiveCadence);
        Assert.Equal(weeks.Length, row.Measure.ExpectedEvaluationCount);
        Assert.Equal(weeks.Length - 1, row.Measure.AdjustedExpectedCount);

        var drill = await DrilldownAsync(manager, $"periodType=Quarter&periodKey={Q}&subjectUserId={employee}");
        Assert.Equal(1, drill.SourcePeriods!.Count(p => p.ExemptReason == "administrativeExemption"));
    }

    [Fact]
    public async Task البند08_ما_قبل_الالتحاق_وما_بعد_انتهاء_الخدمة_خارج_المقام()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_C4_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, joiner) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (_, leaver) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        var weeks = await WeekKeysAsync(manager, "Quarter", Q);
        var third = await RangeAsync(manager, "Week", weeks[2]);
        await TestAuth.SetEmploymentWindowAsync(_factory, joiner, third.Start);
        await TestAuth.SetEmploymentWindowAsync(_factory, leaver, null, third.End);

        var joinerRow = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", joiner);
        var leaverRow = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", leaver);

        Assert.Equal(weeks.Length - 2, joinerRow.Measure.AdjustedExpectedCount);
        Assert.Equal(3, leaverRow.Measure.AdjustedExpectedCount);

        var drill = await DrilldownAsync(manager, $"periodType=Quarter&periodKey={Q}&subjectUserId={joiner}");
        Assert.Equal(2, drill.SourcePeriods!.Count(p => p.ExemptReason == "beforeHireDate"));
    }

    // ===================== البنود 12+14+16+17 — التغطية والمتوسّط الرسميّ =====================

    [Fact]
    public async Task البنود14و16و17_المتوسّط_الرسميّ_يقتصر_على_المؤهّلين_وغيرهم_يظهر_باسمه()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_D1_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, full) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (_, partial) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (templateId, manualId, autoId) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        var weeks = await WeekKeysAsync(manager, "Quarter", Q);
        foreach (var w in weeks)
            await ScoreAsync(manager, templateId, full, manualId, autoId, w, 60m);
        await ScoreAsync(manager, templateId, partial, manualId, autoId, weeks[0], 100m);

        var dto = await PerfAsync(manager, $"periodType=Quarter&periodKey={Q}");
        var fullRow = dto.Employees.Single(e => e.UserId == full);
        var partialRow = dto.Employees.Single(e => e.UserId == partial);

        // البند 11: الدرجة تُعرَض مستقلّة عن التغطية — 100 لا تختفي لأنّ التغطية ضعيفة.
        Assert.Equal(100m, partialRow.Measure.Value);
        // البند 12: التغطية = المكتمل ÷ المعدَّل × 100.
        Assert.Equal(
            Math.Round(100m / weeks.Length, 2, MidpointRounding.AwayFromZero),
            partialRow.Measure.CoveragePercent);
        // البند 14: دون 80% ⇒ مؤقّتة + تغطية غير كافية + خارج المتوسّط الرسميّ.
        Assert.True(partialRow.Measure.IsProvisional);
        Assert.Equal(KpiJourneyState.InsufficientCoverage, partialRow.Measure.JourneyState);
        Assert.False(partialRow.EligibleForRanking);
        Assert.Equal(weeks.Length - 1, partialRow.Measure.MissingCount);

        Assert.Equal(100m, fullRow.Measure.CoveragePercent);
        Assert.False(fullRow.Measure.IsProvisional);
        Assert.True(fullRow.EligibleForRanking);
        Assert.Equal(KpiJourneyState.CompleteEligible, fullRow.Measure.JourneyState);

        // البند 16: المتوسّط الرسميّ = 60 وحدها. لو دخل ناقصُ التغطية لصار 80.
        Assert.Equal(60.00m, dto.Company.Measure.Value);
        Assert.NotEqual(80.00m, dto.Company.Measure.Value);
        Assert.Equal(1, dto.Company.QualifiedMemberCount);

        // البند 17: لا يختفي — اسمه وحالته معروضان منفصلَين عن المتوسّط.
        Assert.NotNull(dto.Company.ExcludedForInsufficientCoverage);
        var excluded = Assert.Single(dto.Company.ExcludedForInsufficientCoverage!);
        Assert.Equal(partial, excluded.UserId);
        Assert.False(string.IsNullOrWhiteSpace(excluded.FullName));
        Assert.Equal(KpiJourneyState.InsufficientCoverage, excluded.Measure.JourneyState);
    }

    [Fact]
    public async Task البند13_عتبة_الاعتماد_المعلَنة_ثمانون_بالمئة_والمستبعَدون_بأسمائهم_في_الترتيب()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_D2_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, partial) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (templateId, manualId, autoId) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        var weeks = await WeekKeysAsync(manager, "Quarter", Q);
        await ScoreAsync(manager, templateId, partial, manualId, autoId, weeks[0], 95m);

        var res = await manager.GetAsync($"/api/kpi/rankings?periodType=Quarter&periodKey={Q}");
        res.EnsureSuccessStatusCode();
        var dto = (await res.ReadAsync<KpiRankingsDto>())!;

        Assert.Equal(0.80m, dto.MinimumCoverage);
        Assert.DoesNotContain(dto.TopPerformers, e => e.UserId == partial);
        Assert.NotNull(dto.ExcludedEmployees);
        Assert.Contains(dto.ExcludedEmployees!, e => e.UserId == partial);
        Assert.True(dto.ExcludedForInsufficientCoverage >= 1);
    }

    // ===================== البند 18 — Drill-down إلى الفترات المصدريّة =====================

    [Fact]
    public async Task البند18_التفصيل_يسمّي_كلّ_فترة_مصدريّة_ومآلها_ولا_يحوّل_المفقود_صفرًا()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_E1_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (templateId, manualId, autoId) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        var weeks = await WeekKeysAsync(manager, "Quarter", Q);
        await ScoreAsync(manager, templateId, employee, manualId, autoId, weeks[0], 40m);
        await ScoreAsync(manager, templateId, employee, manualId, autoId, weeks[1], 80m);

        var drill = await DrilldownAsync(manager, $"periodType=Quarter&periodKey={Q}&subjectUserId={employee}");

        Assert.NotNull(drill.Measure);
        Assert.NotNull(drill.SourcePeriods);
        var periods = drill.SourcePeriods!;

        // كلّ التزام متوقَّع له سطر مسمًّى بمفتاحه وحدوده — لا رقم مجرَّد.
        Assert.Equal(weeks.Length, periods.Count);
        Assert.Equal(weeks, periods.Select(p => p.PeriodKey).ToArray());
        Assert.All(periods, p => Assert.False(string.IsNullOrWhiteSpace(p.Label)));

        Assert.Equal(2, periods.Count(p => p.IsCompleted));
        Assert.Equal(weeks.Length - 2, periods.Count(p => !p.IsCompleted));
        // المفقود يبقى بلا درجة: لا صفر مُلفَّق يسحب المتوسّط.
        Assert.All(periods.Where(p => !p.IsCompleted), p => Assert.Null(p.Score));
        Assert.Equal(60.00m, drill.RecomputedValue);   // (40+80)/2 — بُنيت من المكتمل وحده
        Assert.Equal(weeks.Length - 2, drill.Measure!.MissingCount);
        Assert.Equal(KpiCadence.WeeklyPulse, drill.EffectiveCadence);
        Assert.Equal(KpiCadenceSources.JobRole, drill.CadenceSource);
    }

    // ===================== النطاق خادميّ: المُرشِّح يضيّق ولا يوسّع =====================

    [Fact]
    public async Task النطاق_المُرشِّح_يضيّق_فقط_ولا_يوسّع_حتّى_بلا_كادنس_صريح()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, managerAId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_F2_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (selfClient, selfId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerAId);
        var (_, managerBId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, strangerId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerBId);
        await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        // بلا أيّ مُرشِّح: الموظّف يرى نفسه فقط.
        var mine = await PerfAsync(selfClient, $"periodType=Quarter&periodKey={Q}");
        var only = Assert.Single(mine.Employees);
        Assert.Equal(selfId, only.UserId);

        // محاولة التوسيع بمُرشِّح صريح لا تُوسِّع: 404 لا 403 (لا تسريب وجود).
        foreach (var path in new[] { "performance", "rankings", "drilldown" })
        {
            var res = await selfClient.GetAsync(
                $"/api/kpi/{path}?periodType=Quarter&periodKey={Q}&subjectUserId={strangerId}");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }
    }

    // ===================== البند 3 — المسار الربعيّ الرسميّ مسار حقيقيّ لا اسم =====================

    [Fact]
    public async Task البند03_التقييم_الربعيّ_الرسميّ_يُنشَر_ويُنشأ_ويُعتمَد_ويُغلق_الربع()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_G1_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);

        var (templateId, manualId, autoId) = await PublishAsync(admin, KpiCadence.Quarterly, role);

        // تقييم ربعيّ واحد بمفتاح الربع نفسه — لا مفتاح أسبوع ولا نوع فترة أسبوعيّ.
        var ev = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, employee, PeriodType.Quarterly, Q)))
            .ReadAsync<KpiEvaluationDto>();
        Assert.NotNull(ev);
        await manager.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 90m, null),
                new KpiResultInput(autoId, 90m, null, null)
            }));
        await manager.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null);
        var ceo = await TestAuth.LoginAsRoleAsync(_factory, "CEO");
        var approved = await (await ceo.PostAsync($"/api/kpi-evaluations/{ev.Id}/approve", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.Approved, approved!.Status);

        var row = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);

        Assert.Equal(KpiCadence.Quarterly, row.EffectiveCadence);
        Assert.Equal(KpiCadenceSources.JobRole, row.CadenceSource);
        // المقام دورة ربعيّة واحدة داخل الربع — لا عدد أسابيع.
        Assert.Equal(1, row.Measure.ExpectedEvaluationCount);
        Assert.Equal(1, row.Measure.AdjustedExpectedCount);
        Assert.Equal(1, row.Measure.EligibleEvaluationCount);
        Assert.Equal(0, row.Measure.MissingCount);
        Assert.Equal(100m, row.Measure.CoveragePercent);
        Assert.Equal(90.00m, row.Measure.Value);
        Assert.False(row.Measure.IsProvisional);
        Assert.Equal(KpiJourneyState.CompleteEligible, row.Measure.JourneyState);
        Assert.True(row.EligibleForRanking);
    }

    [Fact]
    public async Task البند03_نوع_فترة_التقييم_يجب_أن_يطابق_تواتر_قالبه_وإلا_رُفِض_صراحةً()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_G2_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);
        var (weekly, _, _) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);
        var weeks = await WeekKeysAsync(manager, "Quarter", Q);

        // نبض أسبوع على قالب ربعيّ: خلط بين المسارين ⇒ رفض معلَّل لا قبول صامت.
        var wrongOnQuarterly = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(quarterly, employee, PeriodType.Weekly, weeks[0]));
        Assert.Equal(HttpStatusCode.BadRequest, wrongOnQuarterly.StatusCode);

        // والعكس: تقييم ربعيّ على قالب أسبوعيّ مرفوض كذلك.
        var wrongOnWeekly = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(weekly, employee, PeriodType.Quarterly, Q));
        Assert.Equal(HttpStatusCode.BadRequest, wrongOnWeekly.StatusCode);

        // والدوريّات التي لا تواتر يقابلها تبقى مرفوضة كما كانت قبل R5.
        var monthly = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(weekly, employee, PeriodType.Monthly, "2026-06"));
        Assert.Equal(HttpStatusCode.BadRequest, monthly.StatusCode);
    }

    private static async Task<KpiDrilldownDto> DrilldownAsync(HttpClient c, string query)
    {
        var res = await c.GetAsync($"/api/kpi/drilldown?{query}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiDrilldownDto>())!;
    }
}
