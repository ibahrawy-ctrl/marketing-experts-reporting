using System.Net;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// GOV-DIRECTORY-SCOPE-FIX-R1 — يتحقّق من توحيد منطق الدليل/النطاق عبر الموديولات الأربعة (ورشة الحوكمة، البنود،
/// الإجراءات، التصعيدات) عبر المصدر الموحّد <see cref="GovernanceDirectoryService"/>.
/// القواعد: صاحب الرؤية الواسعة (Admin/CEO/GM/CeoSupport) يرى الجميع شاملًا الحسّاسين؛ Manager/TeamLeader محصورون بنطاقهم
/// بلا تسريب الحسّاسين؛ HR يرى موظّفي إدارته بلا الحسّاسين؛ Employee/Viewer ممنوعون من دليل الورشة (403).
/// هدف التصعيد لغير الواسع = كل النشطين عدا الحسّاسين. القوائم لا تكون «بدون» دون سبب: نطاق القائد يشمل نفسه ومرؤوسيه.
/// </summary>
[Collection("Integration")]
public class GovernanceDirectoryScopeTests
{
    private const string WorkspaceDir = "/api/governance/items/directory";
    private const string AssigneeDir = "/api/governance/action-items/assignee-directory";
    private const string TargetDir = "/api/governance/escalations/target-directory";

    private readonly CustomWebApplicationFactory _factory;

    public GovernanceDirectoryScopeTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== 1. السياسة (المصادقة/التفويض) =====

    [Fact]
    public async Task WorkspaceDirectory_Anonymous_401()
    {
        var res = await _factory.CreateClient().GetAsync(WorkspaceDir);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task WorkspaceDirectory_Employee_403()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await client.GetAsync(WorkspaceDir);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task WorkspaceDirectory_Viewer_403()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Viewer);
        var res = await client.GetAsync(WorkspaceDir);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 2. الرؤية الواسعة: تشمل الحسّاسين =====

    [Fact]
    public async Task WorkspaceDirectory_Admin_IncludesSensitiveAndRegular()
    {
        var (regularClient, regularId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        _ = regularClient;
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, adminUserId) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);

        var res = await admin.GetAsync(WorkspaceDir);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dir = await res.ReadAsync<GovernanceDirectoryDto>();
        Assert.NotNull(dir);
        Assert.Contains(dir!.Users, u => u.Id == regularId);
        Assert.Contains(dir.Users, u => u.Id == adminUserId);
    }

    [Fact]
    public async Task WorkspaceDirectory_Ceo_IncludesSensitive()
    {
        var (ceoClient, _) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var (_, sensitiveId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);

        var res = await ceoClient.GetAsync(WorkspaceDir);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dir = await res.ReadAsync<GovernanceDirectoryDto>();
        Assert.NotNull(dir);
        Assert.Contains(dir!.Users, u => u.Id == sensitiveId);
    }

    [Fact]
    public async Task WorkspaceDirectory_GeneralManager_IncludesSensitive()
    {
        var (gmClient, _) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);

        var res = await gmClient.GetAsync(WorkspaceDir);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dir = await res.ReadAsync<GovernanceDirectoryDto>();
        Assert.NotNull(dir);
        Assert.Contains(dir!.Users, u => u.Id == ceoId);
    }

    [Fact]
    public async Task WorkspaceDirectory_CeoSupport_IncludesSensitive()
    {
        var (csClient, _) = await TestAuth.CreateUserAsync(_factory, Roles.CeoSupport);
        var (_, adminId) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);

        var res = await csClient.GetAsync(WorkspaceDir);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dir = await res.ReadAsync<GovernanceDirectoryDto>();
        Assert.NotNull(dir);
        Assert.Contains(dir!.Users, u => u.Id == adminId);
    }

    // ===== 3. النطاق المحصور: Manager =====

    [Fact]
    public async Task WorkspaceDirectory_Manager_ScopedExcludesStrangerAndSensitive()
    {
        var (managerClient, managerId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, reportId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId);
        var (_, strangerId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, sensitiveId) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);

        var res = await managerClient.GetAsync(WorkspaceDir);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dir = await res.ReadAsync<GovernanceDirectoryDto>();
        Assert.NotNull(dir);
        Assert.Contains(dir!.Users, u => u.Id == reportId);
        Assert.DoesNotContain(dir.Users, u => u.Id == strangerId);
        Assert.DoesNotContain(dir.Users, u => u.Id == sensitiveId);
    }

    // ===== 4. القائد: يشمل نفسه ومرؤوسيه (ليست «بدون») =====

    [Fact]
    public async Task WorkspaceDirectory_TeamLeader_IncludesSelfAndReport_NotEmpty()
    {
        var (tlClient, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, reportId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tlId);

        var res = await tlClient.GetAsync(WorkspaceDir);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dir = await res.ReadAsync<GovernanceDirectoryDto>();
        Assert.NotNull(dir);
        Assert.NotEmpty(dir!.Users);
        Assert.Contains(dir.Users, u => u.Id == tlId);
        Assert.Contains(dir.Users, u => u.Id == reportId);
    }

    // ===== 5. HR: موظّفو إدارته بلا الحسّاسين =====

    [Fact]
    public async Task WorkspaceDirectory_Hr_OwnDepartmentExcludesSensitive()
    {
        var (hrClient, hrId) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, deptMemberId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, sensitiveId) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        await TestAuth.CreateDepartmentWithUsersAsync(_factory, hrId, deptMemberId);

        var res = await hrClient.GetAsync(WorkspaceDir);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dir = await res.ReadAsync<GovernanceDirectoryDto>();
        Assert.NotNull(dir);
        Assert.Contains(dir!.Users, u => u.Id == deptMemberId);
        Assert.DoesNotContain(dir.Users, u => u.Id == sensitiveId);
    }

    // ===== 6. دليل المُسنَد إليه (الإجراءات): Manager محصور =====

    [Fact]
    public async Task AssigneeDirectory_Manager_ScopedExcludesStranger()
    {
        var (managerClient, managerId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, reportId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId);
        var (_, strangerId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await managerClient.GetAsync(AssigneeDir);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dir = await res.ReadAsync<ActionItemAssigneeDirectoryDto>();
        Assert.NotNull(dir);
        Assert.Contains(dir!.Users, u => u.Id == reportId);
        Assert.DoesNotContain(dir.Users, u => u.Id == strangerId);
    }

    // ===== 7. دليل هدف التصعيد: الرؤية الواسعة تشمل الحسّاسين =====

    [Fact]
    public async Task TargetDirectory_WideViewer_IncludesSensitive()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, sensitiveId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);

        var res = await admin.GetAsync(TargetDir);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dir = await res.ReadAsync<EscalationTargetDirectoryDto>();
        Assert.NotNull(dir);
        Assert.Contains(dir!.Users, u => u.Id == sensitiveId);
    }

    // ===== 8. اتّساق النطاق: الورشة والإجراءات نفس مجموعة المستخدمين للمدير =====

    [Fact]
    public async Task WorkspaceAndAssignee_Manager_SameUserScope()
    {
        var (managerClient, managerId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, reportId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId);

        var wsRes = await managerClient.GetAsync(WorkspaceDir);
        var asRes = await managerClient.GetAsync(AssigneeDir);
        Assert.Equal(HttpStatusCode.OK, wsRes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asRes.StatusCode);

        var ws = await wsRes.ReadAsync<GovernanceDirectoryDto>();
        var asg = await asRes.ReadAsync<ActionItemAssigneeDirectoryDto>();
        Assert.NotNull(ws);
        Assert.NotNull(asg);

        var wsIds = ws!.Users.Select(u => u.Id).OrderBy(x => x).ToArray();
        var asgIds = asg!.Users.Select(u => u.Id).OrderBy(x => x).ToArray();
        Assert.Equal(wsIds, asgIds);
        Assert.Contains(reportId, wsIds);
        Assert.Contains(reportId, asgIds);
    }
}
