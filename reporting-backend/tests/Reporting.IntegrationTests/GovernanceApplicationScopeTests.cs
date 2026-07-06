using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// نطاق التطبيق لبنود الحوكمة (GOV-APPLICATION-SCOPE-R1): «يُطبَّق على من؟» — مستقلّ عن «المُسنَد إليه».
/// 5 قيم: Company (لأصحاب الرؤية الواسعة فقط)، Department، Team، User، RelatedReport.
/// تحقّق صارم لكل نطاق + منع التعارض + احترام نطاق المستخدم لغير الواسع. لا تمسّ Action Items/Escalations.
/// </summary>
[Collection("Integration")]
public class GovernanceApplicationScopeTests
{
    private readonly CustomWebApplicationFactory _factory;

    public GovernanceApplicationScopeTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static CreateGovernanceItemRequest Item(
        GovernanceApplicationScope scope,
        Guid? assignedToUserId = null,
        Guid? relatedUserId = null,
        Guid? departmentId = null,
        Guid? teamId = null,
        Guid? relatedSubmissionId = null,
        string title = "بند نطاق التطبيق")
        => new(title + "-" + Guid.NewGuid().ToString("N")[..8], "وصف", GovernanceCategory.Observation,
            GovernanceSeverity.Medium, scope, assignedToUserId, departmentId, teamId, relatedSubmissionId, relatedUserId, null);

    private static Task<HttpResponseMessage> CreateAsync(HttpClient c, CreateGovernanceItemRequest req)
        => c.PostAsJsonAsync("/api/governance/items", req, TestJson.Options);

    // (1) CEO ينشئ بندًا بنطاق «كل الشركة» ⇒ 200.
    [Fact]
    public async Task Ceo_CreatesCompanyScoped_200()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var res = await CreateAsync(ceo, Item(GovernanceApplicationScope.Company));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<GovernanceItemDetailDto>();
        Assert.Equal(GovernanceApplicationScope.Company, dto!.Item.ApplicationScope);
    }

    // (2) Manager لا يستطيع إنشاء نطاق «كل الشركة» ⇒ 403.
    [Fact]
    public async Task Manager_CannotCreateCompanyScoped_403()
    {
        var (mgr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var res = await CreateAsync(mgr, Item(GovernanceApplicationScope.Company));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (3) TeamLeader لا يستطيع إنشاء نطاق «كل الشركة» ⇒ 403.
    [Fact]
    public async Task TeamLeader_CannotCreateCompanyScoped_403()
    {
        var (tl, _) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var res = await CreateAsync(tl, Item(GovernanceApplicationScope.Company));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (4) نطاق «كل الشركة» لا يتطلّب أيّ مرجع سياقي.
    [Fact]
    public async Task CompanyScope_RequiresNoReferences()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await CreateAsync(admin, Item(GovernanceApplicationScope.Company));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<GovernanceItemDetailDto>();
        Assert.Null(dto!.Item.DepartmentId);
        Assert.Null(dto.Item.TeamId);
        Assert.Null(dto.Item.RelatedUserId);
        Assert.Null(dto.Item.RelatedSubmissionId);
    }

    // (5) نطاق «إدارة محددة» يتطلّب DepartmentId ⇒ 400 عند غيابه.
    [Fact]
    public async Task DepartmentScope_WithoutDepartmentId_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await CreateAsync(admin, Item(GovernanceApplicationScope.Department));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // (6) نطاق «فريق محدد» يتطلّب TeamId ⇒ 400 عند غيابه.
    [Fact]
    public async Task TeamScope_WithoutTeamId_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await CreateAsync(admin, Item(GovernanceApplicationScope.Team));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // (7) نطاق «موظّف محدد» يتطلّب RelatedUserId ⇒ 400 عند غيابه.
    [Fact]
    public async Task UserScope_WithoutRelatedUserId_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await CreateAsync(admin, Item(GovernanceApplicationScope.User));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // (8) نطاق «تقرير مرتبط» يتطلّب RelatedSubmissionId ⇒ 400 عند غيابه.
    [Fact]
    public async Task RelatedReportScope_WithoutSubmissionId_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await CreateAsync(admin, Item(GovernanceApplicationScope.RelatedReport));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // (9) تعارض النطاق مع المرجع ⇒ 400 (Company + DepartmentId صالح).
    [Fact]
    public async Task ScopeMismatch_CompanyWithDepartment_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var deptId = await TestAuth.CreateDepartmentWithUsersAsync(_factory);
        var res = await CreateAsync(admin, Item(GovernanceApplicationScope.Company, departmentId: deptId));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // (10) القائمة والتفاصيل تعملان ويظهر فيهما نطاق التطبيق.
    [Fact]
    public async Task ListAndDetails_ExposeApplicationScope()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await CreateAsync(admin, Item(GovernanceApplicationScope.Company, title: "بند-عرض-النطاق"))).ReadAsync<GovernanceItemDetailDto>();
        var id = created!.Item.Id;

        var detail = await (await admin.GetAsync($"/api/governance/items/{id}")).ReadAsync<GovernanceItemDetailDto>();
        Assert.Equal(GovernanceApplicationScope.Company, detail!.Item.ApplicationScope);

        var list = await (await admin.GetAsync("/api/governance/items")).ReadAsync<List<GovernanceItemListItemDto>>();
        var listed = Assert.Single(list!, i => i.Id == id);
        Assert.Equal(GovernanceApplicationScope.Company, listed.ApplicationScope);
    }

    // (11) Manager لا يستطيع استهداف موظّف خارج نطاقه ⇒ 403.
    [Fact]
    public async Task Manager_CannotTargetUserOutsideScope_403()
    {
        var (mgr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, outsiderId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await CreateAsync(mgr, Item(GovernanceApplicationScope.User, relatedUserId: outsiderId));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (12) TeamLeader يستطيع إنشاء بند بنطاق فريقه ⇒ 200.
    [Fact]
    public async Task TeamLeader_CreatesTeamScopedForOwnTeam_200()
    {
        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        // الفريق بقيادة قائد الفريق وعضويّته فيه أيضًا (كي يظهر ضمن دليله).
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, tlId);

        var res = await CreateAsync(tl, Item(GovernanceApplicationScope.Team, teamId: teamId));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<GovernanceItemDetailDto>();
        Assert.Equal(GovernanceApplicationScope.Team, dto!.Item.ApplicationScope);
        Assert.Equal(teamId, dto.Item.TeamId);
    }

    // (13) CEO يُسنِد بندًا بنطاق «كل الشركة» إلى GM (المُسنَد إليه مستقلّ عن النطاق) ⇒ 200.
    [Fact]
    public async Task Ceo_AssignsCompanyScopedToGm_200()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);

        var res = await CreateAsync(ceo, Item(GovernanceApplicationScope.Company, assignedToUserId: gmId));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<GovernanceItemDetailDto>();
        Assert.Equal(GovernanceApplicationScope.Company, dto!.Item.ApplicationScope);
        Assert.Equal(gmId, dto.Item.AssignedToUserId);
    }

    // (14) البنود القديمة (بُذِرت مباشرةً) تُعرَض بأمان مع قيمة نطاق محفوظة.
    [Fact]
    public async Task ExistingItemWithSeededData_RendersSafely()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        Guid id;
        Guid adminId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            adminId = await db.Users.Where(u => u.Email == "admin@marketingexperts.local").Select(u => u.Id).FirstAsync();
            var entity = new GovernanceItem
            {
                Title = "بند قديم-" + Guid.NewGuid().ToString("N")[..8],
                Category = GovernanceCategory.Observation,
                Severity = GovernanceSeverity.Low,
                Status = GovernanceItemStatus.Open,
                ApplicationScope = GovernanceApplicationScope.Company,
                CreatedById = adminId
            };
            db.Set<GovernanceItem>().Add(entity);
            await db.SaveChangesAsync();
            id = entity.Id;
        }

        var detail = await admin.GetAsync($"/api/governance/items/{id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var dto = await detail.ReadAsync<GovernanceItemDetailDto>();
        Assert.Equal(GovernanceApplicationScope.Company, dto!.Item.ApplicationScope);

        var list = await (await admin.GetAsync("/api/governance/items")).ReadAsync<List<GovernanceItemListItemDto>>();
        Assert.Contains(list!, i => i.Id == id);
    }
}
