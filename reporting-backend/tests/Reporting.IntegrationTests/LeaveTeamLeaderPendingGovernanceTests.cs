using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Auth;
using Reporting.Application.Common;
using Reporting.Application.Leave;
using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// اختبارات طابور الحوكمة «معلّقة عند قادة الفرق» (LEAVE-TL-PENDING-GOVERNANCE-R1) — قراءة-فقط.
/// تُثبِت: التفويض (LeaveGovernanceRead)، الرؤية على مستوى الشركة، استقلاله عن GetPendingAsync
/// (لا استثناء للمُقدِّم/النطاق)، تصنيف التأخّر (Pending/Attention/Critical/ExpiredUnresolved)،
/// الفلاتر والعدّادات والترتيب والترقيم، وحدات الطلب، صفريّة الرصيد، غياب قائد الفريق/عدم نشاطه،
/// وأن الطلبات المتجاوزة لخطوة قائد الفريق (TeamLeaderApproved/HrApproved) لا تظهر — بلا أيّ كتابة.
/// </summary>
[Collection("Integration")]
public class LeaveTeamLeaderPendingGovernanceTests
{
    private readonly CustomWebApplicationFactory _factory;
    public LeaveTeamLeaderPendingGovernanceTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string Endpoint = "/api/leave-requests/governance/team-leader-pending";

    // ===== مساعدات =====

    private async Task<HttpClient> LoginAsync(string role)
        => (await TestAuth.CreateUserAsync(_factory, role)).Client;

    private HttpClient Anon() => _factory.CreateClient();

    /// <summary>ينشئ موظّفًا (اختياريًّا نشطًا/غير نشط) وقائد فريق وفريقًا يربطهما، ويعيد المعرّفات.</summary>
    private async Task<(Guid EmpId, Guid TlId, Guid TeamId, Guid DeptId)> SeedTeamAsync(
        bool employeeActive = true, bool teamLeaderActive = true, bool withTeamLeader = true, bool withTeam = true)
    {
        Guid empId, tlId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var emp = new ApplicationUser
            {
                UserName = $"gov-emp-{Guid.NewGuid():N}@test.local",
                Email = $"gov-emp-{Guid.NewGuid():N}@test.local",
                EmailConfirmed = true,
                FullName = $"موظّف حوكمة {Guid.NewGuid():N}"[..24],
                IsActive = employeeActive
            };
            var tl = new ApplicationUser
            {
                UserName = $"gov-tl-{Guid.NewGuid():N}@test.local",
                Email = $"gov-tl-{Guid.NewGuid():N}@test.local",
                EmailConfirmed = true,
                FullName = $"قائد فريق حوكمة {Guid.NewGuid():N}"[..26],
                IsActive = teamLeaderActive
            };
            await users.CreateAsync(emp, "Passw0rd#1");
            await users.CreateAsync(tl, "Passw0rd#1");
            await users.AddToRoleAsync(emp, Roles.Employee);
            await users.AddToRoleAsync(tl, Roles.TeamLeader);
            empId = emp.Id;
            tlId = tl.Id;
        }

        Guid teamId = Guid.Empty, deptId = Guid.Empty;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dept = new Department { NameAr = $"إدارة حوكمة {Guid.NewGuid():N}", IsActive = true };
            db.Set<Department>().Add(dept);
            deptId = dept.Id;

            if (withTeam)
            {
                var team = new Team
                {
                    NameAr = $"فريق حوكمة {Guid.NewGuid():N}",
                    DepartmentId = dept.Id,
                    TeamLeaderId = withTeamLeader ? tlId : (Guid?)null,
                    IsActive = true
                };
                db.Set<Team>().Add(team);
                teamId = team.Id;

                var e = await db.Users.FirstAsync(u => u.Id == empId);
                e.TeamId = team.Id;
                e.DepartmentId = dept.Id;
            }
            else
            {
                var e = await db.Users.FirstAsync(u => u.Id == empId);
                e.DepartmentId = dept.Id;
            }
            await db.SaveChangesAsync();
        }
        return (empId, tlId, teamId, deptId);
    }

    /// <summary>يزرع طلب إجازة/استئذان مباشرةً في القاعدة بحالة/خطوة/تواريخ محدّدة (لتجهيزات التصنيف).</summary>
    private async Task<Guid> SeedLeaveRequestAsync(
        Guid employeeId,
        DateOnly start, DateOnly end,
        LeaveRequestStatus status = LeaveRequestStatus.Submitted,
        LeaveRequestStep step = LeaveRequestStep.TeamLeader,
        LeaveRequestType type = LeaveRequestType.Leave,
        DateTime? createdAtUtc = null,
        bool isHrRequest = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var req = new LeaveRequest
        {
            RequesterUserId = employeeId,
            Type = type,
            StartDate = start,
            EndDate = type == LeaveRequestType.Permission ? start : end,
            StartTime = type == LeaveRequestType.Permission ? new TimeOnly(9, 0) : null,
            EndTime = type == LeaveRequestType.Permission ? new TimeOnly(11, 0) : null,
            Reason = "سبب اختبار الحوكمة",
            Status = status,
            CurrentStep = step,
            IsHrRequest = isHrRequest,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow
        };
        db.Set<LeaveRequest>().Add(req);
        await db.SaveChangesAsync();
        return req.Id;
    }

    private async Task SeedLedgerAsync(Guid employeeId, Guid requestId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<EmployeeBalanceLedger>().Add(new EmployeeBalanceLedger
        {
            EmployeeId = employeeId,
            BalanceType = BalanceType.AnnualLeave,
            Amount = 1,
            Direction = BalanceDirection.Debit,
            Source = BalanceSource.ApprovedLeave,
            RelatedRequestId = requestId,
            Year = DateTime.UtcNow.Year,
            CreatedBy = employeeId
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedEventAsync(Guid requestId, Guid actorId, string action)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<LeaveRequestEvent>().Add(new LeaveRequestEvent
        {
            LeaveRequestId = requestId,
            ActorUserId = actorId,
            Action = action,
            Step = LeaveRequestStep.TeamLeader,
            FromStatus = LeaveRequestStatus.Draft,
            ToStatus = LeaveRequestStatus.Submitted
        });
        await db.SaveChangesAsync();
    }

    private static async Task<TeamLeaderPendingGovernanceResultDto> ReadResultAsync(HttpResponseMessage res)
        => (await res.ReadAsync<TeamLeaderPendingGovernanceResultDto>())!;

    private static readonly DateOnly Today = ReportCalendarPolicy.RiyadhToday();

    // ============================ التفويض (المرحلة 2) ============================

    [Fact] // 1
    public async Task Anonymous_Gets_401()
    {
        var res = await Anon().GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact] // 2
    public async Task Employee_Gets_403()
    {
        var c = await LoginAsync(Roles.Employee);
        var res = await c.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact] // 3
    public async Task TeamLeader_Only_Gets_403()
    {
        var c = await LoginAsync(Roles.TeamLeader);
        var res = await c.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact] // 4
    public async Task Manager_Only_Gets_403()
    {
        var c = await LoginAsync(Roles.Manager);
        var res = await c.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact] // 5
    public async Task Hr_Gets_200()
    {
        var c = await LoginAsync(Roles.Hr);
        var res = await c.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact] // 6
    public async Task Admin_Gets_200()
    {
        var c = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await c.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact] // 7
    public async Task Ceo_Gets_200()
    {
        var c = await LoginAsync(Roles.Ceo);
        var res = await c.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact] // 8
    public async Task GeneralManager_Gets_200()
    {
        var c = await LoginAsync(Roles.GeneralManager);
        var res = await c.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact] // 9 — CeoSupport محجوب (لا يملك رؤية تفاصيل الإجازات في سياسة الوحدة القائمة CanViewAsync)
    public async Task CeoSupport_Gets_403()
    {
        var c = await LoginAsync(Roles.CeoSupport);
        var res = await c.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ============================ الرؤية على مستوى الشركة والاستقلال ============================

    [Fact] // 10 — الطلب المعلّق عند قائد الفريق يظهر في الطابور
    public async Task PendingAtTeamLeader_Request_Appears()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        Assert.Contains(result.Items, i => i.RequestId == reqId);
    }

    [Fact] // 11 — الرؤية على مستوى الشركة: HR يرى طلبًا لموظّف خارج نطاقه الشخصيّ تمامًا
    public async Task CompanyWide_ShowsRequestsOutsideReaderScope()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(2), Today.AddDays(3));

        var hr = await LoginAsync(Roles.Hr); // لا علاقة تنظيمية بالموظّف
        var result = await ReadResultAsync(await hr.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        Assert.Contains(result.Items, i => i.RequestId == reqId);
    }

    [Fact] // 12 — مستقلّ عن GetPendingAsync: لا يستثني المُقدِّم حتى لو كان القارئ نفسه صاحب الطلب
    public async Task DoesNotExcludeRequester_IndependentOfDecisionQueue()
    {
        // موظّف يحمل أيضًا دور حوكمة (HR) وقدّم طلبًا — يجب أن يظهر طلبه في طابور الحوكمة.
        var self = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var reqId = await SeedLeaveRequestAsync(self.UserId, Today.AddDays(2), Today.AddDays(3));

        string selfName;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            selfName = (await db.Users.FirstAsync(u => u.Id == self.UserId)).FullName;
        }

        var result = await ReadResultAsync(await self.Client.GetAsync(Endpoint + $"?pageSize=500&search={Uri.EscapeDataString(selfName)}"));
        Assert.Contains(result.Items, i => i.RequestId == reqId);

        // بينما /pending (طابور القرار) يستثنيه (مُقدِّم الطلب لا يقرّر طلبه).
        var pending = await self.Client.GetFromJsonAsync<List<LeaveRequestListItemDto>>("/api/leave-requests/pending", TestJson.Options);
        Assert.DoesNotContain(pending!, r => r.Id == reqId);
    }

    // ============================ تصنيف التأخّر ============================

    [Fact] // 13 — Pending: قبل البداية وضمن عتبة المتابعة
    public async Task Classification_Pending()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(5), Today.AddDays(6),
            createdAtUtc: DateTime.UtcNow.AddHours(-1));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.Equal(LeaveGovernanceDelayStatus.Pending, item.DelayStatus);
    }

    [Fact] // 14 — Attention: قبل البداية لكن تجاوز عتبة المتابعة (>24 ساعة)
    public async Task Classification_Attention()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(5), Today.AddDays(6),
            createdAtUtc: DateTime.UtcNow.AddHours(-30));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.Equal(LeaveGovernanceDelayStatus.Attention, item.DelayStatus);
    }

    [Fact] // 15 — Critical: البداية ≤ اليوم ≤ النهاية والطلب ما زال معلّقًا
    public async Task Classification_Critical()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(-1), Today.AddDays(1));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.Equal(LeaveGovernanceDelayStatus.Critical, item.DelayStatus);
        Assert.True(item.StartedAlready);
        Assert.False(item.EndedAlready);
    }

    [Fact] // 16 — ExpiredUnresolved: النهاية < اليوم والطلب ما زال معلّقًا (تجهيزة خياليّة — المرحلة 5)
    public async Task Classification_ExpiredUnresolved()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(-10), Today.AddDays(-5));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.Equal(LeaveGovernanceDelayStatus.ExpiredUnresolved, item.DelayStatus);
        Assert.True(item.EndedAlready);
        Assert.True(item.StartedAlready);
    }

    // ============================ الطلبات التي يجب ألا تظهر ============================

    [Fact] // 17 — TeamLeaderApproved (تجاوز الخطوة) لا يظهر
    public async Task TeamLeaderApproved_DoesNotShow()
    {
        var (empId, _, _, _) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(2), Today.AddDays(3),
            status: LeaveRequestStatus.TeamLeaderApproved, step: LeaveRequestStep.Manager);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + "?pageSize=500"));
        Assert.DoesNotContain(result.Items, i => i.RequestId == reqId);
    }

    [Fact] // 18 — HrApproved (معتمَد نهائيًّا) لا يظهر
    public async Task HrApproved_DoesNotShow()
    {
        var (empId, _, _, _) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(2), Today.AddDays(3),
            status: LeaveRequestStatus.HrApproved, step: LeaveRequestStep.Completed);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + "?pageSize=500"));
        Assert.DoesNotContain(result.Items, i => i.RequestId == reqId);
    }

    [Fact] // 19 — ManagerApproved لا يظهر
    public async Task ManagerApproved_DoesNotShow()
    {
        var (empId, _, _, _) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(2), Today.AddDays(3),
            status: LeaveRequestStatus.ManagerApproved, step: LeaveRequestStep.Hr);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + "?pageSize=500"));
        Assert.DoesNotContain(result.Items, i => i.RequestId == reqId);
    }

    [Fact] // 20 — Submitted لكن عند خطوة غير TeamLeader لا يظهر
    public async Task SubmittedButNotAtTeamLeaderStep_DoesNotShow()
    {
        var (empId, _, _, _) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(2), Today.AddDays(3),
            status: LeaveRequestStatus.Submitted, step: LeaveRequestStep.Manager);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + "?pageSize=500"));
        Assert.DoesNotContain(result.Items, i => i.RequestId == reqId);
    }

    [Fact] // 20أ — المرفوض من قائد الفريق (TeamLeaderRejected) لا يظهر ولو بقيت خطوته عند قائد الفريق
    public async Task Rejected_DoesNotShow()
    {
        var (empId, _, _, _) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(2), Today.AddDays(3),
            status: LeaveRequestStatus.TeamLeaderRejected, step: LeaveRequestStep.TeamLeader);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + "?pageSize=500"));
        Assert.DoesNotContain(result.Items, i => i.RequestId == reqId);
    }

    [Fact] // 20ب — الملغى (Cancelled) لا يظهر ولو بقيت خطوته عند قائد الفريق
    public async Task Cancelled_DoesNotShow()
    {
        var (empId, _, _, _) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(2), Today.AddDays(3),
            status: LeaveRequestStatus.Cancelled, step: LeaveRequestStep.TeamLeader);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + "?pageSize=500"));
        Assert.DoesNotContain(result.Items, i => i.RequestId == reqId);
    }

    // ============================ الحقول المشتقّة ============================

    [Fact] // 21 — وحدات الإجازة = عدد الأيام شاملًا الطرفين
    public async Task RequestedUnits_Leave_InclusiveDays()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(5)); // 3 أيام

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.Equal(3, item.RequestedUnits);
    }

    [Fact] // 22 — وحدات الاستئذان = 1
    public async Task RequestedUnits_Permission_IsOne()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(3),
            type: LeaveRequestType.Permission);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.Equal(1, item.RequestedUnits);
    }

    [Fact] // 23 — صفريّة الرصيد افتراضًا (لا خصم لطلب معلّق)
    public async Task Ledger_ZeroByDefault()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.False(item.HasLedger);
        Assert.Equal(0, item.LedgerCount);
    }

    [Fact] // 24 — عدّ الرصيد يعكس الحركات المرتبطة (تجهيزة صريحة)
    public async Task Ledger_CountReflectsRelatedRows()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));
        await SeedLedgerAsync(empId, reqId);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.True(item.HasLedger);
        Assert.Equal(1, item.LedgerCount);
    }

    [Fact] // 25 — قائد الفريق يظهر باسمه ونشاطه من فريق الموظّف
    public async Task TeamLeader_NameAndActive_Resolved()
    {
        var (empId, tlId, teamId, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.Equal(tlId, item.TeamLeaderUserId);
        Assert.Equal(teamId, item.TeamId);
        Assert.True(item.IsTeamLeaderActive);
        Assert.False(item.MissingTeamLeaderAssignment);
    }

    [Fact] // 26 — غياب فريق ⇒ MissingTeamLeaderAssignment
    public async Task MissingTeam_FlaggedMissingTeamLeader()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync(withTeam: false);
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.True(item.MissingTeamLeaderAssignment);
        Assert.Null(item.TeamLeaderUserId);
    }

    [Fact] // 27 — فريق بلا قائد ⇒ MissingTeamLeaderAssignment
    public async Task TeamWithoutLeader_FlaggedMissing()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync(withTeamLeader: false);
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.True(item.MissingTeamLeaderAssignment);
        Assert.Null(item.TeamLeaderUserId);
    }

    [Fact] // 28 — قائد فريق غير نشط ⇒ IsTeamLeaderActive=false (لكنه يظهر)
    public async Task InactiveTeamLeader_Flagged()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync(teamLeaderActive: false);
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.False(item.IsTeamLeaderActive);
        Assert.False(item.MissingTeamLeaderAssignment);
    }

    // ============================ الموظّف غير النشط ============================

    [Fact] // 29 — موظّف غير نشط لا يظهر افتراضًا (includeInactive=false)
    public async Task InactiveEmployee_HiddenByDefault()
    {
        var (empId, _, _, _) = await SeedTeamAsync(employeeActive: false);
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + "?pageSize=500"));
        Assert.DoesNotContain(result.Items, i => i.RequestId == reqId);
    }

    [Fact] // 30 — includeInactive=true يُظهِر الموظّف غير النشط مع IsEmployeeActive=false
    public async Task InactiveEmployee_ShownWhenIncludeInactive()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync(employeeActive: false);
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&includeInactive=true&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.False(item.IsEmployeeActive);
    }

    // ============================ الفلاتر ============================

    [Fact] // 31 — فلتر نوع الطلب
    public async Task Filter_ByRequestType()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var leaveId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));
        var permId = await SeedLeaveRequestAsync(empId, Today.AddDays(6), Today.AddDays(6), type: LeaveRequestType.Permission);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&requestType=Permission&departmentId={deptId}"));
        Assert.Contains(result.Items, i => i.RequestId == permId);
        Assert.DoesNotContain(result.Items, i => i.RequestId == leaveId);
    }

    [Fact] // 32 — فلتر قائد الفريق
    public async Task Filter_ByTeamLeader()
    {
        var (empId, tlId, _, _) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));
        var (otherEmp, _, _, _) = await SeedTeamAsync();
        var otherReq = await SeedLeaveRequestAsync(otherEmp, Today.AddDays(3), Today.AddDays(4));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&teamLeaderId={tlId}"));
        Assert.Contains(result.Items, i => i.RequestId == reqId);
        Assert.DoesNotContain(result.Items, i => i.RequestId == otherReq);
    }

    [Fact] // 33 — فلتر الإدارة
    public async Task Filter_ByDepartment()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));
        var (otherEmp, _, _, _) = await SeedTeamAsync();
        var otherReq = await SeedLeaveRequestAsync(otherEmp, Today.AddDays(3), Today.AddDays(4));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        Assert.Contains(result.Items, i => i.RequestId == reqId);
        Assert.DoesNotContain(result.Items, i => i.RequestId == otherReq);
    }

    [Fact] // 33أ — فلتر الفريق
    public async Task Filter_ByTeam()
    {
        var (empId, _, teamId, _) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));
        var (otherEmp, _, _, _) = await SeedTeamAsync();
        var otherReq = await SeedLeaveRequestAsync(otherEmp, Today.AddDays(3), Today.AddDays(4));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&teamId={teamId}"));
        Assert.Contains(result.Items, i => i.RequestId == reqId);
        Assert.DoesNotContain(result.Items, i => i.RequestId == otherReq);
    }

    [Fact] // 34 — فلتر التصنيف
    public async Task Filter_ByDelayStatus()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var expiredId = await SeedLeaveRequestAsync(empId, Today.AddDays(-10), Today.AddDays(-5));
        var pendingId = await SeedLeaveRequestAsync(empId, Today.AddDays(5), Today.AddDays(6),
            createdAtUtc: DateTime.UtcNow.AddHours(-1));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&delayStatus=ExpiredUnresolved&departmentId={deptId}"));
        Assert.Contains(result.Items, i => i.RequestId == expiredId);
        Assert.DoesNotContain(result.Items, i => i.RequestId == pendingId);
    }

    [Fact] // 35 — فلتر البحث بالاسم
    public async Task Filter_BySearch()
    {
        var (empId, _, _, _) = await SeedTeamAsync();
        string empName;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            empName = (await db.Users.FirstAsync(u => u.Id == empId)).FullName;
        }
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&search={Uri.EscapeDataString(empName)}"));
        Assert.Contains(result.Items, i => i.RequestId == reqId);
        Assert.All(result.Items, i => Assert.Contains(empName, i.EmployeeName));
    }

    [Fact] // 36 — فلتر مدى تاريخ البداية
    public async Task Filter_ByStartDateRange()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var inRange = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));
        var outRange = await SeedLeaveRequestAsync(empId, Today.AddDays(20), Today.AddDays(21));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var from = Today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = Today.AddDays(10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&startDateFrom={from}&startDateTo={to}&departmentId={deptId}"));
        Assert.Contains(result.Items, i => i.RequestId == inRange);
        Assert.DoesNotContain(result.Items, i => i.RequestId == outRange);
    }

    // ============================ العدّادات والترتيب والترقيم ============================

    [Fact] // 37 — العدّادات تُحصي التصنيفات على كامل المطابقة
    public async Task Counters_ReflectClassifications()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        await SeedLeaveRequestAsync(empId, Today.AddDays(-10), Today.AddDays(-5)); // expired
        await SeedLeaveRequestAsync(empId, Today.AddDays(-1), Today.AddDays(1));   // critical
        await SeedLeaveRequestAsync(empId, Today.AddDays(5), Today.AddDays(6), createdAtUtc: DateTime.UtcNow.AddHours(-30)); // attention

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        Assert.Equal(3, result.Counters.TotalPending);
        Assert.Equal(1, result.Counters.ExpiredUnresolved);
        Assert.Equal(1, result.Counters.Critical);
        Assert.Equal(1, result.Counters.Attention);
    }

    [Fact] // 38 — الترتيب: ExpiredUnresolved أولًا ثمّ Critical ثمّ Attention ثمّ Pending
    public async Task Sort_ByDelaySeverity()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var pendingId = await SeedLeaveRequestAsync(empId, Today.AddDays(5), Today.AddDays(6), createdAtUtc: DateTime.UtcNow.AddHours(-1));
        var expiredId = await SeedLeaveRequestAsync(empId, Today.AddDays(-10), Today.AddDays(-5));
        var criticalId = await SeedLeaveRequestAsync(empId, Today.AddDays(-1), Today.AddDays(1));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var order = result.Items.Select(i => i.RequestId).ToList();
        Assert.True(order.IndexOf(expiredId) < order.IndexOf(criticalId));
        Assert.True(order.IndexOf(criticalId) < order.IndexOf(pendingId));
    }

    [Fact] // 39 — الترقيم يقسّم النتائج ويحفظ الإجمالي
    public async Task Pagination_SplitsResults()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        for (var d = 3; d <= 7; d++)
            await SeedLeaveRequestAsync(empId, Today.AddDays(d), Today.AddDays(d));

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var page1 = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?departmentId={deptId}&page=1&pageSize=2"));
        Assert.Equal(5, page1.Total);
        Assert.Equal(2, page1.Items.Count);
        var page3 = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?departmentId={deptId}&page=3&pageSize=2"));
        Assert.Single(page3.Items);
    }

    // ============================ لا أثر جانبيّ ============================

    [Fact] // 40 — قراءة الطابور لا تُنشئ أيّ حدث/حركة/تدقيق (لا كتابة)
    public async Task ReadOnly_NoSideEffects()
    {
        var (empId, _, _, _) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));

        int eventsBefore, ledgerBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            eventsBefore = await db.Set<LeaveRequestEvent>().CountAsync(e => e.LeaveRequestId == reqId);
            ledgerBefore = await db.Set<EmployeeBalanceLedger>().CountAsync(l => l.RelatedRequestId == reqId);
        }

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        await admin.GetAsync(Endpoint + "?pageSize=500");
        await admin.GetAsync(Endpoint + "?pageSize=500");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var eventsAfter = await db.Set<LeaveRequestEvent>().CountAsync(e => e.LeaveRequestId == reqId);
            var ledgerAfter = await db.Set<EmployeeBalanceLedger>().CountAsync(l => l.RelatedRequestId == reqId);
            var req = await db.Set<LeaveRequest>().FirstAsync(r => r.Id == reqId);
            Assert.Equal(eventsBefore, eventsAfter);
            Assert.Equal(ledgerBefore, ledgerAfter);
            Assert.Equal(LeaveRequestStatus.Submitted, req.Status);
            Assert.Equal(LeaveRequestStep.TeamLeader, req.CurrentStep);
        }
    }

    [Fact] // 41 — آخر حدث يظهر نوعه ووقته
    public async Task LastEvent_Surfaced()
    {
        var (empId, _, _, deptId) = await SeedTeamAsync();
        var reqId = await SeedLeaveRequestAsync(empId, Today.AddDays(3), Today.AddDays(4));
        await SeedEventAsync(reqId, empId, "submitted");

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var result = await ReadResultAsync(await admin.GetAsync(Endpoint + $"?pageSize=500&departmentId={deptId}"));
        var item = Assert.Single(result.Items, i => i.RequestId == reqId);
        Assert.Equal("submitted", item.LastEventType);
        Assert.NotNull(item.LastEventAtUtc);
    }
}
