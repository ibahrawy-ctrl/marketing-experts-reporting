using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Leave;
using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// اختبارات حارس كفاية رصيد الإجازات (V1.1) عند إنشاء طلب إجازة:
/// رصيد كافٍ ⇒ يمرّ بلا إقرار؛ لا رصيد/رصيد أقلّ ⇒ يُرفَض دون إقرار ويُقبَل مع الإقرار مع تخزين اللقطة؛
/// الطلب المُقَرّ به يظهر للإدارة كمتجاوِز للرصيد؛ لا خصم آليّ على الراتب وقت الإنشاء (الخصم يبقى عند الاعتماد النهائي فقط)؛
/// وانعدام التراجع على الأذونات وحدّها الشهري.
/// </summary>
[Collection("Integration")]
public class LeaveBalanceGuardTests
{
    private readonly CustomWebApplicationFactory _factory;

    public LeaveBalanceGuardTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Manager;
        public required (HttpClient C, Guid Id) Tl;
        public required (HttpClient C, Guid Id) Emp;
        public required HttpClient Admin;
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
    private static readonly DateOnly S = new(2026, 9, 7);
    private static readonly DateOnly E = new(2026, 9, 9); // 3 أيام شاملة الطرفين

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private static Task<HttpResponseMessage> OpeningAsync(HttpClient c, Guid userId, decimal amount)
        => c.PostAsJsonAsync($"/api/balances/employees/{userId}/opening",
            new OpeningBalanceRequest(BalanceType.AnnualLeave, amount, Y, "رصيد افتتاحي"), TestJson.Options);

    private static Task<HttpResponseMessage> CreateLeaveAsync(HttpClient emp, DateOnly s, DateOnly e, bool ack)
        => emp.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, s, e, null, null, "إجازة", null, ack), TestJson.Options);

    private static async Task<EmployeeLedgerDto> LedgerAsync(HttpClient admin, Guid userId)
        => (await (await admin.GetAsync($"/api/balances/employees/{userId}/ledger?year={Y}"))
            .ReadAsync<EmployeeLedgerDto>())!;

    // ===== 1) رصيد كافٍ ⇒ يمرّ بلا إقرار ولا تنبيه إلزامي =====
    [Fact]
    public async Task Sufficient_Balance_Passes_Without_Acknowledgement()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, 30);

        var res = await CreateLeaveAsync(org.Emp.C, S, E, ack: false);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var dto = (await res.ReadAsync<LeaveRequestDto>())!;
        Assert.False(dto.IsPotentialUnpaidLeave);
        Assert.Null(dto.BalanceAtRequest);
        Assert.Null(dto.RequestedLeaveDays);
        Assert.Null(dto.UncoveredLeaveDays);
        Assert.False(dto.EmployeeAcknowledgedUnpaidDeduction);
        Assert.Null(dto.EmployeeAcknowledgedAtUtc);
    }

    // ===== 2) لا رصيد إطلاقًا ⇒ يُرفَض دون إقرار بكود leave.balance_ack_required =====
    [Fact]
    public async Task No_Balance_Rejected_Without_Acknowledgement()
    {
        var org = await BuildOrgAsync();
        // بلا رصيد افتتاحي ⇒ المتاح = 0

        var res = await CreateLeaveAsync(org.Emp.C, S, E, ack: false);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("leave.balance_ack_required", await ErrorCodeAsync(res));
    }

    // ===== 3) رصيد أقلّ من الأيام ⇒ يُرفَض دون إقرار ويُقبَل مع الإقرار مع تخزين اللقطة =====
    [Fact]
    public async Task Insufficient_Balance_Rejected_Without_Ack_Accepted_With_Ack()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, 2); // المتاح 2 < المطلوب 3

        var rejected = await CreateLeaveAsync(org.Emp.C, S, E, ack: false);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("leave.balance_ack_required", await ErrorCodeAsync(rejected));

        var accepted = await CreateLeaveAsync(org.Emp.C, S, E, ack: true);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var dto = (await accepted.ReadAsync<LeaveRequestDto>())!;
        Assert.True(dto.IsPotentialUnpaidLeave);
        Assert.Equal(2, dto.BalanceAtRequest);
        Assert.Equal(3, dto.RequestedLeaveDays);
        Assert.Equal(1, dto.UncoveredLeaveDays); // 3 - 2
        Assert.True(dto.EmployeeAcknowledgedUnpaidDeduction);
        Assert.NotNull(dto.EmployeeAcknowledgedAtUtc);
    }

    // ===== 4) الطلب المُقَرّ به يظهر للإدارة كمتجاوِز للرصيد مع عرض الأيام غير المغطّاة =====
    [Fact]
    public async Task Acknowledged_Request_Appears_To_Management_As_Exceeding_Balance()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, 1); // المتاح 1 < المطلوب 3

        var created = (await (await CreateLeaveAsync(org.Emp.C, S, E, ack: true)).ReadAsync<LeaveRequestDto>())!;

        // المراجع (قائد الفريق ضمن المسار) يرى اللقطة عبر تفاصيل الطلب.
        var seen = (await (await org.Tl.C.GetAsync($"/api/leave-requests/{created.Id}")).ReadAsync<LeaveRequestDto>())!;
        Assert.True(seen.IsPotentialUnpaidLeave);
        Assert.Equal(1, seen.BalanceAtRequest);
        Assert.Equal(3, seen.RequestedLeaveDays);
        Assert.Equal(2, seen.UncoveredLeaveDays); // 3 - 1

        // كما يظهر العلَم في قائمة «بانتظار قراري».
        var pending = await (await org.Tl.C.GetAsync("/api/leave-requests/pending")).ReadAsync<List<LeaveRequestListItemDto>>();
        Assert.Contains(pending!, p => p.Id == created.Id && p.IsPotentialUnpaidLeave);
    }

    // ===== 5) لا خصم آليّ على الراتب وقت الإنشاء (لا حركة Ledger للطلب المُقَرّ به قبل الاعتماد) =====
    [Fact]
    public async Task No_Automatic_Salary_Deduction_At_Creation()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, 1);

        var created = (await (await CreateLeaveAsync(org.Emp.C, S, E, ack: true)).ReadAsync<LeaveRequestDto>())!;
        Assert.Equal(LeaveRequestStatus.Submitted, created.Status);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        // لا توجد أيّ حركة خصم إجازة معتمَدة لهذا الطلب — الخصم لا يقع قبل اعتماد قائد الفريق
        // (LEAVE-DEDUCTION-ON-TL-APPROVAL-R1: أوّل نقطة خصم صارت اعتماد قائد الفريق لا الاعتماد النهائي).
        Assert.DoesNotContain(ledger.Entries, e =>
            e.Source == BalanceSource.ApprovedLeave && e.RelatedRequestId == created.Id);
        Assert.Equal(1, ledger.AnnualLeave.Remaining); // لم يتغيّر عن الافتتاحي
    }

    // ===== 6أ) Regression — الطلب المُقَرّ به يُخصَم عدد أيامه كاملًا مرّة واحدة عبر الدورة =====
    // LEAVE-DEDUCTION-ON-TL-APPROVAL-R1: موضع الخصم انتقل إلى اعتماد قائد الفريق، والمقدار والوحدانيّة
    // لم يتغيّرا. اعتمادا المدير والموارد البشرية لاحقًا لا يضيفان خصمًا ولا يغيّران مقداره.
    [Fact]
    public async Task Regression_Deduction_Happens_Once_At_TeamLeader_Approval_And_Not_Repeated()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, 1);

        var created = (await (await CreateLeaveAsync(org.Emp.C, S, E, ack: true)).ReadAsync<LeaveRequestDto>())!;
        await org.Tl.C.PostAsJsonAsync($"/api/leave-requests/{created.Id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);

        // الخصم وقع فورًا عند اعتماد قائد الفريق.
        var afterTl = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(afterTl.Entries.Where(e =>
            e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit
            && e.Amount == 3 && e.RelatedRequestId == created.Id));
        Assert.Equal(-2, afterTl.AnnualLeave.Remaining); // 1 - 3 (سالب مسموح)

        await org.Manager.C.PostAsJsonAsync($"/api/leave-requests/{created.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        var final = await org.Gm.C.PostAsJsonAsync($"/api/leave-requests/{created.Id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, final.StatusCode);

        // ولا خصم إضافيّ من اعتماد المدير أو الاعتماد النهائي.
        var ledger = await LedgerAsync(org.Admin, org.Emp.Id);
        Assert.Single(ledger.Entries.Where(e =>
            e.Source == BalanceSource.ApprovedLeave && e.Direction == BalanceDirection.Debit
            && e.Amount == 3 && e.RelatedRequestId == created.Id));
        Assert.Equal(-2, ledger.AnnualLeave.Remaining);
        Assert.True(ledger.AnnualLeave.IsNegative);
    }

    // ===== 6ب) Regression — الحارس لا يمسّ الأذونات بلا حدّ شهري (تُنشَأ بلا رصيد ولا إقرار) =====
    [Fact]
    public async Task Regression_Permission_Not_Affected_By_Balance_Guard()
    {
        var org = await BuildOrgAsync();
        // بلا رصيد إطلاقًا ولا سياسة حدّ شهري لسنة 2026 — الإذن يجب أن يمرّ (حارس الإجازات لا يمسّه، ولا حدّ شهري).
        var res = await org.Emp.C.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Permission, new DateOnly(2026, 9, 14), null,
                new TimeOnly(9, 0), new TimeOnly(11, 0), "استئذان", null), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var dto = (await res.ReadAsync<LeaveRequestDto>())!;
        Assert.False(dto.IsPotentialUnpaidLeave);
        Assert.Null(dto.BalanceAtRequest);
        Assert.Equal(PermissionShortfallResolution.None, dto.PermissionShortfallResolution);
    }

    // ===== أدوات الأذونات (V1.1.1 — حارس الرصيد الشهري soft-ack) =====
    // سنة معزولة 2089 بلا أي سياسة عامّة سابقة كي لا يتلوّث الـ test DB المشترك. الحدّ الشهري = 1
    // فيكون الإذن الثاني في الشهر متجاوزًا. تُنشأ get-or-create لتفادي تصادم الفهرس الفريد عبر التشغيلات.
    private const int PY = 2089;

    private async Task EnsureMonthlyPolicyAsync(int year, decimal monthlyLimit)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.BalancePolicies.FirstOrDefaultAsync(p => p.Year == year && p.JobRoleId == null);
        if (existing is null)
        {
            db.BalancePolicies.Add(new BalancePolicy
            {
                Year = year,
                JobRoleId = null,
                PermissionUnit = PermissionUnit.Count,
                PermissionMonthlyLimit = monthlyLimit,
                AllowNegativeBalance = true
            });
        }
        else if (existing.PermissionMonthlyLimit != monthlyLimit)
        {
            existing.PermissionMonthlyLimit = monthlyLimit;
        }
        await db.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> CreatePermissionAsync(
        HttpClient c, DateOnly day, PermissionShortfallResolution resolution = PermissionShortfallResolution.None)
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Permission, day, null,
                new TimeOnly(9, 0), new TimeOnly(11, 0), "استئذان", null,
                AcknowledgedUnpaidDeduction: false, PermissionShortfallResolution: resolution), TestJson.Options);

    private async Task<Guid> ApprovePermissionAsync(Org org, DateOnly day)
    {
        var dto = (await (await CreatePermissionAsync(org.Emp.C, day)).ReadAsync<LeaveRequestDto>())!;
        await org.Tl.C.PostAsJsonAsync($"/api/leave-requests/{dto.Id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        await org.Manager.C.PostAsJsonAsync($"/api/leave-requests/{dto.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        await org.Gm.C.PostAsJsonAsync($"/api/leave-requests/{dto.Id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);
        return dto.Id;
    }

    // ===== 7) استئذان ضمن الرصيد الشهري ⇒ يمرّ بلا قرار ولا لقطة =====
    [Fact]
    public async Task Permission_Within_Monthly_Passes_Without_Resolution()
    {
        await EnsureMonthlyPolicyAsync(PY, 1);
        var org = await BuildOrgAsync();

        var res = await CreatePermissionAsync(org.Emp.C, new DateOnly(PY, 3, 5));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var dto = (await res.ReadAsync<LeaveRequestDto>())!;
        Assert.False(dto.IsPotentialUnpaidLeave);
        Assert.Equal(PermissionShortfallResolution.None, dto.PermissionShortfallResolution);
        Assert.Null(dto.BalanceAtRequest);
    }

    // ===== 8) تجاوز الرصيد الشهري بلا قرار ⇒ 400 permission.balance_ack_required =====
    [Fact]
    public async Task Permission_Over_Monthly_NoResolution_Rejected_400()
    {
        await EnsureMonthlyPolicyAsync(PY, 1);
        var org = await BuildOrgAsync();

        await ApprovePermissionAsync(org, new DateOnly(PY, 4, 5)); // يستنفد الحدّ (1)

        var res = await CreatePermissionAsync(org.Emp.C, new DateOnly(PY, 4, 12)); // الثاني متجاوِز
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("permission.balance_ack_required", await ErrorCodeAsync(res));
    }

    // ===== 9) تجاوز + قرار «تعويض الوقت» ⇒ يُقبَل ويُخزَّن لقطة + القرار، ويظهر للمراجع =====
    [Fact]
    public async Task Permission_Over_Monthly_Compensate_Accepted_With_Snapshot()
    {
        await EnsureMonthlyPolicyAsync(PY, 1);
        var org = await BuildOrgAsync();

        await ApprovePermissionAsync(org, new DateOnly(PY, 5, 5));

        var res = await CreatePermissionAsync(org.Emp.C, new DateOnly(PY, 5, 12), PermissionShortfallResolution.CompensateAfterHours);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var dto = (await res.ReadAsync<LeaveRequestDto>())!;
        Assert.True(dto.IsPotentialUnpaidLeave);
        Assert.Equal(PermissionShortfallResolution.CompensateAfterHours, dto.PermissionShortfallResolution);
        Assert.Equal(1, dto.RequestedLeaveDays);
        Assert.NotNull(dto.UncoveredLeaveDays);
        Assert.True(dto.UncoveredLeaveDays > 0);
        Assert.NotNull(dto.EmployeeAcknowledgedAtUtc);

        // المراجع يرى القرار عبر تفاصيل الطلب.
        var seen = (await (await org.Tl.C.GetAsync($"/api/leave-requests/{dto.Id}")).ReadAsync<LeaveRequestDto>())!;
        Assert.True(seen.IsPotentialUnpaidLeave);
        Assert.Equal(PermissionShortfallResolution.CompensateAfterHours, seen.PermissionShortfallResolution);
    }

    // ===== 10) تجاوز + قرار «المعالجة الإدارية/المالية» ⇒ يُقبَل، ويمكن اعتماده نهائيًّا بلا حظر الحدّ الشهري =====
    [Fact]
    public async Task Permission_Over_Monthly_AdminReview_Accepted_And_Final_Approvable()
    {
        await EnsureMonthlyPolicyAsync(PY, 1);
        var org = await BuildOrgAsync();

        await ApprovePermissionAsync(org, new DateOnly(PY, 6, 5));

        var dto = (await (await CreatePermissionAsync(org.Emp.C, new DateOnly(PY, 6, 12), PermissionShortfallResolution.AdminOrPayrollReview))
            .ReadAsync<LeaveRequestDto>())!;
        Assert.Equal(PermissionShortfallResolution.AdminOrPayrollReview, dto.PermissionShortfallResolution);

        // لا خصم آليّ وقت الإنشاء.
        var ledgerBefore = await LedgerAsync(org.Admin, org.Emp.Id, PY);
        Assert.DoesNotContain(ledgerBefore.Entries, e =>
            e.Source == BalanceSource.ApprovedPermission && e.RelatedRequestId == dto.Id);

        // الاعتماد النهائي يمرّ رغم تجاوز الحدّ الشهري لأن الموظّف أقرّ بالتجاوز (الحارس الصلب لا يطلق إلا للقرار None).
        await org.Tl.C.PostAsJsonAsync($"/api/leave-requests/{dto.Id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        await org.Manager.C.PostAsJsonAsync($"/api/leave-requests/{dto.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        var final = await org.Gm.C.PostAsJsonAsync($"/api/leave-requests/{dto.Id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, final.StatusCode);
    }

    private static async Task<EmployeeLedgerDto> LedgerAsync(HttpClient admin, Guid userId, int year)
        => (await (await admin.GetAsync($"/api/balances/employees/{userId}/ledger?year={year}"))
            .ReadAsync<EmployeeLedgerDto>())!;
}
