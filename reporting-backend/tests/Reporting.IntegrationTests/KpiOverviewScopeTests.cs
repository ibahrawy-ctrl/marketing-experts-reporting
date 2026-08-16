using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Application.Reports;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// ملحق نطاق صفحة مؤشرات الأداء (KPI Overview Scope).
/// يثبت أن مصادر بيانات الصفحة (الإدارات/الفِرق من الدليل + ملخّص مؤشرات الأداء)
/// مُقيَّدة بنطاق الرؤية على مستوى الـ API — لا تُرجِع بيانات خارج النطاق،
/// بحيث لا يرى المدير إدارات لا يديرها (مالية/مبيعات/إدارة عامة).
/// </summary>
[Collection("Integration")]
public class KpiOverviewScopeTests
{
    private readonly CustomWebApplicationFactory _factory;

    public KpiOverviewScopeTests(CustomWebApplicationFactory factory) => _factory = factory;

    private record DirDepartment(Guid Id, string NameAr, string? NameEn, string? Code, Guid? ManagerId, bool IsActive);
    private record DirTeam(Guid Id, string NameAr, string? NameEn, Guid DepartmentId, Guid? TeamLeaderId, bool IsActive);

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Mgr;        // مدير إدارة التخطيط (داخل النطاق)
        public required (HttpClient C, Guid Id) Tl;         // قائد فريق ضمن إدارة التخطيط
        public required (HttpClient C, Guid Id) Emp;        // موظف ضمن فريق القائد
        public required (HttpClient C, Guid Id) FinMgr;     // مدير المالية (خارج نطاق Mgr)
        public required (HttpClient C, Guid Id) FinEmp;     // موظف المالية (خارج النطاق)
        public required Guid PlanningDeptId;
        public required Guid FinanceDeptId;
        public required Guid GeneralAdminDeptId;
        public required Guid PlanningTeamId;
        public required Guid FinanceTeamId;
    }

    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var mgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, mgr.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tl.UserId);
        var finMgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var finEmp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, finMgr.UserId);

        var planningDept = await CreateDepartmentAsync($"التخطيط والجودة {Guid.NewGuid():N}", mgr.UserId);
        var financeDept = await CreateDepartmentAsync($"المالية {Guid.NewGuid():N}", finMgr.UserId);
        var generalAdminDept = await CreateDepartmentAsync($"الإدارة العامة {Guid.NewGuid():N}", gm.UserId);

        var planningTeam = await CreateTeamAsync($"فريق التخطيط {Guid.NewGuid():N}", planningDept, tl.UserId);
        var financeTeam = await CreateTeamAsync($"فريق المالية {Guid.NewGuid():N}", financeDept, finMgr.UserId);

        await SetUserOrgAsync(gm.UserId, generalAdminDept, null);
        await SetUserOrgAsync(mgr.UserId, planningDept, null);
        await SetUserOrgAsync(tl.UserId, planningDept, planningTeam);
        await SetUserOrgAsync(emp.UserId, planningDept, planningTeam);
        await SetUserOrgAsync(finMgr.UserId, financeDept, null);
        await SetUserOrgAsync(finEmp.UserId, financeDept, financeTeam);

        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            Mgr = (mgr.Client, mgr.UserId),
            Tl = (tl.Client, tl.UserId),
            Emp = (emp.Client, emp.UserId),
            FinMgr = (finMgr.Client, finMgr.UserId),
            FinEmp = (finEmp.Client, finEmp.UserId),
            PlanningDeptId = planningDept,
            FinanceDeptId = financeDept,
            GeneralAdminDeptId = generalAdminDept,
            PlanningTeamId = planningTeam,
            FinanceTeamId = financeTeam,
        };
    }

    // ===== اختبار 1: المدير لا يرى إدارات خارج نطاقه في مصدر بيانات الإدارات =====
    [Fact]
    public async Task Manager_Departments_Excludes_OutOfScope_Departments()
    {
        var org = await BuildOrgAsync();

        var depts = await (await org.Mgr.C.GetAsync("/api/directory/departments"))
            .ReadAsync<List<DirDepartment>>();
        var ids = depts!.Select(d => d.Id).ToList();

        Assert.Contains(org.PlanningDeptId, ids);            // إدارته
        Assert.DoesNotContain(org.FinanceDeptId, ids);       // المالية — خارج النطاق
        Assert.DoesNotContain(org.GeneralAdminDeptId, ids);  // الإدارة العامة — خارج النطاق
    }

    // ===== اختبار 2: ملخّص مؤشرات الأداء للمدير يستثني المواضيع خارج النطاق =====
    [Fact]
    public async Task Manager_KpiSummary_Excludes_OutOfScope_Subjects()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var org = await BuildOrgAsync();

        await SubmitEvalAsync(admin, templateId, manualId, autoId, org.Emp.Id, TestCalendar.Cycle(1));
        await SubmitEvalAsync(admin, templateId, manualId, autoId, org.FinEmp.Id, TestCalendar.Cycle(1));

        var report = await (await org.Mgr.C.GetAsync($"/api/reports/kpi-summary?periodType=Weekly&periodKey={TestCalendar.Cycle(1)}"))
            .ReadAsync<KpiSummaryReport>();
        var subjects = report!.Rows.Select(r => r.SubjectUserId).ToList();

        Assert.Contains(org.Emp.Id, subjects);          // ضمن نطاقه
        Assert.DoesNotContain(org.FinEmp.Id, subjects);  // المالية — خارج النطاق
    }

    // ===== اختبار 3: قائد الفريق يرى فريقه فقط في مصدر بيانات الفِرق =====
    [Fact]
    public async Task TeamLeader_Teams_Sees_Only_Own_Team()
    {
        var org = await BuildOrgAsync();

        var teams = await (await org.Tl.C.GetAsync("/api/directory/teams"))
            .ReadAsync<List<DirTeam>>();
        var ids = teams!.Select(t => t.Id).ToList();

        Assert.Contains(org.PlanningTeamId, ids);
        Assert.DoesNotContain(org.FinanceTeamId, ids);
    }

    // ===== اختبار 4: الموظف ممنوع من ملخّص مؤشرات الأداء ولا يرى تقييمات غيره =====
    [Fact]
    public async Task Employee_Cannot_Access_KpiSummary_And_Sees_Only_Own_Evaluations()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var org = await BuildOrgAsync();

        await SubmitEvalAsync(admin, templateId, manualId, autoId, org.Emp.Id, TestCalendar.Cycle(2));
        await SubmitEvalAsync(admin, templateId, manualId, autoId, org.FinEmp.Id, TestCalendar.Cycle(2));

        // ملخّص المؤشرات محصور بأدوار المراقبة — الموظف يُمنع
        var summaryRes = await org.Emp.C.GetAsync($"/api/reports/kpi-summary?periodType=Weekly&periodKey={TestCalendar.Cycle(2)}");
        Assert.Equal(HttpStatusCode.Forbidden, summaryRes.StatusCode);

        // قائمة التقييمات للموظف = تقييماته فقط
        var evals = await (await org.Emp.C.GetAsync("/api/kpi-evaluations"))
            .ReadAsync<List<KpiEvaluationListItemDto>>();
        var subjects = evals!.Select(e => e.SubjectUserId).ToList();

        Assert.All(subjects, s => Assert.Equal(org.Emp.Id, s));
        Assert.DoesNotContain(org.FinEmp.Id, subjects);
    }

    // ===== اختبار 5: المدير العام/المسؤول يرى الصورة الكاملة =====
    [Fact]
    public async Task Admin_Sees_All_Departments_And_All_KpiSummary_Rows()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var org = await BuildOrgAsync();

        await SubmitEvalAsync(admin, templateId, manualId, autoId, org.Emp.Id, TestCalendar.Cycle(3));
        await SubmitEvalAsync(admin, templateId, manualId, autoId, org.FinEmp.Id, TestCalendar.Cycle(3));

        var depts = await (await admin.GetAsync("/api/directory/departments"))
            .ReadAsync<List<DirDepartment>>();
        var deptIds = depts!.Select(d => d.Id).ToList();
        Assert.Contains(org.PlanningDeptId, deptIds);
        Assert.Contains(org.FinanceDeptId, deptIds);
        Assert.Contains(org.GeneralAdminDeptId, deptIds);

        var report = await (await admin.GetAsync($"/api/reports/kpi-summary?periodType=Weekly&periodKey={TestCalendar.Cycle(3)}"))
            .ReadAsync<KpiSummaryReport>();
        var subjects = report!.Rows.Select(r => r.SubjectUserId).ToList();
        Assert.Contains(org.Emp.Id, subjects);
        Assert.Contains(org.FinEmp.Id, subjects);  // المسؤول يرى الجميع
    }

    // ===== مساعدون =====

    private async Task<Guid> CreateDepartmentAsync(string nameAr, Guid managerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = nameAr, ManagerId = managerId, IsActive = true };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        return dept.Id;
    }

    private async Task<Guid> CreateTeamAsync(string nameAr, Guid departmentId, Guid? leaderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var team = new Team { NameAr = nameAr, DepartmentId = departmentId, TeamLeaderId = leaderId, IsActive = true };
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

    private static async Task SubmitEvalAsync(HttpClient evaluator, Guid templateId, Guid manualId, Guid autoId,
        Guid subjectId, string periodKey)
    {
        var ev = await (await evaluator.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, subjectId, PeriodType.Weekly, periodKey)))
            .ReadAsync<KpiEvaluationDto>();
        await evaluator.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 70m, null),
                new KpiResultInput(autoId, 70m, null, null)
            }));
        await evaluator.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null);
    }

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
        await admin.PostAsync($"/api/kpi-templates/versions/{versionId}/publish", null);
        return (created.Id, manual!.Id, auto!.Id);
    }
}
