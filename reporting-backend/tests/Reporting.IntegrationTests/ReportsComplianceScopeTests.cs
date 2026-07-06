using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// RPT-SCOPE1 — تقييد نطاق شاشة «متابعة الالتزام بالتقارير» (GET /reports/submission-compliance)
/// على الخادم عبر ScopeResolver. القاعدة: المرشّحون = النطاق المسموح ∩ الفلاتر الاختيارية (لا توسيع).
/// Admin/CEO/GM = كل الشركة؛ Manager = شجرته؛ TeamLeader = فريقه (مرؤوسوه المباشرون)؛
/// HR = على مستوى الشركة (استثناء موثّق)؛ FinanceManager/Accountant منفردين = 403 (خارج CompletionMonitors).
/// المعرّف المرجعي للنطاق = ManagerId (شجرة التسلسل) لا Team.TeamLeaderId.
/// </summary>
[Collection("Integration")]
public class ReportsComplianceScopeTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportsComplianceScopeTests(CustomWebApplicationFactory factory) => _factory = factory;

    // (1) Admin يرى كل المرشّحين عبر مدراء/إدارات مختلفة (كل الشركة).
    [Fact]
    public async Task Admin_SeesAllCandidates_AcrossManagers_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSA1");
        var (_, mId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var e1 = await CreateWeeklyEmployeeAsync(roleId, managerId: mId);
        var e2 = await CreateWeeklyEmployeeAsync(roleId); // غير مرتبط بأيّ مدير اختباري

        var report = await GetComplianceAsync(admin);
        Assert.Contains(report.Rows, r => r.UserId == e1);
        Assert.Contains(report.Rows, r => r.UserId == e2);
    }

    // (2) Manager يرى مرؤوسه المباشر (ManagerId == Manager).
    [Fact]
    public async Task Manager_SeesDirectSubordinate_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSM1");
        var (mgr, mId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var e1 = await CreateWeeklyEmployeeAsync(roleId, managerId: mId);

        var report = await GetComplianceAsync(mgr);
        Assert.Contains(report.Rows, r => r.UserId == e1);
    }

    // (3) Manager لا يرى موظفًا تابعًا لمدير آخر (خارج شجرته).
    [Fact]
    public async Task Manager_DoesNotSeeOtherManagerSubordinate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSM2");
        var (mgr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, otherMgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var e2 = await CreateWeeklyEmployeeAsync(roleId, managerId: otherMgrId);

        var report = await GetComplianceAsync(mgr);
        Assert.DoesNotContain(report.Rows, r => r.UserId == e2);
    }

    // (4) Manager لا يرى موظفًا في إدارة أخرى (لا ينتمي لشجرته).
    [Fact]
    public async Task Manager_DoesNotSeeOtherDepartmentUser()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSM3");
        var (mgr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var otherDept = await CreateDeptAsync();
        var e2 = await CreateWeeklyEmployeeAsync(roleId, deptId: otherDept); // بلا مدير اختباري، إدارة مختلفة

        var report = await GetComplianceAsync(mgr);
        Assert.DoesNotContain(report.Rows, r => r.UserId == e2);
    }

    // (5) TeamLeader يرى مرؤوسه المباشر (نطاق team = ManagerId == TL).
    [Fact]
    public async Task TeamLeader_SeesDirectReportOnly_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RST1");
        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var e1 = await CreateWeeklyEmployeeAsync(roleId, managerId: tlId);

        var report = await GetComplianceAsync(tl);
        Assert.Contains(report.Rows, r => r.UserId == e1);
    }

    // (6) TeamLeader لا يرى موظفًا خارج فريقه (تابع لقائد فريق آخر).
    [Fact]
    public async Task TeamLeader_DoesNotSeeOtherTeamMember()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RST2");
        var (tl, _) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, otherTlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var e2 = await CreateWeeklyEmployeeAsync(roleId, managerId: otherTlId);

        var report = await GetComplianceAsync(tl);
        Assert.DoesNotContain(report.Rows, r => r.UserId == e2);
    }

    // (7) FinanceManager منفردًا = 403 (ليس ضمن CompletionMonitors).
    [Fact]
    public async Task FinanceManager_Alone_403()
    {
        var (fm, _) = await TestAuth.CreateUserAsync(_factory, Roles.FinanceManager);
        var res = await fm.GetAsync("/api/reports/submission-compliance");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (8) Accountant منفردًا = 403 (ليس ضمن CompletionMonitors).
    [Fact]
    public async Task Accountant_Alone_403()
    {
        var (acc, _) = await TestAuth.CreateUserAsync(_factory, Roles.Accountant);
        var res = await acc.GetAsync("/api/reports/submission-compliance");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (9) FinanceManager + Manager = 200 ويرى نطاق المدير (مرؤوسه يظهر).
    [Fact]
    public async Task FinanceManagerPlusManager_SeesManagerScope_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSF1");
        var (client, mId) = await CreateUserWithRolesAsync(Roles.FinanceManager, Roles.Manager);
        var e1 = await CreateWeeklyEmployeeAsync(roleId, managerId: mId);

        var res = await client.GetAsync("/api/reports/submission-compliance");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var report = await res.ReadAsync<SubmissionComplianceReport>();
        Assert.Contains(report!.Rows, r => r.UserId == e1);
    }

    // (10) FinanceManager + Manager لا يرى موظفًا خارج نطاق المدير (الدور المالي لا يوسّع).
    [Fact]
    public async Task FinanceManagerPlusManager_DoesNotSeeOutOfScope()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSF2");
        var (client, _) = await CreateUserWithRolesAsync(Roles.FinanceManager, Roles.Manager);
        var (_, otherMgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var e2 = await CreateWeeklyEmployeeAsync(roleId, managerId: otherMgrId);

        var report = await GetComplianceAsync(client);
        Assert.DoesNotContain(report.Rows, r => r.UserId == e2);
    }

    // (11) FinanceManager + TeamLeader = 200 ويرى نطاق قائد الفريق فقط.
    [Fact]
    public async Task FinanceManagerPlusTeamLeader_SeesTeamLeaderScope_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSF3");
        var (client, tlId) = await CreateUserWithRolesAsync(Roles.FinanceManager, Roles.TeamLeader);
        var e1 = await CreateWeeklyEmployeeAsync(roleId, managerId: tlId);
        var (_, otherTlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var e2 = await CreateWeeklyEmployeeAsync(roleId, managerId: otherTlId);

        var res = await client.GetAsync("/api/reports/submission-compliance");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var report = await res.ReadAsync<SubmissionComplianceReport>();
        Assert.Contains(report!.Rows, r => r.UserId == e1);
        Assert.DoesNotContain(report.Rows, r => r.UserId == e2);
    }

    // (12) فلتر الإدارة داخل النطاق يعمل: مدير يفلتر بإدارة مرؤوسه ⇒ يظهر.
    [Fact]
    public async Task DepartmentFilter_InScope_Works()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSD1");
        var (mgr, mId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var deptX = await CreateDeptAsync();
        var e1 = await CreateWeeklyEmployeeAsync(roleId, managerId: mId, deptId: deptX);

        var report = await GetComplianceAsync(mgr, departmentId: deptX);
        Assert.Contains(report.Rows, r => r.UserId == e1);
    }

    // (13) فلتر إدارة خارج النطاق لا يجلب بيانات خارج النطاق (النتيجة = النطاق ∩ الفلتر).
    [Fact]
    public async Task DepartmentFilter_OutOfScope_ReturnsNoOutOfScopeData()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSD2");
        var (mgr, mId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var deptX = await CreateDeptAsync();
        var e1 = await CreateWeeklyEmployeeAsync(roleId, managerId: mId, deptId: deptX);
        var deptY = await CreateDeptAsync();
        var e2 = await CreateWeeklyEmployeeAsync(roleId, deptId: deptY); // خارج نطاق المدير، إدارة Y

        // المدير يفلتر بإدارة Y (خارج نطاقه): لا e2 (مستبعَد بالنطاق) ولا e1 (ليس في Y).
        var report = await GetComplianceAsync(mgr, departmentId: deptY);
        Assert.DoesNotContain(report.Rows, r => r.UserId == e2);
        Assert.DoesNotContain(report.Rows, r => r.UserId == e1);
    }

    // (14) لا يمكن لفلتر departmentId توسيع نطاق المدير ليشمل موظفًا خارجه.
    [Fact]
    public async Task QueryParam_CannotExpandManagerScope()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSQ1");
        var (mgr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var deptZ = await CreateDeptAsync();
        var e2 = await CreateWeeklyEmployeeAsync(roleId, deptId: deptZ); // خارج نطاق المدير

        // المدير يحاول استهداف إدارة e2 صراحةً ⇒ يجب ألّا يظهر e2.
        var report = await GetComplianceAsync(mgr, departmentId: deptZ);
        Assert.DoesNotContain(report.Rows, r => r.UserId == e2);
    }

    // (15) CEO يرى كل الشركة (company).
    [Fact]
    public async Task Ceo_SeesAll_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSC1");
        var e2 = await CreateWeeklyEmployeeAsync(roleId);
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);

        var report = await GetComplianceAsync(ceo);
        Assert.Contains(report.Rows, r => r.UserId == e2);
    }

    // (16) GeneralManager يرى كل الشركة (company).
    [Fact]
    public async Task GeneralManager_SeesAll_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSG1");
        var e2 = await CreateWeeklyEmployeeAsync(roleId);
        var (gm, _) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);

        var report = await GetComplianceAsync(gm);
        Assert.Contains(report.Rows, r => r.UserId == e2);
    }

    // (17) HR على مستوى الشركة (استثناء موثّق): يرى موظفًا خارج نطاقه الذاتي.
    [Fact]
    public async Task Hr_CompanyWide_SeesOutOfScope_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "RSH1");
        var e2 = await CreateWeeklyEmployeeAsync(roleId); // ليس ضمن نطاق HR الذاتي (own)
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);

        var res = await hr.GetAsync("/api/reports/submission-compliance");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var report = await res.ReadAsync<SubmissionComplianceReport>();
        Assert.Contains(report!.Rows, r => r.UserId == e2);
    }

    // (18) الموظف العادي = 403 وغير المصادَق = 401 (الحارس).
    [Fact]
    public async Task Employee_403_And_Anonymous_401()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var empRes = await emp.GetAsync("/api/reports/submission-compliance");
        Assert.Equal(HttpStatusCode.Forbidden, empRes.StatusCode);

        var anon = _factory.CreateClient();
        var anonRes = await anon.GetAsync("/api/reports/submission-compliance");
        Assert.Equal(HttpStatusCode.Unauthorized, anonRes.StatusCode);
    }

    // ===== أدوات مساعدة =====

    private async Task<SubmissionComplianceReport> GetComplianceAsync(HttpClient client, Guid? departmentId = null, Guid? teamId = null)
    {
        var url = "/api/reports/submission-compliance";
        var qs = new List<string>();
        if (departmentId is not null) qs.Add($"departmentId={departmentId}");
        if (teamId is not null) qs.Add($"teamId={teamId}");
        if (qs.Count > 0) url += "?" + string.Join("&", qs);
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var report = await res.ReadAsync<SubmissionComplianceReport>();
        Assert.NotNull(report);
        return report!;
    }

    // مسمّى وظيفي أسبوعي (Code غير مبيعات) + قالب أساسي منشور ⇒ حامله متوقَّع منه تقرير أسبوعي.
    private async Task<Guid> CreateWeeklyReportingRoleAsync(HttpClient admin, string tag)
    {
        var roleId = await CreateJobRoleAsync(tag);
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
                new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, roleId, PeriodType.Weekly, TemplateClassification.Primary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return roleId;
    }

    // موظف نشط بمسمّى أسبوعي + (اختياريًّا) مدير مباشر/إدارة ⇒ مرشّح في متابعة الالتزام.
    private async Task<Guid> CreateWeeklyEmployeeAsync(Guid jobRoleId, Guid? managerId = null, Guid? deptId = null)
    {
        var (_, userId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: managerId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRoleId;
        if (deptId is not null) user.DepartmentId = deptId;
        await db.SaveChangesAsync();
        return userId;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateUserWithRolesAsync(params string[] roles)
    {
        var (client, userId) = await TestAuth.CreateUserAsync(_factory, roles[0]);
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Reporting.Infrastructure.Identity.ApplicationUser>>();
            var user = await users.FindByIdAsync(userId.ToString());
            foreach (var r in roles.Skip(1))
                await users.AddToRoleAsync(user!, r);
        }
        // إعادة تسجيل الدخول لتضمين الأدوار الإضافية في الـJWT.
        var email = (await GetEmailAsync(userId));
        var fresh = _factory.CreateClient();
        var login = await fresh.PostAsJsonAsync("/api/auth/login",
            new Reporting.Application.Auth.LoginRequest(email, "Passw0rd#1"));
        var auth = await login.Content.ReadFromJsonAsync<Reporting.Application.Auth.AuthResponse>();
        fresh.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (fresh, userId);
    }

    private async Task<string> GetEmailAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        return user.Email!;
    }

    private async Task<Guid> CreateJobRoleAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new JobRole { NameAr = $"دور {tag}", Code = $"{tag}_{Guid.NewGuid():N}".Substring(0, 18) };
        db.JobRoles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task<Guid> CreateDeptAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة {Guid.NewGuid():N}".Substring(0, 16), IsActive = true };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        return dept.Id;
    }
}
