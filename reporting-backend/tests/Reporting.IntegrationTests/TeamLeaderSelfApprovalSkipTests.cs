using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Leave;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// Phase T-WF1 — تخطّي خطوة قائد الفريق لطلبه الشخصي + معالجة الطلبات العالقة.
/// طلب قائد الفريق العادي (إجازة/استئذان، غير موارد بشرية) يبدأ مباشرةً عند المدير
/// (Status=TeamLeaderApproved/CurrentStep=Manager) مع حدث team_leader_step_skipped؛
/// والطلبات العالقة سابقًا تُعالَج بمسار Admin محروس idempotent. لا يُمَسّ مسار الموظّف العادي ولا HR.
/// </summary>
[Collection("Integration")]
public class TeamLeaderSelfApprovalSkipTests
{
    private readonly CustomWebApplicationFactory _factory;

    public TeamLeaderSelfApprovalSkipTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Manager;
        public required (HttpClient C, Guid Id) Tl;
        public required (HttpClient C, Guid Id) Emp;
        public required (HttpClient C, Guid Id) Hr;
        public required HttpClient Admin;
    }

    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, manager.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tl.UserId);
        var hr = await TestAuth.CreateUserAsync(_factory, Roles.Hr, manager.UserId);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        // فريق فعليّ قائده tl يضمّ الموظّف العادي والمدير (T-WF2): كلاهما له قائد فريق فعليّ
        // فلا يُتخطّى عند الإنشاء — التخطّي يبقى لطلب قائد الفريق نفسه ولمن لا قائد فعليّ له.
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tl.UserId, emp.UserId, manager.UserId);

        foreach (var id in new[] { gm.UserId, manager.UserId, tl.UserId, emp.UserId, hr.UserId })
            await admin.PostAsJsonAsync($"/api/balances/employees/{id}/opening",
                new OpeningBalanceRequest(BalanceType.AnnualLeave, 365, 2026, "رصيد اختبار"), TestJson.Options);

        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            Manager = (manager.Client, manager.UserId),
            Tl = (tl.Client, tl.UserId),
            Emp = (emp.Client, emp.UserId),
            Hr = (hr.Client, hr.UserId),
            Admin = admin,
        };
    }

    private static Task<HttpResponseMessage> CreateLeaveAsync(HttpClient c, DateOnly start, DateOnly end)
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, start, end, null, null, "سبب الإجازة", null),
            TestJson.Options);

    private static Task<HttpResponseMessage> CreatePermissionAsync(HttpClient c, DateOnly day)
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Permission, day, null,
                new TimeOnly(9, 0), new TimeOnly(11, 0), "سبب الاستئذان", null), TestJson.Options);

    private static async Task<LeaveRequestDto> OkAsync(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<LeaveRequestDto>())!;
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private const string SkipText = "تم تخطي مراجعة قائد الفريق لأن مقدم الطلب هو قائد الفريق.";

    // ===== 1) الموظّف العادي يبدأ عند قائد الفريق (لا تخطٍّ) =====
    [Fact]
    public async Task NormalEmployee_StartsAt_TeamLeader()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 8, 3), new(2026, 8, 5)));

        Assert.Equal(LeaveRequestStatus.Submitted, created.Status);
        Assert.Equal(LeaveRequestStep.TeamLeader, created.CurrentStep);
        Assert.DoesNotContain(created.Timeline, e => e.Action == "team_leader_step_skipped");
    }

    // ===== 2) قائد الفريق ينشئ طلبًا جديدًا → يبدأ عند المدير مع حدث التخطّي =====
    [Fact]
    public async Task TeamLeader_NewLeave_SkipsTeamLeader_StartsAtManager()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Tl.C, new(2026, 8, 10), new(2026, 8, 12)));

        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, created.Status);
        Assert.Equal(LeaveRequestStep.Manager, created.CurrentStep);
        Assert.Null(created.TeamLeaderReviewerId); // تخطٍّ آلي لا اعتماد بشري
        var skip = Assert.Single(created.Timeline, e => e.Action == "team_leader_step_skipped");
        Assert.Equal(SkipText, skip.Comment);
        Assert.False(created.IsHrRequest);
    }

    // ===== 2-ب) ينطبق على الاستئذان أيضًا =====
    [Fact]
    public async Task TeamLeader_NewPermission_SkipsTeamLeader_StartsAtManager()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreatePermissionAsync(org.Tl.C, new(2026, 8, 18)));

        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, created.Status);
        Assert.Equal(LeaveRequestStep.Manager, created.CurrentStep);
        Assert.Contains(created.Timeline, e => e.Action == "team_leader_step_skipped");
    }

    // ===== 2-ج) الطلب المُتخطّى يمكن للمدير اعتماده (المسار يكمل سليمًا) =====
    [Fact]
    public async Task TeamLeaderSkipped_Request_ManagerCanApprove()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Tl.C, new(2026, 9, 1), new(2026, 9, 3)));

        var res = await org.Manager.C.PostAsJsonAsync(
            $"/api/leave-requests/{created.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        var approved = await OkAsync(res);
        Assert.Equal(LeaveRequestStatus.ManagerApproved, approved.Status);
        Assert.Equal(LeaveRequestStep.Hr, approved.CurrentStep);
    }

    // ===== 3) معالجة طلب عالق سابقًا لقائد فريق =====
    [Fact]
    public async Task StuckTeamLeaderRequest_Remediated_MovesToManager()
    {
        var org = await BuildOrgAsync();
        var stuckId = await InsertStuckLeaveAsync(org.Tl.Id, "سبب عالق");

        var result = await OkRemediateAsync(org.Admin);
        Assert.Contains(stuckId, result.RemediatedRequestIds);
        Assert.True(result.MatchedCount >= 1);
        Assert.True(result.RemediatedCount >= 1);

        var after = await (await org.Tl.C.GetAsync($"/api/leave-requests/{stuckId}")).ReadAsync<LeaveRequestDto>();
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, after!.Status);
        Assert.Equal(LeaveRequestStep.Manager, after.CurrentStep);
        Assert.Equal("سبب عالق", after.Reason); // لا فقد للبيانات
        Assert.Contains(after.Timeline, e => e.Action == "team_leader_step_skipped");
    }

    // ===== 3-ب) المعالجة idempotent — لا تُعيد معالجة المعالَج =====
    [Fact]
    public async Task Remediation_Idempotent()
    {
        var org = await BuildOrgAsync();
        var stuckId = await InsertStuckLeaveAsync(org.Tl.Id, "سبب");

        var first = await OkRemediateAsync(org.Admin);
        Assert.Contains(stuckId, first.RemediatedRequestIds);

        var second = await OkRemediateAsync(org.Admin);
        Assert.DoesNotContain(stuckId, second.RemediatedRequestIds);
    }

    // ===== 3-ج) المعالجة لا تطال طلب موظّف عادي عالق عند قائد الفريق =====
    [Fact]
    public async Task Remediation_DoesNotTouch_NormalEmployeeRequest()
    {
        var org = await BuildOrgAsync();
        var emp = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 10, 1), new(2026, 10, 2)));
        Assert.Equal(LeaveRequestStep.TeamLeader, emp.CurrentStep);

        var result = await OkRemediateAsync(org.Admin);
        Assert.DoesNotContain(emp.Id, result.RemediatedRequestIds);

        var after = await (await org.Emp.C.GetAsync($"/api/leave-requests/{emp.Id}")).ReadAsync<LeaveRequestDto>();
        Assert.Equal(LeaveRequestStatus.Submitted, after!.Status);
        Assert.Equal(LeaveRequestStep.TeamLeader, after.CurrentStep);
    }

    // ===== 3-د) المعالجة محروسة للأدمن فقط =====
    [Fact]
    public async Task Remediation_NonAdmin_Forbidden()
    {
        var org = await BuildOrgAsync();
        foreach (var c in new[] { org.Manager.C, org.Tl.C, org.Emp.C, org.Gm.C, org.Hr.C })
        {
            var res = await c.PostAsync("/api/leave-requests/remediate-team-leader-stuck", null);
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
        var anon = await _factory.CreateClient().PostAsync("/api/leave-requests/remediate-team-leader-stuck", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
    }

    // ===== 4) طلب المدير (غير قائد الفريق وله قائد فعليّ) لا يُتخطّى ويبدأ عند قائد الفريق =====
    [Fact]
    public async Task ManagerRequest_NotSkipped_StartsAtTeamLeader()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Manager.C, new(2026, 11, 1), new(2026, 11, 3)));

        Assert.Equal(LeaveRequestStatus.Submitted, created.Status);
        Assert.Equal(LeaveRequestStep.TeamLeader, created.CurrentStep);
        Assert.DoesNotContain(created.Timeline, e => e.Action == "team_leader_step_skipped");
    }

    // ===== 5) طلب الموارد البشرية يبقى على مساره (لم يُكسَر) =====
    [Fact]
    public async Task HrRequest_StillRoutesToManager_NotBroken()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Hr.C, new(2026, 11, 10), new(2026, 11, 12)));

        Assert.True(created.IsHrRequest);
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, created.Status);
        Assert.Equal(LeaveRequestStep.Manager, created.CurrentStep);
        Assert.Contains(created.Timeline, e => e.Action == "hr_routed");
        Assert.DoesNotContain(created.Timeline, e => e.Action == "team_leader_step_skipped");
    }

    // ===== مساعدات =====

    private async Task<Guid> InsertStuckLeaveAsync(Guid requesterId, string reason)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new LeaveRequest
        {
            RequesterUserId = requesterId,
            Type = LeaveRequestType.Leave,
            StartDate = new(2027, 3, 1),
            EndDate = new(2027, 3, 3),
            Reason = reason,
            Status = LeaveRequestStatus.Submitted,
            CurrentStep = LeaveRequestStep.TeamLeader,
            IsHrRequest = false,
            TeamLeaderReviewerId = null
        };
        db.LeaveRequests.Add(entity);
        db.LeaveRequestEvents.Add(new LeaveRequestEvent
        {
            LeaveRequestId = entity.Id,
            ActorUserId = requesterId,
            Action = "submitted",
            Step = LeaveRequestStep.Employee,
            FromStatus = LeaveRequestStatus.Draft,
            ToStatus = LeaveRequestStatus.Submitted
        });
        await db.SaveChangesAsync();
        return entity.Id;
    }

    private static async Task<TeamLeaderStuckRemediationResultDto> OkRemediateAsync(HttpClient admin)
    {
        var res = await admin.PostAsync("/api/leave-requests/remediate-team-leader-stuck", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<TeamLeaderStuckRemediationResultDto>())!;
    }
}
