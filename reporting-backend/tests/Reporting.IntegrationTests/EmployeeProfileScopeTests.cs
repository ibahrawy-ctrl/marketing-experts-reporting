using System.Net;
using System.Net.Http.Json;
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
/// نطاق ملف أداء الموظّف (Employee Performance Profile — Phase 3).
/// يثبت أن نقطة /api/dashboard/employee-profile/{userId} تفرض النطاق خادمًا:
/// كل دور يفتح فقط الملفات التي يحق له رؤيتها، وأي محاولة لفتح ملف خارج النطاق تُرفض (403)،
/// بينما المدير العام/المسؤول يرى الجميع حسب السياسة.
/// </summary>
[Collection("Integration")]
public class EmployeeProfileScopeTests
{
    private readonly CustomWebApplicationFactory _factory;

    public EmployeeProfileScopeTests(CustomWebApplicationFactory factory) => _factory = factory;

    private record ProfileHeader(
        Guid UserId, string FullName, string? Email, string? JobRoleName,
        string? TeamName, string? DepartmentName, string? DirectManagerName,
        bool IsActive, string StatusKey, string StatusLabel);

    private record ProfileSummary(
        decimal? LastKpiScore, string? LastKpiPeriod, string LastKpiTrend,
        decimal? AverageKpi, int KpiCount,
        int ReportsSubmitted, int ReportsReturned, int ReportsNeedsAction,
        int OpenNotesRequiringAction);

    private record Profile(ProfileHeader Header, ProfileSummary Summary);

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Mgr;
        public required (HttpClient C, Guid Id) Tl;
        public required (HttpClient C, Guid Id) Emp;
        public required (HttpClient C, Guid Id) FinMgr;
        public required (HttpClient C, Guid Id) FinEmp;
    }

    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var mgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, mgr.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tl.UserId);
        var finMgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var finEmp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, finMgr.UserId);

        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            Mgr = (mgr.Client, mgr.UserId),
            Tl = (tl.Client, tl.UserId),
            Emp = (emp.Client, emp.UserId),
            FinMgr = (finMgr.Client, finMgr.UserId),
            FinEmp = (finEmp.Client, finEmp.UserId),
        };
    }

    // ===== اختبار 1: المدير يفتح ملف موظّف ضمن نطاقه ويُرفض خارجه =====
    [Fact]
    public async Task Manager_Opens_InScope_Profile_And_Denied_OutOfScope()
    {
        var org = await BuildOrgAsync();

        var ok = await org.Mgr.C.GetAsync($"/api/dashboard/employee-profile/{org.Emp.Id}");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var profile = await ok.ReadAsync<Profile>();
        Assert.Equal(org.Emp.Id, profile!.Header.UserId);

        // الموظّف في إدارة مالية أخرى — خارج نطاق المدير
        var denied = await org.Mgr.C.GetAsync($"/api/dashboard/employee-profile/{org.FinEmp.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    // ===== اختبار 2: قائد الفريق يفتح أعضاء فريقه فقط =====
    [Fact]
    public async Task TeamLeader_Opens_Only_Team_Members()
    {
        var org = await BuildOrgAsync();

        var own = await org.Tl.C.GetAsync($"/api/dashboard/employee-profile/{org.Emp.Id}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);

        // مدير القائد خارج نطاق القائد (النطاق = هو + مرؤوسوه المباشرون)
        var up = await org.Tl.C.GetAsync($"/api/dashboard/employee-profile/{org.Mgr.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, up.StatusCode);

        var other = await org.Tl.C.GetAsync($"/api/dashboard/employee-profile/{org.FinEmp.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    // ===== اختبار 3: الموظّف يفتح ملفه فقط =====
    [Fact]
    public async Task Employee_Opens_Only_Own_Profile()
    {
        var org = await BuildOrgAsync();

        var self = await org.Emp.C.GetAsync($"/api/dashboard/employee-profile/{org.Emp.Id}");
        Assert.Equal(HttpStatusCode.OK, self.StatusCode);
        var profile = await self.ReadAsync<Profile>();
        Assert.Equal(org.Emp.Id, profile!.Header.UserId);

        var leader = await org.Emp.C.GetAsync($"/api/dashboard/employee-profile/{org.Tl.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, leader.StatusCode);

        var other = await org.Emp.C.GetAsync($"/api/dashboard/employee-profile/{org.FinEmp.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    // ===== اختبار 4: المسؤول يفتح أي ملف حسب السياسة =====
    [Fact]
    public async Task Admin_Opens_Any_Profile()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var org = await BuildOrgAsync();

        var a = await admin.GetAsync($"/api/dashboard/employee-profile/{org.Emp.Id}");
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);

        var b = await admin.GetAsync($"/api/dashboard/employee-profile/{org.FinEmp.Id}");
        Assert.Equal(HttpStatusCode.OK, b.StatusCode);
    }

    // ===== اختبار 5: الملخّص يعكس بيانات KPI الفعلية بلا تجميع دوري جديد =====
    [Fact]
    public async Task Profile_Summary_Reflects_Submitted_Kpi()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var org = await BuildOrgAsync();

        // قبل أي تقييم: لا توجد بيانات كافية
        var before = await (await org.Mgr.C.GetAsync($"/api/dashboard/employee-profile/{org.Emp.Id}")).ReadAsync<Profile>();
        Assert.Equal("insufficient", before!.Header.StatusKey);
        Assert.Null(before.Summary.LastKpiScore);
        Assert.Equal(0, before.Summary.KpiCount);

        await SubmitEvalAsync(admin, templateId, manualId, autoId, org.Emp.Id, "2026-W47");

        var after = await (await org.Mgr.C.GetAsync($"/api/dashboard/employee-profile/{org.Emp.Id}")).ReadAsync<Profile>();
        Assert.NotNull(after!.Summary.LastKpiScore);
        Assert.Equal("2026-W47", after.Summary.LastKpiPeriod);
        Assert.Equal(1, after.Summary.KpiCount);
    }

    // ===== اختبار 6: المستخدم غير المصادق يُرفض =====
    [Fact]
    public async Task Anonymous_Is_Unauthorized()
    {
        var org = await BuildOrgAsync();
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync($"/api/dashboard/employee-profile/{org.Emp.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ===== مساعدون =====

    private async Task SubmitEvalAsync(HttpClient evaluator, Guid templateId, Guid manualId, Guid autoId,
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
        // اعتماد عبر مُصعَّد (CEO ليس المُقيّم ولا الموضوع) كي يظهر التقييم في ملخّص الملف (المعتمَد فقط).
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        await ceo.PostAsync($"/api/kpi-evaluations/{ev.Id}/approve", null);
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
