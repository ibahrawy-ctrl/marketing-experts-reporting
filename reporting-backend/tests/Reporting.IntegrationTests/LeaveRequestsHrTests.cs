using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Common;
using Reporting.Application.Leave;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// اختبارات أمان وتكامل لرقعة V1.0.1-A — الدور الرسمي للموارد البشرية (HR) وتوجيه اعتماد الإجازات.
/// تغطّي: زرع دور HR وقابلية إسناده، رؤية HR لطابور الاعتماد النهائي للطلبات العادية، اعتماد HR النهائي
/// للطلب العادي بعد المدير، منع HR من اعتماد طلبه الشخصي وعدم ظهوره في طابوره، توجيه طلب HR للمدير العام
/// (أو المدير المباشر كبديل موثَّق) ثم الاعتماد النهائي من الإدارة العليا (CEO/Admin)، ومنع تخطّي الخطوات.
/// كل الفرض خادميّ.
/// </summary>
[Collection("Integration")]
public class LeaveRequestsHrTests
{
    private readonly CustomWebApplicationFactory _factory;

    public LeaveRequestsHrTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== هرمية اختبار: GM ← Manager ← TeamLeader ← Employee، + إدارة عليا (Admin/CEO) + موظّفو HR =====
    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Admin;
        public required (HttpClient C, Guid Id) Ceo;
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Manager;
        public required (HttpClient C, Guid Id) Tl;
        public required (HttpClient C, Guid Id) Emp;
        public required (HttpClient C, Guid Id) Hr;        // موارد بشرية، مديره المباشر GM
        public required (HttpClient C, Guid Id) HrNoMgr;   // موارد بشرية بلا مدير مباشر
        public required (HttpClient C, Guid Id) HrUnderMgr;// موارد بشرية، مديره المباشر Manager (بديل المراجعة)
    }

    private async Task<Org> BuildOrgAsync()
    {
        var admin = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var ceo = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, manager.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tl.UserId);
        var hr = await TestAuth.CreateUserAsync(_factory, Roles.Hr, gm.UserId);
        var hrNoMgr = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var hrUnderMgr = await TestAuth.CreateUserAsync(_factory, Roles.Hr, manager.UserId);

        return new Org
        {
            Admin = (admin.Client, admin.UserId),
            Ceo = (ceo.Client, ceo.UserId),
            Gm = (gm.Client, gm.UserId),
            Manager = (manager.Client, manager.UserId),
            Tl = (tl.Client, tl.UserId),
            Emp = (emp.Client, emp.UserId),
            Hr = (hr.Client, hr.UserId),
            HrNoMgr = (hrNoMgr.Client, hrNoMgr.UserId),
            HrUnderMgr = (hrUnderMgr.Client, hrUnderMgr.UserId),
        };
    }

    // ===== مساعدات =====

    private static readonly DateOnly D1 = new(2026, 8, 3);
    private static readonly DateOnly D2 = new(2026, 8, 5);

    private static async Task<LeaveRequestDto> CreateLeaveOkAsync(HttpClient c, DateOnly start, DateOnly end)
        => (await (await c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, start, end, null, null, "سبب الإجازة", null),
            TestJson.Options)).ReadAsync<LeaveRequestDto>())!;

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    // طلب عادي يبلغ الحالة ManagerApproved (بانتظار الاعتماد النهائي للموارد البشرية).
    private static async Task<LeaveRequestDto> NormalRequestToManagerApprovedAsync(Org org)
    {
        var req = await CreateLeaveOkAsync(org.Emp.C, D1, D2);

        var s1 = await org.Tl.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, s1.StatusCode);

        var s2 = await org.Manager.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, s2.StatusCode);

        return (await s2.ReadAsync<LeaveRequestDto>())!;
    }

    // ===== 1) دور HR مزروع وقابل للإسناد وتسجيل الدخول =====
    [Fact]
    public async Task Hr_Role_Is_Seeded_And_Assignable()
    {
        var org = await BuildOrgAsync();
        var res = await org.Hr.C.GetAsync("/api/leave-requests/my");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 2) HR يرى الطلب العادي عند خطوة الاعتماد النهائي في طابوره =====
    [Fact]
    public async Task Hr_Sees_Normal_Request_At_Final_Step_In_Pending()
    {
        var org = await BuildOrgAsync();
        var req = await NormalRequestToManagerApprovedAsync(org);

        var pending = await (await org.Hr.C.GetAsync("/api/leave-requests/pending"))
            .ReadAsync<List<LeaveRequestListItemDto>>();
        Assert.Contains(pending!, r => r.Id == req.Id);
    }

    // ===== 3) HR يعتمد نهائيًّا الطلب العادي بعد المدير ⇒ HrApproved ويؤثّر في التقارير =====
    [Fact]
    public async Task Hr_Can_FinalApprove_Normal_Request_After_Manager()
    {
        var org = await BuildOrgAsync();
        var req = await NormalRequestToManagerApprovedAsync(org);

        var res = await org.Hr.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<LeaveRequestDto>();
        Assert.Equal(LeaveRequestStatus.HrApproved, dto!.Status);
        Assert.True(dto.ImpactsReports);
        Assert.NotNull(dto.HrReviewerId);
    }

    // ===== 4) HR لا يعتمد طلبه الشخصي (لا في خطوة المدير العام ولا في الاعتماد النهائي) =====
    [Fact]
    public async Task Hr_Cannot_Approve_Own_Request()
    {
        var org = await BuildOrgAsync();
        var own = await CreateLeaveOkAsync(org.Hr.C, D1, D2);
        Assert.True(own.IsHrRequest);

        // محاولة خطوة المدير العام على طلبه — ممنوع عند طبقة السياسة (HR ليس ضمن ManagementOnly) ⇒ 403 بلا جسم.
        var asManager = await org.Hr.C.PostAsJsonAsync(
            $"/api/leave-requests/{own.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, asManager.StatusCode);

        // محاولة الاعتماد النهائي على طلبه — يجتاز السياسة (HR ضمن LeaveFinalApproval) لكن يُرفض في الخدمة: لا يعتمد أحد طلبه.
        var asHr = await org.Hr.C.PostAsJsonAsync(
            $"/api/leave-requests/{own.Id}/hr/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, asHr.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(asHr));
    }

    // ===== 5) طلب HR الشخصي لا يظهر في طابور HR نفسه =====
    [Fact]
    public async Task Hr_Own_Request_Not_In_Own_Pending()
    {
        var org = await BuildOrgAsync();
        var own = await CreateLeaveOkAsync(org.Hr.C, D1, D2);

        var pending = await (await org.Hr.C.GetAsync("/api/leave-requests/pending"))
            .ReadAsync<List<LeaveRequestListItemDto>>();
        Assert.DoesNotContain(pending!, r => r.Id == own.Id);
    }

    // ===== 6) طلب HR يُوجَّه لخطوة المدير العام عند الإنشاء (لا قائد فريق) =====
    [Fact]
    public async Task Hr_Request_Routes_To_Manager_Step_On_Create()
    {
        var org = await BuildOrgAsync();
        var own = await CreateLeaveOkAsync(org.Hr.C, D1, D2);

        Assert.True(own.IsHrRequest);
        Assert.Equal(LeaveRequestStatus.TeamLeaderApproved, own.Status);
        Assert.Equal(LeaveRequestStep.Manager, own.CurrentStep);
        Assert.Contains(own.Timeline, e => e.Action == "submitted");
        Assert.Contains(own.Timeline, e => e.Action == "hr_routed");
    }

    // ===== 7) المدير العام يراجع طلب HR (خطوة المدير) =====
    [Fact]
    public async Task Gm_Can_Review_Hr_Request()
    {
        var org = await BuildOrgAsync();
        var own = await CreateLeaveOkAsync(org.Hr.C, D1, D2);

        var pending = await (await org.Gm.C.GetAsync("/api/leave-requests/pending"))
            .ReadAsync<List<LeaveRequestListItemDto>>();
        Assert.Contains(pending!, r => r.Id == own.Id);

        var res = await org.Gm.C.PostAsJsonAsync(
            $"/api/leave-requests/{own.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<LeaveRequestDto>();
        Assert.Equal(LeaveRequestStatus.ManagerApproved, dto!.Status);
    }

    // ===== 8) الإدارة العليا (CEO) تعتمد طلب HR نهائيًّا بعد مراجعة المدير العام =====
    [Fact]
    public async Task Ceo_Can_FinalApprove_Hr_Request_After_Gm()
    {
        var org = await BuildOrgAsync();
        var own = await CreateLeaveOkAsync(org.Hr.C, D1, D2);

        var gmStep = await org.Gm.C.PostAsJsonAsync(
            $"/api/leave-requests/{own.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, gmStep.StatusCode);

        // الطلب الآن في طابور الاعتماد النهائي لدى الإدارة العليا.
        var ceoPending = await (await org.Ceo.C.GetAsync("/api/leave-requests/pending"))
            .ReadAsync<List<LeaveRequestListItemDto>>();
        Assert.Contains(ceoPending!, r => r.Id == own.Id);

        var res = await org.Ceo.C.PostAsJsonAsync(
            $"/api/leave-requests/{own.Id}/hr/approve", new LeaveApproveRequest("معتمد"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<LeaveRequestDto>();
        Assert.Equal(LeaveRequestStatus.HrApproved, dto!.Status);
        Assert.True(dto.ImpactsReports);
    }

    // ===== 9) Admin يعتمد طلب HR نهائيًّا بعد مراجعة المدير العام (تدخّل الإدارة العليا) =====
    [Fact]
    public async Task Admin_Can_FinalApprove_Hr_Request_After_Gm()
    {
        var org = await BuildOrgAsync();
        var own = await CreateLeaveOkAsync(org.Hr.C, D1, D2);

        await org.Gm.C.PostAsJsonAsync(
            $"/api/leave-requests/{own.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);

        var res = await org.Admin.C.PostAsJsonAsync(
            $"/api/leave-requests/{own.Id}/hr/approve", new LeaveApproveRequest("معتمد"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<LeaveRequestDto>();
        Assert.Equal(LeaveRequestStatus.HrApproved, dto!.Status);
    }

    // ===== 10) لا يجوز تخطّي مراجعة المدير العام على طلب HR =====
    [Fact]
    public async Task Cannot_Skip_Gm_On_Hr_Request()
    {
        var org = await BuildOrgAsync();
        var own = await CreateLeaveOkAsync(org.Hr.C, D1, D2); // TeamLeaderApproved (بانتظار المدير العام)

        // الإدارة العليا تحاول الاعتماد النهائي قبل مراجعة المدير العام ⇒ حالة غير صالحة.
        var res = await org.Ceo.C.PostAsJsonAsync(
            $"/api/leave-requests/{own.Id}/hr/approve", new LeaveApproveRequest("معتمد"), TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("leave_request.invalid_state", await ErrorCodeAsync(res));
    }

    // ===== 11) مدير ليس مديرًا عامًّا ولا مديرًا مباشرًا لا يراجع طلب HR =====
    [Fact]
    public async Task NonGm_NonDirectManager_Cannot_Review_Hr_Request()
    {
        var org = await BuildOrgAsync();
        var own = await CreateLeaveOkAsync(org.HrNoMgr.C, D1, D2); // بلا مدير مباشر

        var res = await org.Manager.C.PostAsJsonAsync(
            $"/api/leave-requests/{own.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("auth.forbidden", await ErrorCodeAsync(res));
    }

    // ===== 12) المدير المباشر (غير المدير العام) يراجع طلب HR — بديل موثَّق =====
    [Fact]
    public async Task DirectManager_Can_Review_Hr_Request_Fallback()
    {
        var org = await BuildOrgAsync();
        var own = await CreateLeaveOkAsync(org.HrUnderMgr.C, D1, D2); // مديره المباشر Manager

        var res = await org.Manager.C.PostAsJsonAsync(
            $"/api/leave-requests/{own.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<LeaveRequestDto>();
        Assert.Equal(LeaveRequestStatus.ManagerApproved, dto!.Status);
    }

    // ===== 13) قائد الفريق لا يعتمد نهائيًّا (ليس ضمن LeaveFinalApprovers) =====
    [Fact]
    public async Task Tl_Cannot_FinalApprove_Normal_Request()
    {
        var org = await BuildOrgAsync();
        var req = await NormalRequestToManagerApprovedAsync(org);

        var res = await org.Tl.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/hr/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 14) HR لا يتصرّف كقائد فريق/مدير على الطلبات العادية (ليس ضمن ManagementOnly) =====
    [Fact]
    public async Task Hr_Cannot_Use_Management_Steps_On_Normal_Request()
    {
        var org = await BuildOrgAsync();
        var req = await CreateLeaveOkAsync(org.Emp.C, D1, D2); // Submitted

        var asTl = await org.Hr.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, asTl.StatusCode);

        var asManager = await org.Hr.C.PostAsJsonAsync(
            $"/api/leave-requests/{req.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, asManager.StatusCode);
    }
}
