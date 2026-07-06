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
/// اختبارات أمان وتكامل لأرصدة الإجازات والأذونات (V1.1 — خدمات الموظف):
/// النطاق (الموظّف يرى رصيده فقط)، صلاحية الإدارة (BalanceManagement)، الرصيد الافتتاحي،
/// التعديل اليدوي (سبب إلزامي)، الرصيد السالب، الخصم الآلي عند الاعتماد النهائي،
/// والعكس عند إبطال طلب معتمَد (Reversal) مع إثبات أنّ العكس idempotent ولا يحذف الحركة الأصلية.
/// </summary>
[Collection("Integration")]
public class BalancesTests
{
    private readonly CustomWebApplicationFactory _factory;

    public BalancesTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Manager;
        public required (HttpClient C, Guid Id) Tl;
        public required (HttpClient C, Guid Id) Emp;
        public required HttpClient Admin; // BalanceManager (Admin)
    }

    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, manager.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tl.UserId);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        // فريق فعليّ للموظّف العادي قائده tl (T-WF2): كي يبدأ الطلب عند خطوة قائد الفريق ويعتمدها قائد الفريق الفعليّ.
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tl.UserId, emp.UserId);
        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            Manager = (manager.Client, manager.UserId),
            Tl = (tl.Client, tl.UserId),
            Emp = (emp.Client, emp.UserId),
            Admin = admin,
        };
    }

    private const int Y = 2026;
    private static readonly DateOnly D1 = new(2026, 8, 3);
    private static readonly DateOnly D2 = new(2026, 8, 5); // 3 أيام شاملة

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private static Task<HttpResponseMessage> OpeningAsync(HttpClient c, Guid userId, BalanceType type, decimal amount)
        => c.PostAsJsonAsync($"/api/balances/employees/{userId}/opening",
            new OpeningBalanceRequest(type, amount, Y, "رصيد افتتاحي"), TestJson.Options);

    private static Task<HttpResponseMessage> AdjustAsync(
        HttpClient c, Guid userId, BalanceType type, BalanceDirection dir, decimal amount, string reason)
        => c.PostAsJsonAsync($"/api/balances/employees/{userId}/adjust",
            new BalanceAdjustmentRequest(type, dir, amount, Y, reason), TestJson.Options);

    private static async Task<EmployeeLedgerDto> LedgerAsync(HttpClient admin, Guid userId)
        => (await (await admin.GetAsync($"/api/balances/employees/{userId}/ledger?year={Y}"))
            .ReadAsync<EmployeeLedgerDto>())!;

    private static async Task<LeaveRequestDto> ApproveFullLeaveAsync(Org org, DateOnly s, DateOnly e)
    {
        var created = (await (await org.Emp.C.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, s, e, null, null, "سبب", null), TestJson.Options))
            .ReadAsync<LeaveRequestDto>())!;
        await org.Tl.C.PostAsJsonAsync($"/api/leave-requests/{created.Id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        await org.Manager.C.PostAsJsonAsync($"/api/leave-requests/{created.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        var s3 = await org.Gm.C.PostAsJsonAsync($"/api/leave-requests/{created.Id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);
        return (await s3.ReadAsync<LeaveRequestDto>())!;
    }

    // ===== 1) غير المصادَق ⇒ 401 على رصيدي =====
    [Fact]
    public async Task MyBalances_Anonymous_401()
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync("/api/me/balances");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ===== 2) الموظّف يرى رصيده عبر /me/balances =====
    [Fact]
    public async Task Employee_Sees_Own_Balances()
    {
        var org = await BuildOrgAsync();
        var me = await (await org.Emp.C.GetAsync($"/api/me/balances?year={Y}")).ReadAsync<MyBalancesDto>();
        Assert.NotNull(me);
        Assert.Equal(Y, me!.Year);
        Assert.Equal(BalanceType.AnnualLeave, me.AnnualLeave.BalanceType);
        Assert.Equal(PermissionUnit.Count, me.PermissionUnit); // افتراضي بلا سياسة
    }

    // ===== 3) الموظّف ممنوع من قائمة أرصدة الإدارة ⇒ 403 =====
    [Fact]
    public async Task Employee_Cannot_List_Employees_403()
    {
        var org = await BuildOrgAsync();
        var res = await org.Emp.C.GetAsync("/api/balances/employees");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 4) قائد الفريق والمدير ممنوعان من إدارة الأرصدة ⇒ 403 =====
    [Fact]
    public async Task TeamLeader_And_Manager_Cannot_Manage_Balances_403()
    {
        var org = await BuildOrgAsync();
        var tl = await org.Tl.C.GetAsync("/api/balances/employees");
        var mgr = await org.Manager.C.GetAsync("/api/balances/employees");
        Assert.Equal(HttpStatusCode.Forbidden, tl.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, mgr.StatusCode);
    }

    // ===== 5) الإدارة (Admin) ترى قائمة الأرصدة وتشمل الموظّف =====
    [Fact]
    public async Task Admin_Lists_Employee_Balances()
    {
        var org = await BuildOrgAsync();
        var rows = await (await org.Admin.GetAsync($"/api/balances/employees?year={Y}"))
            .ReadAsync<List<EmployeeBalanceRowDto>>();
        Assert.Contains(rows!, r => r.EmployeeId == org.Emp.Id);
    }

    // ===== 6) الرصيد الافتتاحي يرفع الرصيد المتاح =====
    [Fact]
    public async Task Opening_Balance_Increases_Remaining()
    {
        var org = await BuildOrgAsync();
        var res = await OpeningAsync(org.Admin, org.Emp.Id, BalanceType.AnnualLeave, 21);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Equal(21, ledger.AnnualLeave.Credited);
        Assert.Equal(21, ledger.AnnualLeave.Remaining);
        Assert.False(ledger.AnnualLeave.IsNegative);
    }

    // ===== 7) التعديل اليدوي (خصم) يخفض الرصيد ويسجّل السبب =====
    [Fact]
    public async Task Manual_Debit_Adjustment_Decreases_Remaining()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, BalanceType.AnnualLeave, 10);
        var res = await AdjustAsync(org.Admin, org.Emp.Id, BalanceType.AnnualLeave, BalanceDirection.Debit, 4, "تسوية");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Equal(6, ledger.AnnualLeave.Remaining);
        Assert.Contains(ledger.Entries, e => e.Source == BalanceSource.ManualAdjustment && e.Notes == "تسوية");
    }

    // ===== 8) التعديل اليدوي بلا سبب ⇒ 400 =====
    [Fact]
    public async Task Manual_Adjustment_Without_Reason_400()
    {
        var org = await BuildOrgAsync();
        var res = await AdjustAsync(org.Admin, org.Emp.Id, BalanceType.AnnualLeave, BalanceDirection.Credit, 1, "   ");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("balance.reason_required", await ErrorCodeAsync(res));
    }

    // ===== 9) مقدار غير موجب ⇒ 400 (افتتاحي + تعديل) =====
    [Fact]
    public async Task NonPositive_Amount_400()
    {
        var org = await BuildOrgAsync();
        var opening = await OpeningAsync(org.Admin, org.Emp.Id, BalanceType.AnnualLeave, 0);
        Assert.Equal(HttpStatusCode.BadRequest, opening.StatusCode);
        Assert.Equal("balance.amount_invalid", await ErrorCodeAsync(opening));

        var adjust = await AdjustAsync(org.Admin, org.Emp.Id, BalanceType.AnnualLeave, BalanceDirection.Debit, -2, "سبب");
        Assert.Equal(HttpStatusCode.BadRequest, adjust.StatusCode);
        Assert.Equal("balance.amount_invalid", await ErrorCodeAsync(adjust));
    }

    // ===== 10) الرصيد السالب مسموح ويُعلَّم isNegative =====
    [Fact]
    public async Task Negative_Balance_Is_Flagged()
    {
        var org = await BuildOrgAsync();
        var res = await AdjustAsync(org.Admin, org.Emp.Id, BalanceType.Permission, BalanceDirection.Debit, 3, "خصم بلا رصيد");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode); // مسموح
        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Equal(-3, ledger.Permission.Remaining);
        Assert.True(ledger.Permission.IsNegative);
    }

    // ===== 11) الخصم الآلي عند الاعتماد النهائي للإجازة = عدد الأيام =====
    [Fact]
    public async Task Deduction_At_HrApproved_Leave_Days()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, BalanceType.AnnualLeave, 30);
        var approved = await ApproveFullLeaveAsync(org, D1, D2);
        Assert.Equal(LeaveRequestStatus.HrApproved, approved.Status);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Contains(ledger.Entries, e =>
            e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit
            && e.Amount == 3 && e.RelatedRequestId == approved.Id);
        Assert.Equal(27, ledger.AnnualLeave.Remaining); // 30 - 3
    }

    // ===== 12) الخصم الآلي للإذن المعتمَد = 1 (وحدة عدد) =====
    [Fact]
    public async Task Deduction_At_HrApproved_Permission_Count_One()
    {
        var org = await BuildOrgAsync();
        var created = (await (await org.Emp.C.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Permission, new DateOnly(2026, 8, 10), null,
                new TimeOnly(9, 0), new TimeOnly(11, 0), "استئذان", null), TestJson.Options))
            .ReadAsync<LeaveRequestDto>())!;
        await org.Tl.C.PostAsJsonAsync($"/api/leave-requests/{created.Id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        await org.Manager.C.PostAsJsonAsync($"/api/leave-requests/{created.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        await org.Gm.C.PostAsJsonAsync($"/api/leave-requests/{created.Id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Contains(ledger.Entries, e =>
            e.Source == BalanceSource.ApprovedPermission && e.Direction == BalanceDirection.Debit
            && e.Amount == 1 && e.RelatedRequestId == created.Id);
    }

    // ===== 13) إبطال إجازة معتمَدة يضيف حركة عكس (Credit) ويُعيد الرصيد، دون حذف الأصل =====
    [Fact]
    public async Task Revoke_Approved_Adds_Reversal_And_Restores_Balance()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, BalanceType.AnnualLeave, 30);
        var approved = await ApproveFullLeaveAsync(org, D1, D2); // خصم 3

        var revoke = await org.Admin.PostAsJsonAsync(
            $"/api/leave-requests/{approved.Id}/revoke", new LeaveRevokeRequest("إلغاء بموافقة الموظّف"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        // الأصل (الخصم) باقٍ، والعكس (Credit) مضاف.
        Assert.Contains(ledger.Entries, e => e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit && e.Amount == 3);
        Assert.Contains(ledger.Entries, e => e.Source == BalanceSource.Reversal && e.Direction == BalanceDirection.Credit && e.Amount == 3);
        Assert.Equal(30, ledger.AnnualLeave.Remaining); // عاد كما كان
    }

    // ===== 14) العكس idempotent — إبطال طلب غير معتمَد ⇒ 409 (لا حركة عكس ثانية) =====
    [Fact]
    public async Task Revoke_NonApproved_409()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, BalanceType.AnnualLeave, 30); // رصيد كافٍ لتجاوز حارس الرصيد عند الإنشاء
        var created = (await (await org.Emp.C.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, D1, D2, null, null, "سبب", null), TestJson.Options))
            .ReadAsync<LeaveRequestDto>())!; // Submitted

        var res = await org.Admin.PostAsJsonAsync(
            $"/api/leave-requests/{created.Id}/revoke", new LeaveRevokeRequest("محاولة"), TestJson.Options);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("leave_request.not_approved.conflict", await ErrorCodeAsync(res));
    }

    // ===== 15) إبطال بلا سبب ⇒ 400 =====
    [Fact]
    public async Task Revoke_Without_Reason_400()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, BalanceType.AnnualLeave, 30);
        var approved = await ApproveFullLeaveAsync(org, D1, D2);

        var res = await org.Admin.PostAsJsonAsync(
            $"/api/leave-requests/{approved.Id}/revoke", new LeaveRevokeRequest("   "), TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("leave_request.revoke_reason_required", await ErrorCodeAsync(res));
    }

    // ===== 16) الموظّف ممنوع من إبطال طلب معتمَد (مسار محروس) ⇒ 403 =====
    [Fact]
    public async Task Employee_Cannot_Revoke_403()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, BalanceType.AnnualLeave, 30);
        var approved = await ApproveFullLeaveAsync(org, D1, D2);

        var res = await org.Emp.C.PostAsJsonAsync(
            $"/api/leave-requests/{approved.Id}/revoke", new LeaveRevokeRequest("محاولة"), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
