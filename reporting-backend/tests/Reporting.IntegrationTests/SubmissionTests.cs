using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class SubmissionTests
{
    private readonly CustomWebApplicationFactory _factory;

    public SubmissionTests(CustomWebApplicationFactory factory) => _factory = factory;

    /// <summary>ينشئ قالبًا منشورًا بحقل واحد مطلوب ويعيد (معرّف القالب، معرّف الحقل).</summary>
    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب أداء {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();

        var publishRes = await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishRes.StatusCode);

        return (created.Id, field!.Id);
    }

    [Fact]
    public async Task FullLifecycle_Draft_Fill_Submit_ManagerApprove_Closed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        // APPROVAL-FALLBACK-R1: المعتمِد الأعلى من الطبقة العليا (CEO) — طبقة عليا بلا مدير ⇒ يُغلق في خطوة واحدة.
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);

        // إنشاء مسودة
        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W23")))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Draft, draft!.Status);
        Assert.True(draft.CanEdit);

        // محاولة الإرسال بدون تعبئة الحقل المطلوب
        var earlySubmit = await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, earlySubmit.StatusCode);

        // تعبئة القيم
        var saveRes = await employee.PutAsJsonAsync($"/api/submissions/{draft.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 1500m, null, null, null) }));
        Assert.Equal(HttpStatusCode.OK, saveRes.StatusCode);

        // الإرسال
        var submitted = await (await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);
        Assert.Equal(ceoId, submitted.CurrentApproverId);
        Assert.False(submitted.CanEdit);

        // المعتمِد الأعلى (CEO) يعتمد — طبقة عليا بلا مدير ⇒ مُغلق
        var approved = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);
        Assert.Null(approved.CurrentApproverId);
        Assert.NotNull(approved.ClosedAtUtc);
        Assert.Single(approved.ApprovalSteps);
        Assert.Equal(ApprovalStatus.Approved, approved.ApprovalSteps[0].Status);
    }

    [Fact]
    public async Task TwoLevelApproval_DirectManager_Then_NextLevel_Closed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (gm, gmId) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager", gmId);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W24")))
            .ReadAsync<SubmissionDto>();
        await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 200m, null, null, null) }));
        await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);

        var afterDirect = await (await manager.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.ApprovedByDirectManager, afterDirect!.Status);
        Assert.Equal(gmId, afterDirect.CurrentApproverId);

        var afterNext = await (await gm.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, afterNext!.Status);
        Assert.Null(afterNext.CurrentApproverId);
    }

    [Fact]
    public async Task Return_Allows_EmployeeToEditAndResubmit()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W26")))
            .ReadAsync<SubmissionDto>();
        await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 10m, null, null, null) }));
        await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);

        var returned = await (await manager.PostAsJsonAsync($"/api/submissions/{draft.Id}/return",
            new ApprovalActionRequest("يرجى التصحيح"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Returned, returned!.Status);
        Assert.Null(returned.CurrentApproverId);

        // الموظف يرى أنه قابل للتعديل ويعيد الإرسال
        var mine = await (await employee.GetAsync($"/api/submissions/{draft.Id}")).ReadAsync<SubmissionDto>();
        Assert.True(mine!.CanEdit);

        var resubmitted = await (await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, resubmitted!.Status);
        Assert.Equal(managerId, resubmitted.CurrentApproverId);
    }

    [Fact]
    public async Task CreateDraft_IsIdempotent_PerPeriod()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishTemplateAsync(admin);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var first = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W30")))
            .ReadAsync<SubmissionDto>();
        var second = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W30")))
            .ReadAsync<SubmissionDto>();

        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task Idor_OtherEmployee_CannotAccessOrEditSubmission()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (ownerClient, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (attackerClient, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var draft = await (await ownerClient.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W31")))
            .ReadAsync<SubmissionDto>();

        var getRes = await attackerClient.GetAsync($"/api/submissions/{draft!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, getRes.StatusCode);

        var editRes = await attackerClient.PutAsJsonAsync($"/api/submissions/{draft.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 99m, null, null, null) }));
        Assert.Equal(HttpStatusCode.Forbidden, editRes.StatusCode);

        var submitRes = await attackerClient.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.Forbidden, submitRes.StatusCode);
    }

    [Fact]
    public async Task NonApprover_CannotApprove_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        var (intruder, _) = await TestAuth.CreateUserAsync(_factory, "Manager");

        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W32")))
            .ReadAsync<SubmissionDto>();
        await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 5m, null, null, null) }));
        await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);

        // مدير آخر (ليس الموافِق الحالي) لا يستطيع الاعتماد
        var res = await intruder.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest(null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Summary_AggregatesByStatus_ForManagement()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, TestCalendar.Cycle(1))))
            .ReadAsync<SubmissionDto>();

        var summary = await (await admin.GetAsync($"/api/submissions/summary?periodKey={TestCalendar.Cycle(1)}"))
            .ReadAsync<SubmissionSummaryDto>();
        Assert.NotNull(summary);
        Assert.True(summary!.Total >= 1);
        Assert.Contains(summary.ByStatus, c => c.Status == SubmissionStatus.Draft);
    }

    [Fact]
    public async Task Anonymous_CannotListSubmissions_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/submissions/mine");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ==================== حذف المسودة (Draft Deletion) ====================

    [Fact]
    public async Task DeleteDraft_Owner_DeletesOwnDraft_204_AndDisappearsFromList()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, TestCalendar.Cycle(2))))
            .ReadAsync<SubmissionDto>();
        // تعبئة قيمة كي نتحقّق من حذف القيم المرتبطة عبر Cascade.
        await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 42m, null, null, null) }));

        var del = await employee.DeleteAsync($"/api/submissions/{draft.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // لم تعد موجودة (GET ⇒ 404) ولا تظهر في قائمتي.
        var get = await employee.GetAsync($"/api/submissions/{draft.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        var mine = await (await employee.GetAsync("/api/submissions/mine"))
            .ReadAsync<List<SubmissionListItemDto>>();
        Assert.DoesNotContain(mine!, s => s.Id == draft.Id);
    }

    [Fact]
    public async Task DeleteDraft_OtherEmployee_CannotDelete_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishTemplateAsync(admin);
        var (owner, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (attacker, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var draft = await (await owner.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, TestCalendar.Cycle(3))))
            .ReadAsync<SubmissionDto>();

        var del = await attacker.DeleteAsync($"/api/submissions/{draft!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);

        // ما زالت موجودة لصاحبها.
        var get = await owner.GetAsync($"/api/submissions/{draft.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task DeleteDraft_AdminCannotDeleteOthersDraft_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishTemplateAsync(admin);
        var (owner, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var draft = await (await owner.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, TestCalendar.Cycle(4))))
            .ReadAsync<SubmissionDto>();

        // حتى الأدمن لا يحذف مسودة موظّف آخر (owner-only).
        var del = await admin.DeleteAsync($"/api/submissions/{draft!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);
    }

    [Fact]
    public async Task DeleteDraft_SubmittedReport_CannotDelete_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, TestCalendar.Cycle(5))))
            .ReadAsync<SubmissionDto>();
        await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 7m, null, null, null) }));
        await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);

        // بعد الإرسال لا يمكن الحذف.
        var del = await employee.DeleteAsync($"/api/submissions/{draft.Id}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
    }

    [Fact]
    public async Task DeleteDraft_ClosedReport_CannotDelete_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);
        // APPROVAL-FALLBACK-R1: موظّف مديره المباشر CEO ⇒ الإرسال يوجَّه للـCEO، واعتماده يُغلق التقرير (طبقة عليا بلا مدير).
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);

        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, TestCalendar.Cycle(6))))
            .ReadAsync<SubmissionDto>();
        await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 3m, null, null, null) }));
        var submitted = await (await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);

        var closed = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, closed!.Status);

        var del = await employee.DeleteAsync($"/api/submissions/{draft.Id}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
    }

    [Fact]
    public async Task DeleteDraft_NonExistent_404()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var del = await employee.DeleteAsync($"/api/submissions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
    }

    [Fact]
    public async Task DeleteDraft_Anonymous_401()
    {
        var client = _factory.CreateClient();
        var del = await client.DeleteAsync($"/api/submissions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, del.StatusCode);
    }
}
