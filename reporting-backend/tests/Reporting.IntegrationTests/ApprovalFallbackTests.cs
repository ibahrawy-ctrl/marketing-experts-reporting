using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Leave;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// APPROVAL-FALLBACK-R1 — اختبارات سلسلة الاعتماد الاحتياطية لتقارير التسليم فقط.
/// الترتيب: قائد فريق المقدّم ← المدير المباشر (ManagerId) ← أول مدير عام نشط ← أول Admin/CEO نشط.
/// الطبقة العليا { GeneralManager, Admin, CEO } تُغلق التقرير عند غياب مدير مباشر صالح (اعتماد نهائي).
/// لا يُغلق/يُعلَّق تقرير موظّف لمجرد غياب قائد الفريق أو المدير، مع منع اعتماد الذات ومنع الحلقات.
/// </summary>
[Collection("Integration")]
public class ApprovalFallbackTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ApprovalFallbackTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== أدوات مساعدة =====

    /// <summary>ينشئ قالبًا منشورًا بحقل واحد مطلوب ويعيد (معرّف القالب، معرّف الحقل).</summary>
    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب اعتماد {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();

        var publishRes = await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishRes.StatusCode);

        return (created.Id, field!.Id);
    }

    /// <summary>ينشئ مسودّة، يعبّئ الحقل المطلوب، ثم يرسلها، ويعيد التسليم بعد الإرسال.</summary>
    private static async Task<SubmissionDto> SubmitReportAsync(
        HttpClient submitter, Guid templateId, Guid fieldId, string periodKey)
    {
        var draft = await (await submitter.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey)))
            .ReadAsync<SubmissionDto>();
        await submitter.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 100m, null, null, null) }));
        return (await (await submitter.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
    }

    /// <summary>يقرأ أدوار مستخدم من قاعدة البيانات عبر UserManager.</summary>
    private async Task<IList<string>> GetRolesAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        return await users.GetRolesAsync(user!);
    }

    /// <summary>يضبط المدير المباشر للمستخدم على نفسه (لاختبار منع اعتماد الذات).</summary>
    private async Task SetManagerToSelfAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = await db.Users.FirstAsync(x => x.Id == userId);
        u.ManagerId = userId;
        await db.SaveChangesAsync();
    }

    private static readonly string[] SeniorRoleNames = { "GeneralManager", "Admin", "CEO" };

    // ===== السيناريوهات الـ13 =====

    // 1) للمقدّم قائد فريق ⇒ يُوجَّه أولًا لقائد الفريق.
    [Fact]
    public async Task Scenario01_SubmitterWithTeamLeader_RoutesToTeamLeaderFirst()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W01");

        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);
        Assert.Equal(tlId, submitted.CurrentApproverId);
    }

    // 2) لا قائد فريق ⇒ يُوجَّه للمدير المباشر.
    [Fact]
    public async Task Scenario02_NoTeamLeader_RoutesToDirectManager()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W02");

        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);
        Assert.Equal(managerId, submitted.CurrentApproverId);
    }

    // 3) لا قائد فريق ولا مدير مباشر ⇒ تصعيد عام لأول مدير عام نشط.
    [Fact]
    public async Task Scenario03_NoTeamLeaderNoManager_EscalatesToGeneralManagerTier()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        // نضمن وجود مدير عام نشط واحد على الأقل في القاعدة.
        await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee"); // بلا فريق وبلا مدير

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W03");

        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);
        Assert.NotNull(submitted.CurrentApproverId);
        var roles = await GetRolesAsync(submitted.CurrentApproverId!.Value);
        Assert.Contains("GeneralManager", roles);
    }

    // 4) البديل النهائي (طبقة عليا CEO) يعتمد ⇒ يُغلق التقرير (اعتماد نهائي).
    // ملاحظة: فرع «لا مدير عام في كامل النظام ⇒ Admin/CEO» غير قابل للترتيب في قاعدة الاختبار المشتركة
    //         (يوجد دائمًا مديرون عامّون)، لذا نتحقّق من الإغلاق النهائي عند الطبقة العليا عبر CEO مباشرة.
    [Fact]
    public async Task Scenario04_FinalFallbackSeniorTier_ClosesReport()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W04");
        Assert.Equal(ceoId, submitted.CurrentApproverId);

        var closed = await (await ceo.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest("اعتماد نهائي"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, closed!.Status);
        Assert.Null(closed.CurrentApproverId);
        Assert.NotNull(closed.ClosedAtUtc);
    }

    // 5) لا يُوجَّه الطلب لمقدّمه نفسه (منع اعتماد الذات) — حتى لو كان مديره المباشر هو نفسه.
    [Fact]
    public async Task Scenario05_SubmitterNotRoutedToSelf()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        await TestAuth.CreateUserAsync(_factory, "GeneralManager"); // بديل عام مضمون
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetManagerToSelfAsync(empId); // المدير المباشر = المقدّم نفسه

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W05");

        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);
        Assert.NotNull(submitted.CurrentApproverId);
        Assert.NotEqual(empId, submitted.CurrentApproverId!.Value);
    }

    // 6) قائد الفريق هو المقدّم نفسه ⇒ يُتخطّى ويُوجَّه للمدير المباشر.
    [Fact]
    public async Task Scenario06_TeamLeaderIsSubmitter_SkipsToManager()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        // المقدّم قائد فريقه ذاته ⇒ خطوة قائد الفريق تُتخطّى.
        await TestAuth.CreateTeamWithLeaderAsync(_factory, empId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W06");

        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);
        Assert.Equal(managerId, submitted.CurrentApproverId);
    }

    // 7) المدير المباشر هو المقدّم نفسه ⇒ يُتخطّى ويُصعَّد لطبقة عليا (مدير عام/Admin/CEO).
    [Fact]
    public async Task Scenario07_ManagerIsSubmitter_SkipsToSeniorFallback()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetManagerToSelfAsync(empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W07");

        Assert.NotNull(submitted.CurrentApproverId);
        Assert.NotEqual(empId, submitted.CurrentApproverId!.Value);
        var roles = await GetRolesAsync(submitted.CurrentApproverId.Value);
        Assert.Contains(roles, r => SeniorRoleNames.Contains(r));
    }

    // 8) لا تكرار لأي معتمِد في سلسلة الخطوات (منع الحلقات عبر مجموعة المُزارين).
    [Fact]
    public async Task Scenario08_NoDuplicateApproverInChain()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (gm, gmId) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager", gmId);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W08");
        Assert.Equal(managerId, submitted.CurrentApproverId);

        await manager.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve", new ApprovalActionRequest(null));
        var closed = await (await gm.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();

        Assert.Equal(SubmissionStatus.Closed, closed!.Status);
        var approverIds = closed.ApprovalSteps.Select(s => s.ApproverId).ToList();
        Assert.Equal(approverIds.Count, approverIds.Distinct().Count());
    }

    // 9) تُغلق السلسلة بأمان عند الطبقة العليا (مدير عام) بلا تعليق.
    [Fact]
    public async Task Scenario09_ChainClosesSafelyAtSeniorTier()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (gm, gmId) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager", gmId);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W09");

        var afterManager = await (await manager.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(gmId, afterManager!.CurrentApproverId);

        var closed = await (await gm.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, closed!.Status);
        Assert.Null(closed.CurrentApproverId);
    }

    // 10) سلسلة ManagerId القائمة تبقى تعمل كما كانت (توافق خلفي): مدير مباشر ← مستوى تالٍ ← إغلاق.
    [Fact]
    public async Task Scenario10_ExistingManagerIdChain_StillWorks()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (gm, gmId) = await TestAuth.CreateUserAsync(_factory, "GeneralManager");
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager", gmId);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W10");
        Assert.Equal(managerId, submitted.CurrentApproverId);

        var afterDirect = await (await manager.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.ApprovedByDirectManager, afterDirect!.Status);
        Assert.Equal(gmId, afterDirect.CurrentApproverId);

        var afterNext = await (await gm.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, afterNext!.Status);
    }

    // 11) مسار الإعادة (Returned) يبقى صحيحًا: تُعاد للموظّف ثم يعيد الإرسال لنفس المعتمِد.
    [Fact]
    public async Task Scenario11_ReturnedFlow_StillCorrect()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W11");
        Assert.Equal(managerId, submitted.CurrentApproverId);

        var returned = await (await manager.PostAsJsonAsync($"/api/submissions/{submitted.Id}/return",
            new ApprovalActionRequest("يرجى التصحيح"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Returned, returned!.Status);
        Assert.Null(returned.CurrentApproverId);

        var resubmitted = await (await employee.PostAsync($"/api/submissions/{submitted.Id}/submit", null))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, resubmitted!.Status);
        Assert.Equal(managerId, resubmitted.CurrentApproverId);
    }

    // 12) إشعار/حدث الاعتماد يعمل: بعد الإغلاق يتلقّى المقدّم إشعار "submission.approved".
    [Fact]
    public async Task Scenario12_ApprovalNotification_IsCreated()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W12");
        var closed = await (await ceo.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, closed!.Status);

        var notificationsJson = await (await employee.GetAsync("/api/notifications")).Content.ReadAsStringAsync();
        Assert.Contains("submission.approved", notificationsJson);
    }

    // 13) لا تأثير على مسار الإجازات: طلب الإجازة يبقى يبدأ عند خطوة قائد الفريق.
    [Fact]
    public async Task Scenario13_NoImpactOnLeaveWorkflow()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        var (_, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        // رصيد افتتاحي كافٍ لتفادي حارس الرصيد.
        await admin.PostAsJsonAsync($"/api/balances/employees/{empId}/opening",
            new OpeningBalanceRequest(BalanceType.AnnualLeave, 365, 2026, "رصيد اختبار"), TestJson.Options);

        var created = await (await employee.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 5),
                null, null, "إجازة اختبار", null), TestJson.Options)).ReadAsync<LeaveRequestDto>();

        Assert.Equal(LeaveRequestStatus.Submitted, created!.Status);
        Assert.Equal(LeaveRequestStep.TeamLeader, created.CurrentStep);
    }

    // 14) (إلزامي) للمقدّم قائد فريق صالح ومدير مباشر صالح معًا ⇒ أوّل معتمِد = قائد الفريق لا المدير.
    //      يُثبت صراحةً أن الترتيب: قائد الفريق ← المدير (لا العكس).
    [Fact]
    public async Task Scenario14_TeamLeaderTakesPriorityOverManager()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        // للموظّف مدير مباشر صالح (managerId) وقائد فريق صالح (tlId) في آنٍ واحد.
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W14");

        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);
        Assert.Equal(tlId, submitted.CurrentApproverId);       // ⇐ قائد الفريق أولًا
        Assert.NotEqual(managerId, submitted.CurrentApproverId); // ⇐ ليس المدير
    }
}
