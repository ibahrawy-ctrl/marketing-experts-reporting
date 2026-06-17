using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Reporting.Application.Auth;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Application.Kpi;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// حزمة اختبارات الأمان والاختراق الآمن (Phase 8) — تتحقّق خادميًا من:
/// المصادقة/الجلسة/الرمز، فصل الأدوار (RBAC)، منع IDOR عبر النطاق،
/// التحقّق من المدخلات، عدم تنفيذ XSS/SQLi، وحماية التصدير.
/// كلها غير مدمِّرة ولا تعتمد على أسماء مستخدمين ثابتة.
/// </summary>
[Collection("Integration")]
public class SecurityPenetrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public SecurityPenetrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ============== §6 المصادقة / الجلسة / الرمز ==============

    [Theory]
    [InlineData("/api/submissions")]
    [InlineData("/api/clients")]
    [InlineData("/api/projects")]
    [InlineData("/api/kpi-evaluations")]
    [InlineData("/api/risks")]
    [InlineData("/api/directory/users")]
    [InlineData("/api/reports/kpi-summary")]
    [InlineData("/api/auth/me")]
    public async Task Anonymous_ProtectedEndpoint_Returns401(string path)
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task MalformedToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.valid.jwt");
        var res = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task TamperedTokenSignature_Returns401()
    {
        var client = _factory.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin@marketingexperts.local", "Admin#12345")))
            .Content.ReadFromJsonAsync<AuthResponse>();
        // العبث بآخر محرفين من التوقيع.
        var tampered = login!.AccessToken[..^2] + (login.AccessToken[^1] == 'a' ? "bc" : "aa");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);
        var res = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task UnknownEmail_And_WrongPassword_ReturnSameUnifiedError_NoEnumeration()
    {
        var client = _factory.CreateClient();
        var bad = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin@marketingexperts.local", "WrongPass#9"));
        var unknown = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest($"ghost-{Guid.NewGuid():N}@nowhere.local", "WrongPass#9"));

        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        // الرسالة موحّدة لتفادي تعداد الحسابات (نقارن النص دون traceId المتغيّر).
        var badDetail = (await bad.Content.ReadFromJsonAsync<ProblemBody>())!.Detail;
        var unknownDetail = (await unknown.Content.ReadFromJsonAsync<ProblemBody>())!.Detail;
        Assert.False(string.IsNullOrWhiteSpace(badDetail));
        Assert.Equal(badDetail, unknownDetail);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_ReuseFails()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var login = await (await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin@marketingexperts.local", "Admin#12345")))
            .Content.ReadFromJsonAsync<AuthResponse>();
        var c2 = _factory.CreateClient();
        c2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var logout = await c2.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var reuse = await c2.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    // ============== §3 فصل الأدوار (RBAC) — دور خاطئ ⇐ 403 ==============

    [Fact]
    public async Task Employee_CannotReadGovernanceRisks_Returns403()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await emp.GetAsync("/api/risks");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Employee_CannotCreateReportTemplate_Returns403()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await emp.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest("محاولة غير مصرّح", null, null, PeriodType.Weekly));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Employee_CannotCreateKpiTemplate_Returns403()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await emp.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest("محاولة KPI غير مصرّح", null, null, KpiCadence.WeeklyPulse));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Employee_CannotListEvaluatableSubjects_Returns403()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await emp.GetAsync("/api/kpi-evaluations/evaluatable-subjects");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Employee_CannotAccessAuditLogs_Returns403()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await emp.GetAsync("/api/audit-logs");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Employee_CannotChangeUserRoles_Returns403()
    {
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await emp.PutAsJsonAsync($"/api/directory/users/{empId}/roles",
            new { roles = new[] { "Admin" } });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Employee_CannotCreateClientOrProject_Returns403()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var c = await emp.PostAsJsonAsync("/api/clients", new CreateClientRequest("عميل غير مصرّح"));
        var p = await emp.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(Guid.NewGuid(), "مشروع غير مصرّح", ServiceType.Seo));
        Assert.Equal(HttpStatusCode.Forbidden, c.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, p.StatusCode);
    }

    [Fact]
    public async Task TeamLeader_CannotManageTeam_Returns403()
    {
        // إدارة الفرق مقصورة على Admin/CEO/GM؛ قائد الفريق ممنوع.
        var (tl, _) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var res = await tl.PutAsJsonAsync($"/api/directory/teams/{Guid.NewGuid()}",
            new { nameAr = "محاولة", nameEn = (string?)null, departmentId = Guid.NewGuid(), teamLeaderId = (Guid?)null, isActive = true });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ============== §4 منع IDOR — وصول عبر النطاق ==============

    [Fact]
    public async Task Employee_CannotReadForeignSubmission_Returns403Or404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);

        // موظف آخر يقدّم تقريرًا، ثم موظف غريب (بلا علاقة) يحاول قراءته بالمعرّف.
        var (victim, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (attacker, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var sub = await SubmitAsync(victim, templateId, fieldId, "2026-W31");

        var res = await attacker.GetAsync($"/api/submissions/{sub.Id}");
        Assert.True(res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Employee_CannotReadForeignEmployeeProfile_Returns403()
    {
        var (victim, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (attacker, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var victimId = await CurrentUserIdAsync(victim);

        var res = await attacker.GetAsync($"/api/dashboard/employee-profile/{victimId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Manager_CannotCreateKpi_ForNonDirectReport_Returns403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _, _) = await PublishKpiAsync(admin);

        // مدير ليس له مرؤوس مباشر يطابق هذا الموظّف ⇐ خارج نطاق التقييم.
        var (mgr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var strangerId = await CurrentUserIdAsync(stranger);

        var res = await mgr.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, strangerId, PeriodType.Weekly, "2026-W31"));
        Assert.True(res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest);
    }

    // ============== §7 التحقّق من المدخلات ==============

    [Theory]
    [InlineData("٩٨٧غفقيبلا")]
    [InlineData("not-a-week")]
    [InlineData("2026W31")]
    [InlineData("' OR '1'='1")]
    public async Task KpiCreate_WithMalformedPeriodKey_IsRejected_NoServerError(string badKey)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _, _) = await PublishKpiAsync(admin);
        var (subject, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var subjectId = await CurrentUserIdAsync(subject);

        var res = await admin.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, badKey));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode); // مرفوض، وليس 500
    }

    [Fact]
    public async Task ClientCreate_WithEmptyName_IsRejected_NoServerError()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/clients", new CreateClientRequest("   "));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ============== §8 XSS / SQLi (آمن) ==============

    [Fact]
    public async Task XssPayload_InManagementNote_IsStoredVerbatim_NotSanitizedAwayNorExecuted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (target, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var targetId = await CurrentUserIdAsync(target);

        const string payload = "<script>alert(1)</script><img src=x onerror=alert(1)>";
        var created = await (await admin.PostAsJsonAsync("/api/management-notes",
            new CreateManagementNoteRequest(ManagementNoteEntityType.User, targetId,
                ManagementNoteType.Documentation, payload, false)))
            .ReadAsync<ManagementNoteDto>();

        Assert.NotNull(created);
        // يُخزَّن كنص بيانات حرفي (الهروب يحدث في طبقة العرض React) — لا تنفيذ خادمي ولا حقن.
        Assert.Equal(payload, created!.Body);

        // إزالة الأثر فورًا (تنظيف ذاتي).
        await admin.PostAsync($"/api/management-notes/{created.Id}/resolve", null);
    }

    [Fact]
    public async Task SqliPayload_AsSubmissionPeriodKey_DoesNotLeakDbError()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishWeeklyTemplateAsync(admin);

        var res = await admin.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "'; DROP TABLE submissions;--"));
        // EF Core يَستخدِم معاملات؛ لا 500 ولا تسريب رسالة قاعدة بيانات.
        Assert.NotEqual(HttpStatusCode.InternalServerError, res.StatusCode);
        var bodyText = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Npgsql", bodyText);
        Assert.DoesNotContain("syntax error", bodyText, StringComparison.OrdinalIgnoreCase);
    }

    // ============== §9 حماية التصدير ==============

    [Theory]
    [InlineData("/api/reports/submissions/export")]
    [InlineData("/api/reports/submission-completeness/export-pdf")]
    [InlineData("/api/reports/kpi-summary/export-pdf")]
    [InlineData("/api/reports/executive-summary/export-pdf")]
    public async Task Employee_CannotExport_Returns403(string path)
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await emp.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData("/api/reports/submissions/export")]
    [InlineData("/api/reports/kpi-summary/export-pdf")]
    public async Task Anonymous_CannotExport_Returns401(string path)
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ============== مساعدات ==============

    private sealed record ProblemBody(string? Title, string? Detail, string? Type, int? Status);

    private static async Task<Guid> CurrentUserIdAsync(HttpClient c)
    {
        var me = await (await c.GetAsync("/api/auth/me")).ReadAsync<MeResponse>();
        return me!.UserId;
    }

    private static async Task<SubmissionDto> SubmitAsync(HttpClient c, Guid templateId, Guid fieldId, string periodKey)
    {
        var draft = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey)))
            .ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 1000m, null, null, null) }));
        return (await (await c.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
    }

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishWeeklyTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"تقرير أمان {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private static async Task<(Guid TemplateId, Guid ManualMetricId, Guid AutoMetricId)> PublishKpiAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/kpi-templates",
            new CreateKpiTemplateRequest($"مؤشرات أمان {Guid.NewGuid():N}", null, null, KpiCadence.WeeklyPulse)))
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
}
