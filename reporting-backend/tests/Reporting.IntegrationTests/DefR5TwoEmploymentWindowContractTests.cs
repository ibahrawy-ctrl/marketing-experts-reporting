using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Directory;
using Reporting.Application.Kpi;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// R5 — قرار مالك المنتج: <b>DEF-R5-002</b> «مصدر بيانات الالتحاق والخروج التشغيليّ» مُثبَتًا على واجهة HTTP فعليّة.
/// <list type="bullet">
/// <item>لا شاشة مستقلّة: الحقلان يُقرآن ويُكتبان على سطح إدارة الموظّف القائم (<c>directory/hr/users</c> + <c>PATCH …/employment-window</c>).</item>
/// <item>لا تحرير غير مصرَّح به: من لا يملك صلاحيّة إدارة بيانات الموظّف الأساسيّة يُمنَع.</item>
/// <item>سجلّ تدقيق بقيمة قبل/بعد ومنفِّذ وتوقيت — لا تعديل صامت.</item>
/// <item>تغيير التاريخ لا يعيد كتابة أيّ تقييم تاريخيّ؛ أثره على المقام وحده عند الفترة المطلوبة.</item>
/// <item>القيمة الفارغة حالة «غير مسجَّل» لا حالة خروج: من لم تنتهِ خدمته يبقى في المقام كاملًا.</item>
/// </list>
/// </summary>
[Collection("DecOneIsolated")]
public class DefR5TwoEmploymentWindowContractTests
{
    private readonly DecOneIsolatedFactory _factory;

    public DefR5TwoEmploymentWindowContractTests(DecOneIsolatedFactory factory) => _factory = factory;

    private const string Q = "2026-Q2";

    // ===================== أدوات مساعدة =====================

    private static async Task<(Guid TemplateId, Guid ManualId, Guid AutoId)> PublishWeeklyAsync(
        HttpClient admin, Guid jobRoleId)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"قالب DEF-R5-002 {Guid.NewGuid():N}", null, jobRoleId, KpiCadence.WeeklyPulse)))
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

    private static async Task<System.Text.Json.JsonDocument> ResolveAsync(HttpClient client, string type, string periodKey)
    {
        var res = await client.GetAsync($"/api/kpi/periods/resolve?type={type}&periodKey={periodKey}");
        res.EnsureSuccessStatusCode();
        return System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
    }

    private static DateOnly Date(System.Text.Json.JsonElement e, string prop) => DateOnly.ParseExact(
        e.GetProperty(prop).GetString()!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<string[]> WeekKeysAsync(HttpClient client, string type, string periodKey)
    {
        using var doc = await ResolveAsync(client, type, periodKey);
        return doc.RootElement.GetProperty("weekKeys").EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    private static async Task<(DateOnly Start, DateOnly End)> RangeAsync(HttpClient client, string type, string periodKey)
    {
        using var doc = await ResolveAsync(client, type, periodKey);
        var cur = doc.RootElement.GetProperty("current");
        return (Date(cur, "start"), Date(cur, "end"));
    }

    private static async Task<KpiEmployeeScoreDto> RowAsync(HttpClient c, string query, Guid userId)
    {
        var res = await c.GetAsync($"/api/kpi/performance?{query}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<KpiPerformanceDto>())!.Employees.Single(e => e.UserId == userId);
    }

    /// <summary>ما يراه سطح إدارة الموظّف القائم فعلًا — لا شاشة أخرى ولا نقطة قراءة أخرى.</summary>
    private static async Task<HrDirectoryUserDto> HrRowAsync(HttpClient c, Guid userId)
    {
        var res = await c.GetAsync("/api/directory/hr/users");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<List<HrDirectoryUserDto>>())!.Single(u => u.Id == userId);
    }

    private static Task<HttpResponseMessage> PatchWindowAsync(
        HttpClient c, Guid userId, DateOnly? hire, DateOnly? exit, string? notes = null)
        => c.PatchAsJsonAsync($"/api/directory/users/{userId}/employment-window",
            new UpdateUserEmploymentWindowRequest(hire, exit, notes));

    // ===================== الشرط 1+3 — السطح القائم + التدقيق بقيمة قبل/بعد =====================

    [Fact]
    public async Task تسجيل_نافذة_الخدمة_من_سطح_الموظّف_القائم_يُقرَأ_ويُدقَّق_بالقيمة_قبل_وبعد()
    {
        var (hr, hrId) = await TestAuth.CreateUserAsync(_factory, "HR");
        var (_, employee) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // قبل أيّ تسجيل: «غير مسجَّل» حالة معلَنة على السطح نفسه لا فراغ يُفسَّر خروجًا.
        var before = await HrRowAsync(hr, employee);
        Assert.Null(before.HireDate);
        Assert.Null(before.ExitDate);

        var first = new DateOnly(2026, 1, 4);
        var corrected = new DateOnly(2026, 2, 1);
        var exit = new DateOnly(2026, 6, 30);

        (await PatchWindowAsync(hr, employee, first, null)).EnsureSuccessStatusCode();
        (await PatchWindowAsync(hr, employee, corrected, exit, "تصحيح تاريخ المباشرة")).EnsureSuccessStatusCode();

        // القراءة من السطح القائم ذاته — لا نقطة نهاية ثانية ولا شاشة مستقلّة.
        var after = await HrRowAsync(hr, employee);
        Assert.Equal(corrected, after.HireDate);
        Assert.Equal(exit, after.ExitDate);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await db.AuditLogs
            .Where(a => a.Action == "user.employment_window.updated" && a.EntityId == employee)
            .OrderBy(a => a.CreatedAtUtc).ToListAsync();

        Assert.Equal(2, logs.Count);
        Assert.All(logs, l =>
        {
            Assert.Equal("User", l.EntityType);
            Assert.Equal(hrId, l.ActorId);                 // المنفِّذ مسمًّى
            Assert.NotEqual(default, l.CreatedAtUtc);      // والتوقيت مثبَت
        });

        // القيمة قبل وبعد كلتاهما في السجلّ — التصحيح مرئيّ لا صامت.
        // (التخزين jsonb فيُعاد ترتيب المفاتيح؛ القراءة تكون بالمفتاح لا بمطابقة النصّ.)
        using var data = System.Text.Json.JsonDocument.Parse(logs[1].DataJson!);
        var root = data.RootElement;
        Assert.Equal("2026-01-04", root.GetProperty("oldHireDate").GetString());
        Assert.Equal("2026-02-01", root.GetProperty("newHireDate").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, root.GetProperty("oldExitDate").ValueKind);
        Assert.Equal("2026-06-30", root.GetProperty("newExitDate").GetString());
        Assert.Equal("تصحيح تاريخ المباشرة", root.GetProperty("notes").GetString());
    }

    // ===================== الشرط 2 — لا تحرير غير مصرَّح به =====================

    [Fact]
    public async Task تعديل_نافذة_الخدمة_ممنوع_على_من_لا_يملك_صلاحيّة_إدارة_بيانات_الموظّف()
    {
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (self, employee) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var hire = new DateOnly(2026, 1, 4);

        // المدير المباشر يرى الأداء لكنّه لا يملك سلطة تعديل بيانات التوظيف.
        Assert.Equal(HttpStatusCode.Forbidden, (await PatchWindowAsync(manager, employee, hire, null)).StatusCode);
        // والموظّف نفسه لا يملك تعديل مقام تقييمه.
        Assert.Equal(HttpStatusCode.Forbidden, (await PatchWindowAsync(self, employee, hire, null)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == employee);
        Assert.Null(user.HireDate);   // المنع فعليّ لا شكليّ
        Assert.Null(user.ExitDate);
    }

    // ===================== سلامة القيمة — نافذة غير متّسقة مرفوضة برمز مسمًّى =====================

    [Fact]
    public async Task نافذة_الخدمة_غير_المتّسقة_تُرفَض_برمز_مسمًّى_ولا_تُكتَب()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, "HR");
        var (_, employee) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var noHire = await PatchWindowAsync(hr, employee, null, new DateOnly(2026, 6, 30));
        Assert.Equal(HttpStatusCode.BadRequest, noHire.StatusCode);
        Assert.Contains("user.employment.hire_required", await noHire.Content.ReadAsStringAsync());

        var reversed = await PatchWindowAsync(hr, employee, new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 4));
        Assert.Equal(HttpStatusCode.BadRequest, reversed.StatusCode);
        Assert.Contains("user.employment.range_invalid", await reversed.Content.ReadAsStringAsync());

        var row = await HrRowAsync(hr, employee);
        Assert.Null(row.HireDate);
        Assert.Null(row.ExitDate);
    }

    // ===================== الشرط 6 — من لم يخرج لا يُعامَل معاملة الخارج =====================

    [Fact]
    public async Task من_لم_تنتهِ_خدمته_يبقى_في_المقام_كاملًا_على_قيمة_فارغة()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_W1_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        await PublishWeeklyAsync(admin, role);

        var weeks = await WeekKeysAsync(manager, "Quarter", Q);
        var row = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);

        // لا تاريخ التحاق ولا تاريخ خروج ⇒ لا خصم في الطرفين: المقام هو الدورات كلّها.
        Assert.Equal(weeks.Length, row.Measure.ExpectedEvaluationCount);
        Assert.Equal(weeks.Length, row.Measure.AdjustedExpectedCount);
        Assert.True(row.Measure.AdjustedExpectedCount > 0);

        // وتسجيل تاريخ التحاق وحده (بلا خروج) لا يحوّله إلى خارجٍ عن الخدمة.
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, "HR");
        var q2 = await RangeAsync(manager, "Quarter", Q);
        (await PatchWindowAsync(hr, employee, q2.Start, null)).EnsureSuccessStatusCode();

        var afterHire = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);
        Assert.Equal(weeks.Length, afterHire.Measure.AdjustedExpectedCount);
    }

    // ===================== الشرطان 4+5 — أثر حسابيّ عند الفترة المطلوبة بلا إعادة كتابة تاريخيّة =====================

    [Fact]
    public async Task تغيير_نافذة_الخدمة_يضبط_مقام_الفترة_المطلوبة_ولا_يعيد_كتابة_تقييم_تاريخيّ()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var code = $"R5_W2_{Guid.NewGuid():N}";
        var role = await TestAuth.GetOrCreateJobRoleAsync(_factory, code);
        var (_, employee) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", code, managerId);
        var (templateId, manualId, autoId) = await PublishWeeklyAsync(admin, role);

        var weeks = await WeekKeysAsync(manager, "Quarter", Q);
        var first = await RangeAsync(manager, "Week", weeks[0]);
        var second = await RangeAsync(manager, "Week", weeks[1]);

        // تقييم معتمَد داخل الأسبوع الأوّل — هذه هي الحقيقة التاريخيّة التي يجب ألّا تُمسّ.
        var ev = await (await manager.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, employee, PeriodType.Weekly, weeks[0])))
            .ReadAsync<KpiEvaluationDto>();
        Assert.NotNull(ev);
        await manager.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 90m, null),
                new KpiResultInput(autoId, 80m, null, null)
            }));
        await manager.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null);
        var ceo = await TestAuth.LoginAsRoleAsync(_factory, "CEO");
        (await ceo.PostAsync($"/api/kpi-evaluations/{ev.Id}/approve", null)).EnsureSuccessStatusCode();

        var approved = (await (await manager.GetAsync($"/api/kpi-evaluations/{ev.Id}")).ReadAsync<KpiEvaluationDto>())!;
        Assert.Equal(KpiEvaluationStatus.Approved, approved.Status);
        Assert.NotNull(approved.TotalScore);

        var beforeWindow = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);
        Assert.Equal(weeks.Length, beforeWindow.Measure.AdjustedExpectedCount);

        // تسجيل نافذة الخدمة الحقيقيّة: التحاقٌ مع الأسبوع الأوّل وانتهاء خدمة بنهاية الأسبوع الثاني.
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, "HR");
        (await PatchWindowAsync(hr, employee, first.Start, second.End)).EnsureSuccessStatusCode();

        var afterWindow = await RowAsync(manager, $"periodType=Quarter&periodKey={Q}", employee);

        // المقام يُحسَب بالقيمة السارية عند كلّ دورة داخل الفترة المطلوبة — لا خارجها ولا بأثر عام.
        Assert.Equal(weeks.Length, afterWindow.Measure.ExpectedEvaluationCount);
        Assert.Equal(2, afterWindow.Measure.AdjustedExpectedCount);
        Assert.Equal(1, afterWindow.Measure.EligibleEvaluationCount);
        Assert.Equal(1, afterWindow.Measure.MissingCount);
        Assert.Equal(0.5m, afterWindow.Measure.Coverage);

        // والتقييم التاريخيّ نفسه لم يُمسّ: الحالة والدرجة كما اعتُمِدتا.
        var reread = (await (await manager.GetAsync($"/api/kpi-evaluations/{ev.Id}")).ReadAsync<KpiEvaluationDto>())!;
        Assert.Equal(KpiEvaluationStatus.Approved, reread.Status);
        Assert.Equal(approved.TotalScore, reread.TotalScore);
        Assert.Equal(weeks[0], reread.PeriodKey);
    }
}
