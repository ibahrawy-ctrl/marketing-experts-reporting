using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Common;
using Reporting.Application.Directory;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// Phase A — مصفوفة الصلاحيات المجمّعة الجديدة (عرض فقط) يجب أن تعكس <b>الحقيقة الفعلية</b> لكل دور،
/// مع تأكيد أن <b>التفويض لم يتغيّر</b> (HR لا يكتسب صلاحيات جديدة فعليًّا).
/// </summary>
[Collection("Integration")]
public class RoleMatrixCapabilitiesTests
{
    private readonly CustomWebApplicationFactory _factory;
    public RoleMatrixCapabilitiesTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string StatusOf(RoleAccessDto role, string capKey)
        => role.CapabilityGroups.SelectMany(g => g.Items).Single(i => i.Key == capKey).Status;

    private async Task<List<RoleAccessDto>> GetMatrixAsync()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var matrix = await (await client.GetAsync("/api/directory/role-matrix")).ReadAsync<List<RoleAccessDto>>();
        Assert.NotNull(matrix);
        return matrix!;
    }

    // (1) المصفوفة الجديدة تُظهر قدرات HR الفعلية كـ Active.
    [Fact]
    public async Task RoleMatrix_HR_RealCapabilities_AreActive()
    {
        var hr = (await GetMatrixAsync()).Single(r => r.Role == "HR");
        Assert.NotEmpty(hr.CapabilityGroups);
        foreach (var key in new[]
        {
            "leave.final_approval", "leave.review", "balances.manage", "balances.opening",
            "balances.adjust", "leave.revoke", "hr_requests.view", "hr_requests.process", "jobroles.manage",
        })
            Assert.Equal("Active", StatusOf(hr, key));
    }

    // (2,3) HR لا تظهر له صلاحيات الرؤية الواسعة/الحسّاسة كـ Active (مقترح لاحقًا أو قرار مستقل).
    [Fact]
    public async Task RoleMatrix_HR_BroadAndSensitive_AreNotActive()
    {
        var hr = (await GetMatrixAsync()).Single(r => r.Role == "HR");

        // رؤية واسعة غير ممنوحة الآن — تظهر «مقترح لاحقًا».
        Assert.Equal("ProposedLater", StatusOf(hr, "reports.view.all"));
        Assert.Equal("ProposedLater", StatusOf(hr, "reports.export"));
        Assert.Equal("ProposedLater", StatusOf(hr, "reports.analytics"));
        Assert.Equal("ProposedLater", StatusOf(hr, "kpi.view.company"));

        // اعتماد/إرجاع التقارير ليست لـ HR إطلاقًا.
        Assert.NotEqual("Active", StatusOf(hr, "reports.approve"));
        Assert.NotEqual("Active", StatusOf(hr, "reports.return"));

        // صلاحيات حسّاسة — قرار مستقل، ليست Active.
        Assert.Equal("SensitiveDecision", StatusOf(hr, "users.reset_password"));
        Assert.NotEqual("Active", StatusOf(hr, "users.manage"));
        Assert.NotEqual("Active", StatusOf(hr, "kpi.evaluate"));
    }

    // (7) Admin يظهر بصلاحياته الفعلية الكاملة.
    [Fact]
    public async Task RoleMatrix_Admin_CoreCapabilities_AreActive()
    {
        var admin = (await GetMatrixAsync()).Single(r => r.Role == "Admin");
        foreach (var key in new[]
        {
            "users.reset_password", "users.manage", "users.manage_roles", "positions.manage",
            "kpi.evaluate", "report_templates.manage", "reports.view.all", "audit.view",
        })
            Assert.Equal("Active", StatusOf(admin, key));
    }

    // (7) CeoSupport: أرصدة + مسمّيات + إعادة تعيين كلمة المرور Active؛ لا اعتماد ولا تقييم KPI.
    [Fact]
    public async Task RoleMatrix_CeoSupport_Correct()
    {
        var cs = (await GetMatrixAsync()).Single(r => r.Role == "CeoSupport");
        Assert.Equal("Active", StatusOf(cs, "balances.manage"));
        Assert.Equal("Active", StatusOf(cs, "jobroles.manage"));
        Assert.Equal("Active", StatusOf(cs, "users.reset_password"));
        Assert.Equal("Active", StatusOf(cs, "reports.export"));
        Assert.NotEqual("Active", StatusOf(cs, "reports.approve"));
        Assert.NotEqual("Active", StatusOf(cs, "kpi.evaluate"));
    }

    // (7) CEO/GM: اعتماد نهائي للإجازات + أرصدة + رؤية KPI شركة Active؛ لا إعادة تعيين كلمة مرور.
    [Theory]
    [InlineData("CEO")]
    [InlineData("GeneralManager")]
    public async Task RoleMatrix_CeoGm_Correct(string role)
    {
        var r = (await GetMatrixAsync()).Single(x => x.Role == role);
        Assert.Equal("Active", StatusOf(r, "leave.final_approval"));
        Assert.Equal("Active", StatusOf(r, "balances.manage"));
        Assert.Equal("Active", StatusOf(r, "hr_requests.process"));
        Assert.Equal("Active", StatusOf(r, "kpi.view.company"));
        Assert.Equal("Active", StatusOf(r, "reports.approve"));
        Assert.NotEqual("Active", StatusOf(r, "users.reset_password"));
    }

    // (2) المنطقة المستقبلية (People Operations) كلها مقترح لاحقًا لـ HR — غير مُفعّلة.
    [Fact]
    public async Task RoleMatrix_HR_FutureArea_IsProposedLater()
    {
        var hr = (await GetMatrixAsync()).Single(r => r.Role == "HR");
        var future = hr.CapabilityGroups.Single(g => g.Key == "future");
        Assert.NotEmpty(future.Items);
        Assert.All(future.Items, i => Assert.Equal("ProposedLater", i.Status));
    }

    // ── تأكيد أن التفويض لم يتغيّر (لا يكفي العرض) ───────────────────────

    // (5) HR لا يستطيع فعليًّا إعادة تعيين كلمة مرور — 403 (السياسة UserPasswordReset لم تتغيّر).
    [Fact]
    public async Task HR_Cannot_ResetPassword_StillForbidden()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, "HR");
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await hr.PostAsJsonAsync(
            $"/api/directory/users/{targetId}/reset-password", new { newPassword = "NewPassw0rd#1" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (3,4) HR لا يستطيع فعليًّا إدارة القوالب (TemplateGovernance) — 403 (لا اعتماد/إدارة فرق).
    [Fact]
    public async Task HR_Cannot_ManageTemplates_StillForbidden()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, "HR");
        var res = await hr.PostAsJsonAsync("/api/kpi-templates", new { });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (6) HR لا يستطيع فعليًّا فتح طابور تقييمات KPI الإدارية (ManagementOnly) — 403.
    [Fact]
    public async Task HR_Cannot_AccessEvaluatableSubjects_StillForbidden()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, "HR");
        var res = await hr.GetAsync("/api/kpi-evaluations/evaluatable-subjects");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (2) مطابقة العرض للحقيقة: IsActive في نموذج العرض يطابق عضوية الدور في مصفوفات الأدوار الفعلية.
    [Fact]
    public void DisplayModel_Mirrors_RealRoleArrays()
    {
        // أمثلة قاطعة من Roles.cs (مصدر السياسات) — لا يجوز أن ينحرف العرض عنها.
        Assert.True(RoleCapabilities.IsActive("HR", "leave.final_approval"));
        Assert.True(RoleCapabilities.IsActive("HR", "balances.manage"));
        Assert.True(RoleCapabilities.IsActive("HR", "jobroles.manage"));
        Assert.False(RoleCapabilities.IsActive("HR", "users.reset_password"));
        Assert.False(RoleCapabilities.IsActive("HR", "reports.approve"));
        Assert.True(RoleCapabilities.IsActive("Admin", "positions.manage"));
        Assert.False(RoleCapabilities.IsActive("CeoSupport", "reports.approve"));
    }
}
