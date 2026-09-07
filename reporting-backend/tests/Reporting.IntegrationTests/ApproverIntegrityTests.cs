using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Directory;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// RPT-APPROVER-INTEGRITY-01 — سلامة مسار الاعتماد بعد تغيّر التنظيم.
///
/// السبب الجذريّ المُعالَج: المعتمِد الحاليّ لقطة تُلتقَط لحظة التسليم ولا يُعاد توجيهها عند تعطيل
/// المعتمِد أو حذفه صلبًا لاحقًا، والقوائم تُرشَّح بـ CurrentApproverId ⇒ تسليم مفتوح لا يراه أحد.
///
/// طبقتا العلاج المختبَرتان هنا: (أ) حارس التعطيل الذي يعيد التوجيه أو يحجب التعطيل،
/// (ب) سطح الإنقاذ الإداريّ الذي يكشف ما نشأ سابقًا. مع الضوابط السالبة (Returned/Closed/محذوف إداريًّا).
///
/// ملاحظة على القياس: قاعدة الاختبارات مشتركة ومتراكمة ⇒ كلّ التوكيدات على **معرّفات محدّدة**
/// أنشأها الاختبار نفسه، ولا توكيد على عدّ عالميّ إطلاقًا.
/// </summary>
[Collection("Integration")]
public class ApproverIntegrityTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ApproverIntegrityTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== أدوات مساعدة =====

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب سلامة المعتمِد {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
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
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey)))
            .ReadAsync<SubmissionDto>();
        await submitter.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 100m, null, null, null) }));
        return (await (await submitter.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
    }

    /// <summary>يعطّل مستخدمًا عبر واجهة الدليل التنظيميّ مع الحفاظ على بقيّة حقوله كما هي.</summary>
    private async Task<HttpResponseMessage> DeactivateAsync(HttpClient admin, Guid userId)
    {
        string name, email;
        Guid? deptId, teamId, mgrId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == userId);
            name = u.FullName;
            email = u.Email!;
            deptId = u.DepartmentId;
            teamId = u.TeamId;
            mgrId = u.ManagerId;
        }
        return await admin.PutAsJsonAsync($"/api/directory/users/{userId}",
            new UpdateUserRequest(name, email, false, deptId, teamId, mgrId));
    }

    private async Task<(Guid? ApproverId, SubmissionStatus Status, bool IsDeleted)> ReadSubmissionAsync(Guid submissionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.ReportSubmissions.IgnoreQueryFilters().AsNoTracking().FirstAsync(x => x.Id == submissionId);
        return (s.CurrentApproverId, s.Status, s.IsDeleted);
    }

    private async Task<Guid?> ReadPendingStepApproverAsync(Guid submissionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ApprovalSteps.AsNoTracking()
            .Where(a => a.ReportSubmissionId == submissionId && a.Status == ApprovalStatus.Pending)
            .OrderByDescending(a => a.Level)
            .Select(a => (Guid?)a.ApproverId)
            .FirstOrDefaultAsync();
    }

    private async Task MutateSubmissionAsync(Guid submissionId, Action<Reporting.Domain.Entities.Submissions.ReportSubmission> mutate)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.ReportSubmissions.IgnoreQueryFilters().FirstAsync(x => x.Id == submissionId);
        mutate(s);
        await db.SaveChangesAsync();
    }

    private async Task SetTeamActiveAsync(Guid teamId, bool isActive)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t = await db.Teams.FirstAsync(x => x.Id == teamId);
        t.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    private async Task<ApproverIntegrityReportDto> GetIntegrityAsync(HttpClient admin)
        => (await (await admin.GetAsync("/api/submissions/approver-integrity"))
            .ReadAsync<ApproverIntegrityReportDto>())!;

    private async Task<int> CountAuditAsync(string action, Guid entityId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AuditLogs.AsNoTracking().CountAsync(a => a.Action == action && a.EntityId == entityId);
    }

    // ===== 1) معتمِد معطَّل ⇒ إعادة توجيه تلقائيّة عند التعطيل =====
    [Fact]
    public async Task InactiveApprover_OnDeactivation_SubmissionIsReroutedToNextValidApprover()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gmId);
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W02");
        Assert.Equal(tlId, submitted.CurrentApproverId);

        var res = await DeactivateAsync(admin, tlId);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var after = await ReadSubmissionAsync(submitted.Id);
        Assert.Equal(gmId, after.ApproverId);
        Assert.Equal(SubmissionStatus.Submitted, after.Status);
        Assert.Equal(gmId, await ReadPendingStepApproverAsync(submitted.Id));
    }

    // ===== 2) مرجع معتمِد يتيم (حذف صلب سابق) ⇒ يظهر في سطح الإنقاذ =====
    [Fact]
    public async Task OrphanApproverReference_IsSurfacedByRescueEndpoint()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W03");

        await MutateSubmissionAsync(submitted.Id, s => s.CurrentApproverId = Guid.NewGuid());

        var report = await GetIntegrityAsync(admin);
        var item = Assert.Single(report.Items, i => i.SubmissionId == submitted.Id);
        Assert.Equal(ApproverIssueKind.OrphanApprover, item.Kind);
        Assert.Equal(gmId, item.SuggestedApproverId);
    }

    // ===== 3) معتمِد فارغ على تسليم مفتوح ⇒ يظهر في سطح الإنقاذ =====
    [Fact]
    public async Task NullApproverOnOpenSubmission_IsSurfacedByRescueEndpoint()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W04");

        await MutateSubmissionAsync(submitted.Id, s => s.CurrentApproverId = null);

        var report = await GetIntegrityAsync(admin);
        var item = Assert.Single(report.Items, i => i.SubmissionId == submitted.Id);
        Assert.Equal(ApproverIssueKind.NullApprover, item.Kind);
    }

    // ===== 4) تسليم Returned بلا معتمِد ⇒ سلوك مصمَّم: لا يظهر ولا يُعاد توجيهه =====
    [Fact]
    public async Task ReturnedSubmission_IsNeitherSurfacedNorRerouted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (teamLeader, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gmId);
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W05");
        var returned = await teamLeader.PostAsJsonAsync($"/api/submissions/{submitted.Id}/return",
            new ApprovalActionRequest("يُرجى الاستكمال"));
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);

        var beforeState = await ReadSubmissionAsync(submitted.Id);
        Assert.Equal(SubmissionStatus.Returned, beforeState.Status);
        Assert.Null(beforeState.ApproverId);

        var report = await GetIntegrityAsync(admin);
        Assert.DoesNotContain(report.Items, i => i.SubmissionId == submitted.Id);

        Assert.Equal(HttpStatusCode.OK, (await DeactivateAsync(admin, tlId)).StatusCode);

        var afterState = await ReadSubmissionAsync(submitted.Id);
        Assert.Equal(SubmissionStatus.Returned, afterState.Status);
        Assert.Null(afterState.ApproverId);
    }

    // ===== 5) تسليم مغلق ⇒ لا يتغيّر ولا يظهر حتى لو كان معتمِده معطَّلًا =====
    [Fact]
    public async Task ClosedSubmission_IsNeverModifiedNorSurfaced()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gmId);
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W06");
        await MutateSubmissionAsync(submitted.Id, s =>
        {
            s.Status = SubmissionStatus.Closed;
            s.ClosedAtUtc = DateTime.UtcNow;
            s.CurrentApproverId = tlId;
        });

        var report = await GetIntegrityAsync(admin);
        Assert.DoesNotContain(report.Items, i => i.SubmissionId == submitted.Id);

        Assert.Equal(HttpStatusCode.OK, (await DeactivateAsync(admin, tlId)).StatusCode);

        var after = await ReadSubmissionAsync(submitted.Id);
        Assert.Equal(SubmissionStatus.Closed, after.Status);
        Assert.Equal(tlId, after.ApproverId);
    }

    // ===== 6) تسليم محذوف إداريًّا ⇒ لا يظهر ولا يُعاد توجيهه =====
    [Fact]
    public async Task AdministrativelyDeletedSubmission_IsNeitherSurfacedNorRerouted()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gmId);
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W07");
        var del = await admin.PostAsJsonAsync($"/api/submissions/{submitted.Id}/admin-delete",
            new AdminDeleteRequest("حذف إداريّ ضمن اختبار سلامة المعتمِد"));
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var report = await GetIntegrityAsync(admin);
        Assert.DoesNotContain(report.Items, i => i.SubmissionId == submitted.Id);

        Assert.Equal(HttpStatusCode.OK, (await DeactivateAsync(admin, tlId)).StatusCode);

        var after = await ReadSubmissionAsync(submitted.Id);
        Assert.True(after.IsDeleted);
        Assert.Null(after.ApproverId);
    }

    // ===== 7) فريق متوقف (IsActive=false) ⇒ مسار قائد الفريق يُتخطّى وتُستعمل الإدارة المباشرة =====
    [Fact]
    public async Task StoppedTeam_RerouteSkipsTeamLeaderPathAndUsesDirectManager()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gmId);
        var (_, backupLeaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gmId);
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W08");
        Assert.Equal(tlId, submitted.CurrentApproverId);

        // إيقاف الفريق إداريًّا ثم تنصيب قائد بديل نشط: لا يجوز أن يُختار لأنّ الفريق نفسه متوقّف.
        await SetTeamActiveAsync(teamId, false);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var t = await db.Teams.FirstAsync(x => x.Id == teamId);
            t.TeamLeaderId = backupLeaderId;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await DeactivateAsync(admin, tlId)).StatusCode);

        var after = await ReadSubmissionAsync(submitted.Id);
        Assert.Equal(gmId, after.ApproverId);
        Assert.NotEqual(backupLeaderId, after.ApproverId);
    }

    // ===== 8) فريق نشط بقائد صالح ⇒ تعطيل مستخدم غير ذي صلة لا يمسّ التسليم =====
    [Fact]
    public async Task ActiveTeamWithValidLeader_UnrelatedDeactivationLeavesSubmissionUntouched()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gmId);
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);
        var (_, unrelatedId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W09");
        Assert.Equal(tlId, submitted.CurrentApproverId);

        Assert.Equal(HttpStatusCode.OK, (await DeactivateAsync(admin, unrelatedId)).StatusCode);

        var after = await ReadSubmissionAsync(submitted.Id);
        Assert.Equal(tlId, after.ApproverId);

        var report = await GetIntegrityAsync(admin);
        Assert.DoesNotContain(report.Items, i => i.SubmissionId == submitted.Id);
    }

    // ===== 9) قائد فريق بلا الدور الاسميّ ⇒ سلطة الاعتماد من TeamLeaderId لا من اسم الدور =====
    // (الضابط الحاكم لحالة «القائد الرسميّ موظّف بالدور»: النظام لا يشترط دور TeamLeader للاعتماد.)
    [Fact]
    public async Task LeaderWithoutTeamLeaderRole_CanStillApprove_AndIsReroutedOnDeactivation()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (leaderClientOwner, plainLeaderId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, plainLeaderId, empId);

        var first = await SubmitReportAsync(employee, templateId, fieldId, "2026-W10");
        Assert.Equal(plainLeaderId, first.CurrentApproverId);

        // الاعتماد ينجح رغم أنّ القائد يحمل دور Employee فقط.
        var approve = await leaderClientOwner.PostAsJsonAsync($"/api/submissions/{first.Id}/approve",
            new ApprovalActionRequest("معتمد"));
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        // تسليم ثانٍ معلّق ثمّ تعطيل القائد ⇒ إعادة توجيه للإدارة المباشرة.
        var second = await SubmitReportAsync(employee, templateId, fieldId, "2026-W11");
        Assert.Equal(plainLeaderId, second.CurrentApproverId);

        Assert.Equal(HttpStatusCode.OK, (await DeactivateAsync(admin, plainLeaderId)).StatusCode);
        Assert.Equal(gmId, (await ReadSubmissionAsync(second.Id)).ApproverId);
    }

    // ===== 10) الأثر التدقيقيّ: user.deactivated + approval.rerouted =====
    [Fact]
    public async Task Deactivation_WritesExplicitAuditEvents()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gmId);
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W12");
        Assert.Equal(0, await CountAuditAsync("user.deactivated", tlId));

        Assert.Equal(HttpStatusCode.OK, (await DeactivateAsync(admin, tlId)).StatusCode);

        Assert.Equal(1, await CountAuditAsync("user.deactivated", tlId));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rerouted = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "approval.rerouted")
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => a.DataJson!.ToString())
            .Take(20).ToListAsync();
        Assert.Contains(rerouted, j => j.Contains(submitted.Id.ToString()));
    }

    // ===== 11) إعادة التوجيه Idempotent: الاستدعاء الثاني لا يجد شيئًا ولا يغيّر شيئًا =====
    [Fact]
    public async Task Reroute_IsIdempotent_SecondRunChangesNothing()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gmId);
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W13");
        Assert.Equal(HttpStatusCode.OK, (await DeactivateAsync(admin, tlId)).StatusCode);
        Assert.Equal(gmId, (await ReadSubmissionAsync(submitted.Id)).ApproverId);

        using var scope = _factory.Services.CreateScope();
        var submissions = scope.ServiceProvider.GetRequiredService<ISubmissionService>();
        var again = await submissions.RerouteApprovalsForDeactivatedUserAsync(tlId, gmId, dryRun: false);

        Assert.True(again.Succeeded);
        Assert.Equal(0, again.Value!.ReroutedCount);
        Assert.Equal(0, again.Value!.FailedCount);
        Assert.Equal(gmId, (await ReadSubmissionAsync(submitted.Id)).ApproverId);
    }

    // ===== 12) لا تسرّب عبر الفرق: تعطيل قائد فريق أ لا يمسّ تسليم فريق ب =====
    [Fact]
    public async Task Reroute_DoesNotLeakAcrossTeams()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, leaderA) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gmId);
        var (empAClient, empA) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderA, empA);

        var (_, leaderB) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gmId);
        var (empBClient, empB) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderB, empB);

        var subA = await SubmitReportAsync(empAClient, templateId, fieldId, "2026-W14");
        var subB = await SubmitReportAsync(empBClient, templateId, fieldId, "2026-W14");
        Assert.Equal(leaderA, subA.CurrentApproverId);
        Assert.Equal(leaderB, subB.CurrentApproverId);

        Assert.Equal(HttpStatusCode.OK, (await DeactivateAsync(admin, leaderA)).StatusCode);

        Assert.Equal(gmId, (await ReadSubmissionAsync(subA.Id)).ApproverId);
        Assert.Equal(leaderB, (await ReadSubmissionAsync(subB.Id)).ApproverId);
    }

    // ===== 13) انعدام أيّ بديل صالح ⇒ يُحجَب التعطيل (منع نشوء تسليم غير مرئيّ) =====
    [Fact]
    public async Task NoValidSuccessor_DeactivationIsBlocked_AndSubmissionStaysVisible()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        // المقدّم من الطبقة العليا بلا مدير مباشر ⇒ لا تصعيد عامّ بعد قائد الفريق (APPROVAL-FALLBACK-R1).
        var (seniorClient, seniorId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, leaderId, seniorId);

        var submitted = await SubmitReportAsync(seniorClient, templateId, fieldId, "2026-W15");
        Assert.Equal(leaderId, submitted.CurrentApproverId);

        var res = await DeactivateAsync(admin, leaderId);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        // لا تعطيل ولا تغيير: التسليم يبقى مرئيًّا لمعتمِده الأصليّ.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.Users.AsNoTracking().Where(u => u.Id == leaderId).Select(u => u.IsActive).FirstAsync());
        }
        Assert.Equal(leaderId, (await ReadSubmissionAsync(submitted.Id)).ApproverId);
        Assert.True(await CountAuditAsync("user.deactivated", leaderId) == 0);
    }

    // ===== 14) منع الحذف الصلب لمستخدم له مراجع تشغيليّة (منشأ المرجع اليتيم) =====
    [Fact]
    public async Task HardDelete_IsBlocked_WhenUserHasOperationalReferences()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishTemplateAsync(admin);

        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, gmId);
        await SubmitReportAsync(employee, templateId, fieldId, "2026-W16");

        var res = await admin.DeleteAsync($"/api/directory/users/{empId}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Users.AsNoTracking().AnyAsync(u => u.Id == empId));
    }

    // ===== 15) سطح الإنقاذ محكوم بالتفويض: موظّف عاديّ لا يراه =====
    [Fact]
    public async Task RescueEndpoint_IsForbiddenForNonPrivilegedRoles()
    {
        var employee = await TestAuth.LoginAsRoleAsync(_factory, Roles.Employee);
        var res = await employee.GetAsync("/api/submissions/approver-integrity");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
