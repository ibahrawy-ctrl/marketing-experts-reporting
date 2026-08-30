using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// تصدير KPI للمالية (KPI-FIN1، مُحاذًى إلى DEC-01 §5: المصدر هو **المسار الربعيّ الرسميّ وحده**
/// ونبض الأسبوع لا يدخله). يغطّي: RBAC (قراءة/تصدير Admin/CEO/GM/HR/CeoSupport = 200؛
/// Manager/TL/Employee/Viewer = 403؛ Anonymous = 401)، الحالة المسموحة (Approved افتراضيًّا، Closed مدعوم،
/// Draft/InProgress/Submitted تُرفض 400)، احترام الفلاتر (السنة/الربع/الإدارة/الفريق)، الربع الفارغ
/// (معاينة فارغة + CSV ترويسة فقط)، الـCSV (BOM + ترويسات عربية)، عدم تغيير أيّ تقييم، والتدقيق على
/// التصدير فقط (kpi.finance_exported بلا أسماء/درجات). كلها على مستوى الشركة (بلا ScopeResolver)، قراءة بحتة.
/// </summary>
[Collection("Integration")]
public class KpiFinanceExportTests
{
    private readonly CustomWebApplicationFactory _factory;

    public KpiFinanceExportTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== هرمية الأدوار للـRBAC =====
    private sealed class Org
    {
        public required HttpClient Admin;
        public required HttpClient Ceo;
        public required HttpClient Gm;
        public required HttpClient Hr;
        public required HttpClient CeoSupport;
        public required HttpClient Manager;
        public required HttpClient Tl;
        public required HttpClient Emp;
        public required HttpClient Viewer;
    }

    private async Task<Org> BuildOrgAsync()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var ceo = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var hr = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var ceoSupport = await TestAuth.CreateUserAsync(_factory, Roles.CeoSupport);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Viewer);
        return new Org
        {
            Admin = admin,
            Ceo = ceo.Client,
            Gm = gm.Client,
            Hr = hr.Client,
            CeoSupport = ceoSupport.Client,
            Manager = manager.Client,
            Tl = tl.Client,
            Emp = emp.Client,
            Viewer = viewer.Client,
        };
    }

    // ===== مساعدات =====

    private const string PreviewUrl = "/api/kpi-evaluations/finance-export";
    private const string CsvUrl = "/api/kpi-evaluations/finance-export/csv";

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private static async Task<(Guid TemplateId, Guid ManualMetricId, Guid AutoMetricId)> PublishKpiAsync(
        HttpClient admin, KpiCadence cadence = KpiCadence.Quarterly)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"مؤشرات مالية {Guid.NewGuid():N}", null, null, cadence)))
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

    /// <summary>
    /// ينشئ تقييمًا بالمسار المطلوب (manual=auto=score)، يحفظ، يُرسل (⇒ UnderReview + إسناد مُراجِع)،
    /// ثم يعتمد عبر مُراجِع مُصعَّد (CEO؛ Admin/CEO/GM) ليس المُدخِل ولا الموضوع ⇒ Status=Approved.
    /// </summary>
    private async Task<Guid> ApproveAsync(
        HttpClient evaluator, Guid templateId, Guid subjectId, Guid manualId, Guid autoId, string periodKey, decimal score,
        PeriodType periodType = PeriodType.Quarterly)
    {
        var (approver, _) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        return await ApproveAsync(evaluator, approver, templateId, subjectId, manualId, autoId, periodKey, score, periodType);
    }

    private static async Task<Guid> ApproveAsync(
        HttpClient evaluator, HttpClient approver, Guid templateId, Guid subjectId, Guid manualId, Guid autoId,
        string periodKey, decimal score, PeriodType periodType = PeriodType.Quarterly)
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
        var approved = await (await approver.PostAsync($"/api/kpi-evaluations/{ev.Id}/approve", null))
            .ReadAsync<KpiEvaluationDto>();
        Assert.Equal(KpiEvaluationStatus.Approved, approved!.Status);
        return ev.Id;
    }

    /// <summary>ينشئ تقييمًا ربعيًّا ويُرسله فقط (بلا اعتماد) ⇒ Status=Submitted.</summary>
    private static async Task<Guid> SubmitQuarterlyAsync(
        HttpClient evaluator, Guid templateId, Guid subjectId, Guid manualId, Guid autoId, string quarterKey, decimal score)
    {
        var ev = await (await evaluator.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Quarterly, quarterKey)))
            .ReadAsync<KpiEvaluationDto>();
        await evaluator.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, score, null),
                new KpiResultInput(autoId, score, null, null)
            }));
        await evaluator.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null);
        return ev.Id;
    }

    private static async Task<KpiFinanceExportDto> PreviewAsync(HttpClient c, string query)
        => (await (await c.GetAsync(PreviewUrl + query)).ReadAsync<KpiFinanceExportDto>())!;

    private static async Task<(byte[] Bytes, string Text)> CsvAsync(HttpClient c, string query)
    {
        var res = await c.GetAsync(CsvUrl + query);
        res.EnsureSuccessStatusCode();
        var bytes = await res.Content.ReadAsByteArrayAsync();
        // النصّ بعد تخطّي BOM (3 بايتات).
        var text = bytes.Length >= 3 ? Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3) : string.Empty;
        return (bytes, text);
    }

    /// <summary>يُسنِد للموظّف إدارةً وفريقًا جديدين (قبل إنشاء التقييم كي يلتقطهما التقييم).</summary>
    private async Task<(Guid DeptId, Guid TeamId)> AssignSubjectOrgAsync(Guid subjectId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة KPI {Guid.NewGuid():N}", IsActive = true };
        db.Set<Department>().Add(dept);
        var team = new Team { NameAr = $"فريق KPI {Guid.NewGuid():N}", DepartmentId = dept.Id, IsActive = true };
        db.Set<Team>().Add(team);
        var u = await db.Users.FirstAsync(x => x.Id == subjectId);
        u.DepartmentId = dept.Id;
        u.TeamId = team.Id;
        await db.SaveChangesAsync();
        return (dept.Id, team.Id);
    }

    private async Task SetClosedAsync(Guid evalId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var e = await db.KpiEvaluations.FirstAsync(x => x.Id == evalId);
        e.Status = KpiEvaluationStatus.Closed;
        await db.SaveChangesAsync();
    }

    private int CountFinanceAudits()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.AuditLogs.Count(a => a.Action == "kpi.finance_exported");
    }

    private (string? DataJson, DateTime CreatedAtUtc)? LatestFinanceAudit()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = db.AuditLogs.Where(a => a.Action == "kpi.finance_exported")
            .OrderByDescending(a => a.CreatedAtUtc).FirstOrDefault();
        return log is null ? null : (log.DataJson, log.CreatedAtUtc);
    }

    // ===== 1) RBAC معاينة: الأدوار المسموحة ⇒ 200 =====
    [Fact]
    public async Task Preview_AllowedRoles_200()
    {
        var org = await BuildOrgAsync();
        foreach (var c in new[] { org.Admin, org.Ceo, org.Gm, org.Hr, org.CeoSupport })
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"{PreviewUrl}?year=2026&quarter=2")).StatusCode);
    }

    // ===== 2) RBAC معاينة: الأدوار الممنوعة ⇒ 403 =====
    [Fact]
    public async Task Preview_ForbiddenRoles_403()
    {
        var org = await BuildOrgAsync();
        foreach (var c in new[] { org.Manager, org.Tl, org.Emp, org.Viewer })
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"{PreviewUrl}?year=2026&quarter=2")).StatusCode);
    }

    // ===== 3) RBAC معاينة: مجهول ⇒ 401 =====
    [Fact]
    public async Task Preview_Anonymous_401()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"{PreviewUrl}?year=2026&quarter=2")).StatusCode);
    }

    // ===== 4) RBAC تصدير CSV: الأدوار المسموحة ⇒ 200 =====
    [Fact]
    public async Task Csv_AllowedRoles_200()
    {
        var org = await BuildOrgAsync();
        foreach (var c in new[] { org.Admin, org.Ceo, org.Gm, org.Hr, org.CeoSupport })
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"{CsvUrl}?year=2026&quarter=2")).StatusCode);
    }

    // ===== 5) RBAC تصدير CSV: الأدوار الممنوعة ⇒ 403 =====
    [Fact]
    public async Task Csv_ForbiddenRoles_403()
    {
        var org = await BuildOrgAsync();
        foreach (var c in new[] { org.Manager, org.Tl, org.Emp, org.Viewer })
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"{CsvUrl}?year=2026&quarter=2")).StatusCode);
    }

    // ===== 6) RBAC تصدير CSV: مجهول ⇒ 401 =====
    [Fact]
    public async Task Csv_Anonymous_401()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"{CsvUrl}?year=2026&quarter=2")).StatusCode);
    }

    // ===== 7) الحالة الافتراضية = Approved فقط (تخفي المُرسَل غير المعتمَد) =====
    [Fact]
    public async Task Default_ApprovedOnly()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);
        var (_, approvedSubject) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, submittedSubject) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var approvedId = await ApproveAsync(org.Admin, templateId, approvedSubject, manualId, autoId, "2026-Q2", 80m);
        var submittedId = await SubmitQuarterlyAsync(org.Admin, templateId, submittedSubject, manualId, autoId, "2026-Q2", 70m);

        var dto = await PreviewAsync(org.Admin, "?year=2026&quarter=2");
        Assert.Equal(KpiEvaluationStatus.Approved, dto.Status);
        Assert.Contains(dto.Rows, r => r.EvaluationId == approvedId);
        Assert.DoesNotContain(dto.Rows, r => r.EvaluationId == submittedId);
    }

    // ===== 8) Closed مدعوم: status=Closed يُظهر المغلق، والافتراضي (Approved) يخفيه =====
    [Fact]
    public async Task ClosedStatus_Supported()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);
        var (_, subject) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var id = await ApproveAsync(org.Admin, templateId, subject, manualId, autoId, "2026-Q2", 90m);
        await SetClosedAsync(id);

        var closed = await PreviewAsync(org.Admin, "?year=2026&quarter=2&status=Closed");
        Assert.Equal(KpiEvaluationStatus.Closed, closed.Status);
        Assert.Contains(closed.Rows, r => r.EvaluationId == id);

        var approved = await PreviewAsync(org.Admin, "?year=2026&quarter=2");
        Assert.DoesNotContain(approved.Rows, r => r.EvaluationId == id);
    }

    // ===== 9) الحالات الممنوعة (Draft/InProgress/Submitted) ⇒ 400 kpi_finance.status_invalid =====
    [Theory]
    [InlineData("Draft")]
    [InlineData("InProgress")]
    [InlineData("Submitted")]
    public async Task RejectsDisallowedStatus_400(string status)
    {
        var org = await BuildOrgAsync();
        var res = await org.Admin.GetAsync($"{PreviewUrl}?year=2026&quarter=2&status={status}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("kpi_finance.status_invalid", await ErrorCodeAsync(res));
    }

    // ===== 10) يحترم الربع =====
    [Fact]
    public async Task RespectsQuarter()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);
        var (_, subject) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var id = await ApproveAsync(org.Admin, templateId, subject, manualId, autoId, "2026-Q2", 65m);

        Assert.Contains((await PreviewAsync(org.Admin, "?year=2026&quarter=2")).Rows, r => r.EvaluationId == id);
        Assert.DoesNotContain((await PreviewAsync(org.Admin, "?year=2026&quarter=1")).Rows, r => r.EvaluationId == id);
    }

    // ===== 11) يحترم السنة =====
    [Fact]
    public async Task RespectsYear()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);
        var (_, subject) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var id = await ApproveAsync(org.Admin, templateId, subject, manualId, autoId, "2026-Q2", 55m);

        Assert.Contains((await PreviewAsync(org.Admin, "?year=2026&quarter=2")).Rows, r => r.EvaluationId == id);
        Assert.DoesNotContain((await PreviewAsync(org.Admin, "?year=2099&quarter=2")).Rows, r => r.EvaluationId == id);
    }

    // ===== 12) يحترم فلتر الإدارة =====
    [Fact]
    public async Task RespectsDepartmentFilter()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);
        var (_, subject) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (deptId, _) = await AssignSubjectOrgAsync(subject);
        var id = await ApproveAsync(org.Admin, templateId, subject, manualId, autoId, "2026-Q2", 75m);

        Assert.Contains((await PreviewAsync(org.Admin, $"?year=2026&quarter=2&departmentId={deptId}")).Rows, r => r.EvaluationId == id);
        Assert.DoesNotContain((await PreviewAsync(org.Admin, $"?year=2026&quarter=2&departmentId={Guid.NewGuid()}")).Rows, r => r.EvaluationId == id);
    }

    // ===== 13) يحترم فلتر الفريق =====
    [Fact]
    public async Task RespectsTeamFilter()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);
        var (_, subject) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, teamId) = await AssignSubjectOrgAsync(subject);
        var id = await ApproveAsync(org.Admin, templateId, subject, manualId, autoId, "2026-Q2", 85m);

        Assert.Contains((await PreviewAsync(org.Admin, $"?year=2026&quarter=2&teamId={teamId}")).Rows, r => r.EvaluationId == id);
        Assert.DoesNotContain((await PreviewAsync(org.Admin, $"?year=2026&quarter=2&teamId={Guid.NewGuid()}")).Rows, r => r.EvaluationId == id);
    }

    // ===== 14) ربع فارغ ⇒ معاينة فارغة (RowCount=0) =====
    [Fact]
    public async Task EmptyQuarter_PreviewEmpty()
    {
        var org = await BuildOrgAsync();
        var dto = await PreviewAsync(org.Admin, "?year=2099&quarter=1");
        Assert.Equal(0, dto.RowCount);
        Assert.Empty(dto.Rows);
    }

    // ===== 15) ربع فارغ ⇒ CSV ترويسة فقط مع BOM (بلا صفوف) =====
    [Fact]
    public async Task EmptyQuarter_CsvHeadersOnly_WithBom()
    {
        var org = await BuildOrgAsync();
        var (bytes, text) = await CsvAsync(org.Admin, "?year=2099&quarter=1");

        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);

        // سطر ترويسة واحد فقط (ينتهي بسطر جديد) بلا صفوف بيانات.
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Contains("اسم الموظف", lines[0]);
    }

    // ===== 16) CSV: BOM + الترويسات العربية بالترتيب وعنوان «تاريخ آخر تحديث / اعتماد» =====
    [Fact]
    public async Task Csv_HasBom_And_ArabicHeaders()
    {
        var org = await BuildOrgAsync();
        var (bytes, text) = await CsvAsync(org.Admin, "?year=2026&quarter=2");

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, new[] { bytes[0], bytes[1], bytes[2] });
        var header = text.Split('\n')[0];
        Assert.Equal(
            "اسم الموظف,الإدارة,الفريق,المسمى الوظيفي,نوع الفترة,مفتاح الفترة,السنة,الربع,القالب المستخدم,الدرجة النهائية,الحالة,تاريخ آخر تحديث / اعتماد",
            header.TrimEnd('\r'));
    }

    // ===== 17) CSV: صفّ التقييم يحمل الاسم والدرجة (معزول بفلتر الإدارة) =====
    [Fact]
    public async Task Csv_RowMatchesEvaluation()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);
        var (_, subject) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (deptId, _) = await AssignSubjectOrgAsync(subject);
        await ApproveAsync(org.Admin, templateId, subject, manualId, autoId, "2026-Q2", 80m);

        var (_, text) = await CsvAsync(org.Admin, $"?year=2026&quarter=2&departmentId={deptId}");
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // ترويسة + صفّ واحد
        Assert.Contains("مستخدم Employee", lines[1]);
        Assert.Contains("80", lines[1]);
        Assert.Contains("2026-Q2", lines[1]);
        Assert.Contains("Approved", lines[1]);
    }

    // ===== 18) التصدير لا يغيّر أيّ تقييم (الحالة/الدرجة قبل وبعد المعاينة والـCSV) =====
    [Fact]
    public async Task Export_DoesNotChangeEvaluation()
    {
        var org = await BuildOrgAsync();
        var (templateId, manualId, autoId) = await PublishKpiAsync(org.Admin);
        var (_, subject) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var id = await ApproveAsync(org.Admin, templateId, subject, manualId, autoId, "2026-Q2", 88m);

        var before = (await (await org.Admin.GetAsync($"/api/kpi-evaluations/{id}")).ReadAsync<KpiEvaluationDto>())!;

        await PreviewAsync(org.Admin, "?year=2026&quarter=2");
        await CsvAsync(org.Admin, "?year=2026&quarter=2");

        var after = (await (await org.Admin.GetAsync($"/api/kpi-evaluations/{id}")).ReadAsync<KpiEvaluationDto>())!;
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(KpiEvaluationStatus.Approved, after.Status);
        Assert.Equal(before.TotalScore, after.TotalScore);
        Assert.Equal(88m, after.TotalScore);
    }

    // ===== 19) التدقيق على التصدير فقط: المعاينة لا تُسجّل، الـCSV يُسجّل صفًّا واحدًا =====
    [Fact]
    public async Task Audit_OnCsvOnly()
    {
        var org = await BuildOrgAsync();

        var before = CountFinanceAudits();
        await PreviewAsync(org.Admin, "?year=2026&quarter=2");
        Assert.Equal(before, CountFinanceAudits()); // المعاينة لا تُسجّل تدقيقًا

        await CsvAsync(org.Admin, "?year=2026&quarter=2");
        Assert.Equal(before + 1, CountFinanceAudits()); // التصدير يُسجّل صفًّا واحدًا
    }

    // ===== 20) محتوى التدقيق: المرشّحات وعدد الصفوف فقط — بلا أسماء أو درجات =====
    [Fact]
    public async Task Audit_Payload_NoNamesOrScores()
    {
        var org = await BuildOrgAsync();
        await CsvAsync(org.Admin, "?year=2026&quarter=2");

        var log = LatestFinanceAudit();
        Assert.NotNull(log);
        var json = log!.Value.DataJson!;
        // يحوي مفاتيح الفلاتر/العدد فقط.
        Assert.Contains("\"year\"", json);
        Assert.Contains("\"quarter\"", json);
        Assert.Contains("\"rowCount\"", json);
        // لا يحوي أيّ أسماء أو درجات (الأعمدة الحسّاسة).
        Assert.DoesNotContain("name", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("score", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("employee", json, StringComparison.OrdinalIgnoreCase);
    }

    // ===== 21) القيم الحدّية للسنة/الربع تُرفض (400) =====
    [Theory]
    [InlineData("year=2026&quarter=0")]
    [InlineData("year=2026&quarter=5")]
    [InlineData("year=1999&quarter=2")]
    public async Task InvalidYearOrQuarter_400(string query)
    {
        var org = await BuildOrgAsync();
        var res = await org.Admin.GetAsync($"{PreviewUrl}?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 22) DEC-01 §5 (DEF-R5-003) — التصدير المالي لا يستهلك نبض الأسبوع =====
    // نبض أسبوعيّ **معتمَد** داخل نفس الربع ولنفس الموظّف لا يظهر في التصدير ولا في الـCSV،
    // بينما تقييمه الربعيّ الرسميّ يظهر — والعمود «نوع الفترة» لا يحمل Weekly في أيّ صفّ.
    [Fact]
    public async Task DecOne_FinanceExport_ConsumesQuarterlyTrackOnly_NotWeeklyPulse()
    {
        var org = await BuildOrgAsync();
        var (_, subject) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (deptId, _) = await AssignSubjectOrgAsync(subject);

        var (quarterlyTemplate, qManual, qAuto) = await PublishKpiAsync(org.Admin, KpiCadence.Quarterly);
        var (weeklyTemplate, wManual, wAuto) = await PublishKpiAsync(org.Admin, KpiCadence.WeeklyPulse);

        var quarterlyId = await ApproveAsync(org.Admin, quarterlyTemplate, subject, qManual, qAuto, "2026-Q2", 90m);
        var pulseId = await ApproveAsync(org.Admin, weeklyTemplate, subject, wManual, wAuto, "2026-W25", 60m,
            PeriodType.Weekly);

        var dto = await PreviewAsync(org.Admin, $"?year=2026&quarter=2&departmentId={deptId}");
        Assert.Contains(dto.Rows, r => r.EvaluationId == quarterlyId);
        Assert.DoesNotContain(dto.Rows, r => r.EvaluationId == pulseId);
        Assert.All(dto.Rows, r => Assert.Equal(PeriodType.Quarterly, r.PeriodType));

        var (_, text) = await CsvAsync(org.Admin, $"?year=2026&quarter=2&departmentId={deptId}");
        Assert.Contains("2026-Q2", text);
        Assert.DoesNotContain("2026-W25", text);
    }

    // ===== 23) DEC-01 §5 (OBS-R5-02) — التصدير يرى ما يراه المتوسّط الرسميّ، لا أقلّ =====
    // سجلّ **من المسار الربعيّ** (قالبه Quarterly) أُنشئ بمفتاح دورة داخل الربع — كما تفعل السجلّات
    // السابقة لـDEC-01 — يجب أن يظهر في التصدير: تمييز المسار بتواتر القالب لا بشكل المفتاح.
    // ولولا ذلك لَاختلف الرقم الرسميّ عن الرقم المالي على البيانات نفسها، وهو خلط من نوع آخر.
    [Fact]
    public async Task DecOne_FinanceExport_IncludesQuarterlyTrackRow_EvenWhenKeyedByCycle()
    {
        var org = await BuildOrgAsync();
        var (_, subject) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (deptId, _) = await AssignSubjectOrgAsync(subject);

        var (quarterlyTemplate, qManual, qAuto) = await PublishKpiAsync(org.Admin, KpiCadence.Quarterly);
        var (weeklyTemplate, wManual, wAuto) = await PublishKpiAsync(org.Admin, KpiCadence.WeeklyPulse);

        var legacyQuarterlyId = await ApproveAsync(
            org.Admin, quarterlyTemplate, subject, qManual, qAuto, "2026-Q2", 77m, PeriodType.Quarterly);
        // ونبض أسبوعيّ معتمَد داخل الربع نفسه — يبقى خارج التصدير رغم تطابق المدى.
        var pulseId = await ApproveAsync(
            org.Admin, weeklyTemplate, subject, wManual, wAuto, "2026-W21", 60m, PeriodType.Weekly);

        // محاكاة السجلّ القديم: بعد DEC-01 لم يعد الـAPI يقبل إنشاء ربعيّ بمفتاح دورة، فالشكل
        // القديم لا يوجد إلّا في بيانات سابقة. نُنزِله مباشرة إلى القاعدة كما هو في الواقع.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var legacy = await db.KpiEvaluations.FirstAsync(e => e.Id == legacyQuarterlyId);
            legacy.PeriodKey = "2026-W20";
            await db.SaveChangesAsync();
        }

        var dto = await PreviewAsync(org.Admin, $"?year=2026&quarter=2&departmentId={deptId}");
        Assert.Contains(dto.Rows, r => r.EvaluationId == legacyQuarterlyId);
        Assert.DoesNotContain(dto.Rows, r => r.EvaluationId == pulseId);
    }
}
