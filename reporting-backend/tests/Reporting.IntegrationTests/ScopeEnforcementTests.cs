using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// يتحقق من أن نقاط القوائم الفعلية (التقارير المقدمة، مؤشرات الأداء، الدليل)
/// تطبّق نطاق الرؤية — قائد الفريق يرى فريقه فقط ولا يرى الفرع الآخر كأنه مدير.
/// </summary>
[Collection("Integration")]
public class ScopeEnforcementTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ScopeEnforcementTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) SalesMgr;
        public required (HttpClient C, Guid Id) SalesTl;
        public required (HttpClient C, Guid Id) Omar;     // فريق المبيعات
        public required (HttpClient C, Guid Id) MktTl;
        public required (HttpClient C, Guid Id) Yousef;   // فرع آخر (تسويق)
    }

    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var salesMgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var salesTl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, salesMgr.UserId);
        var omar = await TestAuth.CreateUserAsync(_factory, Roles.Employee, salesTl.UserId);
        var mktTl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gm.UserId);
        var yousef = await TestAuth.CreateUserAsync(_factory, Roles.Employee, mktTl.UserId);

        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            SalesMgr = (salesMgr.Client, salesMgr.UserId),
            SalesTl = (salesTl.Client, salesTl.UserId),
            Omar = (omar.Client, omar.UserId),
            MktTl = (mktTl.Client, mktTl.UserId),
            Yousef = (yousef.Client, yousef.UserId),
        };
    }

    [Fact]
    public async Task TeamLeader_Submissions_List_Sees_Only_Own_Team()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var org = await BuildOrgAsync();

        await SubmitAsync(org.Omar.C, templateId, fieldId, "2026-W40");
        await SubmitAsync(org.Yousef.C, templateId, fieldId, "2026-W40");

        var list = await (await org.SalesTl.C.GetAsync("/api/submissions"))
            .ReadAsync<List<SubmissionListItemDto>>();
        var submitters = list!.Select(s => s.SubmitterId).ToList();

        Assert.Contains(org.Omar.Id, submitters);          // ضمن فريقه
        Assert.DoesNotContain(org.Yousef.Id, submitters);  // الفرع الآخر — ممنوع
        Assert.DoesNotContain(org.SalesMgr.Id, submitters);
    }

    [Fact]
    public async Task TeamLeader_Kpi_List_Sees_Only_Own_Team()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _, _) = await PublishKpiAsync(admin);
        var org = await BuildOrgAsync();

        await admin.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, org.Omar.Id, PeriodType.Weekly, "2026-W40"));
        await admin.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, org.Yousef.Id, PeriodType.Weekly, "2026-W40"));

        var list = await (await org.SalesTl.C.GetAsync("/api/kpi-evaluations"))
            .ReadAsync<List<KpiEvaluationListItemDto>>();
        var subjects = list!.Select(e => e.SubjectUserId).ToList();

        Assert.Contains(org.Omar.Id, subjects);
        Assert.DoesNotContain(org.Yousef.Id, subjects);
    }

    [Fact]
    public async Task TeamLeader_Cannot_Read_OutOfScope_Submission()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var org = await BuildOrgAsync();

        var yousefSub = await SubmitAsync(org.Yousef.C, templateId, fieldId, "2026-W41");

        var res = await org.SalesTl.C.GetAsync($"/api/submissions/{yousefSub.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task TeamLeader_Cannot_Approve_OutOfScope_Kpi()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, manualId, autoId) = await PublishKpiAsync(admin);
        var org = await BuildOrgAsync();

        var ev = await (await admin.PostAsJsonAsync("/api/kpi-evaluations",
            new CreateKpiEvaluationRequest(templateId, org.Yousef.Id, PeriodType.Weekly, "2026-W42")))
            .ReadAsync<KpiEvaluationDto>();
        await admin.PutAsJsonAsync($"/api/kpi-evaluations/{ev!.Id}/results",
            new SaveKpiResultsRequest(new[]
            {
                new KpiResultInput(manualId, null, 70m, null),
                new KpiResultInput(autoId, 70m, null, null)
            }));
        await admin.PostAsync($"/api/kpi-evaluations/{ev.Id}/submit", null);

        // قائد فريق المبيعات يحاول اعتماد تقييم موظف من فرع آخر
        var res = await org.SalesTl.C.PostAsync($"/api/kpi-evaluations/{ev.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task TeamLeader_Directory_Users_Sees_Only_Own_Team()
    {
        var org = await BuildOrgAsync();

        var users = await (await org.SalesTl.C.GetAsync("/api/directory/users"))
            .ReadAsync<List<DirUser>>();
        var ids = users!.Select(u => u.Id).ToList();

        Assert.Contains(org.SalesTl.Id, ids);
        Assert.Contains(org.Omar.Id, ids);
        Assert.DoesNotContain(org.Yousef.Id, ids);
        Assert.DoesNotContain(org.MktTl.Id, ids);
        Assert.DoesNotContain(org.SalesMgr.Id, ids);
    }

    private record DirUser(Guid Id, string FullName);

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
            new CreateTemplateRequest($"تقرير أسبوعي {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
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
