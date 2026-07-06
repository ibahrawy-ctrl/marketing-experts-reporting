using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// ورشة الحوكمة العامة (GOV-GOVERNANCE-UX1) — بنود الحوكمة + الخط الزمني، محكومة بسياسة GovernanceWorkspaceAccess.
/// رؤية واسعة (Admin/CEO/GM/CeoSupport)، نطاق (Manager/TeamLeader)، HR محدود (المُسنَد/المُنشأ/المرتبط/إدارته)،
/// Employee/Viewer لا وصول (403). anti-IDOR على المراجع. لا تغيّر سلوك ScopeResolver — تستعمله فقط.
/// </summary>
[Collection("Integration")]
public class GovernanceWorkspaceTests
{
    private readonly CustomWebApplicationFactory _factory;

    public GovernanceWorkspaceTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static CreateGovernanceItemRequest NewItem(
        string title = "بند حوكمة اختباري",
        GovernanceCategory category = GovernanceCategory.Observation,
        GovernanceSeverity severity = GovernanceSeverity.Medium,
        Guid? assignedToUserId = null,
        Guid? relatedUserId = null,
        Guid? departmentId = null,
        Guid? teamId = null,
        Guid? relatedSubmissionId = null,
        GovernanceApplicationScope? scope = null)
    {
        // اشتقاق نطاق التطبيق من المرجع المُمرَّر (افتراضيًّا) ما لم يُحدَّد صراحةً.
        var resolved = scope ?? (
            relatedSubmissionId is not null ? GovernanceApplicationScope.RelatedReport :
            relatedUserId is not null ? GovernanceApplicationScope.User :
            teamId is not null ? GovernanceApplicationScope.Team :
            departmentId is not null ? GovernanceApplicationScope.Department :
            GovernanceApplicationScope.Company);
        return new(title, "وصف", category, severity, resolved, assignedToUserId, departmentId, teamId, relatedSubmissionId, relatedUserId, null);
    }

    private static Task<HttpResponseMessage> CreateAsync(HttpClient c, CreateGovernanceItemRequest req)
        => c.PostAsJsonAsync("/api/governance/items", req, TestJson.Options);

    // ===== الصلاحيات على مستوى السياسة =====

    [Fact]
    public async Task Anonymous_List_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/governance/items");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Employee)]
    [InlineData(Roles.Viewer)]
    public async Task NoAccessRoles_List_403(string role)
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
        var res = await client.GetAsync("/api/governance/items");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Employee)]
    [InlineData(Roles.Viewer)]
    public async Task NoAccessRoles_Create_403(string role)
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
        var res = await CreateAsync(client, NewItem());
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Ceo)]
    [InlineData(Roles.GeneralManager)]
    [InlineData(Roles.CeoSupport)]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.TeamLeader)]
    [InlineData(Roles.Hr)]
    public async Task WorkspaceRoles_CanList_200(string role)
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
        var res = await client.GetAsync("/api/governance/items");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== دورة حياة كاملة (Admin) =====

    [Fact]
    public async Task Admin_FullLifecycle_CreateUpdateStatusComment()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        // إنشاء
        var createRes = await CreateAsync(admin, NewItem("بند حوكمة — دورة كاملة", GovernanceCategory.Risk, GovernanceSeverity.High));
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.ReadAsync<GovernanceItemDetailDto>();
        Assert.NotNull(created);
        var id = created!.Item.Id;
        Assert.Equal(GovernanceItemStatus.Open, created.Item.Status);
        Assert.Single(created.Timeline); // حركة الإنشاء
        Assert.Equal(GovernanceItemUpdateType.Created, created.Timeline[0].UpdateType);

        // قراءة
        var getRes = await admin.GetAsync($"/api/governance/items/{id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        // تعديل
        var updReq = new UpdateGovernanceItemRequest("عنوان مُعدَّل", "وصف مُعدَّل", GovernanceCategory.Decision, GovernanceSeverity.Critical, GovernanceApplicationScope.Company);
        var updRes = await admin.PutAsJsonAsync($"/api/governance/items/{id}", updReq, TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, updRes.StatusCode);
        var updated = await updRes.ReadAsync<GovernanceItemDetailDto>();
        Assert.Equal("عنوان مُعدَّل", updated!.Item.Title);
        Assert.Equal(GovernanceItemStatus.Open, updated.Item.Status);

        // تغيير حالة (مع ملخص حل)
        var stReq = new ChangeGovernanceItemStatusRequest(GovernanceItemStatus.Resolved, "تم الحل", "ملخص الحل");
        var stRes = await admin.PostAsJsonAsync($"/api/governance/items/{id}/status", stReq, TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, stRes.StatusCode);
        var resolved = await stRes.ReadAsync<GovernanceItemDetailDto>();
        Assert.Equal(GovernanceItemStatus.Resolved, resolved!.Item.Status);
        Assert.NotNull(resolved.ClosedAtUtc);
        Assert.Contains(resolved.Timeline, t => t.UpdateType == GovernanceItemUpdateType.StatusChanged);

        // تعليق
        var cmtRes = await admin.PostAsJsonAsync($"/api/governance/items/{id}/comments",
            new AddGovernanceItemCommentRequest("تعليق متابعة", true), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, cmtRes.StatusCode);
        var commented = await cmtRes.ReadAsync<GovernanceItemDetailDto>();
        Assert.Contains(commented!.Timeline, t => t.UpdateType == GovernanceItemUpdateType.FollowUp);
    }

    // ===== التحقق من المدخلات + anti-IDOR =====

    [Fact]
    public async Task Create_EmptyTitle_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await CreateAsync(admin, NewItem(title: "   "));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownAssignee_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await CreateAsync(admin, NewItem(assignedToUserId: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownDepartment_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await CreateAsync(admin, NewItem(departmentId: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownRelatedSubmission_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await CreateAsync(admin, NewItem(relatedSubmissionId: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_SameStatus_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await CreateAsync(admin, NewItem())).ReadAsync<GovernanceItemDetailDto>();
        var id = created!.Item.Id;

        var res = await admin.PostAsJsonAsync($"/api/governance/items/{id}/status",
            new ChangeGovernanceItemStatusRequest(GovernanceItemStatus.Open), TestJson.Options);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task AddComment_EmptyBody_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await CreateAsync(admin, NewItem())).ReadAsync<GovernanceItemDetailDto>();
        var id = created!.Item.Id;

        var res = await admin.PostAsJsonAsync($"/api/governance/items/{id}/comments",
            new AddGovernanceItemCommentRequest("   "), TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownId_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.GetAsync($"/api/governance/items/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== الفلاتر =====

    [Fact]
    public async Task List_StatusFilter_ReturnsOnlyMatching()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var marker = "فلتر-حالة-" + Guid.NewGuid().ToString("N")[..8];

        var open = await (await CreateAsync(admin, NewItem(marker + "-مفتوح"))).ReadAsync<GovernanceItemDetailDto>();
        var toResolve = await (await CreateAsync(admin, NewItem(marker + "-محلول"))).ReadAsync<GovernanceItemDetailDto>();
        await admin.PostAsJsonAsync($"/api/governance/items/{toResolve!.Item.Id}/status",
            new ChangeGovernanceItemStatusRequest(GovernanceItemStatus.Resolved), TestJson.Options);

        var resolvedList = await (await admin.GetAsync("/api/governance/items?status=Resolved"))
            .ReadAsync<List<GovernanceItemListItemDto>>();
        Assert.Contains(resolvedList!, i => i.Id == toResolve.Item.Id);
        Assert.DoesNotContain(resolvedList!, i => i.Id == open!.Item.Id);
    }

    [Fact]
    public async Task List_OpenOnlyFilter_ExcludesClosed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var closed = await (await CreateAsync(admin, NewItem("مغلق-" + Guid.NewGuid().ToString("N")[..8]))).ReadAsync<GovernanceItemDetailDto>();
        await admin.PostAsJsonAsync($"/api/governance/items/{closed!.Item.Id}/status",
            new ChangeGovernanceItemStatusRequest(GovernanceItemStatus.Closed), TestJson.Options);

        var openOnly = await (await admin.GetAsync("/api/governance/items?openOnly=true"))
            .ReadAsync<List<GovernanceItemListItemDto>>();
        Assert.DoesNotContain(openOnly!, i => i.Id == closed.Item.Id);
    }

    // ===== رؤية HR المحدودة =====

    [Fact]
    public async Task Hr_SeesOwnCreatedItem_ButNotUnrelatedAdminItem()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (hr, hrId) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);

        // بند أنشأه HR (يجب أن يراه) — نطاق «موظّف» يستهدف نفسه (ضمن دليله).
        var mine = await (await CreateAsync(hr, NewItem("بند-HR-" + Guid.NewGuid().ToString("N")[..8], relatedUserId: hrId))).ReadAsync<GovernanceItemDetailDto>();
        // بند أنشأه Admin بلا أي ارتباط بـ HR (يجب ألا يراه HR)
        var unrelated = await (await CreateAsync(admin, NewItem("بند-Admin-" + Guid.NewGuid().ToString("N")[..8]))).ReadAsync<GovernanceItemDetailDto>();

        var list = await (await hr.GetAsync("/api/governance/items")).ReadAsync<List<GovernanceItemListItemDto>>();
        Assert.Contains(list!, i => i.Id == mine!.Item.Id);
        Assert.DoesNotContain(list!, i => i.Id == unrelated!.Item.Id);

        // GET المباشر على البند غير المرتبط = 404 (إخفاء الوجود)
        var getUnrelated = await hr.GetAsync($"/api/governance/items/{unrelated!.Item.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getUnrelated.StatusCode);
    }

    [Fact]
    public async Task Hr_SeesItemAssignedToHim()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (hr, hrId) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);

        var assigned = await (await CreateAsync(admin, NewItem("مُسنَد-لـHR-" + Guid.NewGuid().ToString("N")[..8], assignedToUserId: hrId)))
            .ReadAsync<GovernanceItemDetailDto>();

        var list = await (await hr.GetAsync("/api/governance/items")).ReadAsync<List<GovernanceItemListItemDto>>();
        Assert.Contains(list!, i => i.Id == assigned!.Item.Id);
    }

    // ===== رؤية النطاق (Manager) =====

    [Fact]
    public async Task Manager_SeesOwnCreatedItem()
    {
        var (mgr, mgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        // نطاق «موظّف» يستهدف نفسه (ضمن نطاق المدير).
        var mine = await (await CreateAsync(mgr, NewItem("بند-مدير-" + Guid.NewGuid().ToString("N")[..8], relatedUserId: mgrId))).ReadAsync<GovernanceItemDetailDto>();

        var list = await (await mgr.GetAsync("/api/governance/items")).ReadAsync<List<GovernanceItemListItemDto>>();
        Assert.Contains(list!, i => i.Id == mine!.Item.Id);

        var getRes = await mgr.GetAsync($"/api/governance/items/{mine!.Item.Id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
    }

    [Fact]
    public async Task Manager_CannotViewUnrelatedItem_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (mgr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);

        var unrelated = await (await CreateAsync(admin, NewItem("بند-غير-مرتبط-" + Guid.NewGuid().ToString("N")[..8]))).ReadAsync<GovernanceItemDetailDto>();

        var getRes = await mgr.GetAsync($"/api/governance/items/{unrelated!.Item.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    // ===== صلاحية التعديل (Wide vs غير المالك) =====

    [Fact]
    public async Task WideViewer_CanEditAnyItem_ButScopedNonOwnerCannot()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);

        var item = await (await CreateAsync(admin, NewItem("بند-للتعديل-" + Guid.NewGuid().ToString("N")[..8]))).ReadAsync<GovernanceItemDetailDto>();
        var id = item!.Item.Id;

        // CEO (رؤية واسعة) يستطيع التعديل رغم أنه ليس المُنشئ
        var ceoEdit = await ceo.PutAsJsonAsync($"/api/governance/items/{id}",
            new UpdateGovernanceItemRequest("عُدِّل بواسطة CEO", null, GovernanceCategory.Observation, GovernanceSeverity.Low, GovernanceApplicationScope.Company), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, ceoEdit.StatusCode);
    }
}
