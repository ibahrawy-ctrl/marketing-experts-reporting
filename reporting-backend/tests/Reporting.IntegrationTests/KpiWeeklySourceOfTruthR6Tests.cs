using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Kpi;
using Reporting.Application.Reports;
using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// <b>R6/§7 — الحزمة الإلزاميّة لعقد «النبض الأسبوعيّ المعتمَد مصدرَ الحقيقة الوحيد».</b>
/// <para>
/// العقد الحاكم (§4): الحقيقة الوحيدة هي
/// <c>PeriodType=Weekly ∧ Template.Cadence=WeeklyPulse ∧ Status=Approved ∧ IsDeleted=false</c>،
/// وحبيبات القراءة <c>Weekly|Monthly|Quarterly|Yearly</c> <b>نوافذ تجميع مشتقّة</b> لا أنواع تقييم.
/// </para>
/// <para>
/// كلّ اختبار هنا يقابل بندًا مسمًّى من §7، والاسم يحمل رقم البند كي يبقى الأثر مقروءًا في التقرير
/// وفي مخرجات التشغيل معًا. القياس على واجهة HTTP فعليّة وقاعدة معزولة — لا استدعاء خدمة مباشر.
/// </para>
/// </summary>
[Collection("DecOneIsolated")]
public class KpiWeeklySourceOfTruthR6Tests
{
    private readonly DecOneIsolatedFactory _factory;

    public KpiWeeklySourceOfTruthR6Tests(DecOneIsolatedFactory factory) => _factory = factory;

    // ===================== أدوات مساعدة =====================

    private static async Task<(Guid TemplateId, Guid ManualId, Guid AutoId)> PublishAsync(
        HttpClient admin, KpiCadence cadence, Guid? jobRoleId = null)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
                new CreateKpiTemplateRequest($"قالب R6 {Guid.NewGuid():N}", null, jobRoleId, cadence)))
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

    private static async Task<string[]> WeekKeysAsync(HttpClient client, string type, string periodKey)
    {
        var res = await client.GetAsync($"/api/kpi/periods/resolve?type={type}&periodKey={periodKey}");
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("weekKeys").EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    /// <summary>
    /// مفاتيح النوافذ الأكبر تُبنى من مكوّنات التاريخ الرقميّة صراحةً. ثقافة تشغيل الاختبارات عربيّة،
    /// فتنسيق <c>yyyy-MM</c> عليها يُخرِج تقويم أمّ القرى الهجريّ (<c>1448-03</c>) — مفتاحًا صحيح
    /// الشكل يشير إلى شهر لا وجود له في البيانات، فيبدو الأمر «فقدان بيانات» وهو خطأ تنسيق.
    /// </summary>
    private static string MonthKey(DateOnly d) => FormattableString.Invariant($"{d.Year:0000}-{d.Month:00}");

    /// <inheritdoc cref="MonthKey"/>
    private static string QuarterKey(DateOnly d) =>
        FormattableString.Invariant($"{d.Year:0000}-Q{(d.Month - 1) / 3 + 1}");

    private static async Task<KpiPerformanceDto> PerfAsync(HttpClient c, string query)
    {
        var res = await c.GetAsync($"/api/kpi/performance?{query}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiPerformanceDto>())!;
    }

    private static async Task<KpiEmployeeScoreDto> RowAsync(HttpClient c, string query, Guid userId)
        => (await PerfAsync(c, query)).Employees.Single(e => e.UserId == userId);

    private static async Task<KpiRankingsDto> RankingsAsync(HttpClient c, string query)
    {
        var res = await c.GetAsync($"/api/kpi/rankings?{query}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiRankingsDto>())!;
    }

    private static async Task<KpiDrilldownDto> DrilldownAsync(HttpClient c, string query)
    {
        var res = await c.GetAsync($"/api/kpi/drilldown?{query}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiDrilldownDto>())!;
    }

    private static async Task<KpiAggregateDto> AggregateAsync(HttpClient c, string query)
    {
        var res = await c.GetAsync($"/api/kpi-evaluations/aggregate?{query}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiAggregateDto>())!;
    }

    /// <summary>ينشئ تقييمًا ويُدخل درجته ويرسله ويعتمده — «نتيجة معتمَدة» فعليّة عبر الرحلة لا صفّ مزروع.</summary>
    private async Task<Guid> ApprovedWeeklyAsync(
        HttpClient evaluator, Guid templateId, Guid subjectId,
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
        return ev.Id;
    }

    /// <summary>
    /// يبذر صفّ تقييم بحالة/مسار/حذف <b>محدَّدين مباشرةً في القاعدة</b>.
    /// <para>
    /// البذر المباشر ضرورة لا اختصار: (أ) مسار الكتابة الربعيّة مُقفَل برمز
    /// <c>legacy_quarterly_write_disabled</c> فلا سبيل عبر الـAPI لتمثيل السجلّات <b>القائمة</b>
    /// التي تبقى بلا حذف ولا هجرة (WS-2)؛ (ب) حالات مثل <c>Rejected</c> و<c>NeedsRevision</c>
    /// و<c>IsDeleted</c> تحتاج تركيبًا دقيقًا لقياس <b>الاستبعاد</b> وحده. الغرض هنا تمثيل الحالة
    /// المرصودة في القاعدة، لا اختبار مسار الكتابة — وهذا الأخير مقيس في بنود «العقد» أعلاه بالرحلة.
    /// </para>
    /// </summary>
    private async Task<Guid> SeedRowAsync(
        Guid templateId, Guid subjectId, PeriodType periodType, string periodKey,
        KpiEvaluationStatus status, decimal score, bool isDeleted = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var versionId = await db.KpiTemplateVersions.Where(v => v.KpiTemplateId == templateId)
            .OrderByDescending(v => v.VersionNumber).Select(v => v.Id).FirstAsync();
        var subject = await db.Users.AsNoTracking()
            .Where(u => u.Id == subjectId).Select(u => new { u.DepartmentId, u.TeamId }).FirstAsync();
        var row = new KpiEvaluation
        {
            KpiTemplateVersionId = versionId,
            SubjectUserId = subjectId,
            DepartmentId = subject.DepartmentId,
            TeamId = subject.TeamId,
            PeriodType = periodType,
            PeriodKey = periodKey,
            Status = status,
            TotalScore = score,
            IsDeleted = isDeleted,
            ReviewedAtUtc = DateTime.UtcNow
        };
        db.KpiEvaluations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    /// <inheritdoc cref="MonthKey"/>
    private static string Iso(DateOnly d) =>
        FormattableString.Invariant($"{d.Year:0000}-{d.Month:00}-{d.Day:00}");

    /// <summary>مفتاح الشهر المنقضي — نافذة **مغلقة** متعدّدة الأسابيع، صالحة لقياس المقام والاكتمال.</summary>
    private static string PrevMonthKey()
    {
        var today = TestCalendar.Today;
        return MonthKey(new DateOnly(today.Year, today.Month, 1).AddDays(-1));
    }

    private async Task<Guid> CreateDepartmentAsync(Guid managerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة R6 {Guid.NewGuid():N}", ManagerId = managerId, IsActive = true };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        return dept.Id;
    }

    private async Task<Guid> CreateTeamAsync(Guid departmentId, Guid leaderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var team = new Team
        {
            NameAr = $"فريق R6 {Guid.NewGuid():N}",
            DepartmentId = departmentId,
            TeamLeaderId = leaderId,
            IsActive = true
        };
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }

    private async Task SetUserOrgAsync(Guid userId, Guid? departmentId, Guid? teamId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = await db.Users.FirstAsync(x => x.Id == userId);
        u.DepartmentId = departmentId;
        u.TeamId = teamId;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// تاريخ التعيين يُعفي الأسابيع المنتهية قبله من <b>المقام</b> (<c>beforeHireDate</c>).
    /// هذا هو ما يسمح بقياس «الموظّف وزنه واحد» بعددَي تقييمات مختلفين مع بقاء الاثنين مؤهّلَين
    /// للمتوسّط الرسميّ: بدونه يسقط صاحب التقييم الواحد تحت عتبة التغطية فيختفي من المقارنة،
    /// فيصير الاختبار قياسًا للعتبة لا لطريقة التوسيط.
    /// </summary>
    private async Task SetHireDateAsync(Guid userId, DateOnly hire)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = await db.Users.FirstAsync(x => x.Id == userId);
        u.HireDate = hire;
        await db.SaveChangesAsync();
    }

    /// <summary>موظّف بمسمّى وظيفيّ خاصّ به + قالب نبض أسبوعيّ منشور على مسمّاه = عالَم معزول للقياس.</summary>
    private async Task<(HttpClient Admin, HttpClient Manager, Guid ManagerId, HttpClient EmployeeClient,
        Guid EmployeeId, Guid TemplateId, Guid ManualId, Guid AutoId)> WeeklyWorldAsync()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R6_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (empClient, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(
            _factory, "Employee", code, managerId);
        var (template, manual, auto) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);
        return (admin, manager, managerId, empClient, employee, template, manual, auto);
    }

    private sealed record TeamWorld(
        HttpClient Admin, HttpClient Manager, Guid ManagerId, Guid DepartmentId, Guid TeamId,
        Guid Emp1, Guid Emp2, string WeekA, string WeekB, string Window,
        /// <summary>مسمّى الفريق الوظيفيّ — يلزم لضمّ عضو ثالث <b>بنفس الكادنس</b> بلا أيّ تقييم.</summary>
        string RoleCode);

    /// <summary>
    /// فريق من موظّفَين داخل نافذة <b>أسبوعين مغلقين</b>:
    /// الأوّل معتمَد في الأسبوعين (60 ثمّ 100 ⇒ متوسّطه 80)، والثاني عُيِّن مع بداية الأسبوع الثاني
    /// فأسبوعه الأوّل مُعفى، وله تقييم واحد معتمَد (40 ⇒ متوسّطه 40). كلاهما بتغطية 100% من مقامه.
    /// <para>
    /// الأرقام مختارة لتفرِّق بين طريقتَي التوسيط تفريقًا لا يقبل التأويل:
    /// توسيط الموظّفين أوّلًا ⇒ (80 + 40) ÷ 2 = <b>60</b>؛ ومتوسّط التقييمات الخام
    /// ⇒ (60 + 100 + 40) ÷ 3 = <b>66.67</b>. أيّ تسرّب للترجيح بعدد التقييمات يظهر فورًا.
    /// </para>
    /// النافذة <c>Custom</c> عمدًا: هي الحبيبة الوحيدة التي تحدّ الأسابيع إلى اثنين بالضبط،
    /// فيبقى المقام صغيرًا معلومًا ولا يختلط القياس بعدد أسابيع الشهر المتغيّر.
    /// </summary>
    private async Task<TeamWorld> TeamWorldAsync()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R6_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, emp1) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (_, emp2) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (template, manual, auto) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);

        var dept = await CreateDepartmentAsync(managerId);
        var team = await CreateTeamAsync(dept, managerId);
        await SetUserOrgAsync(managerId, dept, null);
        await SetUserOrgAsync(emp1, dept, team);
        await SetUserOrgAsync(emp2, dept, team);

        var weekA = TestCalendar.Cycle(2);
        var weekB = TestCalendar.Cycle(1);
        await SetHireDateAsync(emp2, TestCalendar.CycleStart(weekB));

        await ApprovedWeeklyAsync(manager, template, emp1, manual, auto, weekA, 60m);
        await ApprovedWeeklyAsync(manager, template, emp1, manual, auto, weekB, 100m);
        await ApprovedWeeklyAsync(manager, template, emp2, manual, auto, weekB, 40m);

        var from = TestCalendar.CycleStart(weekA);
        var to = TestCalendar.CycleStart(weekB).AddDays(6);
        var window = $"periodType=Custom&from={Iso(from)}&to={Iso(to)}&teamId={team}";
        return new TeamWorld(admin, manager, managerId, dept, team, emp1, emp2, weekA, weekB, window, code);
    }

    // ═══════════════════════ §7 — العقد (Contract) ═══════════════════════

    /// <summary>§7/Contract/1 — Weekly creation succeeds: المسار الأسبوعيّ هو مسار الكتابة الوحيد وهو مفتوح.</summary>
    [Fact]
    public async Task عقد01_إنشاء_تقييم_أسبوعيّ_ينجح_ويُعاد_بمساره_ومفتاحه()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);

        var res = await w.Manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(w.TemplateId, w.EmployeeId, PeriodType.Weekly, week));
        res.EnsureSuccessStatusCode();

        var dto = await res.ReadAsync<KpiEvaluationDto>();
        Assert.Equal(PeriodType.Weekly, dto!.PeriodType);
        Assert.Equal(week, dto.PeriodKey);
    }

    /// <summary>§7/Contract/2 — Quarterly creation blocked: 400 برمز الكتابة المتقاعدة، وبلا كتابة صامتة.</summary>
    [Fact]
    public async Task عقد02_إنشاء_تقييم_ربعيّ_مردود_400_ولا_يكتب_صفًّا_واحدًا()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R6_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);

        var res = await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(quarterly, employee, PeriodType.Quarterly, "2026-Q2"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("legacy_quarterly_write_disabled", await res.Content.ReadAsStringAsync());

        // الرفض ليس شكليًّا: لا صفّ لهذا الموظّف إطلاقًا (WS-2 — لا كتابة خلف الردّ).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.KpiEvaluations.CountAsync(e => e.SubjectUserId == employee));
    }

    /// <summary>
    /// §7/Contract/3 — <c>cadence=Quarterly</c> ⇒ 400 <c>legacy_cadence_disabled</c> على
    /// <b>كلّ</b> سطوح القراءة الأربعة. وحدة الحكم مقصودة: لو رُدّت واحدة وقُبلت أخرى لصار
    /// «صفر أداء» في إحداها يُقرأ حقيقةً تشغيليّة بينما هو أثر مسار متقاعد.
    /// </summary>
    [Fact]
    public async Task عقد03_الكادنس_الربعيّ_مردود_برمزه_على_سطوح_القراءة_الأربعة()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, week, 80m);

        var q = $"periodType=Week&periodKey={week}&subjectUserId={w.EmployeeId}";
        foreach (var path in new[] { "/api/kpi/performance", "/api/kpi/rankings", "/api/kpi/drilldown" })
        {
            var res = await w.Manager.GetAsync($"{path}?{q}&cadence=Quarterly");
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Contains("legacy_cadence_disabled", await res.Content.ReadAsStringAsync());
        }

        var agg = await w.Manager.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Weekly&periodKey={week}&cadence=Quarterly");
        Assert.Equal(HttpStatusCode.BadRequest, agg.StatusCode);
        Assert.Contains("legacy_cadence_disabled", await agg.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// §7/Contract/4 — كادنس/حبيبة غير صالحين يُرَدّان بخطأ قانونيّ مسمًّى، لا بتجاهل صامت.
    /// التمييز جوهريّ: قيمة غير مفهومة تُتجاهَل تعني استجابة 200 بمحتوى لا يطابق ما طُلب.
    /// </summary>
    [Fact]
    public async Task عقد04_قيمة_كادنس_أو_حبيبة_غير_صالحة_تُرَدّ_ولا_تُتجاهَل_صامتًا()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);

        // كادنس غير موجود في التعداد ⇒ فشل ربط النموذج القانونيّ (400) لا سقوط إلى الوضع التلقائيّ.
        var badCadence = await w.Manager.GetAsync(
            $"/api/kpi/performance?periodType=Week&periodKey={week}&cadence=NotACadence");
        Assert.Equal(HttpStatusCode.BadRequest, badCadence.StatusCode);

        // حبيبة قراءة غير مدعومة ⇒ رمز مسمًّى من الخدمة نفسها.
        var badGranularity = await w.Manager.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Fortnightly&periodKey={week}");
        Assert.Equal(HttpStatusCode.BadRequest, badGranularity.StatusCode);
        Assert.Contains("kpi_aggregate.granularity_invalid", await badGranularity.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// §7/Contract/5 — الحبيبات الأربع مشتقّة كلّها من <b>نفس</b> حقائق الأسبوع المعتمَدة.
    /// الإثبات: أسبوع معتمَد واحد بدرجة واحدة ⇒ الحبيبات الأربع تُعيد نفس المتوسّط ونفس عدد
    /// التقييمات ونفس النقطة الأسبوعيّة، ويعلن كلّ ردّ أنّ الكادنس المطبَّق نبضٌ أسبوعيّ.
    /// </summary>
    [Fact]
    public async Task عقد05_الحبيبات_الأربع_تُشتقّ_من_نفس_حقائق_الأسبوع_المعتمَدة()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, week, 76m);

        // انتماء الأسبوع إلى أيّ نافذة أكبر يُحسم بمرجع **الثلاثاء** (السبت + 3) لا بيوم بدايته:
        // قاعدة منع الاحتساب المزدوج حين يقع الأسبوع على حدّ شهر أو ربع أو سنة. اشتقاق مفاتيح
        // النوافذ من يوم البداية كان سيجعل أسبوعًا حدوديًّا يبدو «مفقودًا» من شهره الحقيقيّ.
        // وتُبنى المفاتيح من مكوّنات التاريخ الرقميّة لا من تنسيقه: ثقافة تشغيل الاختبارات عربيّة،
        // و«yyyy-MM» فيها يُخرِج تقويمًا هجريًّا (1448-03) فيصير المفتاح لشهر لا وجود له في البيانات.
        var anchor = TestCalendar.CycleStart(week).AddDays(3);
        var month = MonthKey(anchor);
        var quarter = QuarterKey(anchor);
        var year = FormattableString.Invariant($"{anchor.Year:0000}");

        var grains = new[]
        {
            ("Weekly", week), ("Monthly", month), ("Quarterly", quarter), ("Yearly", year)
        };

        foreach (var (granularity, key) in grains)
        {
            var dto = await AggregateAsync(w.Manager,
                $"granularity={granularity}&periodKey={key}&subjectUserId={w.EmployeeId}");

            Assert.Equal(KpiCadence.WeeklyPulse, dto.AppliedCadence);
            // الحبيبة جزء من المُدَّعى: فشلٌ بلا اسم الحبيبة لا يقول أيّ نافذة انكسرت.
            Assert.Equal(($"{granularity}:{key}", (decimal?)76m, 1),
                ($"{granularity}:{key}", dto.Average, dto.EvaluationsCount));
            Assert.Equal(week, Assert.Single(dto.Weeks).PeriodKey);
        }
    }

    // ═══════════════════════ §7 — الأهليّة (Eligibility) ═══════════════════════

    /// <summary>§7/Eligibility/1 — Approved included: الحالة الوحيدة التي تصنع بسطًا.</summary>
    [Fact]
    public async Task أهليّة01_المعتمَد_وحده_يصنع_البسط_ويظهر_في_التفصيل()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        var id = await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, week, 88m);

        var q = $"periodType=Week&periodKey={week}&subjectUserId={w.EmployeeId}";
        var row = await RowAsync(w.Manager, q, w.EmployeeId);
        Assert.Equal(88m, row.Measure.Value);
        Assert.Equal(1, row.Measure.EligibleEvaluationCount);
        Assert.Equal(0, row.Measure.MissingCount);

        var drill = await DrilldownAsync(w.Manager, q);
        Assert.Contains(drill.Rows, r => r.EvaluationId == id);
    }

    /// <summary>
    /// §7/Eligibility/2–7 — الحالات الستّ غير المعتمَدة مستبعَدة كلّها: <c>Draft</c> و<c>Submitted</c>
    /// و<c>UnderReview</c> و<c>NeedsRevision</c> و<c>Rejected</c> و<c>Closed</c>.
    /// <para>
    /// <c>Closed</c> ضمنها عمدًا: هو «اكتمال دورة حياة» لا «اعتماد مراجِع»، وخلط المعنيين هو ما
    /// أنتج تباعد <c>{Approved}</c> مقابل <c>{Approved, Closed}</c> بين المستهلكين قبل R6.
    /// </para>
    /// والاستبعاد هنا <b>ليس صفرًا</b>: الصفّ يظهر بمقامه معلنًا نقصه، ولا قيمة ملفّقة.
    /// </summary>
    [Theory]
    [InlineData(KpiEvaluationStatus.Draft)]
    [InlineData(KpiEvaluationStatus.Submitted)]
    [InlineData(KpiEvaluationStatus.UnderReview)]
    [InlineData(KpiEvaluationStatus.NeedsRevision)]
    [InlineData(KpiEvaluationStatus.Rejected)]
    [InlineData(KpiEvaluationStatus.Closed)]
    public async Task أهليّة02_كلّ_حالة_دون_الاعتماد_مستبعَدة_ولا_تُقرَأ_صفرًا(KpiEvaluationStatus status)
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        var id = await SeedRowAsync(w.TemplateId, w.EmployeeId, PeriodType.Weekly, week, status, 95m);

        var q = $"periodType=Week&periodKey={week}&subjectUserId={w.EmployeeId}";
        var row = await RowAsync(w.Manager, q, w.EmployeeId);

        Assert.Null(row.Measure.Value);
        Assert.Equal(0, row.Measure.EligibleEvaluationCount);
        Assert.Equal(1, row.Measure.AdjustedExpectedCount);
        Assert.Equal(1, row.Measure.MissingCount);
        Assert.Equal(KpiDataQuality.NoData, row.Measure.DataQuality);
        Assert.False(row.EligibleForRanking);

        Assert.DoesNotContain((await DrilldownAsync(w.Manager, q)).Rows, r => r.EvaluationId == id);
    }

    /// <summary>§7/Eligibility/8 — IsDeleted excluded: المحذوف ناعمًا لا يدخل درجةً ولا تفصيلًا.</summary>
    [Fact]
    public async Task أهليّة03_المعتمَد_المحذوف_ناعمًا_مستبعَد_من_الدرجة_والتفصيل()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        var id = await SeedRowAsync(w.TemplateId, w.EmployeeId, PeriodType.Weekly, week,
            KpiEvaluationStatus.Approved, 95m, isDeleted: true);

        var q = $"periodType=Week&periodKey={week}&subjectUserId={w.EmployeeId}";
        var row = await RowAsync(w.Manager, q, w.EmployeeId);

        Assert.Null(row.Measure.Value);
        Assert.Equal(0, row.Measure.EligibleEvaluationCount);
        Assert.DoesNotContain((await DrilldownAsync(w.Manager, q)).Rows, r => r.EvaluationId == id);
    }

    /// <summary>
    /// §7/Eligibility/9 — Legacy Quarterly excluded: صفّ ربعيّ <b>معتمَد</b> قائم في القاعدة
    /// لا يحرّك رقمًا في أيّ نافذة قراءة تشمل فترته. هذا هو الاستبعاد الذي يجعل الأرقام قابلة
    /// للمقارنة عبر الزمن بلا حذف أيّ سجلّ تاريخيّ (WS-2).
    /// </summary>
    [Fact]
    public async Task أهليّة04_الصفّ_الربعيّ_الإرثيّ_المعتمَد_لا_يحرّك_رقمًا_في_أيّ_نافذة()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R6_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        await PublishAsync(admin, KpiCadence.WeeklyPulse, role);
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);

        var week = TestCalendar.Cycle(1);
        var quarterKey = QuarterKey(TestCalendar.CycleStart(week).AddDays(3)); // مرجع الثلاثاء (انظر عقد05)
        var legacyId = await SeedRowAsync(quarterly, employee, PeriodType.Quarterly, quarterKey,
            KpiEvaluationStatus.Approved, 99m);

        var q = $"periodType=Quarter&periodKey={quarterKey}&subjectUserId={employee}";
        var row = await RowAsync(manager, q, employee);
        Assert.Null(row.Measure.Value);
        Assert.Equal(0, row.Measure.EligibleEvaluationCount);
        Assert.Equal(KpiCadence.WeeklyPulse, row.EffectiveCadence);

        var drill = await DrilldownAsync(manager, q);
        Assert.DoesNotContain(drill.Rows, r => r.EvaluationId == legacyId);
        Assert.DoesNotContain(drill.Rows, r => r.PeriodKey == quarterKey);

        var agg = await AggregateAsync(manager,
            $"granularity=Quarterly&periodKey={quarterKey}&subjectUserId={employee}");
        Assert.Null(agg.Average);
        Assert.Equal(0, agg.EvaluationsCount);
    }

    // ═══════════════════════ §7 — التجميع (Aggregation) ═══════════════════════

    /// <summary>
    /// §7/Aggregation/1 — Missing is not zero: أسبوع معتمَد واحد داخل شهر من عدّة أسابيع يُقرَأ
    /// <b>بدرجته كما هي</b>، لا مقسومة على أسابيع لم تُقيَّم. النقص يُعلَن في المقام لا في البسط.
    /// <para>
    /// لو دخل الأسبوع الناقص البسطَ صفرًا لصار موظّف بأسبوع واحد ممتاز يقرأ «متدنّي الأداء»،
    /// وهو تشويه إحصائيّ يعاقب على غياب البيانات لا على الأداء.
    /// </para>
    /// </summary>
    [Fact]
    public async Task تجميع01_الالتزام_الناقص_يُعلَن_في_المقام_ولا_يدخل_البسط_صفرًا()
    {
        var w = await WeeklyWorldAsync();
        var month = PrevMonthKey();
        var weeks = await WeekKeysAsync(w.Admin, "Month", month);
        Assert.True(weeks.Length >= 4, $"الشهر {month} يجب أن يضمّ 4 أسابيع فأكثر ليكون القياس ذا معنى.");

        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, weeks[0], 60m);

        var row = await RowAsync(
            w.Manager, $"periodType=Month&periodKey={month}&subjectUserId={w.EmployeeId}", w.EmployeeId);

        Assert.Equal(60m, row.Measure.Value);
        Assert.Equal(1, row.Measure.EligibleEvaluationCount);
        Assert.Equal(weeks.Length, row.Measure.AdjustedExpectedCount);
        Assert.Equal(weeks.Length - 1, row.Measure.MissingCount);
    }

    /// <summary>
    /// §7/Aggregation/2 — employee-first then team average: يُحسب متوسّط كلّ موظّف أوّلًا،
    /// ثمّ يُحسب متوسّط المجموعة على <b>متوسّطات الأعضاء</b> لا على التقييمات الخام.
    /// </summary>
    [Fact]
    public async Task تجميع02_متوسّط_الفريق_يُبنى_على_متوسّطات_الأعضاء_لا_على_التقييمات()
    {
        var t = await TeamWorldAsync();
        var perf = await PerfAsync(t.Manager, t.Window);

        Assert.Equal(80m, perf.Employees.Single(e => e.UserId == t.Emp1).Measure.Value);
        Assert.Equal(40m, perf.Employees.Single(e => e.UserId == t.Emp2).Measure.Value);

        var team = perf.Teams.Single(g => g.GroupId == t.TeamId);
        Assert.Equal(60m, team.Measure.Value);
        Assert.Equal(2, team.ScoredMemberCount);
        Assert.Equal(2, team.TotalMemberCount);
        Assert.Equal(2, team.QualifiedMemberCount);
    }

    /// <summary>
    /// §7/Aggregation/3 — one employee = one weight: كثرة تقييمات موظّف لا ترجّح صوته في متوسّط
    /// الفريق. الاختبار يعيد حساب المتوسّط الخامّ من صفوف التفصيل نفسها ويثبت أنّ الرقم المعروض
    /// <b>ليس</b> إيّاه — فلا يبقى الادّعاء رهنَ ثابت مكتوب باليد.
    /// </summary>
    [Fact]
    public async Task تجميع03_الموظّف_وزنه_واحد_مهما_كثرت_تقييماته()
    {
        var t = await TeamWorldAsync();
        var perf = await PerfAsync(t.Manager, t.Window);
        var drill = await DrilldownAsync(t.Manager, t.Window);

        var scores = drill.Rows.Where(r => r.TotalScore is not null).Select(r => r.TotalScore!.Value).ToList();
        Assert.Equal(3, scores.Count); // موظّف بتقييمين وآخر بتقييم واحد
        var rawAverage = Math.Round(scores.Average(), 2); // 66.67 — الترجيح بعدد التقييمات

        var team = perf.Teams.Single(g => g.GroupId == t.TeamId);
        Assert.Equal(60m, team.Measure.Value);
        Assert.NotEqual(rawAverage, team.Measure.Value);
    }

    /// <summary>
    /// §7/Aggregation/4 — denominator disclosure: المقام مُفصَح عنه بمكوّناته لا كرقم واحد غامض:
    /// المتوقَّع قبل الإعفاء، والمُعفى، والمتوقَّع بعده، والناقص، والتغطية — لكلّ موظّف وللمجموعة.
    /// بدون هذا الإفصاح يبدو انخفاض مقام الموظّف الثاني (من 2 إلى 1) تلاعبًا لا إعفاءً موثَّقًا.
    /// </summary>
    [Fact]
    public async Task تجميع04_المقام_مُفصَح_عنه_بمكوّناته_لا_كرقم_واحد()
    {
        var t = await TeamWorldAsync();
        var perf = await PerfAsync(t.Manager, t.Window);

        var m1 = perf.Employees.Single(e => e.UserId == t.Emp1).Measure;
        Assert.Equal((2, 0, 2, 1, 0), (m1.ExpectedEvaluationCount, m1.ExemptCount,
            m1.AdjustedExpectedCount, m1.Coverage == 1m ? 1 : 0, m1.MissingCount));
        Assert.Equal(100m, m1.CoveragePercent);

        var m2 = perf.Employees.Single(e => e.UserId == t.Emp2).Measure;
        Assert.Equal((2, 1, 1, 1, 0), (m2.ExpectedEvaluationCount, m2.ExemptCount,
            m2.AdjustedExpectedCount, m2.Coverage == 1m ? 1 : 0, m2.MissingCount));

        // المجموعة تُفصح عن مقامها البشريّ أيضًا: كم عضوًا له درجة، وكم دخل المتوسّط الرسميّ، وكم العدد الكلّيّ.
        var team = perf.Teams.Single(g => g.GroupId == t.TeamId);
        Assert.Equal((2, 2, 2), (team.ScoredMemberCount, team.QualifiedMemberCount, team.TotalMemberCount));
        Assert.Equal(3, team.Measure.EligibleEvaluationCount);
        Assert.Equal(3, team.Measure.AdjustedExpectedCount);
    }

    /// <summary>
    /// §7/Aggregation/5 — Running = Provisional: الفترة الجارية لا تُنتِج حكمًا نهائيًّا ولو اكتملت
    /// التزاماتها المنقضية حتّى اللحظة؛ «مؤقّت» هو التوصيف الصادق لفترة لم تُغلق بعد.
    /// </summary>
    [Fact]
    public async Task تجميع05_الفترة_الجارية_تُوصَف_مؤقّتة_لا_نهائيّة()
    {
        var w = await WeeklyWorldAsync();
        var current = TestCalendar.Cycle(0);
        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, current, 90m);

        var q = $"periodType=Week&periodKey={current}&subjectUserId={w.EmployeeId}";
        var perf = await PerfAsync(w.Manager, q);

        Assert.True(perf.PeriodResolved.IsOpen);
        var measure = perf.Employees.Single(e => e.UserId == w.EmployeeId).Measure;
        Assert.Equal(90m, measure.Value);
        Assert.Equal(KpiCompletenessState.Provisional, measure.Completeness);
    }

    /// <summary>§7/Aggregation/6 — Ended + 100% = Final: فترة منتهية بكلّ التزاماتها معتمَدة ⇒ حكم نهائيّ.</summary>
    [Fact]
    public async Task تجميع06_الفترة_المنتهية_المكتملة_تُوصَف_نهائيّة()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, week, 90m);

        var q = $"periodType=Week&periodKey={week}&subjectUserId={w.EmployeeId}";
        var perf = await PerfAsync(w.Manager, q);

        Assert.False(perf.PeriodResolved.IsOpen);
        var measure = perf.Employees.Single(e => e.UserId == w.EmployeeId).Measure;
        Assert.Equal(1, measure.EligibleEvaluationCount);
        Assert.Equal(1, measure.AdjustedExpectedCount);
        Assert.Equal(KpiCompletenessState.Final, measure.Completeness);
    }

    /// <summary>
    /// §7/Aggregation/7 — Ended + &lt;100% = Incomplete: فترة منتهية بنقص لا تُوصَف نهائيّة ولو أُغلق
    /// زمنها. الفرق بين «انتهى الوقت» و«اكتملت البيانات» هو ما يمنع تثبيت رقم ناقص كحكم رسميّ.
    /// </summary>
    [Fact]
    public async Task تجميع07_الفترة_المنتهية_الناقصة_تُوصَف_غير_مكتملة()
    {
        var w = await WeeklyWorldAsync();
        var month = PrevMonthKey();
        var weeks = await WeekKeysAsync(w.Admin, "Month", month);
        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, weeks[0], 90m);

        var perf = await PerfAsync(w.Manager, $"periodType=Month&periodKey={month}&subjectUserId={w.EmployeeId}");

        Assert.False(perf.PeriodResolved.IsOpen);
        var measure = perf.Employees.Single(e => e.UserId == w.EmployeeId).Measure;
        Assert.True(measure.EligibleEvaluationCount < measure.AdjustedExpectedCount);
        Assert.Equal(KpiCompletenessState.Incomplete, measure.Completeness);
    }

    /// <summary>
    /// §7/Aggregation/8 — 80% band does not hide the average: من كانت تغطيته دون الحدّ الأدنى
    /// يخرج من <b>المتوسّط الرسميّ</b> ولا يخرج من <b>العرض</b>. درجته تبقى ظاهرة موسومة «مؤقّتة»
    /// واسمه يُدرَج في قائمة المستبعَدين — إخفاؤه كان سيجعل الغياب يبدو انعدامًا للمشكلة.
    /// </summary>
    [Fact]
    public async Task تجميع08_نطاق_الثمانين_يُخرِج_من_المتوسّط_ولا_يُخفي_الدرجة()
    {
        var w = await WeeklyWorldAsync();
        var month = PrevMonthKey();
        var weeks = await WeekKeysAsync(w.Admin, "Month", month);
        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, weeks[0], 72m);

        var q = $"periodType=Month&periodKey={month}";
        var perf = await PerfAsync(w.Manager, q);
        var row = perf.Employees.Single(e => e.UserId == w.EmployeeId);

        Assert.Equal(72m, row.Measure.Value);          // الدرجة معروضة
        Assert.True(row.Measure.IsProvisional);        // وموسومة مؤقّتة
        Assert.False(row.EligibleForRanking);          // وخارج المتوسّط الرسميّ
        Assert.True(row.Measure.Coverage < 0.8m);

        var rankings = await RankingsAsync(w.Manager, q);
        Assert.DoesNotContain(rankings.TopPerformers, e => e.UserId == w.EmployeeId);
        Assert.Contains(rankings.ExcludedEmployees ?? Array.Empty<KpiEmployeeScoreDto>(),
            e => e.UserId == w.EmployeeId && e.Measure.Value == 72m);
    }

    // ═══════════════════════ §7 — المستهلكون (Consumers) ═══════════════════════

    /// <summary>
    /// §7/Consumers/1 — Rankings use Weekly Approved only: الترتيب لا يقرأ صفًّا ربعيًّا إرثيًّا
    /// ولا صفًّا أسبوعيًّا غير معتمَد، مهما علت درجتهما. لا يكفي غيابهما عن «الأفضل»: يجب ألّا
    /// تظهر درجتهما في <b>أيّ</b> قائمة يعيدها العقد، وإلّا صار الاستبعاد ترتيبًا لا استبعادًا.
    /// </summary>
    [Fact]
    public async Task مستهلك01_الترتيب_لا_يقرأ_إلّا_النبض_الأسبوعيّ_المعتمَد()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        var quarter = QuarterKey(TestCalendar.CycleStart(week).AddDays(3));

        var (legacyTemplate, _, _) = await PublishAsync(w.Admin, KpiCadence.Quarterly);
        await SeedRowAsync(legacyTemplate, w.EmployeeId, PeriodType.Quarterly, quarter,
            KpiEvaluationStatus.Approved, 99m);
        await SeedRowAsync(w.TemplateId, w.EmployeeId, PeriodType.Weekly, week,
            KpiEvaluationStatus.Submitted, 95m);

        var rankings = await RankingsAsync(w.Manager, $"periodType=Week&periodKey={week}");

        var everyone = rankings.TopPerformers
            .Concat(rankings.NeedsSupport)
            .Concat(rankings.ExcludedEmployees ?? Array.Empty<KpiEmployeeScoreDto>())
            .Concat(rankings.CadenceNotConfiguredEmployees ?? Array.Empty<KpiEmployeeScoreDto>())
            .ToList();

        Assert.DoesNotContain(everyone, e => e.Measure.Value == 99m || e.Measure.Value == 95m);
        Assert.All(everyone.Where(e => e.UserId == w.EmployeeId), e => Assert.Null(e.Measure.Value));
    }

    /// <summary>
    /// §7/Consumers/3 — Legacy summary excludes non-approved statuses: نقطة النهاية المهجورة
    /// <c>GET /api/reports/kpi-summary</c> ما زالت لها مستهلكون، فبقاؤها منفذًا خلفيًّا يلتفّ على
    /// مصدر الحقيقة كان يعني رقمين رسميّين متناقضين في نفس النظام. تُقاس هنا على مسارها الإرثيّ
    /// (المحرّك الموحّد مُطفأ افتراضيًّا) لأنّه المسار الذي كان يخرق العقد فعلًا.
    /// </summary>
    [Fact]
    public async Task مستهلك02_الملخّص_الإرثيّ_يستبعد_غير_المعتمَد_والصفوف_الربعيّة()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        var quarter = QuarterKey(TestCalendar.CycleStart(week).AddDays(3));

        var (legacyTemplate, _, _) = await PublishAsync(w.Admin, KpiCadence.Quarterly);
        await SeedRowAsync(legacyTemplate, w.EmployeeId, PeriodType.Quarterly, quarter,
            KpiEvaluationStatus.Approved, 99m);

        // الصفّ غير المعتمَد يُبذَر على قالب نبض **آخر**: تفرّد التسليم لكلّ (موظّف، قالب، فترة) يجعل
        // بذره على نفس القالب يصطدم بالصفّ الذي ستنشئه الرحلة، فيعيد الإنشاء الصفَّ القائم ويعتمده
        // بدرجته القديمة — فيقيس الاختبار تصادم تهيئة لا استبعاد حالة.
        var (otherWeekly, _, _) = await PublishAsync(w.Admin, KpiCadence.WeeklyPulse);
        await SeedRowAsync(otherWeekly, w.EmployeeId, PeriodType.Weekly, week,
            KpiEvaluationStatus.UnderReview, 95m);

        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, week, 70m);

        var res = await w.Manager.GetAsync($"/api/reports/kpi-summary?periodType=Weekly&periodKey={week}");
        res.EnsureSuccessStatusCode();
        var summary = (await res.ReadAsync<KpiSummaryReport>())!;

        var mine = summary.Rows.Where(r => r.SubjectUserId == w.EmployeeId).ToList();
        Assert.Equal(70m, Assert.Single(mine).TotalScore);
        Assert.DoesNotContain(summary.Rows, r => r.TotalScore == 95m || r.TotalScore == 99m);
    }

    /// <summary>
    /// §7/Consumers/6 — No duplicate counting across windows: الأسبوع الواقع على حدّ شهرين
    /// يُحتسَب في شهر <b>واحد</b> بالضبط — شهر يوم الثلاثاء المرجعيّ. من دون هذه القاعدة يظهر
    /// نفس الإنجاز مرّتين في مجموعَي شهرين، فتتضخّم الأرقام السنويّة بلا عمل إضافيّ.
    /// </summary>
    [Fact]
    public async Task مستهلك03_الأسبوع_الحدوديّ_يُحتسَب_في_نافذة_واحدة_لا_نافذتين()
    {
        var w = await WeeklyWorldAsync();

        // أوّل أسبوع منقضٍ يعبر حدّ شهرين ضمن آخر 12 أسبوعًا — البحث ضروريّ لأنّ وجود الحدّ
        // يعتمد على التقويم لا على اختيارنا، وتصليب مفتاح بعينه كان سيجعل الاختبار موسميًّا.
        var boundary = Enumerable.Range(1, 12).Select(TestCalendar.Cycle)
            .FirstOrDefault(k => TestCalendar.CycleStart(k).Month != TestCalendar.CycleStart(k).AddDays(6).Month);
        Assert.NotNull(boundary);

        var start = TestCalendar.CycleStart(boundary!);
        var owningMonth = MonthKey(start.AddDays(3));       // شهر الثلاثاء المرجعيّ
        var otherMonth = MonthKey(start.Month == start.AddDays(3).Month ? start.AddDays(6) : start);
        Assert.NotEqual(owningMonth, otherMonth);

        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, boundary!, 84m);

        Assert.Contains(boundary, await WeekKeysAsync(w.Admin, "Month", owningMonth));
        Assert.DoesNotContain(boundary, await WeekKeysAsync(w.Admin, "Month", otherMonth));

        var owning = await RowAsync(
            w.Manager, $"periodType=Month&periodKey={owningMonth}&subjectUserId={w.EmployeeId}", w.EmployeeId);
        var other = await RowAsync(
            w.Manager, $"periodType=Month&periodKey={otherMonth}&subjectUserId={w.EmployeeId}", w.EmployeeId);

        Assert.Equal((84m, 1), (owning.Measure.Value, owning.Measure.EligibleEvaluationCount));
        Assert.Equal((null, 0), (other.Measure.Value, other.Measure.EligibleEvaluationCount));
    }

    // ═══════════════════════ §6+§7 — الصلاحيّات (Permissions) ═══════════════════════

    /// <summary>§7/Permissions/1 — Employee self: الموظّف يقرأ رقمه هو، بنطاق <c>own</c> معلَن.</summary>
    [Fact]
    public async Task صلاحيّة01_الموظّف_يقرأ_رقم_نفسه_بنطاق_معلَن()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, week, 82m);

        var dto = await AggregateAsync(w.EmployeeClient, $"granularity=Weekly&periodKey={week}&subjectUserId={w.EmployeeId}");
        Assert.Equal(82m, dto.Average);
        Assert.Equal("own", dto.ScopeType);
    }

    /// <summary>§7/Permissions/2 — Manager in scope: المدير يقرأ مرؤوسه، وهو النطاق الذي يديره فعلًا.</summary>
    [Fact]
    public async Task صلاحيّة02_المدير_يقرأ_مرؤوسه_داخل_نطاقه()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, week, 82m);

        var dto = await AggregateAsync(w.Manager, $"granularity=Weekly&periodKey={week}&subjectUserId={w.EmployeeId}");
        Assert.Equal(82m, dto.Average);
    }

    /// <summary>
    /// §7/Permissions/3 — Outside scope: قراءة موظّف خارج النطاق تُرَدّ بـ403 لا تُعاد فارغة.
    /// الردّ الفارغ كان سيخلط «لا صلاحيّة» بـ«لا بيانات» ويسرّب وجود الموظّف من عدمه ضمنًا.
    /// </summary>
    [Fact]
    public async Task صلاحيّة03_قراءة_خارج_النطاق_تُرَدّ_لا_تُعاد_فارغة()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, week, 82m);
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var res = await stranger.GetAsync(
            $"/api/kpi-evaluations/aggregate?granularity=Weekly&periodKey={week}&subjectUserId={w.EmployeeId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>
    /// §7/Permissions/4 — Admin without automatic business authority (§6: «Admin لا يملك قرار أعمال تلقائيًّا»).
    /// <para>
    /// الارتفاع التقنيّ يفتح <b>الاطّلاع</b> الإشرافيّ ولا يمنح <b>القرار</b>: من أدخل التقييم لا يعتمده،
    /// ولو كان مسؤول النظام. لولا ذلك لأمكن لحساب واحد أن يصنع رقمًا رسميًّا ويصادق عليه بلا شاهد،
    /// فينهار فصل الإدخال عن المراجعة الذي يقوم عليه معنى «معتمَد» كلّه.
    /// </para>
    /// </summary>
    [Fact]
    public async Task صلاحيّة04_المسؤول_يطّلع_ولا_يعتمد_ما_أدخله_بنفسه()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);

        var ev = await (await w.Admin.PostAsJsonAsync("/api/kpi-evaluations",
                new CreateKpiEvaluationRequest(w.TemplateId, w.EmployeeId, PeriodType.Weekly, week)))
            .ReadAsync<KpiEvaluationDto>();
        await w.Admin.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(w.ManualId, null, 88m, null),
                new KpiResultInput(w.AutoId, 88m, null, null)
            }));
        (await w.Admin.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null)).EnsureSuccessStatusCode();

        // القرار: مرفوض لأنّ المُعتمِد هو المُدخِل نفسه.
        var approve = await w.Admin.PostAsync($"/api/kpi-evaluations/{ev.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);

        // الاطّلاع: مسموح — الإشراف لا يتعطّل بمنع القرار. والمُدَّعى أنّ الصفّ **لم يُعتمَد**،
        // لا أنّه على حالة انتقاليّة بعينها: مسار المراجعة (Submitted ⇄ UnderReview) ليس من عقد R6.
        var read = await w.Admin.GetAsync($"/api/kpi-evaluations/{ev.Id}");
        read.EnsureSuccessStatusCode();
        Assert.NotEqual(KpiEvaluationStatus.Approved, (await read.ReadAsync<KpiEvaluationDto>())!.Status);
    }

    /// <summary>
    /// §7/Permissions/5 — Legacy archive authorization (§6: «Legacy archive = authorized audit/admin scope فقط»).
    /// <para>
    /// السجلّ الربعيّ الإرثيّ <b>باقٍ كما هو</b> (WS-2: بلا حذف ولا هجرة ولا تعديل) ويُقرأ عبر مسار
    /// السجلّ المصرَّح به وحده، لا عبر أيّ سطح تشغيليّ. هذا هو ما يجعل إخراجه من الأرقام
    /// <b>استبعادًا من الحساب</b> لا محوًا للتاريخ.
    /// </para>
    /// </summary>
    [Fact]
    public async Task صلاحيّة05_السجلّ_الربعيّ_الإرثيّ_يبقى_كما_هو_ويُقرأ_بصلاحيّة_وحدها()
    {
        var w = await WeeklyWorldAsync();
        var quarter = QuarterKey(TestCalendar.CycleStart(TestCalendar.Cycle(1)).AddDays(3));
        var (legacyTemplate, _, _) = await PublishAsync(w.Admin, KpiCadence.Quarterly);
        var legacyId = await SeedRowAsync(legacyTemplate, w.EmployeeId, PeriodType.Quarterly, quarter,
            KpiEvaluationStatus.Approved, 99m);

        var read = await w.Admin.GetAsync($"/api/kpi-evaluations/{legacyId}");
        read.EnsureSuccessStatusCode();
        var dto = (await read.ReadAsync<KpiEvaluationDto>())!;
        Assert.Equal((PeriodType.Quarterly, quarter, KpiEvaluationStatus.Approved, (decimal?)99m),
            (dto.PeriodType, dto.PeriodKey, dto.Status, dto.TotalScore));

        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var denied = await stranger.GetAsync($"/api/kpi-evaluations/{legacyId}");
        Assert.True(denied.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"القراءة غير المصرَّح بها للأرشيف الإرثيّ يجب أن تُمنَع؛ وردت {(int)denied.StatusCode}.");
    }

    // ═══════════════ §7 — فحوص UAT المسمّاة والمطلوبة (سبعة) ═══════════════

    /// <summary>
    /// §7/UAT/1 — «فاطمة»: دعم الرئيس التنفيذيّ يتابع على كامل المؤسّسة بلا اعتماد فنّيّ.
    /// <para>
    /// النطاق الحوكميّ يوسّع <b>الاطّلاع</b> ولا يمنح <b>القرار</b>: لو صار الاطّلاع الكامل بابًا
    /// للاعتماد لأمكن لدور متابعة أن يصنع أرقامًا رسميّة، فيسقط شرط «معتمَد من مسؤول الخطّ» الذي
    /// تقوم عليه دلالة الرقم كلّها.
    /// </para>
    /// </summary>
    [Fact]
    public async Task فحصUAT1_فاطمة_تتابع_الرقم_على_كامل_المؤسّسة_ولا_تعتمد_تقييمًا()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, week, 82m);

        var (fatima, _) = await TestAuth.CreateUserAsync(_factory, "CeoSupport");

        // (أ) المتابعة: الرقم مقروء بنطاق مُعلَن، وهو نطاق حوكمة لا نطاق خطّ إداريّ.
        var perf = await PerfAsync(fatima, $"periodType=Week&periodKey={week}&subjectUserId={w.EmployeeId}");
        Assert.Equal("governance", perf.ScopeType);
        Assert.Equal(82m, perf.Employees.Single(e => e.UserId == w.EmployeeId).Measure.Value);

        // (ب) القرار: تقييم مُرسَل ينتظر الاعتماد — والاعتماد من فاطمة مرفوض.
        var pending = await (await w.Manager.PostAsJsonAsync("/api/kpi-evaluations",
                new CreateKpiEvaluationRequest(
                    w.TemplateId, w.EmployeeId, PeriodType.Weekly, TestCalendar.Cycle(2))))
            .ReadAsync<KpiEvaluationDto>();
        await w.Manager.PutAsJsonAsync($"/api/kpi-evaluations/{pending!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(w.ManualId, null, 91m, null),
                new KpiResultInput(w.AutoId, 91m, null, null)
            }));
        (await w.Manager.PostAsync($"/api/kpi-evaluations/{pending.Id}/submit", null)).EnsureSuccessStatusCode();

        var approve = await fatima.PostAsync($"/api/kpi-evaluations/{pending.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);

        var still = await w.Admin.GetAsync($"/api/kpi-evaluations/{pending.Id}");
        still.EnsureSuccessStatusCode();
        Assert.NotEqual(KpiEvaluationStatus.Approved, (await still.ReadAsync<KpiEvaluationDto>())!.Status);
    }

    /// <summary>
    /// §7/UAT/2 — Missing weeks: الأسابيع الناقصة <b>تُسمّى واحدًا واحدًا</b> بمفتاحها ومداها،
    /// لا تُختصر في عدّاد. «ناقص 3» لا يقبل التحقّق ولا التصحيح؛ أمّا تسمية الأسبوع بعينه فتحوّل
    /// النقص إلى بند عمل قابل للإغلاق، وتمنع تفسير الغياب أداءً متدنّيًا.
    /// </summary>
    [Fact]
    public async Task فحصUAT2_الأسابيع_الناقصة_تُسمّى_واحدًا_واحدًا_لا_تُختصَر_في_عدّاد()
    {
        var w = await WeeklyWorldAsync();
        var month = PrevMonthKey();
        var weeks = await WeekKeysAsync(w.Admin, "Month", month);
        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, weeks[0], 75m);

        var drill = await DrilldownAsync(
            w.Manager, $"periodType=Month&periodKey={month}&subjectUserId={w.EmployeeId}");
        var periods = Assert.IsAssignableFrom<IReadOnlyList<KpiSourcePeriodDto>>(drill.SourcePeriods);

        Assert.Equal(weeks.OrderBy(k => k, StringComparer.Ordinal),
            periods.Select(p => p.PeriodKey).OrderBy(k => k, StringComparer.Ordinal));

        var completed = Assert.Single(periods.Where(p => p.IsCompleted));
        Assert.Equal((weeks[0], (decimal?)75m), (completed.PeriodKey, completed.Score));

        var missing = periods.Where(p => !p.IsCompleted && !p.IsExempt).ToList();
        Assert.Equal(weeks.Length - 1, missing.Count);
        Assert.All(missing, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Label));
            Assert.True(p.Start <= p.End);
            Assert.Null(p.Score); // غياب لا صفر
        });
    }

    /// <summary>
    /// §7/UAT/3 — No evaluations: من لا تقييم له يُعلَن <c>NoData</c> بلا رقم، ولا يُقحَم في مقارنة.
    /// صفرٌ هنا ليس «قراءة متحفّظة» بل ادّعاء أداء لم يُقَس، ويُنزِل الموظّف والفريق معًا بغير سبب.
    /// </summary>
    [Fact]
    public async Task فحصUAT3_من_لا_تقييم_له_يُعلَن_بلا_بيانات_ولا_يُقرَأ_صفرًا()
    {
        var w = await WeeklyWorldAsync();
        var month = PrevMonthKey();
        var weeks = await WeekKeysAsync(w.Admin, "Month", month);
        var q = $"periodType=Month&periodKey={month}&subjectUserId={w.EmployeeId}";

        var row = await RowAsync(w.Manager, q, w.EmployeeId);
        Assert.Null(row.Measure.Value);
        Assert.Equal(KpiDataQuality.NoData, row.Measure.DataQuality);
        Assert.Equal(0, row.Measure.EligibleEvaluationCount);
        Assert.Null(row.IsBelowTarget); // «دون العتبة» حكم لا يُطلَق على رقم غير موجود
        Assert.False(row.EligibleForRanking);

        var drill = await DrilldownAsync(w.Manager, q);
        Assert.Equal(0, drill.RowCount);
        Assert.Null(drill.RecomputedValue);
        var periods = Assert.IsAssignableFrom<IReadOnlyList<KpiSourcePeriodDto>>(drill.SourcePeriods);
        Assert.Equal(weeks.Length, periods.Count);
        Assert.All(periods, p => Assert.False(p.IsCompleted));
    }

    /// <summary>
    /// §7/UAT/4 — Approved + Draft/UnderReview في نفس الأسبوع: الرقم من المعتمَد وحده،
    /// والمستبعَد بالحالة <b>مُعلَن بعدده</b> لا مُسقَط بصمت.
    /// <para>
    /// الصفوف غير المعتمَدة تُبذَر على قوالب نبض <b>أخرى</b>: تفرّد التسليم لكلّ (موظّف، قالب، فترة)
    /// يجعل بذرها على نفس القالب تصادمَ تهيئة يقيس شيئًا آخر تمامًا.
    /// </para>
    /// </summary>
    [Fact]
    public async Task فحصUAT4_المعتمَد_وحده_يصنع_الرقم_والمستبعَد_بالحالة_يُعلَن_بعدده()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, week, 90m);

        var (draftTemplate, _, _) = await PublishAsync(w.Admin, KpiCadence.WeeklyPulse);
        var (reviewTemplate, _, _) = await PublishAsync(w.Admin, KpiCadence.WeeklyPulse);
        await SeedRowAsync(draftTemplate, w.EmployeeId, PeriodType.Weekly, week,
            KpiEvaluationStatus.Draft, 20m);
        await SeedRowAsync(reviewTemplate, w.EmployeeId, PeriodType.Weekly, week,
            KpiEvaluationStatus.UnderReview, 100m);

        var row = await RowAsync(
            w.Manager, $"periodType=Week&periodKey={week}&subjectUserId={w.EmployeeId}", w.EmployeeId);

        Assert.Equal(90m, row.Measure.Value);
        Assert.Equal(1, row.Measure.EligibleEvaluationCount);
        Assert.Equal(2, row.Measure.ExcludedByStatusCount);
    }

    /// <summary>
    /// §7/UAT/5 — Template/weight changed: نشر إصدار جديد بأوزان مختلفة <b>لا يعيد كتابة التاريخ</b>.
    /// التقييم المعتمَد يظلّ مربوطًا بالإصدار الذي حُسِب عليه وبدرجته كما هي، وإلّا تغيّرت أرقام
    /// أرباع مُقفَلة بأثر رجعيّ لمجرّد تعديل قالب اليوم — وهو أخطر أشكال فقدان قابليّة المقارنة.
    /// </summary>
    [Fact]
    public async Task فحصUAT5_تغيير_القالب_وأوزانه_لا_يعيد_كتابة_درجة_معتمَدة_سابقة()
    {
        var w = await WeeklyWorldAsync();
        var week = TestCalendar.Cycle(1);
        var evaluationId = await ApprovedWeeklyAsync(
            w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, week, 82m);

        var before = (await (await w.Admin.GetAsync($"/api/kpi-evaluations/{evaluationId}"))
            .ReadAsync<KpiEvaluationDto>())!;

        // إصدار ثانٍ بأوزان مغايرة تمامًا (30/70 بدل 50/50) ثمّ نشره. الإصدار الجديد يُنسَخ من
        // السابق بمؤشّراته، فالتغيير يكون بتعديل أوزان النسخة لا بإضافة مؤشّرات فوقها.
        var v2 = await (await w.Admin.PostAsync($"/api/kpi-templates/{w.TemplateId}/versions", null))
            .ReadAsync<KpiTemplateVersionDto>();
        Assert.Equal(2, v2!.VersionNumber);
        var manual2 = v2.Metrics.Single(m => m.Name == "الالتزام");
        var auto2 = v2.Metrics.Single(m => m.Name == "الإنجاز");
        (await w.Admin.PutAsJsonAsync($"/api/kpi-templates/metrics/{manual2.Id}",
            new UpsertKpiMetricRequest("الالتزام", null, 30m, null, null, KpiCalcMethod.Manual, null)))
            .EnsureSuccessStatusCode();
        (await w.Admin.PutAsJsonAsync($"/api/kpi-templates/metrics/{auto2.Id}",
            new UpsertKpiMetricRequest("الإنجاز", null, 70m, 100m, "%", KpiCalcMethod.Auto, null)))
            .EnsureSuccessStatusCode();
        (await w.Admin.PostAsync($"/api/kpi-templates/versions/{v2.Id}/publish", null))
            .EnsureSuccessStatusCode();

        var after = (await (await w.Admin.GetAsync($"/api/kpi-evaluations/{evaluationId}"))
            .ReadAsync<KpiEvaluationDto>())!;

        Assert.Equal(before.KpiTemplateVersionId, after.KpiTemplateVersionId);
        Assert.NotEqual(v2.Id, after.KpiTemplateVersionId);
        Assert.Equal((decimal?)82m, after.TotalScore);
        Assert.Equal(KpiEvaluationStatus.Approved, after.Status);

        var row = await RowAsync(
            w.Manager, $"periodType=Week&periodKey={week}&subjectUserId={w.EmployeeId}", w.EmployeeId);
        Assert.Equal(82m, row.Measure.Value);
    }

    /// <summary>
    /// §7/UAT/6 — Drill-down lineage: الرقم المعروض يُعاد إنتاجه يدويًّا من صفوفه المُعادة نفسها.
    /// كلّ صفّ يحمل معرّف تقييمه وقالبه وفترته وحالته، فيصير النَّسَب مسارًا قابلًا للتفتيش من الرقم
    /// إلى مصدره — لا رقمًا يُطلَب تصديقه.
    /// </summary>
    [Fact]
    public async Task فحصUAT6_الرقم_المعروض_يُعاد_إنتاجه_من_صفوف_تفصيله_بنَسَب_كامل()
    {
        var w = await WeeklyWorldAsync();
        var month = PrevMonthKey();
        var weeks = await WeekKeysAsync(w.Admin, "Month", month);
        var e1 = await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, weeks[0], 60m);
        var e2 = await ApprovedWeeklyAsync(w.Manager, w.TemplateId, w.EmployeeId, w.ManualId, w.AutoId, weeks[1], 90m);

        var q = $"periodType=Month&periodKey={month}&subjectUserId={w.EmployeeId}";
        var displayed = (await RowAsync(w.Manager, q, w.EmployeeId)).Measure.Value;
        var drill = await DrilldownAsync(w.Manager, q);

        Assert.Equal(2, drill.RowCount);
        Assert.Equal(new[] { e1, e2 }.OrderBy(x => x), drill.Rows.Select(r => r.EvaluationId).OrderBy(x => x));
        Assert.Equal(75m, displayed);
        Assert.Equal(displayed, drill.RecomputedValue);

        // إعادة الإنتاج اليدويّة: متوسّط درجات الصفوف = الرقم المعروض.
        Assert.Equal(displayed, Math.Round(drill.Rows.Select(r => r.TotalScore!.Value).Average(), 2));

        Assert.All(drill.Rows, r =>
        {
            Assert.Equal(w.EmployeeId, r.SubjectUserId);
            Assert.False(string.IsNullOrWhiteSpace(r.TemplateTitle));
            Assert.Equal(KpiCadence.WeeklyPulse, r.Cadence);
            Assert.Equal(PeriodType.Weekly, r.PeriodType);
            Assert.Contains(r.PeriodKey, weeks);
            Assert.Equal(KpiEvaluationStatus.Approved, r.Status);
            Assert.True(r.PeriodStart <= r.PeriodEnd);
        });
    }

    /// <summary>
    /// §7/UAT/7 — Team denominator disclosure: عضو ثالث بلا أيّ تقييم يبقى <b>محسوبًا في المقام
    /// البشريّ</b> ومُعلَنًا، ولا يدخل المتوسّط صفرًا ولا يختفي منه.
    /// <para>
    /// الرقم وحده (60) لا يفرّق بين فريق من عضوين مغطّيين وفريق من ثلاثة ثلثه بلا بيانات؛
    /// والفرق بين <c>TotalMemberCount</c> و<c>ScoredMemberCount</c> هو ما يمنع قراءة تغطية ناقصة
    /// أداءً كاملًا.
    /// </para>
    /// </summary>
    [Fact]
    public async Task فحصUAT7_عضو_الفريق_بلا_بيانات_يُعلَن_في_المقام_ولا_يدخل_المتوسّط_صفرًا()
    {
        var t = await TeamWorldAsync();
        var (_, emp3) = await TestAuth.CreateUserWithJobRoleCodeAsync(
            _factory, "Employee", t.RoleCode, t.ManagerId);
        await SetUserOrgAsync(emp3, t.DepartmentId, t.TeamId);

        var perf = await PerfAsync(t.Manager, t.Window);

        var third = perf.Employees.Single(e => e.UserId == emp3);
        Assert.Null(third.Measure.Value);
        Assert.Equal(KpiDataQuality.NoData, third.Measure.DataQuality);
        Assert.False(third.EligibleForRanking);

        var team = perf.Teams.Single(g => g.GroupId == t.TeamId);
        Assert.Equal((2, 2, 3), (team.ScoredMemberCount, team.QualifiedMemberCount, team.TotalMemberCount));
        Assert.Equal(60m, team.Measure.Value); // لا (80+40+0)÷3 = 40
    }

    // ═════════ R6.1/§1.2 — إغلاق IC-10: دورة حياة السجلّ الربعيّ **القائم** ═════════

    /// <summary>
    /// التحوّلات السبع التي تُغيّر أعمدة صفّ التقييم نفسه. لكلٍّ منها الحالة السابقة التي تجعل
    /// فحص الحالة القائم <b>ينجح</b> — وإلّا صار الاختبار قياسًا لـ<c>not_reviewable.conflict</c>
    /// لا لإقفال المسار الإرثيّ.
    /// </summary>
    public static TheoryData<string> LifecycleTransitions => new()
    {
        "results", "submit", "approve", "request-revision", "reject", "reopen", "admin-delete"
    };

    private sealed record LegacyWorld(
        HttpClient Evaluator, Guid EvaluatorId, HttpClient Reviewer, Guid ReviewerId,
        Guid EmployeeId, Guid QuarterlyTemplateId, Guid WeeklyTemplateId, Guid ManualId, Guid AutoId);

    /// <summary>
    /// عالَم فيه قالبان منشوران — ربعيّ إرثيّ وأسبوعيّ — على نفس الموظّف، ومُدخِل ومُراجِع
    /// مُصعَّد (GM). الازدواج مقصود: الضابط السالب يجب أن يجري على نفس المستخدمين ونفس الصلاحيات
    /// ونفس الحالات، فلا يبقى فارق بين الحالتين إلّا <c>PeriodType</c> وحده.
    /// </summary>
    private async Task<LegacyWorld> LegacyWorldAsync()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (evaluator, evaluatorId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (reviewer, reviewerId) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var code = $"R61_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(
            _factory, "Employee", code, evaluatorId);
        var (quarterly, _, _) = await PublishAsync(admin, KpiCadence.Quarterly, role);
        var (weekly, manual, auto) = await PublishAsync(admin, KpiCadence.WeeklyPulse, role);
        return new LegacyWorld(
            evaluator, evaluatorId, reviewer, reviewerId, employee, quarterly, weekly, manual, auto);
    }

    /// <summary>
    /// يبذر صفًّا كامل الإسناد (مُدخِل + مُراجِع) بحالة محدَّدة. الإسناد إلزاميّ هنا: بدونه يُردّ
    /// الطلب بـ<c>auth.forbidden</c> فيمرّ الاختبار لسبب خاطئ ولا يمسّ الحارس الإرثيّ أصلًا.
    /// والبذر المباشر ضرورة لأنّ §5.4 أقفل إنشاء الربعيّ عبر الـAPI (انظر <see cref="SeedRowAsync"/>).
    /// </summary>
    private async Task<Guid> SeedLifecycleRowAsync(
        LegacyWorld w, PeriodType periodType, string periodKey, KpiEvaluationStatus status)
    {
        var templateId = periodType == PeriodType.Quarterly ? w.QuarterlyTemplateId : w.WeeklyTemplateId;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var versionId = await db.KpiTemplateVersions.Where(v => v.KpiTemplateId == templateId)
            .OrderByDescending(v => v.VersionNumber).Select(v => v.Id).FirstAsync();
        var row = new KpiEvaluation
        {
            KpiTemplateVersionId = versionId,
            SubjectUserId = w.EmployeeId,
            EvaluatorId = w.EvaluatorId,
            ReviewerId = w.ReviewerId,
            PeriodType = periodType,
            PeriodKey = periodKey,
            Status = status,
            TotalScore = status == KpiEvaluationStatus.Approved ? 70m : null,
            SubmittedAtUtc = status == KpiEvaluationStatus.Draft ? null : DateTime.UtcNow,
            ReviewedAtUtc = status == KpiEvaluationStatus.Approved ? DateTime.UtcNow : null
        };
        db.KpiEvaluations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static KpiEvaluationStatus PreStateFor(string transition) => transition switch
    {
        "results" or "submit" => KpiEvaluationStatus.Draft,
        "approve" or "request-revision" or "reject" => KpiEvaluationStatus.UnderReview,
        _ => KpiEvaluationStatus.Approved // reopen · admin-delete
    };

    private async Task<HttpResponseMessage> InvokeTransitionAsync(LegacyWorld w, string transition, Guid row)
    {
        var reason = new KpiReviewActionRequest("سبب مقيس");
        return transition switch
        {
            "results" => await w.Evaluator.PutAsJsonAsync($"/api/kpi-evaluations/{row}/results",
                new SaveKpiResultsRequest(new[] { new KpiResultInput(w.ManualId, null, 90m, null) })),
            "submit" => await w.Evaluator.PostAsync($"/api/kpi-evaluations/{row}/submit", null),
            "approve" => await w.Reviewer.PostAsync($"/api/kpi-evaluations/{row}/approve", null),
            "request-revision" => await w.Reviewer.PostAsJsonAsync(
                $"/api/kpi-evaluations/{row}/request-revision", reason),
            "reject" => await w.Reviewer.PostAsJsonAsync($"/api/kpi-evaluations/{row}/reject", reason),
            "reopen" => await w.Reviewer.PostAsJsonAsync($"/api/kpi-evaluations/{row}/reopen", reason),
            _ => await w.Reviewer.PostAsJsonAsync($"/api/kpi-evaluations/{row}/admin-delete", reason)
        };
    }

    /// <summary>
    /// R6.1/§1.2/1 — كلّ تحوّل تشغيليّ على تقييم ربعيّ <b>قائم</b> يُردّ 400 برمز الكتابة المتقاعدة.
    /// هذه هي الثغرة التي تركها §5.4: الإنشاء أُقفل، فبقي الصفّ الربعيّ القائم قابلًا للإرسال
    /// والاعتماد — أي أنّ المسار المتقاعد ظلّ قادرًا على إنتاج <b>حقائق معتمَدة جديدة</b>.
    /// </summary>
    [Theory]
    [MemberData(nameof(LifecycleTransitions))]
    public async Task إرث01_كلّ_تحوّل_تشغيليّ_على_تقييم_ربعيّ_قائم_مردود_400(string transition)
    {
        var w = await LegacyWorldAsync();
        var row = await SeedLifecycleRowAsync(
            w, PeriodType.Quarterly, "2025-Q2", PreStateFor(transition));

        var res = await InvokeTransitionAsync(w, transition, row);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("legacy_quarterly_write_disabled", await res.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// R6.1/§1.2/2 — <b>الضابط السالب</b>: نفس التحوّل، نفس المستخدمين، نفس الحالة السابقة،
    /// نفس الإسناد — على صفّ <c>Weekly</c> ⇒ ينجح. بدون هذا الضابط يبقى احتمال أن يكون الردّ
    /// في «إرث01» أثرَ إعدادٍ ناقص لا أثرَ الحارس، فيصير الاختبار خاليًا من المعنى.
    /// </summary>
    [Theory]
    [MemberData(nameof(LifecycleTransitions))]
    public async Task إرث02_ضابط_سالب_نفس_التحوّل_على_صفّ_أسبوعيّ_مطابق_ينجح(string transition)
    {
        var w = await LegacyWorldAsync();
        var row = await SeedLifecycleRowAsync(
            w, PeriodType.Weekly, TestCalendar.Cycle(1), PreStateFor(transition));

        var res = await InvokeTransitionAsync(w, transition, row);

        Assert.True(res.IsSuccessStatusCode,
            $"التحوّل «{transition}» على صفّ أسبوعيّ مطابق يجب أن ينجح؛ الردّ: "
            + $"{(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// R6.1/§1.2/3 — الردّ ليس شكليًّا: بعد المحاولات السبع لا يتغيّر عمود واحد في الصفّ الربعيّ
    /// ولا يُضاف حدث مراجعة واحد. هذا هو إثبات «لا تغيّر بيانات قائمة» و«لا تحذفه ماديًّا» معًا.
    /// </summary>
    [Fact]
    public async Task إرث03_صفر_تغيير_على_الصفّ_الربعيّ_بعد_كلّ_المحاولات_المردودة()
    {
        var w = await LegacyWorldAsync();
        // مفاتيح ربع متمايزة: الفهرس الفريد (النسخة، الموظّف، المفتاح) يمنع تكرار المفتاح لنفس الصفّ.
        var quarters = new[] { "2023-Q1", "2023-Q2", "2023-Q3", "2023-Q4", "2024-Q1", "2024-Q2", "2024-Q3" };
        var transitions = new[]
            { "results", "submit", "approve", "request-revision", "reject", "reopen", "admin-delete" };
        var rows = new Dictionary<string, Guid>();
        for (var i = 0; i < transitions.Length; i++)
            rows[transitions[i]] = await SeedLifecycleRowAsync(
                w, PeriodType.Quarterly, quarters[i], PreStateFor(transitions[i]));

        var before = await SnapshotAsync(rows.Values);

        foreach (var (transition, row) in rows)
            Assert.Equal(HttpStatusCode.BadRequest, (await InvokeTransitionAsync(w, transition, row)).StatusCode);

        Assert.Equal(before, await SnapshotAsync(rows.Values));
    }

    /// <summary>بصمة الأعمدة الحاكمة + عدد أحداث المراجعة + عدد النتائج، مقصورة على صفوف الاختبار وحدها.</summary>
    private async Task<string> SnapshotAsync(IEnumerable<Guid> rowIds)
    {
        var ids = rowIds.OrderBy(x => x).ToList();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.KpiEvaluations.IgnoreQueryFilters().AsNoTracking()
            .Where(e => ids.Contains(e.Id)).OrderBy(e => e.Id)
            .Select(e => new
            {
                e.Id, e.Status, e.TotalScore, e.IsDeleted, e.DeletedAtUtc, e.ReviewNote,
                e.ReviewerId, e.ReviewedAtUtc, e.SubmittedAtUtc, e.UpdatedAtUtc, e.PeriodType, e.PeriodKey,
                Events = db.KpiEvaluationReviewEvents.Count(v => v.KpiEvaluationId == e.Id),
                Results = db.KpiResults.Count(r => r.KpiEvaluationId == e.Id)
            })
            .ToListAsync();
        return string.Join("\n", rows.Select(r =>
            FormattableString.Invariant($"{r.Id}|{r.Status}|{r.TotalScore}|{r.IsDeleted}|{r.DeletedAtUtc:O}")
            + FormattableString.Invariant($"|{r.ReviewNote}|{r.ReviewerId}|{r.ReviewedAtUtc:O}")
            + FormattableString.Invariant($"|{r.SubmittedAtUtc:O}|{r.UpdatedAtUtc:O}|{r.PeriodType}|{r.PeriodKey}")
            + FormattableString.Invariant($"|ev={r.Events}|res={r.Results}")));
    }

    /// <summary>
    /// R6.1/§1.2/4 مُحدَّثًا بـR6.3/§2 — <b>القراءة</b> الأرشيفيّة تبقى مفتوحة بالكامل: الصفّ يُقرأ
    /// وسجلّ مراجعته القائم يُقرأ. أمّا <b>إلحاق</b> توثيق جديد (تعليق) فأُقفل: العقد الحاكم يقصر
    /// التعامل مع الربعيّ الإرثيّ على أرشيف إرثيّ مخوَّل، والمسار الحاليّ مفتوح لصلاحيّة تشغيليّة
    /// عامّة لا لأرشيف. كان هذا الاختبار يؤكّد نجاح التعليق (عقد R6.1)؛ بُدِّل التأكيد لأنّ العقد
    /// نفسه تغيّر بقرار المالك — والتغطية <b>ازدادت</b> لا نقصت: أُضيف إثبات صفر تغيير وصفر حدث.
    /// </summary>
    [Fact]
    public async Task إرث04_قراءة_الأرشيف_مفتوحة_وإلحاق_التوثيق_الجديد_مقفل_على_الصفّ_الربعيّ()
    {
        var w = await LegacyWorldAsync();
        var row = await SeedLifecycleRowAsync(
            w, PeriodType.Quarterly, "2023-Q4", KpiEvaluationStatus.Approved);

        // (1) القراءة الأرشيفيّة — مفتوحة كما هي.
        var read = await w.Reviewer.GetAsync($"/api/kpi-evaluations/{row}");
        read.EnsureSuccessStatusCode();
        Assert.Equal(PeriodType.Quarterly, (await read.ReadAsync<KpiEvaluationDto>())!.PeriodType);

        (await w.Reviewer.GetAsync($"/api/kpi-evaluations/{row}/review-events")).EnsureSuccessStatusCode();

        // (2) إلحاق تعليق جديد — مردود 400 برمز الكتابة نفسه، بلا أثر.
        var before = await SnapshotAsync(new[] { row });
        var comment = await w.Reviewer.PostAsJsonAsync(
            $"/api/kpi-evaluations/{row}/comment", new KpiReviewActionRequest("ملاحظة أرشيفيّة"));

        Assert.Equal(HttpStatusCode.BadRequest, comment.StatusCode);
        Assert.Equal("legacy_quarterly_write_disabled", await ProblemTypeAsync(comment));
        Assert.Equal(before, await SnapshotAsync(new[] { row }));

        // (3) الصفّ لم يُحذف ولم تتغيّر حالته — الحفظ التاريخيّ قائم (WS-2).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var after = await db.KpiEvaluations.IgnoreQueryFilters().AsNoTracking().FirstAsync(e => e.Id == row);
        Assert.Equal(KpiEvaluationStatus.Approved, after.Status);
        Assert.False(after.IsDeleted);
    }

    /// <summary>
    /// R6.1/§1.1 — القرار المعتمَد: رمزان متمايزان، وكلاهما <b>400</b> بعقد أخطاء واحد
    /// (<c>ProblemDetails</c> بـ<c>type</c> = رمز الخطأ). الكتابة — إنشاءً كانت أم تحوّلًا —
    /// رمزها <c>legacy_quarterly_write_disabled</c>، والقراءة رمزها <c>legacy_cadence_disabled</c>.
    /// </summary>
    [Fact]
    public async Task إرث05_عقد_الأخطاء_موحّد_400_ورمزا_الكتابة_والقراءة_متمايزان()
    {
        var w = await LegacyWorldAsync();
        var week = TestCalendar.Cycle(1);
        var row = await SeedLifecycleRowAsync(
            w, PeriodType.Quarterly, "2023-Q3", KpiEvaluationStatus.UnderReview);

        var write = await w.Reviewer.PostAsync($"/api/kpi-evaluations/{row}/approve", null);
        var read = await w.Reviewer.GetAsync(
            $"/api/kpi/performance?periodType=Week&periodKey={week}&cadence=Quarterly");

        Assert.Equal(HttpStatusCode.BadRequest, write.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, read.StatusCode);
        Assert.Equal("legacy_quarterly_write_disabled", await ProblemTypeAsync(write));
        Assert.Equal("legacy_cadence_disabled", await ProblemTypeAsync(read));
    }

    /// <summary>
    /// R6.2/§1.2 (قرار المالك) — <c>RequestReopen</c> على تقييم ربعيّ إرثيّ مقفل.
    ///
    /// المبرّر: بعد إقفال <c>reopen</c> نفسه في R6.1 لم يعد للطلب مآل ممكن، فيُنتج إشعارًا لمسار
    /// لا يُكمَل (<i>dead-end workflow</i>). الاختبار يُثبت الخمسة المطلوبة معًا لا واحدة منها:
    /// <c>400</c> · <c>legacy_quarterly_write_disabled</c> · صفر إشعار جديد · صفر حدث مراجعة جديد ·
    /// صفر تغيير على الصفّ.
    ///
    /// <b>الضابط السالب داخل الاختبار نفسه</b>: النداء عينه على صفّ <b>أسبوعيّ</b> مطابق (نفس
    /// المستخدم ونفس الصلاحية ونفس الحالة) يجب أن ينجح ويُنتج إشعارًا وحدث مراجعة — وإلّا كان الرفض
    /// الربعيّ رفضًا لسبب آخر (صلاحية أو حالة) والاختبار أخضر فارغ.
    /// </summary>
    [Fact]
    public async Task إرث06_طلب_إعادة_الفتح_على_الربعيّ_مردود_بلا_إشعار_ولا_حدث_ولا_تغيير()
    {
        var w = await LegacyWorldAsync();
        var quarterly = await SeedLifecycleRowAsync(
            w, PeriodType.Quarterly, "2023-Q2", KpiEvaluationStatus.Approved);
        var weekly = await SeedLifecycleRowAsync(
            w, PeriodType.Weekly, TestCalendar.Cycle(1), KpiEvaluationStatus.Approved);

        const string reopenType = "kpi.reopen_requested";

        // السبب فريد لكلّ تشغيل ⇒ عدّ الإشعارات يقتصر على ما أنتجه هذا الاختبار وحده،
        // فلا يتأثّر بتشغيل متوازٍ لاختبارات أخرى على القاعدة المشتركة نفسها.
        var reason = $"طلب إعادة فتح لسجلّ ربعيّ إرثيّ {Guid.NewGuid():N}";
        var body = new KpiReviewActionRequest(reason);
        var before = await SnapshotAsync(new[] { quarterly });

        var denied = await w.Reviewer.PostAsJsonAsync(
            $"/api/kpi-evaluations/{quarterly}/request-reopen", body);

        // (1) و(2) — الحالة والرمز.
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal("legacy_quarterly_write_disabled", await ProblemTypeAsync(denied));

        // (3) — صفر إشعار جديد.
        Assert.Equal(0, Count(db => db.Notifications.Count(n => n.Type == reopenType && n.Body == reason)));

        // (4) — صفر حدث مراجعة وصفر سطر تدقيق على الصفّ الربعيّ.
        Assert.Equal(0, Count(db => db.KpiEvaluationReviewEvents.Count(v => v.KpiEvaluationId == quarterly)));
        Assert.Equal(0, Count(db => db.AuditLogs.Count(a => a.EntityId == quarterly && a.Action == reopenType)));

        // (5) — الصفّ نفسه لم يتغيّر بأيّ عمود.
        Assert.Equal(before, await SnapshotAsync(new[] { quarterly }));

        // الضابط السالب: نفس النداء على صفّ أسبوعيّ مطابق ينجح ويُنتج الأثرَين.
        var allowed = await w.Reviewer.PostAsJsonAsync(
            $"/api/kpi-evaluations/{weekly}/request-reopen", body);
        Assert.True(
            allowed.IsSuccessStatusCode,
            $"الضابط السالب فشل ⇒ الرفض الربعيّ غير مقيس: {allowed.StatusCode} — "
            + await allowed.Content.ReadAsStringAsync());
        Assert.Equal(1, Count(db => db.KpiEvaluationReviewEvents.Count(v => v.KpiEvaluationId == weekly)));
        Assert.True(
            Count(db => db.Notifications.Count(n => n.Type == reopenType && n.Body == reason)) > 0,
            "الضابط السالب لم يُنتج إشعارًا ⇒ عدّاد الإشعارات لا يقيس شيئًا.");
    }

    /// <summary>
    /// R6.3/§3 — الباذر لا يُنشئ ولا ينشر قالب تقييم ربعيًّا. القياس بالعنوان لا بالعدّ العامّ:
    /// عنوانا التعريفَين الربعيَّين في <c>TemplateSeeder.KpiDefs</c> يخصّان الباذر وحده ولا يُنشئهما
    /// اختبار آخر، بينما القوالب الربعيّة التي تنشئها الاختبارات عبر الـAPI مشروعة ولا تُقاس هنا.
    ///
    /// <b>الضابط الموجب</b>: قالب أسبوعيّ حاكم من الباذر نفسه يجب أن يكون موجودًا ومنشورًا —
    /// بدونه قد ينجح التأكيد لأنّ الباذر لم يعمل أصلًا لا لأنّ الربعيّ أُقفل.
    /// </summary>
    [Fact]
    public async Task إرث08_الباذر_لا_يبذر_قالبًا_ربعيًّا_ويبقى_يبذر_الأسبوعيّ_الحاكم()
    {
        string[] quarterlySeedTitles = { "مؤشرات مندوب المبيعات", "مؤشرات مشتري الإعلانات" };
        const string weeklySeedTitle = "النبض الأسبوعي العام";

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // (1) لا وجود لأيّ من العنوانين الربعيَّين — لا منشورًا ولا مسودّة.
        var seeded = await db.KpiTemplates.AsNoTracking()
            .Where(t => quarterlySeedTitles.Contains(t.Title))
            .Select(t => new { t.Title, t.Cadence, t.Status }).ToListAsync();
        Assert.Empty(seeded);

        // (2) الضابط الموجب: الأسبوعيّ الحاكم مبذور ومنشور بدوريّة أسبوعيّة.
        var weekly = await db.KpiTemplates.AsNoTracking()
            .Where(t => t.Title == weeklySeedTitle)
            .Select(t => new { t.Id, t.Cadence, t.Status }).SingleAsync();
        Assert.Equal(KpiCadence.WeeklyPulse, weekly.Cadence);
        Assert.Equal(TemplateStatus.Published, weekly.Status);
        Assert.True(await db.KpiTemplateVersions.AsNoTracking()
            .AnyAsync(v => v.KpiTemplateId == weekly.Id && v.IsPublished));

        // (3) والباذر idempotent: تشغيله ثانيةً على القاعدة نفسها لا يُنشئ قالبًا ربعيًّا،
        // ولا يُكرّر الأسبوعيّ الحاكم (المعرّف نفسه لا معرّف جديد).
        await TemplateSeeder.SeedAsync(scope.ServiceProvider);
        Assert.Empty(await db.KpiTemplates.AsNoTracking()
            .Where(t => quarterlySeedTitles.Contains(t.Title)).ToListAsync());
        Assert.Equal(weekly.Id, await db.KpiTemplates.AsNoTracking()
            .Where(t => t.Title == weeklySeedTitle).Select(t => t.Id).SingleAsync());
    }

    public static TheoryData<string, string> AnnotationSurfaces => new()
    {
        { "comment", "kpi.review_comment" },
        { "flag", "kpi.flagged" },
    };

    /// <summary>
    /// R6.3/§2 — <c>Comment</c> و<c>Flag</c> مقفلان على التقييم الربعيّ الإرثيّ من المسارات
    /// التشغيليّة. المبرّر الحاكم ليس تغيّر الحالة (كلاهما <c>from == to</c>) بل **مصدر الصلاحيّة**:
    /// العقد يقصر التعامل مع الربعيّ الإرثيّ على أرشيف إرثيّ مخوَّل، وهذان المساران مفتوحان اليوم
    /// لصلاحيّات تشغيليّة عامّة (<c>KpiReviewers</c> يشمل Manager/TeamLeader · <c>KpiReviewFlaggers</c>
    /// يشمل Hr) ولا توجد صلاحيّة أرشيف في النظام أصلًا.
    ///
    /// الاختبار يُثبت أربعة معًا: <c>400</c> · الرمز القائم نفسه (لا ثالث) · صفر أثر (حدث مراجعة،
    /// سطر تدقيق، إشعار) · صفر تغيير على الصفّ بأيّ عمود بما فيه <c>Status</c> و<c>TotalScore</c>.
    /// و<b>الضابط السالب</b> في نفس الاختبار: النداء عينه على صفّ أسبوعيّ مطابق ينجح ويُنتج الأثر —
    /// بدونه قد ينجح التأكيد لأنّ المسار معطَّل كلّيًّا لا لأنّ الربعيّ مرفوض.
    /// </summary>
    [Theory]
    [MemberData(nameof(AnnotationSurfaces))]
    public async Task إرث07_التعليق_والإشارة_مقفلان_على_الربعيّ_ويعملان_على_الأسبوعيّ(
        string surface, string auditAction)
    {
        var w = await LegacyWorldAsync();
        var quarterly = await SeedLifecycleRowAsync(
            w, PeriodType.Quarterly, "2023-Q3", KpiEvaluationStatus.Approved);
        var weekly = await SeedLifecycleRowAsync(
            w, PeriodType.Weekly, TestCalendar.Cycle(1), KpiEvaluationStatus.Approved);

        // السبب فريد لكلّ تشغيل ⇒ عدّادا التدقيق والإشعار يقيسان ما أنتجه هذا الاختبار وحده.
        var reason = $"توثيق مقيس {surface} {Guid.NewGuid():N}";
        var body = new KpiReviewActionRequest(reason);
        var before = await SnapshotAsync(new[] { quarterly });

        var denied = await w.Reviewer.PostAsJsonAsync(
            $"/api/kpi-evaluations/{quarterly}/{surface}", body);

        // (1) و(2) — الحالة والرمز القائم نفسه.
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal("legacy_quarterly_write_disabled", await ProblemTypeAsync(denied));

        // (3) — صفر حدث مراجعة وصفر سطر تدقيق وصفر إشعار على الصفّ الربعيّ.
        Assert.Equal(0, Count(db => db.KpiEvaluationReviewEvents.Count(v => v.KpiEvaluationId == quarterly)));
        Assert.Equal(0, Count(db => db.AuditLogs.Count(a => a.EntityId == quarterly && a.Action == auditAction)));
        Assert.Equal(0, Count(db => db.Notifications.Count(n => n.Body == reason)));

        // (4) — الصفّ نفسه لم يتغيّر بأيّ عمود (الحالة والدرجة والحذف ضمن البصمة).
        Assert.Equal(before, await SnapshotAsync(new[] { quarterly }));

        // الضابط السالب: نفس النداء على صفّ أسبوعيّ مطابق ينجح ويُنتج حدث المراجعة وسطر التدقيق.
        var allowed = await w.Reviewer.PostAsJsonAsync(
            $"/api/kpi-evaluations/{weekly}/{surface}", body);
        Assert.True(
            allowed.IsSuccessStatusCode,
            $"الضابط السالب فشل ⇒ الرفض الربعيّ غير مقيس: {allowed.StatusCode} — "
            + await allowed.Content.ReadAsStringAsync());
        Assert.Equal(1, Count(db => db.KpiEvaluationReviewEvents.Count(v => v.KpiEvaluationId == weekly)));
        Assert.True(
            Count(db => db.AuditLogs.Count(a => a.EntityId == weekly && a.Action == auditAction)) > 0,
            "الضابط السالب لم يُنتج سطر تدقيق ⇒ عدّاد التدقيق لا يقيس شيئًا.");

        // والحالة الأسبوعيّة لم تتحرّك أيضًا: التوثيق توثيق لا تحوّل (from == to) حيثما كان مسموحًا.
        Assert.Equal(
            (int)KpiEvaluationStatus.Approved,
            Count(db => (int)db.KpiEvaluations.First(e => e.Id == weekly).Status));
    }

    private int Count(Func<AppDbContext, int> count)
    {
        using var scope = _factory.Services.CreateScope();
        return count(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("type").GetString();
    }
}
