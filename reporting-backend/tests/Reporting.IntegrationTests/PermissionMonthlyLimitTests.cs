using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Application.Leave;
using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// حارس الحد الشهري للأذونات (V1.1 — خدمات الموظف، مُحدَّث V1.1.1 — soft-ack). عند الإنشاء صار الحارس
/// «إقرارًا ليّنًا»: تجاوز الرصيد الشهري بلا قرار ⇒ 400 permission.balance_ack_required؛ مع قرار
/// (PermissionShortfallResolution غير None) ⇒ يُقبَل ويُخزَّن لقطة + القرار. أمّا عند الاعتماد النهائي
/// HrApproved فيبقى الحارس صلبًا (409) لكن فقط للطلبات بقرار None (لم يُقرّ صاحبها بالتجاوز) — أهمّ موضع،
/// بعدّ المعتمَد فقط، قبل أي تعديل حالة أو كتابة حركة. العدّ من leave_requests حسب الشهر (StartDate).
/// يستخدم سنواتٍ مستقبلية معزولة (2090–2099) كي لا يتلوّث الـ test DB المشترك، والسياسات تُنشأ
/// get-or-create لتفادي تصادم الفهرس الفريد (Year, JobRoleId) عبر التشغيلات. لا سياسة عامّة لسنة 2026
/// تُزرع هنا (الزرع dev-only عبر OrgSeeder) فتبقى اختبارات 2026 بلا حد.
/// </summary>
[Collection("Integration")]
public class PermissionMonthlyLimitTests
{
    private readonly CustomWebApplicationFactory _factory;

    public PermissionMonthlyLimitTests(CustomWebApplicationFactory factory) => _factory = factory;

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

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private static Task<HttpResponseMessage> CreatePermissionAsync(HttpClient c, DateOnly day)
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Permission, day, null,
                new TimeOnly(9, 0), new TimeOnly(11, 0), "استئذان", null), TestJson.Options);

    private static Task<HttpResponseMessage> CreatePermissionWithResolutionAsync(
        HttpClient c, DateOnly day, PermissionShortfallResolution resolution)
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Permission, day, null,
                new TimeOnly(9, 0), new TimeOnly(11, 0), "استئذان", null,
                AcknowledgedUnpaidDeduction: false, PermissionShortfallResolution: resolution), TestJson.Options);

    private static Task<HttpResponseMessage> CreateLeaveAsync(HttpClient c, DateOnly s, DateOnly e)
        => c.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, s, e, null, null, "إجازة", null), TestJson.Options);

    private async Task RunApprovalsToManagerAsync(Org org, Guid id)
    {
        await org.Tl.C.PostAsJsonAsync($"/api/leave-requests/{id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        await org.Manager.C.PostAsJsonAsync($"/api/leave-requests/{id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
    }

    private async Task<HttpResponseMessage> HrApproveAsync(Org org, Guid id)
        => await org.Gm.C.PostAsJsonAsync($"/api/leave-requests/{id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);

    /// <summary>ينشئ إذنًا ويمرّره عبر السلسلة الكاملة حتى الاعتماد النهائي؛ يعيد المعرّف وردّ HR النهائي.</summary>
    private async Task<(Guid Id, HttpResponseMessage Hr)> ApprovePermissionAsync(Org org, DateOnly day)
    {
        var dto = (await (await CreatePermissionAsync(org.Emp.C, day)).ReadAsync<LeaveRequestDto>())!;
        await RunApprovalsToManagerAsync(org, dto.Id);
        var hr = await HrApproveAsync(org, dto.Id);
        return (dto.Id, hr);
    }

    private static async Task<EmployeeLedgerDto> LedgerAsync(HttpClient admin, Guid userId, int year)
        => (await (await admin.GetAsync($"/api/balances/employees/{userId}/ledger?year={year}")).ReadAsync<EmployeeLedgerDto>())!;

    // ===== أدوات قاعدة البيانات (لا يوجد API لإنشاء سياسة رصيد؛ يُزرع dev-only عبر OrgSeeder) =====
    private async Task EnsureGeneralPolicyAsync(int year, decimal? monthlyLimit)
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

    private async Task<Guid> CreateJobRoleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jr = new JobRole { NameAr = "مسمّى اختبار الحد الشهري", Code = $"QA-PML-{Guid.NewGuid():N}", IsActive = true };
        db.JobRoles.Add(jr);
        await db.SaveChangesAsync();
        return jr.Id;
    }

    private async Task AssignJobRoleAsync(Guid userId, Guid jobRoleId)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = await users.FindByIdAsync(userId.ToString());
        u!.JobRoleId = jobRoleId;
        await users.UpdateAsync(u);
    }

    private async Task EnsureRolePolicyAsync(int year, Guid jobRoleId, decimal? monthlyLimit)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.BalancePolicies.FirstOrDefaultAsync(p => p.Year == year && p.JobRoleId == jobRoleId);
        if (existing is null)
        {
            db.BalancePolicies.Add(new BalancePolicy
            {
                Year = year,
                JobRoleId = jobRoleId,
                PermissionUnit = PermissionUnit.Count,
                PermissionMonthlyLimit = monthlyLimit,
                AllowNegativeBalance = true
            });
            await db.SaveChangesAsync();
        }
    }

    // ===== 1) أول وثاني إذن في الشهر يُقبلان (الخصم يُسجَّل) =====
    [Fact]
    public async Task FirstAndSecond_Permission_SameMonth_Accepted()
    {
        const int year = 2090;
        await EnsureGeneralPolicyAsync(year, 2);
        var org = await BuildOrgAsync();

        var p1 = await ApprovePermissionAsync(org, new DateOnly(year, 3, 5));
        var p2 = await ApprovePermissionAsync(org, new DateOnly(year, 3, 12));
        Assert.Equal(HttpStatusCode.OK, p1.Hr.StatusCode);
        Assert.Equal(HttpStatusCode.OK, p2.Hr.StatusCode);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id, year);
        var debits = ledger.Entries.Count(e =>
            e.Source == BalanceSource.ApprovedPermission && e.Direction == BalanceDirection.Debit && e.Amount == 1);
        Assert.Equal(2, debits);

        // DTO «أرصدتي» يعرض الحد الشهري + المستخدَم/المتبقّي (غير فارغين عند وجود حد). القيمة العددية
        // مرتبطة بالشهر التقويمي الحالي (UTC) فلا نؤكّد رقمًا بعينه تفاديًا للاعتماد على الساعة.
        var me = (await (await org.Emp.C.GetAsync($"/api/me/balances?year={year}")).ReadAsync<MyBalancesDto>())!;
        Assert.Equal(2, me.PermissionMonthlyLimit);
        Assert.NotNull(me.PermissionUsedThisMonth);
        Assert.NotNull(me.PermissionRemainingThisMonth);
    }

    // ===== 2) الإذن الثالث بلا قرار ⇒ 400 إقرار مطلوب؛ ومع قرار ⇒ يُقبَل ويُخزَّن لقطة (حارس soft-ack الجديد) =====
    [Fact]
    public async Task Third_Permission_SameMonth_NoResolution_400_Then_WithResolution_AcceptedWithSnapshot()
    {
        const int year = 2090;
        await EnsureGeneralPolicyAsync(year, 2);
        var org = await BuildOrgAsync();

        await ApprovePermissionAsync(org, new DateOnly(year, 4, 5));
        await ApprovePermissionAsync(org, new DateOnly(year, 4, 12));

        // بلا قرار ⇒ 400 (يُعرض الخياران بالواجهة).
        var third = await CreatePermissionAsync(org.Emp.C, new DateOnly(year, 4, 19));
        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);
        Assert.Equal("permission.balance_ack_required", await ErrorCodeAsync(third));

        // مع قرار (تعويض الوقت) ⇒ يُقبَل ويُخزَّن لقطة الرصيد الشهري + القرار.
        var withRes = await CreatePermissionWithResolutionAsync(
            org.Emp.C, new DateOnly(year, 4, 26), PermissionShortfallResolution.CompensateAfterHours);
        Assert.Equal(HttpStatusCode.OK, withRes.StatusCode);
        var dto = (await withRes.ReadAsync<LeaveRequestDto>())!;
        Assert.True(dto.IsPotentialUnpaidLeave);
        Assert.Equal(PermissionShortfallResolution.CompensateAfterHours, dto.PermissionShortfallResolution);
        Assert.Equal(1, dto.RequestedLeaveDays);            // إذن واحد لكل طلب (الوحدة Count)
        Assert.NotNull(dto.UncoveredLeaveDays);
        Assert.True(dto.UncoveredLeaveDays > 0);
        Assert.NotNull(dto.EmployeeAcknowledgedAtUtc);
    }

    // ===== 3) الإذن الثالث في شهر مختلف يُقبل (الحد شهري لا تراكمي) =====
    [Fact]
    public async Task Third_Permission_DifferentMonth_Accepted()
    {
        const int year = 2091;
        await EnsureGeneralPolicyAsync(year, 2);
        var org = await BuildOrgAsync();

        await ApprovePermissionAsync(org, new DateOnly(year, 5, 5));
        await ApprovePermissionAsync(org, new DateOnly(year, 5, 12));
        var other = await ApprovePermissionAsync(org, new DateOnly(year, 6, 3)); // شهر مختلف
        Assert.Equal(HttpStatusCode.OK, other.Hr.StatusCode);
    }

    // ===== 4) إبطال إذن معتمَد يحرّر فتحة الشهر فيُقبل إذن جديد بدلًا منه =====
    [Fact]
    public async Task Revoke_Approved_Frees_Monthly_Slot()
    {
        const int year = 2092;
        await EnsureGeneralPolicyAsync(year, 2);
        var org = await BuildOrgAsync();

        var p1 = await ApprovePermissionAsync(org, new DateOnly(year, 7, 5));
        await ApprovePermissionAsync(org, new DateOnly(year, 7, 12));

        // الثالث الآن يتجاوز الحد ⇒ يلزم قرار (400 بلا قرار).
        var blocked = await CreatePermissionAsync(org.Emp.C, new DateOnly(year, 7, 19));
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Equal("permission.balance_ack_required", await ErrorCodeAsync(blocked));

        // إبطال الأول (المعتمَد) ⇒ يحوّله إلى Cancelled فيُستبعَد من العدّ.
        var revoke = await org.Admin.PostAsJsonAsync(
            $"/api/leave-requests/{p1.Id}/revoke", new LeaveRevokeRequest("إلغاء بطلب الموظّف"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        // إذن جديد في نفس الشهر يُقبل الآن.
        var freed = await ApprovePermissionAsync(org, new DateOnly(year, 7, 26));
        Assert.Equal(HttpStatusCode.OK, freed.Hr.StatusCode);
    }

    // ===== 5) غياب السياسة ⇒ لا حد (عدّة أذونات في الشهر كلها تُقبل) =====
    [Fact]
    public async Task NoPolicy_NoLimit()
    {
        const int year = 2093; // لا سياسة لهذه السنة
        var org = await BuildOrgAsync();

        var p1 = await ApprovePermissionAsync(org, new DateOnly(year, 8, 5));
        var p2 = await ApprovePermissionAsync(org, new DateOnly(year, 8, 12));
        var p3 = await ApprovePermissionAsync(org, new DateOnly(year, 8, 19));
        Assert.Equal(HttpStatusCode.OK, p1.Hr.StatusCode);
        Assert.Equal(HttpStatusCode.OK, p2.Hr.StatusCode);
        Assert.Equal(HttpStatusCode.OK, p3.Hr.StatusCode);
    }

    // ===== 6) سياسة عامّة تنطبق على الجميع (موظّف بلا مسمّى محدود بالعام) =====
    [Fact]
    public async Task GeneralPolicy_Applies_To_All()
    {
        const int year = 2098;
        await EnsureGeneralPolicyAsync(year, 2);
        var org = await BuildOrgAsync(); // الموظّف بلا JobRole

        await ApprovePermissionAsync(org, new DateOnly(year, 2, 5));
        await ApprovePermissionAsync(org, new DateOnly(year, 2, 12));
        var third = await CreatePermissionAsync(org.Emp.C, new DateOnly(year, 2, 19));
        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);
        Assert.Equal("permission.balance_ack_required", await ErrorCodeAsync(third));
    }

    // ===== 7) سياسة المسمّى الوظيفي تطغى على العامّة (الأخصّ يطغى) =====
    [Fact]
    public async Task RoleSpecificPolicy_Overrides_General()
    {
        const int year = 2094;
        await EnsureGeneralPolicyAsync(year, 2);
        var org = await BuildOrgAsync();

        var jobRoleId = await CreateJobRoleAsync();
        await AssignJobRoleAsync(org.Emp.Id, jobRoleId);
        await EnsureRolePolicyAsync(year, jobRoleId, 1); // الأخصّ: حد 1 فقط

        var p1 = await ApprovePermissionAsync(org, new DateOnly(year, 9, 5));
        Assert.Equal(HttpStatusCode.OK, p1.Hr.StatusCode);

        // الثاني يتجاوز الحد رغم أنّ العامّة تسمح بـ2 — لأن سياسة المسمّى (1) أخصّ ⇒ يلزم قرار (400).
        var second = await CreatePermissionAsync(org.Emp.C, new DateOnly(year, 9, 12));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("permission.balance_ack_required", await ErrorCodeAsync(second));
    }

    // ===== 8) الإجازة السنوية لا تتأثّر بحد الأذونات الشهري =====
    [Fact]
    public async Task AnnualLeave_Unaffected_By_Permission_Limit()
    {
        const int year = 2095;
        await EnsureGeneralPolicyAsync(year, 2);
        var org = await BuildOrgAsync();
        await org.Admin.PostAsJsonAsync($"/api/balances/employees/{org.Emp.Id}/opening",
            new OpeningBalanceRequest(BalanceType.AnnualLeave, 30, year, "افتتاحي"), TestJson.Options);

        // استنفاد حد الأذونات لا يمنع اعتماد إجازة في نفس الشهر.
        await ApprovePermissionAsync(org, new DateOnly(year, 10, 3));
        await ApprovePermissionAsync(org, new DateOnly(year, 10, 6));

        var leaveDto = (await (await CreateLeaveAsync(org.Emp.C, new DateOnly(year, 10, 12), new DateOnly(year, 10, 14)))
            .ReadAsync<LeaveRequestDto>())!;
        await RunApprovalsToManagerAsync(org, leaveDto.Id);
        var hr = await HrApproveAsync(org, leaveDto.Id);
        Assert.Equal(HttpStatusCode.OK, hr.StatusCode);
    }

    // ===== 9) الرصيد السالب لا يزال مسموحًا (الحد الشهري قيد على العدد لا على الرصيد) =====
    [Fact]
    public async Task NegativeBalance_Still_Allowed_Under_MonthlyLimit()
    {
        const int year = 2096;
        await EnsureGeneralPolicyAsync(year, 2);
        var org = await BuildOrgAsync(); // لا رصيد افتتاحي للأذونات

        var p1 = await ApprovePermissionAsync(org, new DateOnly(year, 11, 5));
        Assert.Equal(HttpStatusCode.OK, p1.Hr.StatusCode);

        var ledger = await LedgerAsync(org.Admin, org.Emp.Id, year);
        Assert.True(ledger.Permission.Remaining < 0);
        Assert.True(ledger.Permission.IsNegative);
    }

    // ===== 10) الرفض عند الاعتماد النهائي ⇒ 409 ولا يكتب أي حركة Ledger (أهمّ موضع) =====
    [Fact]
    public async Task RejectionAtFinalApproval_409_NoLedgerMovement()
    {
        // سنة 2099 بلا سياسة عامّة إطلاقًا + مسمّى وظيفي فريد لكل تشغيل، فلا تتلوّث من تشغيل سابق
        // في قاعدة الاختبار المشتركة الدائمة. تُنشأ سياسة المسمّى (حد=2) بعد إنشاء الأذونات الثلاثة.
        const int year = 2099;
        var org = await BuildOrgAsync();
        var jobRoleId = await CreateJobRoleAsync();
        await AssignJobRoleAsync(org.Emp.Id, jobRoleId);

        // إنشاء ثلاثة أذونات قبل وجود السياسة (حتى لا يحجب حارس الإنشاء الثالث).
        var d1 = (await (await CreatePermissionAsync(org.Emp.C, new DateOnly(year, 1, 5))).ReadAsync<LeaveRequestDto>())!;
        var d2 = (await (await CreatePermissionAsync(org.Emp.C, new DateOnly(year, 1, 12))).ReadAsync<LeaveRequestDto>())!;
        var d3 = (await (await CreatePermissionAsync(org.Emp.C, new DateOnly(year, 1, 19))).ReadAsync<LeaveRequestDto>())!;

        // الآن نضع سياسة المسمّى حد=2 (فريدة لهذا التشغيل).
        await EnsureRolePolicyAsync(year, jobRoleId, 2);

        // اعتماد الأول والثاني (ضمن الحد).
        await RunApprovalsToManagerAsync(org, d1.Id);
        Assert.Equal(HttpStatusCode.OK, (await HrApproveAsync(org, d1.Id)).StatusCode);
        await RunApprovalsToManagerAsync(org, d2.Id);
        Assert.Equal(HttpStatusCode.OK, (await HrApproveAsync(org, d2.Id)).StatusCode);

        // الثالث: يصل إلى ManagerApproved ثم يُرفض الاعتماد النهائي بـ409.
        await RunApprovalsToManagerAsync(org, d3.Id);
        var hr3 = await HrApproveAsync(org, d3.Id);
        Assert.Equal(HttpStatusCode.Conflict, hr3.StatusCode);
        Assert.Equal("leave_request.permission_monthly_limit.conflict", await ErrorCodeAsync(hr3));

        // لا حركة ApprovedPermission للطلب الثالث (لم يُخصَم).
        var ledger = await LedgerAsync(org.Admin, org.Emp.Id, year);
        Assert.DoesNotContain(ledger.Entries, e =>
            e.Source == BalanceSource.ApprovedPermission && e.RelatedRequestId == d3.Id);
        // إجمالي الخصومات = 2 فقط (الأول والثاني).
        Assert.Equal(2, ledger.Entries.Count(e =>
            e.Source == BalanceSource.ApprovedPermission && e.Direction == BalanceDirection.Debit));
    }
}
