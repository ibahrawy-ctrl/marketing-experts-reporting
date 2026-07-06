using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Leave;
using Reporting.Application.Payroll;
using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// اختبارات «عرض التأثير على الرواتب» (FIN-L1). تغطّي: منطق الظهور (طلب مؤثّر يظهر، غير المؤثّر لا)،
/// تصنيف نوع التأثير (إجازة بلا راتب / استئذان بتعويض / استئذان بمعالجة إدارية)، الفلاتر (الافتراضي HrApproved،
/// كل الحالات، حالة الاعتماد، الشهر/السنة، الموظّف، نوع التأثير، حالة المراجعة)، RBAC (قراءة Admin/CEO/GM/HR/CeoSupport
/// = 200؛ Manager/TL/Employee/Viewer = 403؛ Anonymous = 401؛ تحديث المراجعة Admin/HR فقط، البقيّة 403)،
/// الإنشاء الكسول للمراجعة وتحديثها (صفّ واحد، idempotent)، وعدم مساس الطلب الأصلي أو الراتب إطلاقًا.
/// كلها على مستوى الشركة (بلا ScopeResolver)، إعلامية بحتة.
/// </summary>
[Collection("Integration")]
public class PayrollImpactTests
{
    private readonly CustomWebApplicationFactory _factory;

    public PayrollImpactTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== هرمية: GM ← Manager ← TeamLeader ← Employee + أدوار قراءة/منع =====
    private sealed class Org
    {
        public required HttpClient Admin;
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Manager;
        public required (HttpClient C, Guid Id) Tl;
        public required (HttpClient C, Guid Id) Emp;
        public required HttpClient Ceo;
        public required (HttpClient C, Guid Id) Hr;
        public required HttpClient CeoSupport;
        public required HttpClient Viewer;
    }

    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, manager.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tl.UserId);
        var ceo = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var hr = await TestAuth.CreateUserAsync(_factory, Roles.Hr, gm.UserId);
        var ceoSupport = await TestAuth.CreateUserAsync(_factory, Roles.CeoSupport);
        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Viewer);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        // فريق فعليّ للموظّف العادي قائده tl (T-WF2): كي يبدأ الطلب عند خطوة قائد الفريق ويعتمدها قائد الفريق الفعليّ.
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tl.UserId, emp.UserId);
        return new Org
        {
            Admin = admin,
            Gm = (gm.Client, gm.UserId),
            Manager = (manager.Client, manager.UserId),
            Tl = (tl.Client, tl.UserId),
            Emp = (emp.Client, emp.UserId),
            Ceo = ceo.Client,
            Hr = (hr.Client, hr.UserId),
            CeoSupport = ceoSupport.Client,
            Viewer = viewer.Client,
        };
    }

    // ===== مساعدات =====

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private static Task<HttpResponseMessage> OpeningAsync(HttpClient admin, Guid userId, decimal amount, int year)
        => admin.PostAsJsonAsync($"/api/balances/employees/{userId}/opening",
            new OpeningBalanceRequest(BalanceType.AnnualLeave, amount, year, "رصيد افتتاحي"), TestJson.Options);

    private static Task<HttpResponseMessage> CreateLeaveAsync(HttpClient emp, DateOnly s, DateOnly e, bool ack)
        => emp.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, s, e, null, null, "إجازة", null, ack), TestJson.Options);

    private static Task<HttpResponseMessage> CreatePermissionAsync(
        HttpClient emp, DateOnly day, PermissionShortfallResolution resolution = PermissionShortfallResolution.None)
        => emp.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Permission, day, null,
                new TimeOnly(9, 0), new TimeOnly(11, 0), "استئذان", null,
                AcknowledgedUnpaidDeduction: false, PermissionShortfallResolution: resolution), TestJson.Options);

    private static async Task ApproveChainAsync(Org org, Guid id)
    {
        await org.Tl.C.PostAsJsonAsync($"/api/leave-requests/{id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        await org.Manager.C.PostAsJsonAsync($"/api/leave-requests/{id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        await org.Gm.C.PostAsJsonAsync($"/api/leave-requests/{id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);
    }

    /// <summary>إجازة مؤثّرة معتمَدة نهائيًّا — الموظّف بلا رصيد ⇒ كل الأيام غير مغطّاة.</summary>
    private async Task<Guid> CreateApprovedImpactedLeaveAsync(Org org, DateOnly s, DateOnly e)
    {
        var dto = (await (await CreateLeaveAsync(org.Emp.C, s, e, ack: true)).ReadAsync<LeaveRequestDto>())!;
        await ApproveChainAsync(org, dto.Id);
        return dto.Id;
    }

    private static async Task<PayrollImpactListDto> ListAsync(HttpClient c, string query = "")
        => (await (await c.GetAsync("/api/payroll/leave-impacts" + query)).ReadAsync<PayrollImpactListDto>())!;

    // سنة معزولة لاختبارات الأذونات (حدّ شهري 1) كي لا تتلوّث القاعدة المشتركة.
    private const int PY = 2087;

    private async Task EnsureMonthlyPolicyAsync(int year, decimal monthlyLimit)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.BalancePolicies.FirstOrDefaultAsync(p => p.Year == year && p.JobRoleId == null);
        if (existing is null)
            db.BalancePolicies.Add(new BalancePolicy
            {
                Year = year,
                JobRoleId = null,
                PermissionUnit = PermissionUnit.Count,
                PermissionMonthlyLimit = monthlyLimit,
                AllowNegativeBalance = true
            });
        else if (existing.PermissionMonthlyLimit != monthlyLimit)
            existing.PermissionMonthlyLimit = monthlyLimit;
        await db.SaveChangesAsync();
    }

    // ===== 1) طلب مؤثّر يظهر، وغير المؤثّر لا يظهر =====
    [Fact]
    public async Task ImpactedLeave_Appears_NonImpacted_Hidden()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, 2, 2026); // المتاح 2

        // يُنشآن قبل أيّ اعتماد كي يبقى الرصيد المرجعي = 2 لكليهما (الخصم يقع فقط عند الاعتماد النهائي).
        // إجازة 3 أيام > الرصيد ⇒ مؤثّرة (بإقرار).
        var impacted = (await (await CreateLeaveAsync(org.Emp.C, new(2026, 1, 5), new(2026, 1, 7), ack: true)).ReadAsync<LeaveRequestDto>())!;
        // إجازة يومين ضمن الرصيد ⇒ غير مؤثّرة.
        var clean = (await (await CreateLeaveAsync(org.Emp.C, new(2026, 2, 9), new(2026, 2, 10), ack: false)).ReadAsync<LeaveRequestDto>())!;
        await ApproveChainAsync(org, impacted.Id);
        await ApproveChainAsync(org, clean.Id);

        var list = await ListAsync(org.Admin);
        Assert.Contains(list.Items, i => i.LeaveRequestId == impacted.Id);
        Assert.DoesNotContain(list.Items, i => i.LeaveRequestId == clean.Id);
    }

    // ===== 2) الفلتر الافتراضي = HrApproved فقط ⇒ يخفي الطلب المؤثّر غير المعتمَد نهائيًّا =====
    [Fact]
    public async Task DefaultFilter_HrApprovedOnly_HidesSubmitted()
    {
        var org = await BuildOrgAsync();
        var submitted = (await (await CreateLeaveAsync(org.Emp.C, new(2026, 3, 5), new(2026, 3, 7), ack: true)).ReadAsync<LeaveRequestDto>())!;
        Assert.Equal(LeaveRequestStatus.Submitted, submitted.Status);

        var list = await ListAsync(org.Admin);
        Assert.DoesNotContain(list.Items, i => i.LeaveRequestId == submitted.Id);
    }

    // ===== 3) allApprovalStatuses=true ⇒ يُظهِر الطلب المؤثّر غير المعتمَد =====
    [Fact]
    public async Task AllApprovalStatuses_ShowsSubmitted()
    {
        var org = await BuildOrgAsync();
        var submitted = (await (await CreateLeaveAsync(org.Emp.C, new(2026, 3, 5), new(2026, 3, 7), ack: true)).ReadAsync<LeaveRequestDto>())!;

        var list = await ListAsync(org.Admin, "?allApprovalStatuses=true");
        Assert.Contains(list.Items, i => i.LeaveRequestId == submitted.Id);
    }

    // ===== 4) فلتر حالة اعتماد صريح =====
    [Fact]
    public async Task ApprovalStatusFilter_Explicit()
    {
        var org = await BuildOrgAsync();
        var submitted = (await (await CreateLeaveAsync(org.Emp.C, new(2026, 3, 5), new(2026, 3, 7), ack: true)).ReadAsync<LeaveRequestDto>())!;

        var asSubmitted = await ListAsync(org.Admin, "?approvalStatus=Submitted");
        Assert.Contains(asSubmitted.Items, i => i.LeaveRequestId == submitted.Id);

        var asHrApproved = await ListAsync(org.Admin, "?approvalStatus=HrApproved");
        Assert.DoesNotContain(asHrApproved.Items, i => i.LeaveRequestId == submitted.Id);
    }

    // ===== 5) تصنيف: إجازة مؤثّرة ⇒ UnpaidLeave =====
    [Fact]
    public async Task Classify_UnpaidLeave()
    {
        var org = await BuildOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 4, 5), new(2026, 4, 7));

        var item = (await ListAsync(org.Admin)).Items.Single(i => i.LeaveRequestId == id);
        Assert.Equal(LeaveRequestType.Leave, item.Type);
        Assert.Equal(PayrollImpactType.UnpaidLeave, item.ImpactType);
        Assert.True(item.IsPotentialUnpaidLeave);
    }

    // ===== 6) تصنيف: استئذان بتعهّد تعويض بعد الدوام ⇒ PermissionAfterHoursCompensation =====
    [Fact]
    public async Task Classify_PermissionAfterHoursCompensation()
    {
        await EnsureMonthlyPolicyAsync(PY, 1);
        var org = await BuildOrgAsync();

        // يحجز الفتحة الشهرية (القائم يُحتسب).
        await CreatePermissionAsync(org.Emp.C, new(PY, 3, 5));
        var over = (await (await CreatePermissionAsync(org.Emp.C, new(PY, 3, 6), PermissionShortfallResolution.CompensateAfterHours)).ReadAsync<LeaveRequestDto>())!;

        var item = (await ListAsync(org.Admin, $"?allApprovalStatuses=true&employeeUserId={org.Emp.Id}")).Items.Single(i => i.LeaveRequestId == over.Id);
        Assert.Equal(LeaveRequestType.Permission, item.Type);
        Assert.Equal(PayrollImpactType.PermissionAfterHoursCompensation, item.ImpactType);
        Assert.Equal(PermissionShortfallResolution.CompensateAfterHours, item.PermissionShortfallResolution);
    }

    // ===== 7) تصنيف: استئذان بمعالجة إدارية/مالية ⇒ PermissionAdminOrPayrollReview =====
    [Fact]
    public async Task Classify_PermissionAdminOrPayrollReview()
    {
        await EnsureMonthlyPolicyAsync(PY, 1);
        var org = await BuildOrgAsync();

        await CreatePermissionAsync(org.Emp.C, new(PY, 4, 5));
        var over = (await (await CreatePermissionAsync(org.Emp.C, new(PY, 4, 6), PermissionShortfallResolution.AdminOrPayrollReview)).ReadAsync<LeaveRequestDto>())!;

        var item = (await ListAsync(org.Admin, $"?allApprovalStatuses=true&employeeUserId={org.Emp.Id}")).Items.Single(i => i.LeaveRequestId == over.Id);
        Assert.Equal(PayrollImpactType.PermissionAdminOrPayrollReview, item.ImpactType);
        Assert.Equal(PermissionShortfallResolution.AdminOrPayrollReview, item.PermissionShortfallResolution);
    }

    // ===== 8) فلتر نوع التأثير =====
    [Fact]
    public async Task ImpactTypeFilter()
    {
        var org = await BuildOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 5, 5), new(2026, 5, 7));

        var matching = await ListAsync(org.Admin, "?impactType=UnpaidLeave");
        Assert.Contains(matching.Items, i => i.LeaveRequestId == id);

        var nonMatching = await ListAsync(org.Admin, "?impactType=PermissionAfterHoursCompensation");
        Assert.DoesNotContain(nonMatching.Items, i => i.LeaveRequestId == id);
    }

    // ===== 9) فلتر حالة المراجعة المالية (الافتراضي Pending، ثم Processed بعد المراجعة) =====
    [Fact]
    public async Task ReviewStatusFilter()
    {
        var org = await BuildOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 6, 5), new(2026, 6, 7));

        // قبل المراجعة: Pending ضمنيًّا.
        Assert.Contains((await ListAsync(org.Admin, "?reviewStatus=Pending")).Items, i => i.LeaveRequestId == id);
        Assert.DoesNotContain((await ListAsync(org.Admin, "?reviewStatus=Processed")).Items, i => i.LeaveRequestId == id);

        await org.Admin.PatchAsJsonAsync($"/api/payroll/leave-impacts/{id}/review",
            new PayrollImpactReviewRequest(PayrollImpactReviewStatus.Processed, "تمّت المعالجة"), TestJson.Options);

        Assert.Contains((await ListAsync(org.Admin, "?reviewStatus=Processed")).Items, i => i.LeaveRequestId == id);
        Assert.DoesNotContain((await ListAsync(org.Admin, "?reviewStatus=Pending")).Items, i => i.LeaveRequestId == id);
    }

    // ===== 10) فلتر الشهر/السنة =====
    [Fact]
    public async Task MonthYearFilter()
    {
        var org = await BuildOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 3, 5), new(2026, 3, 7));

        Assert.Contains((await ListAsync(org.Admin, "?month=2026-03")).Items, i => i.LeaveRequestId == id);
        Assert.DoesNotContain((await ListAsync(org.Admin, "?month=2026-04")).Items, i => i.LeaveRequestId == id);
        Assert.Contains((await ListAsync(org.Admin, "?year=2026")).Items, i => i.LeaveRequestId == id);
    }

    // ===== 11) فلتر الموظّف =====
    [Fact]
    public async Task EmployeeUserIdFilter()
    {
        var org = await BuildOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 7, 5), new(2026, 7, 7));

        Assert.Contains((await ListAsync(org.Admin, $"?employeeUserId={org.Emp.Id}")).Items, i => i.LeaveRequestId == id);
        Assert.DoesNotContain((await ListAsync(org.Admin, $"?employeeUserId={org.Gm.Id}")).Items, i => i.LeaveRequestId == id);
    }

    // ===== 12) GetById لطلب مؤثّر ⇒ 200 + تفاصيل + canManage للأدمن =====
    [Fact]
    public async Task GetById_Impacted_ReturnsDetail()
    {
        var org = await BuildOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 8, 5), new(2026, 8, 7));

        var res = await org.Admin.GetAsync($"/api/payroll/leave-impacts/{id}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var detail = (await res.ReadAsync<PayrollImpactDetailDto>())!;
        Assert.Equal(id, detail.Item.LeaveRequestId);
        Assert.Equal("إجازة", detail.Reason);
        Assert.True(detail.CanManage); // الأدمن ضمن PayrollImpactManagers
    }

    // ===== 13) GetById لطلب غير مؤثّر ⇒ 404 =====
    [Fact]
    public async Task GetById_NonImpacted_404()
    {
        var org = await BuildOrgAsync();
        await OpeningAsync(org.Admin, org.Emp.Id, 30, 2026);
        var clean = (await (await CreateLeaveAsync(org.Emp.C, new(2026, 9, 7), new(2026, 9, 8), ack: false)).ReadAsync<LeaveRequestDto>())!;
        await ApproveChainAsync(org, clean.Id);

        var res = await org.Admin.GetAsync($"/api/payroll/leave-impacts/{clean.Id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("payroll_impact.not_found", await ErrorCodeAsync(res));
    }

    // ===== 14) GetById لمعرّف غير موجود ⇒ 404 =====
    [Fact]
    public async Task GetById_Nonexistent_404()
    {
        var org = await BuildOrgAsync();
        var res = await org.Admin.GetAsync($"/api/payroll/leave-impacts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("payroll_impact.not_found", await ErrorCodeAsync(res));
    }

    // ===== 15) RBAC قراءة: Admin/CEO/GM/HR/CeoSupport ⇒ 200 =====
    [Fact]
    public async Task RBAC_Read_AllowedRoles_200()
    {
        var org = await BuildOrgAsync();
        foreach (var c in new[] { org.Admin, org.Ceo, org.Gm.C, org.Hr.C, org.CeoSupport })
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/payroll/leave-impacts")).StatusCode);
    }

    // ===== 16) RBAC قراءة: Manager/TeamLeader/Employee/Viewer ⇒ 403 =====
    [Fact]
    public async Task RBAC_Read_ForbiddenRoles_403()
    {
        var org = await BuildOrgAsync();
        foreach (var c in new[] { org.Manager.C, org.Tl.C, org.Emp.C, org.Viewer })
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/payroll/leave-impacts")).StatusCode);
    }

    // ===== 17) RBAC قراءة: مجهول ⇒ 401 =====
    [Fact]
    public async Task RBAC_Read_Anonymous_401()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/payroll/leave-impacts")).StatusCode);
    }

    // ===== 18) تحديث المراجعة: الأدمن (إنشاء كسول) يضبط الحالة/الملاحظة/المراجِع =====
    [Fact]
    public async Task Review_Admin_LazyCreate_SetsFields()
    {
        var org = await BuildOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 10, 5), new(2026, 10, 7));

        var res = await org.Admin.PatchAsJsonAsync($"/api/payroll/leave-impacts/{id}/review",
            new PayrollImpactReviewRequest(PayrollImpactReviewStatus.Processed, "روجِعت ماليًّا"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var detail = (await res.ReadAsync<PayrollImpactDetailDto>())!;
        Assert.Equal(PayrollImpactReviewStatus.Processed, detail.Item.ReviewStatus);
        Assert.Equal("روجِعت ماليًّا", detail.Item.FinanceNote);
        Assert.NotNull(detail.Item.ReviewedByUserId);
        Assert.NotNull(detail.Item.ReviewedAtUtc);

        // ينعكس في القائمة.
        var item = (await ListAsync(org.Admin)).Items.Single(i => i.LeaveRequestId == id);
        Assert.Equal(PayrollImpactReviewStatus.Processed, item.ReviewStatus);
    }

    // ===== 19) تحديث المراجعة: HR — التحديث الثاني يعدّل نفس الصفّ (idempotent، صفّ واحد) =====
    [Fact]
    public async Task Review_Hr_Idempotent_Update()
    {
        var org = await BuildOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 11, 5), new(2026, 11, 7));

        await org.Hr.C.PatchAsJsonAsync($"/api/payroll/leave-impacts/{id}/review",
            new PayrollImpactReviewRequest(PayrollImpactReviewStatus.Processed, "أولى"), TestJson.Options);
        var second = await org.Hr.C.PatchAsJsonAsync($"/api/payroll/leave-impacts/{id}/review",
            new PayrollImpactReviewRequest(PayrollImpactReviewStatus.NeedsReview, "ثانية"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var detail = (await second.ReadAsync<PayrollImpactDetailDto>())!;
        Assert.Equal(PayrollImpactReviewStatus.NeedsReview, detail.Item.ReviewStatus);
        Assert.Equal("ثانية", detail.Item.FinanceNote);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.PayrollImpactReviews.CountAsync(r => r.LeaveRequestId == id);
        Assert.Equal(1, count); // صفّ واحد فقط — لا تكرار
    }

    // ===== 20) تحديث المراجعة: الأدوار القارئة فقط والأدنى ⇒ 403 =====
    [Fact]
    public async Task Review_ReadOnly_And_Lower_Roles_403()
    {
        var org = await BuildOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 12, 5), new(2026, 12, 7));
        var body = new PayrollImpactReviewRequest(PayrollImpactReviewStatus.Processed, "محاولة");

        foreach (var c in new[] { org.Ceo, org.Gm.C, org.CeoSupport, org.Emp.C, org.Tl.C })
            Assert.Equal(HttpStatusCode.Forbidden,
                (await c.PatchAsJsonAsync($"/api/payroll/leave-impacts/{id}/review", body, TestJson.Options)).StatusCode);
    }

    // ===== 21) المراجعة لا تمسّ الطلب الأصلي ولا الراتب؛ والمراجعة على غير مؤثّر ⇒ 404 =====
    [Fact]
    public async Task Review_DoesNotModify_LeaveRequest_NoDeduction_And_404_On_NonImpacted()
    {
        var org = await BuildOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 2, 3), new(2026, 2, 5));

        var before = (await (await org.Gm.C.GetAsync($"/api/leave-requests/{id}")).ReadAsync<LeaveRequestDto>())!;
        var ledgerBefore = (await (await org.Admin.GetAsync($"/api/balances/employees/{org.Emp.Id}/ledger?year=2026")).ReadAsync<EmployeeLedgerDto>())!;

        await org.Admin.PatchAsJsonAsync($"/api/payroll/leave-impacts/{id}/review",
            new PayrollImpactReviewRequest(PayrollImpactReviewStatus.Ignored, "بلا أثر"), TestJson.Options);

        var after = (await (await org.Gm.C.GetAsync($"/api/leave-requests/{id}")).ReadAsync<LeaveRequestDto>())!;
        Assert.Equal(before.Status, after.Status); // الحالة لم تتغيّر (HrApproved)
        Assert.Equal(before.UncoveredLeaveDays, after.UncoveredLeaveDays);
        Assert.Equal(before.BalanceAtRequest, after.BalanceAtRequest);
        Assert.Equal(before.IsPotentialUnpaidLeave, after.IsPotentialUnpaidLeave);

        var ledgerAfter = (await (await org.Admin.GetAsync($"/api/balances/employees/{org.Emp.Id}/ledger?year=2026")).ReadAsync<EmployeeLedgerDto>())!;
        Assert.Equal(ledgerBefore.Entries.Count, ledgerAfter.Entries.Count); // لا حركة راتب جديدة من المراجعة

        // المراجعة على طلب غير مؤثّر ⇒ 404 (لا يُنشأ صفّ مراجعة).
        await OpeningAsync(org.Admin, org.Emp.Id, 30, 2026);
        var clean = (await (await CreateLeaveAsync(org.Emp.C, new(2026, 5, 18), new(2026, 5, 19), ack: false)).ReadAsync<LeaveRequestDto>())!;
        var res = await org.Admin.PatchAsJsonAsync($"/api/payroll/leave-impacts/{clean.Id}/review",
            new PayrollImpactReviewRequest(PayrollImpactReviewStatus.Processed, "x"), TestJson.Options);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("payroll_impact.not_found", await ErrorCodeAsync(res));
    }
}
