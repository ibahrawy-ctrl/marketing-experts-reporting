using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Leave;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// Phase T-WF2 — توجيه خطوة قائد الفريق إلى قائد الفريق الفعليّ حصرًا.
/// خطوة قائد الفريق (في الطلب العادي) يعتمدها مَن هو == Team.TeamLeaderId للموظّف فقط؛ لا يكفي
/// اتّساع النطاق الإداري (مدير/مدير عام/إدارة عليا) ولا كون المعتمِد قائد فريق آخر.
/// وإن لم يكن للموظّف فريق أو لا قائد فريق محدّد ⇒ fallback آمن: يُتخطّى عند الإنشاء ويُوجَّه للمدير
/// (بلا توقّف) مع حدث team_leader_step_skipped. ويُحفَظ T-WF1 (طلب قائد الفريق نفسه يُتخطّى).
/// </summary>
[Collection("Integration")]
public class TeamLeaderExactRoutingTests
{
    private readonly CustomWebApplicationFactory _factory;

    public TeamLeaderExactRoutingTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Manager;
        public required (HttpClient C, Guid Id) Tl;       // قائد فريق الموظّف الفعليّ
        public required (HttpClient C, Guid Id) OtherTl;  // قائد فريق آخر (ليس قائد فريق الموظّف)
        public required (HttpClient C, Guid Id) Emp;      // موظّف ضمن فريق tl
        public required (HttpClient C, Guid Id) EmpNoTeam;// موظّف بلا فريق (fallback)
        public required (HttpClient C, Guid Id) Hr;
        public required HttpClient Admin;
    }

    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, manager.UserId);
        var otherTl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, manager.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tl.UserId);
        var empNoTeam = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tl.UserId);
        var hr = await TestAuth.CreateUserAsync(_factory, Roles.Hr, manager.UserId);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        // فريق فعليّ قائده tl يضمّ emp فقط؛ empNoTeam يبقى بلا فريق (يختبر مسار fallback).
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tl.UserId, emp.UserId);

        foreach (var id in new[] { gm.UserId, manager.UserId, tl.UserId, otherTl.UserId, emp.UserId, empNoTeam.UserId, hr.UserId })
            await admin.PostAsJsonAsync($"/api/balances/employees/{id}/opening",
                new OpeningBalanceRequest(BalanceType.AnnualLeave, 365, 2026, "رصيد اختبار"), TestJson.Options);

        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            Manager = (manager.Client, manager.UserId),
            Tl = (tl.Client, tl.UserId),
            OtherTl = (otherTl.Client, otherTl.UserId),
            Emp = (emp.Client, emp.UserId),
            EmpNoTeam = (empNoTeam.Client, empNoTeam.UserId),
            Hr = (hr.Client, hr.UserId),
            Admin = admin,
        };
    }

    private static Task<HttpResponseMessage> CreateLeaveAsync(HttpClient c, DateOnly start, DateOnly end)
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, start, end, null, null, "سبب الإجازة", null),
            TestJson.Options);

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

    private static Task<HttpResponseMessage> TeamLeaderApproveAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);

    private static Task<HttpResponseMessage> ManagerApproveAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);

    private static Task<HttpResponseMessage> HrApproveAsync(HttpClient c, Guid id)
        => c.PostAsJsonAsync($"/api/leave-requests/{id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);

    private const string FallbackSkipText =
        "تم تخطي مراجعة قائد الفريق لعدم وجود قائد فريق محدد، وتم توجيه الطلب إلى المدير.";
    private const string SelfSkipText =
        "تم تخطي مراجعة قائد الفريق لأن مقدم الطلب هو قائد الفريق.";

    // ===== A) موظّف له قائد فريق فعليّ: يبدأ عند قائد الفريق؛ القائد الفعليّ وحده يعتمد؛
    //          أيّ مدير/مدير عام/قائد فريق آخر/أدمن نطاقه يشمله ⇒ 403؛ بعد اعتماد القائد ⇒ المدير. =====
    [Fact]
    public async Task A_EmployeeWithTeamLeader_OnlyActualLeader_Approves_HigherScope_Forbidden()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 1, 5), new(2026, 1, 7)));

        Assert.Equal(LeaveRequestStatus.Submitted, created.Status);
        Assert.Equal(LeaveRequestStep.TeamLeader, created.CurrentStep);

        // المدير العام نطاقه يشمل الموظّف لكنه ليس قائد الفريق ⇒ 403 على خطوة قائد الفريق.
        var gmTry = await TeamLeaderApproveAsync(org.Gm.C, created.Id);
        Assert.Equal(HttpStatusCode.Forbidden, gmTry.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(gmTry));

        // المدير المباشر (الأعلى) نطاقه يشمل الموظّف لكنه ليس قائد الفريق ⇒ 403.
        var mgrTry = await TeamLeaderApproveAsync(org.Manager.C, created.Id);
        Assert.Equal(HttpStatusCode.Forbidden, mgrTry.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(mgrTry));

        // قائد فريق آخر (ليس قائد فريق الموظّف) ⇒ 403 (جوهر إصلاح T-WF2).
        var otherTlTry = await TeamLeaderApproveAsync(org.OtherTl.C, created.Id);
        Assert.Equal(HttpStatusCode.Forbidden, otherTlTry.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(otherTlTry));

        // الأدمن أيضًا لا يعتمد خطوة قائد الفريق (ليس القائد الفعليّ) ⇒ 403.
        var adminTry = await TeamLeaderApproveAsync(org.Admin, created.Id);
        Assert.Equal(HttpStatusCode.Forbidden, adminTry.StatusCode);

        // ما زال الطلب عند قائد الفريق (لم تتغيّر حالته بأيّ محاولة مرفوضة).
        var still = await (await org.Emp.C.GetAsync($"/api/leave-requests/{created.Id}")).ReadAsync<LeaveRequestDto>();
        Assert.Equal(LeaveRequestStatus.Submitted, still!.Status);
        Assert.Equal(LeaveRequestStep.TeamLeader, still.CurrentStep);

        // قائد الفريق الفعليّ يعتمد ⇒ ينتقل إلى المدير.
        var approved = await OkAsync(await TeamLeaderApproveAsync(org.Tl.C, created.Id));
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, approved.Status);
        Assert.Equal(LeaveRequestStep.Manager, approved.CurrentStep);
        Assert.Equal(org.Tl.Id, approved.TeamLeaderReviewerId);

        // ثم المدير (ضمن النطاق) يعتمد خطوته (المسار يكمل سليمًا — لم تتغيّر خطوة المدير في T-WF2).
        var mgrApproved = await OkAsync(await ManagerApproveAsync(org.Manager.C, created.Id));
        Assert.Equal(LeaveRequestStatus.ManagerApproved, mgrApproved.Status);
        Assert.Equal(LeaveRequestStep.Hr, mgrApproved.CurrentStep);
    }

    // ===== B) موظّف بلا قائد فريق فعليّ: لا توقّف — يُتخطّى عند الإنشاء ويُوجَّه للمدير مع حدث واضح،
    //          ويمكن اعتماده حتى النهاية (لا deadlock). =====
    [Fact]
    public async Task B_EmployeeWithoutTeamLeader_FallsBackToManager_NoDeadlock()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.EmpNoTeam.C, new(2026, 2, 5), new(2026, 2, 7)));

        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, created.Status);
        Assert.Equal(LeaveRequestStep.Manager, created.CurrentStep);
        Assert.Null(created.TeamLeaderReviewerId); // تخطٍّ آليّ لا اعتماد بشري
        var skip = Assert.Single(created.Timeline, e => e.Action == "team_leader_step_skipped");
        Assert.Equal(FallbackSkipText, skip.Comment);
        Assert.False(created.IsHrRequest);

        // لا deadlock — المدير ثم الإدارة العليا يعتمدان حتى النهاية.
        Assert.Equal(LeaveRequestStatus.ManagerApproved, (await OkAsync(await ManagerApproveAsync(org.Manager.C, created.Id))).Status);
        Assert.Equal(LeaveRequestStatus.HrApproved, (await OkAsync(await HrApproveAsync(org.Gm.C, created.Id))).Status);
    }

    // ===== C) قائد الفريق ينشئ طلبًا لنفسه: T-WF1 محفوظ — يُتخطّى ويبدأ عند المدير بحدث الذات. =====
    [Fact]
    public async Task C_TeamLeaderSelfRequest_StillSkips_StartsAtManager()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Tl.C, new(2026, 3, 5), new(2026, 3, 7)));

        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, created.Status);
        Assert.Equal(LeaveRequestStep.Manager, created.CurrentStep);
        Assert.Null(created.TeamLeaderReviewerId);
        var skip = Assert.Single(created.Timeline, e => e.Action == "team_leader_step_skipped");
        Assert.Equal(SelfSkipText, skip.Comment);
    }

    // ===== D) مسار طلب الموارد البشرية لم يُكسَر، وHR لا يعتمد طلبه الشخصي (self-check). =====
    [Fact]
    public async Task D_HrRequest_NotBroken_And_Hr_Cannot_Approve_Own()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Hr.C, new(2026, 4, 5), new(2026, 4, 7)));

        Assert.True(created.IsHrRequest);
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, created.Status);
        Assert.Equal(LeaveRequestStep.Manager, created.CurrentStep);
        Assert.Contains(created.Timeline, e => e.Action == "hr_routed");
        Assert.DoesNotContain(created.Timeline, e => e.Action == "team_leader_step_skipped");

        // HR لا يعتمد طلبه الشخصي (لا في خطوة المدير ولا في الاعتماد النهائي).
        Assert.Equal(HttpStatusCode.Forbidden, (await ManagerApproveAsync(org.Hr.C, created.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await HrApproveAsync(org.Hr.C, created.Id)).StatusCode);

        // المسار يكمل سليمًا: المدير العام يراجع ثم الإدارة العليا (الأدمن) تعتمد نهائيًّا.
        Assert.Equal(LeaveRequestStatus.ManagerApproved, (await OkAsync(await ManagerApproveAsync(org.Gm.C, created.Id))).Status);
        Assert.Equal(LeaveRequestStatus.HrApproved, (await OkAsync(await HrApproveAsync(org.Admin, created.Id))).Status);
    }

    // ===== E) Regression — خطوة المدير لم تتغيّر: المدير العام (نطاقه يشمل الموظّف) يعتمد خطوة المدير
    //          للطلب الذي اعتمده قائد الفريق الفعليّ (لا قيد قائد فريق على خطوة المدير). =====
    [Fact]
    public async Task E_ManagerStep_Unchanged_ScopeBasedApproval()
    {
        var org = await BuildOrgAsync();
        var created = await OkAsync(await CreateLeaveAsync(org.Emp.C, new(2026, 5, 5), new(2026, 5, 7)));

        // قائد الفريق الفعليّ يعتمد خطوته.
        await OkAsync(await TeamLeaderApproveAsync(org.Tl.C, created.Id));

        // خطوة المدير ضمن النطاق: المدير المباشر (نطاقه يشمل الموظّف) يعتمدها بلا قيد قائد فريق.
        var mgr = await OkAsync(await ManagerApproveAsync(org.Manager.C, created.Id));
        Assert.Equal(LeaveRequestStatus.ManagerApproved, mgr.Status);
        Assert.Equal(LeaveRequestStep.Hr, mgr.CurrentStep);

        // الاعتماد النهائي (LeaveFinalApprovers) — لم يتغيّر.
        var fin = await OkAsync(await HrApproveAsync(org.Gm.C, created.Id));
        Assert.Equal(LeaveRequestStatus.HrApproved, fin.Status);
    }
}
