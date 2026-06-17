using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// يبني هيكلًا تنظيميًا كاملًا (CEO←GM←مدير←قائد فريق←موظف، فرعان: مبيعات/تسويق)
/// ويتحقق من عزل نطاق الرؤية لكل دور + تدفّق التقرير الأسبوعي صعودًا في التسلسل الإداري.
/// </summary>
[Collection("Integration")]
public class OrgHierarchyTests
{
    private readonly CustomWebApplicationFactory _factory;

    public OrgHierarchyTests(CustomWebApplicationFactory factory) => _factory = factory;

    private record ScopeDto(string Type, List<Guid> Ids);
    private record DashDto(string DashboardType, ScopeDto Scope, List<string> Permissions);
    private record MemberPerfDto(Guid UserId, string Name, decimal? KpiAverage, string KpiTrend, int ReportsTotal, int ReportsCompleted);

    /// <summary>عقد الهيكل التنظيمي: عملاء مسجّلو الدخول + معرّفات.</summary>
    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Ceo;
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) SalesMgr;
        public required (HttpClient C, Guid Id) SalesTl;
        public required (HttpClient C, Guid Id) Omar;
        public required (HttpClient C, Guid Id) Fatima;
        public required (HttpClient C, Guid Id) MktMgr;
        public required (HttpClient C, Guid Id) ContentTl;
        public required (HttpClient C, Guid Id) Yousef;
    }

    private async Task<Org> BuildOrgAsync()
    {
        var ceo = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager, ceo.UserId);

        var salesMgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var salesTl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, salesMgr.UserId);
        var omar = await TestAuth.CreateUserAsync(_factory, Roles.Employee, salesTl.UserId);
        var fatima = await TestAuth.CreateUserAsync(_factory, Roles.Employee, salesTl.UserId);

        var mktMgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var contentTl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, mktMgr.UserId);
        var yousef = await TestAuth.CreateUserAsync(_factory, Roles.Employee, contentTl.UserId);

        return new Org
        {
            Ceo = (ceo.Client, ceo.UserId),
            Gm = (gm.Client, gm.UserId),
            SalesMgr = (salesMgr.Client, salesMgr.UserId),
            SalesTl = (salesTl.Client, salesTl.UserId),
            Omar = (omar.Client, omar.UserId),
            Fatima = (fatima.Client, fatima.UserId),
            MktMgr = (mktMgr.Client, mktMgr.UserId),
            ContentTl = (contentTl.Client, contentTl.UserId),
            Yousef = (yousef.Client, yousef.UserId),
        };
    }

    private static async Task<DashDto> DashAsync(HttpClient c)
        => (await (await c.GetAsync("/api/dashboard/me")).ReadAsync<DashDto>())!;

    [Fact]
    public async Task TeamLeader_Sees_Only_Direct_Team()
    {
        var org = await BuildOrgAsync();
        var dash = await DashAsync(org.SalesTl.C);

        Assert.Equal("team", dash.Scope.Type);
        // يرى نفسه + موظفيه المباشرين فقط
        Assert.Contains(org.SalesTl.Id, dash.Scope.Ids);
        Assert.Contains(org.Omar.Id, dash.Scope.Ids);
        Assert.Contains(org.Fatima.Id, dash.Scope.Ids);
        // لا يرى الفرع الآخر ولا المدير الأعلى
        Assert.DoesNotContain(org.ContentTl.Id, dash.Scope.Ids);
        Assert.DoesNotContain(org.Yousef.Id, dash.Scope.Ids);
        Assert.DoesNotContain(org.SalesMgr.Id, dash.Scope.Ids);
        Assert.DoesNotContain(org.Gm.Id, dash.Scope.Ids);
    }

    [Fact]
    public async Task Manager_Sees_TeamLeader_And_Everyone_Below()
    {
        var org = await BuildOrgAsync();
        var dash = await DashAsync(org.SalesMgr.C);

        Assert.Equal("department", dash.Scope.Type);
        // يرى قائد الفريق ومن تحته (غير مباشر)
        Assert.Contains(org.SalesMgr.Id, dash.Scope.Ids);
        Assert.Contains(org.SalesTl.Id, dash.Scope.Ids);
        Assert.Contains(org.Omar.Id, dash.Scope.Ids);
        Assert.Contains(org.Fatima.Id, dash.Scope.Ids);
        // لا يرى فرع التسويق ولا المدير العام
        Assert.DoesNotContain(org.ContentTl.Id, dash.Scope.Ids);
        Assert.DoesNotContain(org.Yousef.Id, dash.Scope.Ids);
        Assert.DoesNotContain(org.Gm.Id, dash.Scope.Ids);
        Assert.Contains("ViewAnalytics", dash.Permissions);
    }

    [Fact]
    public async Task GM_And_CEO_See_Whole_Company()
    {
        var org = await BuildOrgAsync();

        var gm = await DashAsync(org.Gm.C);
        Assert.Equal("company", gm.Scope.Type);
        foreach (var id in new[] { org.Gm.Id, org.SalesMgr.Id, org.SalesTl.Id, org.Omar.Id, org.MktMgr.Id, org.ContentTl.Id, org.Yousef.Id })
            Assert.Contains(id, gm.Scope.Ids);

        var ceo = await DashAsync(org.Ceo.C);
        Assert.Equal("company", ceo.Scope.Type);
        Assert.Contains(org.Yousef.Id, ceo.Scope.Ids);
    }

    [Fact]
    public async Task Employee_Sees_Self_Only()
    {
        var org = await BuildOrgAsync();
        var dash = await DashAsync(org.Omar.C);

        Assert.Equal("own", dash.Scope.Type);
        Assert.Single(dash.Scope.Ids);
        Assert.Equal(org.Omar.Id, dash.Scope.Ids[0]);
        Assert.DoesNotContain(org.Fatima.Id, dash.Scope.Ids);
    }

    [Fact]
    public async Task MembersPerformance_Manager_Excludes_Other_Branch()
    {
        var org = await BuildOrgAsync();
        var members = await (await org.SalesMgr.C.GetAsync("/api/dashboard/members-performance"))
            .ReadAsync<List<MemberPerfDto>>();

        var ids = members!.Select(m => m.UserId).ToList();
        Assert.Contains(org.SalesTl.Id, ids);
        Assert.Contains(org.Omar.Id, ids);
        Assert.DoesNotContain(org.ContentTl.Id, ids);
        Assert.DoesNotContain(org.Yousef.Id, ids);
    }

    [Fact]
    public async Task WeeklyReport_Flows_Up_The_Hierarchy_And_Closes_At_Top()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var org = await BuildOrgAsync();

        // الموظف عمر ينشئ تقريرًا أسبوعيًا ويعبّئه ويرسله
        var draft = await (await org.Omar.C.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W50")))
            .ReadAsync<SubmissionDto>();
        await org.Omar.C.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 1200m, null, null, null) }));
        var submitted = await (await org.Omar.C.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>();

        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);
        Assert.Equal(org.SalesTl.Id, submitted.CurrentApproverId); // المدير المباشر = قائد الفريق

        // قائد فريق آخر (فرع التسويق) لا يستطيع اعتماد تقرير عمر
        var wrongApprove = await org.ContentTl.C.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest(null));
        Assert.Equal(HttpStatusCode.Forbidden, wrongApprove.StatusCode);

        // قائد الفريق يعتمد ⇒ يصعد للمدير
        var afterTl = await (await org.SalesTl.C.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد من القائد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.ApprovedByDirectManager, afterTl!.Status);
        Assert.Equal(org.SalesMgr.Id, afterTl.CurrentApproverId);

        // المدير يعتمد ⇒ يصعد للمدير العام
        var afterMgr = await (await org.SalesMgr.C.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.ApprovedByNextLevel, afterMgr!.Status);
        Assert.Equal(org.Gm.Id, afterMgr.CurrentApproverId);

        // المدير العام يعتمد ⇒ يصعد للمدير التنفيذي
        var afterGm = await (await org.Gm.C.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(org.Ceo.Id, afterGm!.CurrentApproverId);

        // المدير التنفيذي يعتمد ⇒ يُغلق (لا مستوى أعلى)
        var afterCeo = await (await org.Ceo.C.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, afterCeo!.Status);
        Assert.Null(afterCeo.CurrentApproverId);
    }

    [Fact]
    public async Task PendingReport_Visible_To_Manager_Scope_Not_Other_Branch()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishWeeklyTemplateAsync(admin);
        var org = await BuildOrgAsync();

        // عمر ينشئ مسودة (لم تُرسل) للفترة
        var draft = await (await org.Omar.C.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W51")))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Draft, draft!.Status);

        // مديره يراها ضمن "تحتاج إجراء"
        var pendingMgr = await (await org.SalesMgr.C.GetAsync("/api/dashboard/pending-reports?periodKey=2026-W51"))
            .ReadAsync<List<PendingRow>>();
        Assert.Contains(pendingMgr!, p => p.SubmissionId == draft.Id);

        // مدير الفرع الآخر لا يراها
        var pendingOther = await (await org.MktMgr.C.GetAsync("/api/dashboard/pending-reports?periodKey=2026-W51"))
            .ReadAsync<List<PendingRow>>();
        Assert.DoesNotContain(pendingOther!, p => p.SubmissionId == draft.Id);
    }

    private record PendingRow(Guid SubmissionId, Guid SubmitterId, string SubmitterName, string TemplateTitle, string Status, string PeriodKey);

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
}
