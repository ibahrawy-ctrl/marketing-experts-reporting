using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Common;
using Reporting.Application.Leave;
using Reporting.Application.Payroll;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// FIN-R1 — الأدوار المالية (المدير المالي FinanceManager / الحسابات Accountant) وصلاحياتها.
/// يغطّي 12 سيناريو: على FIN-L1 (عرض التأثير على الرواتب) قراءةً وتحديثَ مراجعة، وعلى KPI-FIN1
/// (تصدير KPI للمالية) معاينةً وتصديرَ CSV — كلاهما 200 للدورين الماليّين. ويؤكّد بقاء Employee/TeamLeader
/// ممنوعَين (403) لئلّا تتسرّب الصلاحية. لا يمسّ أيّ منطق احتساب/خصم/راتب — فحص أذونات فقط.
/// </summary>
[Collection("Integration")]
public class FinanceRolesPermissionTests
{
    private readonly CustomWebApplicationFactory _factory;

    public FinanceRolesPermissionTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string PayrollListUrl = "/api/payroll/leave-impacts";
    private const string KpiPreviewUrl = "/api/kpi-evaluations/finance-export";
    private const string KpiCsvUrl = "/api/kpi-evaluations/finance-export/csv";

    private Task<(HttpClient Client, Guid UserId)> FinanceManagerAsync()
        => TestAuth.CreateUserAsync(_factory, Roles.FinanceManager);

    private Task<(HttpClient Client, Guid UserId)> AccountantAsync()
        => TestAuth.CreateUserAsync(_factory, Roles.Accountant);

    // ===== هرمية لإنشاء إجازة مؤثّرة معتمَدة نهائيًّا (للمراجعة المالية) =====
    private sealed class LeaveOrg
    {
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Manager;
        public required (HttpClient C, Guid Id) Tl;
        public required (HttpClient C, Guid Id) Emp;
    }

    private async Task<LeaveOrg> BuildLeaveOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var manager = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, manager.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tl.UserId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tl.UserId, emp.UserId);
        return new LeaveOrg
        {
            Gm = (gm.Client, gm.UserId),
            Manager = (manager.Client, manager.UserId),
            Tl = (tl.Client, tl.UserId),
            Emp = (emp.Client, emp.UserId),
        };
    }

    /// <summary>إجازة 3 أيام بلا رصيد (مؤثّرة) معتمَدة نهائيًّا ⇒ تظهر في عرض التأثير على الرواتب.</summary>
    private async Task<Guid> CreateApprovedImpactedLeaveAsync(LeaveOrg org, DateOnly s, DateOnly e)
    {
        var dto = (await (await org.Emp.C.PostAsJsonAsync("/api/leave-requests",
            new CreateLeaveRequestRequest(LeaveRequestType.Leave, s, e, null, null, "إجازة", null, true), TestJson.Options))
            .ReadAsync<LeaveRequestDto>())!;
        await org.Tl.C.PostAsJsonAsync($"/api/leave-requests/{dto.Id}/team-leader/approve", new LeaveApproveRequest(null), TestJson.Options);
        await org.Manager.C.PostAsJsonAsync($"/api/leave-requests/{dto.Id}/manager/approve", new LeaveApproveRequest(null), TestJson.Options);
        await org.Gm.C.PostAsJsonAsync($"/api/leave-requests/{dto.Id}/hr/approve", new LeaveApproveRequest("موافق"), TestJson.Options);
        return dto.Id;
    }

    private static Task<HttpResponseMessage> ReviewAsync(HttpClient c, Guid id)
        => c.PatchAsJsonAsync($"/api/payroll/leave-impacts/{id}/review",
            new PayrollImpactReviewRequest(PayrollImpactReviewStatus.Processed, "مراجعة مالية"), TestJson.Options);

    // ========== FIN-L1: عرض التأثير على الرواتب ==========

    // ===== 1) المدير المالي يقرأ FIN-L1 ⇒ 200 =====
    [Fact]
    public async Task FinanceManager_PayrollImpact_Read_200()
    {
        var (client, _) = await FinanceManagerAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(PayrollListUrl)).StatusCode);
    }

    // ===== 2) الحسابات يقرأ FIN-L1 ⇒ 200 =====
    [Fact]
    public async Task Accountant_PayrollImpact_Read_200()
    {
        var (client, _) = await AccountantAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(PayrollListUrl)).StatusCode);
    }

    // ===== 3) المدير المالي يحدّث المراجعة المالية ⇒ 200 =====
    [Fact]
    public async Task FinanceManager_PayrollImpact_Review_200()
    {
        var org = await BuildLeaveOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 1, 12), new(2026, 1, 14));
        var (client, _) = await FinanceManagerAsync();
        Assert.Equal(HttpStatusCode.OK, (await ReviewAsync(client, id)).StatusCode);
    }

    // ===== 4) الحسابات يحدّث المراجعة المالية ⇒ 200 =====
    [Fact]
    public async Task Accountant_PayrollImpact_Review_200()
    {
        var org = await BuildLeaveOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 2, 12), new(2026, 2, 14));
        var (client, _) = await AccountantAsync();
        Assert.Equal(HttpStatusCode.OK, (await ReviewAsync(client, id)).StatusCode);
    }

    // ===== 5) الموظّف يبقى ممنوعًا من FIN-L1 (قراءةً ومراجعةً) ⇒ 403 =====
    [Fact]
    public async Task Employee_PayrollImpact_StillForbidden_403()
    {
        var org = await BuildLeaveOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 3, 12), new(2026, 3, 14));
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync(PayrollListUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ReviewAsync(emp, id)).StatusCode);
    }

    // ===== 6) قائد الفريق يبقى ممنوعًا من FIN-L1 (قراءةً ومراجعةً) ⇒ 403 =====
    [Fact]
    public async Task TeamLeader_PayrollImpact_StillForbidden_403()
    {
        var org = await BuildLeaveOrgAsync();
        var id = await CreateApprovedImpactedLeaveAsync(org, new(2026, 4, 12), new(2026, 4, 14));
        var (tl, _) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        Assert.Equal(HttpStatusCode.Forbidden, (await tl.GetAsync(PayrollListUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ReviewAsync(tl, id)).StatusCode);
    }

    // ========== KPI-FIN1: تصدير KPI للمالية ==========

    // ===== 7) المدير المالي يعاين تصدير KPI ⇒ 200 =====
    [Fact]
    public async Task FinanceManager_KpiFinance_Preview_200()
    {
        var (client, _) = await FinanceManagerAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{KpiPreviewUrl}?year=2026&quarter=2")).StatusCode);
    }

    // ===== 8) الحسابات يعاين تصدير KPI ⇒ 200 =====
    [Fact]
    public async Task Accountant_KpiFinance_Preview_200()
    {
        var (client, _) = await AccountantAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{KpiPreviewUrl}?year=2026&quarter=2")).StatusCode);
    }

    // ===== 9) المدير المالي يصدّر CSV ⇒ 200 =====
    [Fact]
    public async Task FinanceManager_KpiFinance_Csv_200()
    {
        var (client, _) = await FinanceManagerAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{KpiCsvUrl}?year=2026&quarter=2")).StatusCode);
    }

    // ===== 10) الحسابات يصدّر CSV ⇒ 200 =====
    [Fact]
    public async Task Accountant_KpiFinance_Csv_200()
    {
        var (client, _) = await AccountantAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{KpiCsvUrl}?year=2026&quarter=2")).StatusCode);
    }

    // ===== 11) الموظّف يبقى ممنوعًا من تصدير KPI (معاينةً وتصديرًا) ⇒ 403 =====
    [Fact]
    public async Task Employee_KpiFinance_StillForbidden_403()
    {
        var (emp, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync($"{KpiPreviewUrl}?year=2026&quarter=2")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync($"{KpiCsvUrl}?year=2026&quarter=2")).StatusCode);
    }

    // ===== 12) قائد الفريق يبقى ممنوعًا من تصدير KPI (معاينةً وتصديرًا) ⇒ 403 =====
    [Fact]
    public async Task TeamLeader_KpiFinance_StillForbidden_403()
    {
        var (tl, _) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        Assert.Equal(HttpStatusCode.Forbidden, (await tl.GetAsync($"{KpiPreviewUrl}?year=2026&quarter=2")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tl.GetAsync($"{KpiCsvUrl}?year=2026&quarter=2")).StatusCode);
    }
}
