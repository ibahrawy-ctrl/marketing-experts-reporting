using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// منح رؤية التقارير المخفيّ (REPORT-VIEW-GRANTS-R1). يتحقّق أن الأدمن وحده يدير المنح، وأن المستفيد يرى
/// تقارير الهدف بحالات معتمدة فقط (لا مسودّات/مُعادة)، ولا يملك أي قدرة اعتماد/إرجاع/تعديل/إرسال، ولا يُضاف
/// للفريق، وأن المنح معزول تمامًا (لا يُوسّع النطاق الأساسي بدونه).
/// </summary>
[Collection("Integration")]
public class ReportViewGrantTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportViewGrantTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب منح {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        var publishRes = await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishRes.StatusCode);
        return (created.Id, field!.Id);
    }

    private static async Task<SubmissionDto> SubmitReportAsync(
        HttpClient submitter, Guid templateId, Guid fieldId, string periodKey)
    {
        var draft = await (await submitter.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey))).ReadAsync<SubmissionDto>();
        await submitter.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 100m, null, null, null) }));
        return (await (await submitter.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
    }

    private async Task<Guid?> TeamIdOfAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.TeamId).FirstAsync();
    }

    [Fact]
    public async Task Admin_CreateUserGrant_GranteeSeesTargetSubmittedReport()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (target, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (grantee, granteeId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var submitted = await SubmitReportAsync(target, templateId, fieldId, TestCalendar.Cycle(1));
        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);

        // قبل المنح: المستفيد لا يرى تقرير الهدف (نطاق=own).
        var beforeGet = await grantee.GetAsync($"/api/submissions/{submitted.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, beforeGet.StatusCode);

        var grant = await (await admin.PostAsJsonAsync("/api/report-view-grants",
            new CreateReportViewGrantRequest(granteeId, ReportViewGrantScopeKind.User, TargetUserId: targetId)))
            .ReadAsync<ReportViewGrantDto>();
        Assert.NotNull(grant);
        Assert.True(grant!.IsActive);

        // بعد المنح: المستفيد يرى التقرير المُرسَل عبر القائمة وعبر الجلب المباشر.
        var list = await (await grantee.GetAsync($"/api/submissions?periodKey={TestCalendar.Cycle(1)}"))
            .ReadAsync<List<SubmissionDto>>();
        Assert.Contains(list!, s => s.Id == submitted.Id);

        var afterGet = await grantee.GetAsync($"/api/submissions/{submitted.Id}");
        Assert.Equal(HttpStatusCode.OK, afterGet.StatusCode);
        var seen = await afterGet.ReadAsync<SubmissionDto>();
        Assert.False(seen!.CanEdit); // عرض فقط
    }

    [Fact]
    public async Task Admin_CreateTeamGrant_GranteeSeesTeamMemberSubmittedReports()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (target, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee", leaderId);
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, targetId);
        var (grantee, granteeId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var submitted = await SubmitReportAsync(target, templateId, fieldId, TestCalendar.Cycle(2));

        await admin.PostAsJsonAsync("/api/report-view-grants",
            new CreateReportViewGrantRequest(granteeId, ReportViewGrantScopeKind.Team, TargetTeamId: teamId));

        var list = await (await grantee.GetAsync($"/api/submissions?periodKey={TestCalendar.Cycle(2)}"))
            .ReadAsync<List<SubmissionDto>>();
        Assert.Contains(list!, s => s.Id == submitted.Id);

        // المستفيد لا يُضاف للفريق إطلاقًا.
        Assert.Null(await TeamIdOfAsync(granteeId));
    }

    [Fact]
    public async Task Grantee_CannotSee_DraftOrReturned_Reports()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (target, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (grantee, granteeId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await admin.PostAsJsonAsync("/api/report-view-grants",
            new CreateReportViewGrantRequest(granteeId, ReportViewGrantScopeKind.User, TargetUserId: targetId));

        // مسودة (لم تُرسَل) — مخفيّة عن المستفيد.
        var draft = await (await target.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, TestCalendar.Cycle(3)))).ReadAsync<SubmissionDto>();
        var draftGet = await grantee.GetAsync($"/api/submissions/{draft!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, draftGet.StatusCode);

        // مُعادة للتعديل — مخفيّة عن المستفيد.
        var submitted = await SubmitReportAsync(target, templateId, fieldId, TestCalendar.Cycle(4));
        await manager.PostAsJsonAsync($"/api/submissions/{submitted.Id}/return",
            new ApprovalActionRequest("يرجى التصحيح"));
        var returnedGet = await grantee.GetAsync($"/api/submissions/{submitted.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, returnedGet.StatusCode);

        // القائمة لا تحوي المسودة ولا المُعادة.
        var list = await (await grantee.GetAsync("/api/submissions"))
            .ReadAsync<List<SubmissionDto>>();
        Assert.DoesNotContain(list!, s => s.Id == draft.Id);
        Assert.DoesNotContain(list!, s => s.Id == submitted.Id);
    }

    [Fact]
    public async Task Grantee_CannotApprove_Return_Escalate_Edit_Submit_OnGrantedReport()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (target, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (grantee, granteeId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await admin.PostAsJsonAsync("/api/report-view-grants",
            new CreateReportViewGrantRequest(granteeId, ReportViewGrantScopeKind.User, TargetUserId: targetId));

        var submitted = await SubmitReportAsync(target, templateId, fieldId, TestCalendar.Cycle(5));

        var approve = await grantee.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest(null));
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);

        var ret = await grantee.PostAsJsonAsync($"/api/submissions/{submitted.Id}/return",
            new ApprovalActionRequest("لا"));
        Assert.Equal(HttpStatusCode.Forbidden, ret.StatusCode);

        var esc = await grantee.PostAsJsonAsync($"/api/submissions/{submitted.Id}/escalate",
            new ApprovalActionRequest(null));
        Assert.Equal(HttpStatusCode.Forbidden, esc.StatusCode);

        var edit = await grantee.PutAsJsonAsync($"/api/submissions/{submitted.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 9m, null, null, null) }));
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);

        var submit = await grantee.PostAsync($"/api/submissions/{submitted.Id}/submit", null);
        Assert.Equal(HttpStatusCode.Forbidden, submit.StatusCode);
    }

    [Fact]
    public async Task NonAdmin_CannotManageGrants_403()
    {
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (grantee, granteeId) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");

        var list = await grantee.GetAsync("/api/report-view-grants");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

        var create = await grantee.PostAsJsonAsync("/api/report-view-grants",
            new CreateReportViewGrantRequest(granteeId, ReportViewGrantScopeKind.User, TargetUserId: targetId));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        var del = await grantee.DeleteAsync($"/api/report-view-grants/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotManageGrants_401()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/report-view-grants")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/report-view-grants",
                new CreateReportViewGrantRequest(Guid.NewGuid(), ReportViewGrantScopeKind.User, TargetUserId: Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.DeleteAsync($"/api/report-view-grants/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/report-view-grants/effective/me")).StatusCode);
    }

    [Fact]
    public async Task DuplicateActiveGrant_Returns409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (_, granteeId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var first = await admin.PostAsJsonAsync("/api/report-view-grants",
            new CreateReportViewGrantRequest(granteeId, ReportViewGrantScopeKind.User, TargetUserId: targetId));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await admin.PostAsJsonAsync("/api/report-view-grants",
            new CreateReportViewGrantRequest(granteeId, ReportViewGrantScopeKind.User, TargetUserId: targetId));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Revoke_DisablesVisibility_AndReactivateRestores()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (target, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (grantee, granteeId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var submitted = await SubmitReportAsync(target, templateId, fieldId, TestCalendar.Cycle(6));
        var grant = await (await admin.PostAsJsonAsync("/api/report-view-grants",
            new CreateReportViewGrantRequest(granteeId, ReportViewGrantScopeKind.User, TargetUserId: targetId)))
            .ReadAsync<ReportViewGrantDto>();

        Assert.Equal(HttpStatusCode.OK, (await grantee.GetAsync($"/api/submissions/{submitted.Id}")).StatusCode);

        var revoke = await admin.DeleteAsync($"/api/report-view-grants/{grant!.Id}");
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await grantee.GetAsync($"/api/submissions/{submitted.Id}")).StatusCode);

        // إعادة الإنشاء تعيد تفعيل المُلغى (لا تكرار) ⇒ الرؤية تعود.
        var reactivate = await admin.PostAsJsonAsync("/api/report-view-grants",
            new CreateReportViewGrantRequest(granteeId, ReportViewGrantScopeKind.User, TargetUserId: targetId));
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await grantee.GetAsync($"/api/submissions/{submitted.Id}")).StatusCode);
    }

    [Fact]
    public async Task EffectiveForMe_ReturnsGranteesActiveGrants()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (grantee, granteeId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var created = await (await admin.PostAsJsonAsync("/api/report-view-grants",
            new CreateReportViewGrantRequest(granteeId, ReportViewGrantScopeKind.User, TargetUserId: targetId)))
            .ReadAsync<ReportViewGrantDto>();

        var mine = await (await grantee.GetAsync("/api/report-view-grants/effective/me"))
            .ReadAsync<List<ReportViewGrantDto>>();
        Assert.Contains(mine!, g => g.Id == created!.Id && g.TargetUserId == targetId);
    }

    [Fact]
    public async Task CreateGrant_ScopeMismatch_Returns400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, granteeId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // نطاق المستخدم مع فريق مستهدَف (تعارض).
        var res = await admin.PostAsJsonAsync("/api/report-view-grants",
            new CreateReportViewGrantRequest(granteeId, ReportViewGrantScopeKind.User, TargetTeamId: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task WithoutGrant_GranteeSeesOnlyOwn_BaseScopeUnaffected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (target, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (grantee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var submitted = await SubmitReportAsync(target, templateId, fieldId, TestCalendar.Cycle(7));

        // بلا منح: المستفيد لا يرى تقرير الهدف (المنح معزول ولا يُوسّع النطاق الأساسي).
        var list = await (await grantee.GetAsync($"/api/submissions?periodKey={TestCalendar.Cycle(7)}"))
            .ReadAsync<List<SubmissionDto>>();
        Assert.DoesNotContain(list!, s => s.Id == submitted.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await grantee.GetAsync($"/api/submissions/{submitted.Id}")).StatusCode);
    }
}
