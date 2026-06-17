using System.Net;
using Reporting.Application.Common;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class DashboardTests
{
    private readonly CustomWebApplicationFactory _factory;

    public DashboardTests(CustomWebApplicationFactory factory) => _factory = factory;

    private record ScopeDto(string Type, List<Guid> Ids);
    private record UserDto(Guid Id, string Name, string Role);
    private record CardDto(string Key, string Title, decimal? Value, string Status, string? DrilldownKey);
    private record DashDto(
        string DashboardType,
        UserDto User,
        ScopeDto Scope,
        List<string> Permissions,
        List<CardDto> SummaryCards);

    [Fact]
    public async Task Anonymous_GetDashboard_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/dashboard/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Admin_Gets_Governance_Dashboard()
    {
        var client = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await client.GetAsync("/api/dashboard/me");
        res.EnsureSuccessStatusCode();
        var dto = await res.ReadAsync<DashDto>();

        Assert.NotNull(dto);
        Assert.Equal("AdminGovernance", dto!.DashboardType);
        Assert.Equal("governance", dto.Scope.Type);
        Assert.Contains("ViewGovernance", dto.Permissions);
        Assert.Contains("ManageUsers", dto.Permissions);
        Assert.NotEmpty(dto.SummaryCards);
    }

    [Fact]
    public async Task Employee_Gets_Own_Scope_Dashboard()
    {
        var (client, userId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await client.GetAsync("/api/dashboard/me");
        res.EnsureSuccessStatusCode();
        var dto = await res.ReadAsync<DashDto>();

        Assert.NotNull(dto);
        Assert.Equal("Employee", dto!.DashboardType);
        Assert.Equal("own", dto.Scope.Type);
        // own scope contains only self — no leakage of other users.
        Assert.Single(dto.Scope.Ids);
        Assert.Equal(userId, dto.Scope.Ids[0]);
        Assert.DoesNotContain("ViewGovernance", dto.Permissions);
        Assert.DoesNotContain("ApproveReports", dto.Permissions);
    }

    [Fact]
    public async Task TeamLeader_Scope_Includes_Direct_Reports_Only()
    {
        var (leaderClient, leaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, reportId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: leaderId);
        var (_, outsiderId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await leaderClient.GetAsync("/api/dashboard/me");
        res.EnsureSuccessStatusCode();
        var dto = await res.ReadAsync<DashDto>();

        Assert.NotNull(dto);
        Assert.Equal("TeamLeader", dto!.DashboardType);
        Assert.Equal("team", dto.Scope.Type);
        Assert.Contains(leaderId, dto.Scope.Ids);
        Assert.Contains(reportId, dto.Scope.Ids);
        Assert.DoesNotContain(outsiderId, dto.Scope.Ids);
        Assert.Contains("ApproveReports", dto.Permissions);
    }

    [Fact]
    public async Task Manager_Scope_Includes_Indirect_Reports()
    {
        var (mgrClient, mgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, leaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, managerId: mgrId);
        var (_, deepId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: leaderId);

        var res = await mgrClient.GetAsync("/api/dashboard/me");
        res.EnsureSuccessStatusCode();
        var dto = await res.ReadAsync<DashDto>();

        Assert.NotNull(dto);
        Assert.Equal("Manager", dto!.DashboardType);
        Assert.Equal("department", dto.Scope.Type);
        Assert.Contains(mgrId, dto.Scope.Ids);
        Assert.Contains(leaderId, dto.Scope.Ids);
        Assert.Contains(deepId, dto.Scope.Ids);
        Assert.Contains("ViewAnalytics", dto.Permissions);
    }

    // ===== نقاط الـDrill-down =====

    private record TrendPointDto(string PeriodKey, decimal Score);
    private record TrendDto(Guid SubjectId, string SubjectName, List<TrendPointDto> Points);
    private record MemberPerfDto(Guid UserId, string Name, decimal? KpiAverage, string KpiTrend, int ReportsTotal, int ReportsCompleted);

    [Fact]
    public async Task Anonymous_Drilldown_401()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/kpi-trends")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/members-performance")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/recent-activity")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/pending-reports")).StatusCode);
    }

    [Fact]
    public async Task KpiTrends_Defaults_To_Self()
    {
        var (client, userId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await client.GetAsync("/api/dashboard/kpi-trends");
        res.EnsureSuccessStatusCode();
        var dto = await res.ReadAsync<TrendDto>();
        Assert.NotNull(dto);
        Assert.Equal(userId, dto!.SubjectId);
    }

    [Fact]
    public async Task KpiTrends_Outside_Scope_Forbidden()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, outsiderId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        // موظف لا يرى اتجاه شخص آخر (IDOR/BOLA).
        var res = await client.GetAsync($"/api/dashboard/kpi-trends?subjectId={outsiderId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task MembersPerformance_TeamLeader_DirectReports_Only()
    {
        var (leaderClient, leaderId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, reportId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: leaderId);
        var (_, outsiderId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await leaderClient.GetAsync("/api/dashboard/members-performance");
        res.EnsureSuccessStatusCode();
        var members = await res.ReadAsync<List<MemberPerfDto>>();

        Assert.NotNull(members);
        var ids = members!.Select(m => m.UserId).ToList();
        Assert.Contains(leaderId, ids);
        Assert.Contains(reportId, ids);
        Assert.DoesNotContain(outsiderId, ids);
    }

    [Fact]
    public async Task RecentActivity_And_PendingReports_Return_Ok()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        (await client.GetAsync("/api/dashboard/recent-activity")).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/dashboard/pending-reports")).EnsureSuccessStatusCode();
    }
}
