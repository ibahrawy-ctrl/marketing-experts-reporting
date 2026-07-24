using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Kpi;
using Reporting.Application.Leave;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1 — P8: عدم الأثر التنظيمي.
/// ضبط تجاوز اعتماد التقارير/مراجعة KPI = إبراهيم للأربعة لا يغيّر: الدليل التنظيميّ، رؤية المدير
/// للتقارير/التقييمات خارج الاعتماد/المراجعة، مسار اعتماد الإجازة (سلسلة ManagerId القائمة)، ولا
/// ManagerId/TeamId/DepartmentId. التجاوز يمسّ اعتماد التقارير ومراجعة KPI فقط.
/// </summary>
[Collection("Integration")]
public class RoleAwareOverrideNoOrgImpactTests
{
    private readonly CustomWebApplicationFactory _factory;

    public RoleAwareOverrideNoOrgImpactTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Ibrahim;   // CEO — التجاوز الصريح
        public required (HttpClient C, Guid Id) Ahmed;      // GM — مدير محسن/محمد المباشر
        public required (HttpClient C, Guid Id) Mohsen;     // HR — ManagerId=أحمد + تجاوز=إبراهيم
        public required (HttpClient C, Guid Id) Mohamed;    // Manager — ManagerId=أحمد + تجاوز=إبراهيم
        public required HttpClient Admin;
    }

    private async Task<Org> BuildOrgAsync()
    {
        var ibrahim = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var ahmed = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager, ibrahim.UserId);
        var mohsen = await TestAuth.CreateUserAsync(_factory, Roles.Hr, ahmed.UserId);
        var mohamed = await TestAuth.CreateUserAsync(_factory, Roles.Manager, ahmed.UserId);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        foreach (var id in new[] { mohsen.UserId, mohamed.UserId })
        {
            await SetOverridesAsync(id, ibrahim.UserId);
            await admin.PostAsJsonAsync($"/api/balances/employees/{id}/opening",
                new OpeningBalanceRequest(BalanceType.AnnualLeave, 365, 2026, "رصيد اختبار"), TestJson.Options);
        }

        return new Org
        {
            Ibrahim = (ibrahim.Client, ibrahim.UserId),
            Ahmed = (ahmed.Client, ahmed.UserId),
            Mohsen = (mohsen.Client, mohsen.UserId),
            Mohamed = (mohamed.Client, mohamed.UserId),
            Admin = admin,
        };
    }

    private record DirUser(Guid Id, string FullName);

    // ===== 1) الدليل التنظيميّ: المدير أحمد ما زال يرى مرؤوسيه المباشرين رغم ضبط التجاوز. =====
    [Fact]
    public async Task Directory_ManagerStillSeesDirectReports_AfterOverride()
    {
        var org = await BuildOrgAsync();

        var users = await (await org.Ahmed.C.GetAsync("/api/directory/users"))
            .ReadAsync<List<DirUser>>();
        var ids = users!.Select(u => u.Id).ToList();

        Assert.Contains(org.Mohsen.Id, ids);
        Assert.Contains(org.Mohamed.Id, ids);
    }

    // ===== 2) رؤية التقارير خارج الاعتماد: أحمد يرى تقرير محسن المُسلَّم رغم توجيه اعتماده لإبراهيم. =====
    [Fact]
    public async Task ReportVisibility_ManagerSeesSubmission_EvenThoughApproverIsIbrahim()
    {
        var org = await BuildOrgAsync();
        var (templateId, fieldId) = await PublishTemplateAsync(org.Admin);

        var submitted = await SubmitReportAsync(org.Mohsen.C, templateId, fieldId, "2026-W20");
        Assert.Equal(org.Ibrahim.Id, submitted.CurrentApproverId); // الاعتماد لإبراهيم

        var list = await (await org.Ahmed.C.GetAsync("/api/submissions"))
            .ReadAsync<List<SubmissionListItemDto>>();
        Assert.Contains(org.Mohsen.Id, list!.Select(s => s.SubmitterId)); // الرؤية للمدير باقية
    }

    // ===== 3) رؤية KPI خارج المراجعة: أحمد يرى تقييم محسن رغم أن المراجِع إبراهيم. =====
    [Fact]
    public async Task KpiVisibility_ManagerSeesEvaluation_EvenThoughReviewerIsIbrahim()
    {
        var org = await BuildOrgAsync();
        var submitted = await SubmitKpiAsync(org.Admin, org.Mohsen.Id, "2026-W20");
        Assert.Equal(org.Ibrahim.Id, submitted.ReviewerId);

        var list = await (await org.Ahmed.C.GetAsync("/api/kpi-evaluations"))
            .ReadAsync<List<KpiEvaluationListItemDto>>();
        Assert.Contains(org.Mohsen.Id, list!.Select(e => e.SubjectUserId));
    }

    // ===== 4) اعتماد الإجازة يبقى على سلسلة ManagerId (أحمد)، لا يتأثّر بتجاوز اعتماد التقارير. =====
    [Fact]
    public async Task LeaveApproval_UsesManagerChain_NotReportOverride()
    {
        var org = await BuildOrgAsync();

        var created = (await (await org.Mohsen.C.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, new(2026, 5, 5), new(2026, 5, 7),
                null, null, "سبب الإجازة", null), TestJson.Options))
            .ReadAsync<LeaveRequestDto>())!;
        Assert.Equal(LeaveRequestStep.Manager, created.CurrentStep); // بلا فريق ⇒ خطوة المدير

        // المدير المباشر أحمد يعتمد خطوة المدير ⇒ 200 (سلسلة الإجازة القائمة سليمة).
        var mgr = await org.Ahmed.C.PostAsJsonAsync($"/api/leave-requests/{created.Id}/manager/approve",
            new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, mgr.StatusCode);
        Assert.Equal(LeaveRequestStatus.ManagerApproved, (await mgr.ReadAsync<LeaveRequestDto>())!.Status);
    }

    // ===== 5) ManagerId/TeamId/DepartmentId للأربعة لم تتغيّر — التجاوز حقلان مستقلّان فقط. =====
    [Fact]
    public async Task Structure_ManagerIdTeamDepartment_Unchanged_OnlyOverrideFieldsSet()
    {
        var org = await BuildOrgAsync();

        foreach (var (id, expectedManager) in new[] { (org.Mohsen.Id, org.Ahmed.Id), (org.Mohamed.Id, org.Ahmed.Id) })
        {
            var (managerId, teamId, deptId, reportOvr, kpiOvr) = await GetStructureAsync(id);
            Assert.Equal(expectedManager, managerId);   // ManagerId كما أُنشئ
            Assert.Null(teamId);                          // لم يُضبط فريق
            Assert.Null(deptId);                          // لم تُضبط إدارة
            Assert.Equal(org.Ibrahim.Id, reportOvr);      // التجاوز الوحيد المُضاف
            Assert.Equal(org.Ibrahim.Id, kpiOvr);
        }
    }

    // ===== أدوات =====

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب أداء {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private static async Task<SubmissionDto> SubmitReportAsync(HttpClient c, Guid templateId, Guid fieldId, string period)
    {
        var draft = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, period)))
            .ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 1500m, null, null, null) }));
        return (await (await c.PostAsync($"/api/submissions/{draft.Id}/submit", null)).ReadAsync<SubmissionDto>())!;
    }

    private async Task<KpiEvaluationDto> SubmitKpiAsync(HttpClient admin, Guid subjectId, string period)
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

        var ev = await (await admin.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(created.Id, subjectId, PeriodType.Weekly, period)))
            .ReadAsync<KpiEvaluationDto>();
        await admin.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manual!.Id, null, 80m, null),
                new KpiResultInput(auto!.Id, 80m, null, null),
            }));
        return (await (await admin.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null))
            .ReadAsync<KpiEvaluationDto>())!;
    }

    private async Task SetOverridesAsync(Guid userId, Guid overrideId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.ReportApproverOverrideUserId = overrideId;
        user.KpiReviewerOverrideUserId = overrideId;
        await db.SaveChangesAsync();
    }

    private async Task<(Guid? ManagerId, Guid? TeamId, Guid? DepartmentId, Guid? ReportOvr, Guid? KpiOvr)> GetStructureAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == userId);
        return (u.ManagerId, u.TeamId, u.DepartmentId, u.ReportApproverOverrideUserId, u.KpiReviewerOverrideUserId);
    }
}
