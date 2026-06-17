using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Common;
using Reporting.Application.Leave;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// اختبارات أمان وتكامل لرقعة V1.0.1 — الإجازات والاستئذانات.
/// تغطّي الـ20 سيناريو المطلوبة قبل النشر: النطاق، منع اعتماد الطلب الذاتي، عدم تصرّف المراجِع
/// خارج نطاقه أو على خطوتين، ترتيب الخطوات، التحقق من المدخلات، التداخل، الإلغاء، والمسار الكامل
/// Employee→TL→Manager→HR وأنّ HrApproved وحده يؤثّر في التقارير.
/// </summary>
[Collection("Integration")]
public class LeaveRequestsTests
{
    private readonly CustomWebApplicationFactory _factory;

    public LeaveRequestsTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== هرمية اختبار: GM ← Manager ← TeamLeader ← Employee، وفرع آخر منفصل =====
    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;        // مدير عام — اعتماد نهائي (LeaveFinalApproval)
        public required (HttpClient C, Guid Id) Manager;   // مدير الفرع
        public required (HttpClient C, Guid Id) Tl;        // قائد فريق
        public required (HttpClient C, Guid Id) Emp;       // موظّف ضمن نطاق Tl/Manager/Gm
        public required (HttpClient C, Guid Id) OtherTl;   // قائد فريق فرع آخر
        public required (HttpClient C, Guid Id) OtherEmp;  // موظّف فرع آخر — خارج نطاق Tl
    }

    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, manager.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tl.UserId);
        var otherTl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, gm.UserId);
        var otherEmp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, otherTl.UserId);

        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            Manager = (manager.Client, manager.UserId),
            Tl = (tl.Client, tl.UserId),
            Emp = (emp.Client, emp.UserId),
            OtherTl = (otherTl.Client, otherTl.UserId),
            OtherEmp = (otherEmp.Client, otherEmp.UserId),
        };
    }

    // ===== مساعدات =====

    private static Task<HttpResponseMessage> CreateLeaveAsync(
        HttpClient c, DateOnly start, DateOnly end, string reason = "سبب الإجازة")
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, start, end, null, null, reason, null),
            TestJson.Options);

    private static Task<HttpResponseMessage> CreatePermissionAsync(
        HttpClient c, DateOnly day, TimeOnly from, TimeOnly to, string reason = "سبب الاستئذان")
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Permission, day, null, from, to, reason, null),
            TestJson.Options);

    private static async Task<LeaveRequestDto> CreateLeaveOkAsync(
        HttpClient c, DateOnly start, DateOnly end, string reason = "سبب الإجازة")
        => (await (await CreateLeaveAsync(c, start, end, reason)).ReadAsync<LeaveRequestDto>())!;

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private static readonly DateOnly D1 = new(2026, 8, 3);
    private static readonly DateOnly D2 = new(2026, 8, 5);

    // ===== 1) الموظّف ينشئ ويرى طلبه فقط =====
    [Fact]
    public async Task Employee_Creates_And_Sees_Only_Own_Requests()
    {
        var org = await BuildOrgAsync();
        var created = await CreateLeaveOkAsync(org.Emp.C, D1, D2);

        Assert.Equal(LeaveRequestStatus.Submitted, created.Status);
        Assert.Equal(LeaveRequestStep.TeamLeader, created.CurrentStep);
        Assert.False(created.ImpactsReports);

        var mine = await (await org.Emp.C.GetAsync("/api/leave-requests/my"))
            .ReadAsync<List<LeaveRequestListItemDto>>();
        Assert.Contains(mine!, r => r.Id == created.Id);

        // موظّف فرع آخر لا يرى الطلب في قائمته.
        var otherMine = await (await org.OtherEmp.C.GetAsync("/api/leave-requests/my"))
            .ReadAsync<List<LeaveRequestListItemDto>>();
        Assert.DoesNotContain(otherMine!, r => r.Id == created.Id);
    }

    // ===== 2) سبب مطلوب =====
    [Fact]
    public async Task Create_Without_Reason_Fails()
    {
        var org = await BuildOrgAsync();
        var res = await CreateLeaveAsync(org.Emp.C, D1, D2, "   ");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("leave_request.reason_required", await ErrorCodeAsync(res));
    }

    // ===== 3) إجازة بلا تاريخ نهاية =====
    [Fact]
    public async Task Leave_Without_EndDate_Fails()
    {
        var org = await BuildOrgAsync();
        var res = await org.Emp.C.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, D1, null, null, null, "سبب", null),
            TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("leave_request.end_date_required", await ErrorCodeAsync(res));
    }

    // ===== 4) نهاية تسبق البداية =====
    [Fact]
    public async Task Leave_EndBeforeStart_Fails()
    {
        var org = await BuildOrgAsync();
        var res = await CreateLeaveAsync(org.Emp.C, D2, D1);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("leave_request.end_before_start", await ErrorCodeAsync(res));
    }

    // ===== 5) استئذان بلا أوقات =====
    [Fact]
    public async Task Permission_Without_Times_Fails()
    {
        var org = await BuildOrgAsync();
        var res = await org.Emp.C.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Permission, D1, null, null, null, "سبب", null),
            TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("leave_request.times_required", await ErrorCodeAsync(res));
    }

    // ===== 6) استئذان: وقت النهاية لا يلي البداية =====
    [Fact]
    public async Task Permission_EndTime_NotAfterStart_Fails()
    {
        var org = await BuildOrgAsync();
        var res = await CreatePermissionAsync(org.Emp.C, D1, new TimeOnly(14, 0), new TimeOnly(13, 0));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("leave_request.end_time_before_start", await ErrorCodeAsync(res));
    }

    // ===== 7) تداخل الفترات =====
    [Fact]
    public async Task Overlapping_Period_Conflicts()
    {
        var org = await BuildOrgAsync();
        await CreateLeaveOkAsync(org.Emp.C, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5));

        var res = await CreateLeaveAsync(org.Emp.C, new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 8));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("leave_request.period.conflict", await ErrorCodeAsync(res));
    }

    // ===== 8) لا يعتمد أحد طلبه (المراجِع نفسه هو صاحب الطلب) =====
    [Fact]
    public async Task Reviewer_Cannot_Approve_Own_Request()
    {
        var org = await BuildOrgAsync();
        // قائد الفريق ينشئ طلبه الخاص (Submitted، خطوة قائد الفريق) ثم يحاول اعتماده.
        var own = await CreateLeaveOkAsync(org.Tl.C, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2));
        var res = await org.Tl.C.PostAsJsonAsync(
            $"/api/leave-requests/{own.Id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(res));
    }

    // ===== 9) المراجِع لا يتصرّف خارج نطاقه =====
    [Fact]
    public async Task Reviewer_Cannot_Approve_OutOfScope_Request()
    {
        var org = await BuildOrgAsync();
        var req = await CreateLeaveOkAsync(org.Emp.C, D1, D2);

        // قائد فريق الفرع الآخر يحاول اعتماد طلب موظّف ليس ضمن نطاقه.
        var res = await org.OtherTl.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(res));
    }

    // ===== 10) لا يتصرّف الشخص نفسه في خطوتين على الطلب ذاته =====
    [Fact]
    public async Task Same_Reviewer_Cannot_Act_On_Two_Steps()
    {
        var org = await BuildOrgAsync();
        var req = await CreateLeaveOkAsync(org.Emp.C, D1, D2);

        // المدير العام (نطاق واسع) يعتمد خطوة قائد الفريق...
        var step1 = await org.Gm.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, step1.StatusCode);

        // ...ثم يحاول اعتماد خطوة المدير على الطلب نفسه — ممنوع.
        var step2 = await org.Gm.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, step2.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(step2));
    }

    // ===== 11) ترتيب الخطوات: قرار قبل اكتمال السابق =====
    [Fact]
    public async Task Decision_In_Wrong_State_Fails()
    {
        var org = await BuildOrgAsync();
        var req = await CreateLeaveOkAsync(org.Emp.C, D1, D2); // Submitted

        // المدير يحاول اعتماد خطوة المدير قبل اعتماد قائد الفريق.
        var res = await org.Manager.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("leave_request.invalid_state", await ErrorCodeAsync(res));
    }

    // ===== 12) الرفض يستلزم سببًا =====
    [Fact]
    public async Task Reject_Without_Reason_Fails()
    {
        var org = await BuildOrgAsync();
        var req = await CreateLeaveOkAsync(org.Emp.C, D1, D2);

        var res = await org.Tl.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/team-leader/reject", new LeaveRejectRequest("   "), TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("leave_request.rejection_reason_required", await ErrorCodeAsync(res));
    }

    // ===== 13) الإلغاء حقّ صاحب الطلب وحده =====
    [Fact]
    public async Task NonOwner_Cannot_Cancel()
    {
        var org = await BuildOrgAsync();
        var req = await CreateLeaveOkAsync(org.Emp.C, D1, D2);

        var res = await org.OtherEmp.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/cancel", new { }, TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(res));
    }

    // ===== 14) لا يُلغى الطلب بعد الاعتماد النهائي =====
    [Fact]
    public async Task Cannot_Cancel_After_HrApproved()
    {
        var org = await BuildOrgAsync();
        var req = await ApproveFullChainAsync(org);

        var res = await org.Emp.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/cancel", new { }, TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("leave_request.cannot_cancel", await ErrorCodeAsync(res));
    }

    // ===== 15) المسار الكامل Employee→TL→Manager→HR ⇒ HrApproved يؤثّر في التقارير =====
    [Fact]
    public async Task Full_Approval_Chain_Sets_HrApproved_And_ImpactsReports()
    {
        var org = await BuildOrgAsync();
        var final = await ApproveFullChainAsync(org);

        Assert.Equal(LeaveRequestStatus.HrApproved, final.Status);
        Assert.True(final.ImpactsReports);
        Assert.NotNull(final.TeamLeaderReviewerId);
        Assert.NotNull(final.ManagerReviewerId);
        Assert.NotNull(final.HrReviewerId);
        Assert.False(final.CanCancel);
        // الخطّ الزمني يسجّل كل قرار.
        Assert.Contains(final.Timeline, e => e.Action == "submitted");
        Assert.Contains(final.Timeline, e => e.Action == "team_leader_approved");
        Assert.Contains(final.Timeline, e => e.Action == "manager_approved");
        Assert.Contains(final.Timeline, e => e.Action == "hr_approved");
    }

    // ===== 16) قائمة «بانتظار قراري» تحترم النطاق =====
    [Fact]
    public async Task Pending_List_Is_Scoped()
    {
        var org = await BuildOrgAsync();
        var mine = await CreateLeaveOkAsync(org.Emp.C, D1, D2);
        var other = await CreateLeaveOkAsync(org.OtherEmp.C, D1, D2);

        var pending = await (await org.Tl.C.GetAsync("/api/leave-requests/pending"))
            .ReadAsync<List<LeaveRequestListItemDto>>();
        Assert.Contains(pending!, r => r.Id == mine.Id);          // ضمن فريقه
        Assert.DoesNotContain(pending!, r => r.Id == other.Id);   // فرع آخر
    }

    // ===== 17) الموظّف ممنوع من قائمة المراجعة (سياسة ManagementOnly) =====
    [Fact]
    public async Task Employee_Cannot_Access_Pending_List()
    {
        var org = await BuildOrgAsync();
        var res = await org.Emp.C.GetAsync("/api/leave-requests/pending");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 18) الموظّف ممنوع من الاعتماد النهائي (سياسة LeaveFinalApproval) =====
    [Fact]
    public async Task Employee_Cannot_Call_Hr_Approve()
    {
        var org = await BuildOrgAsync();
        var req = await CreateLeaveOkAsync(org.Emp.C, D1, D2);
        var res = await org.Emp.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/hr/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 19) عرض طلب خارج النطاق ممنوع =====
    [Fact]
    public async Task Cannot_View_OutOfScope_Request()
    {
        var org = await BuildOrgAsync();
        var req = await CreateLeaveOkAsync(org.Emp.C, D1, D2);

        var res = await org.OtherTl.C.GetAsync($"/api/leave-requests/{req.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(res));

        // طلب غير موجود ⇒ 404
        var missing = await org.Gm.C.GetAsync($"/api/leave-requests/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("leave_request.not_found", await ErrorCodeAsync(missing));
    }

    // ===== 20) الإعادة للتعديل: تتطلّب سببًا وتُعيد الحالة ReturnedForEdit =====
    [Fact]
    public async Task Return_Requires_Reason_And_Sets_ReturnedForEdit()
    {
        var org = await BuildOrgAsync();
        var req = await CreateLeaveOkAsync(org.Emp.C, D1, D2);

        var noReason = await org.Manager.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/return", new LeaveReturnRequest("  "), TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal("leave_request.return_reason_required", await ErrorCodeAsync(noReason));

        var ok = await org.Manager.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/return", new LeaveReturnRequest("يرجى تصحيح التواريخ"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var dto = await ok.ReadAsync<LeaveRequestDto>();
        Assert.Equal(LeaveRequestStatus.ReturnedForEdit, dto!.Status);
        Assert.Equal(LeaveRequestStep.Employee, dto.CurrentStep);
    }

    // ===== مساعد المسار الكامل (مراجعون متمايزون ضمن النطاق) =====
    private static async Task<LeaveRequestDto> ApproveFullChainAsync(Org org)
    {
        var req = await CreateLeaveOkAsync(org.Emp.C, D1, D2);

        var s1 = await org.Tl.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, s1.StatusCode);

        var s2 = await org.Manager.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, s2.StatusCode);

        var s3 = await org.Gm.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, s3.StatusCode);

        return (await s3.ReadAsync<LeaveRequestDto>())!;
    }
}
