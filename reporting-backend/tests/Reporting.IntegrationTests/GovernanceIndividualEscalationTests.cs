using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1) — كيان مستقلّ تمامًا عن بنود الحوكمة العامة وعن سير اعتماد التقارير.
/// محكوم بسياسة GovernanceEscalationAccess (Viewer مستثنى). الرؤية: واسعة (Admin/CEO/GM/CeoSupport)،
/// نطاق (Manager/TeamLeader)، HR محدود، Employee إنشاء + رؤية ما يخصّه فقط. القراءة غير المصرّح بها تُقنَّع 404 لا 403.
/// أعلام الصلاحية الدقيقة تُفرَض داخل الخدمة. لا يمسّ ScopeResolver/Workflow/CurrentApproverId — يقرأ النطاق فقط.
/// </summary>
[Collection("Integration")]
public class GovernanceIndividualEscalationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public GovernanceIndividualEscalationTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static CreateGovernanceEscalationRequest NewEscalation(
        string title = "تصعيد فردي اختباري",
        EscalationType type = EscalationType.Performance,
        EscalationSeverity severity = EscalationSeverity.Medium,
        EscalationTargetType targetType = EscalationTargetType.Other,
        Guid? targetUserId = null,
        Guid? targetDepartmentId = null,
        Guid? targetTeamId = null,
        Guid? relatedSubmissionId = null,
        Guid? relatedGovernanceItemId = null)
        => new(title, "وصف اختباري", type, severity, targetType,
            targetUserId, targetDepartmentId, targetTeamId, relatedSubmissionId, relatedGovernanceItemId);

    private static Task<HttpResponseMessage> CreateAsync(HttpClient c, CreateGovernanceEscalationRequest req)
        => c.PostAsJsonAsync("/api/governance/escalations", req, TestJson.Options);

    // ===== الصلاحيات على مستوى السياسة =====

    [Fact]
    public async Task Anonymous_List_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/governance/escalations");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_Create_401()
    {
        var res = await CreateAsync(_factory.CreateClient(), NewEscalation());
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Viewer_List_403()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Viewer);
        var res = await client.GetAsync("/api/governance/escalations");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Viewer_Create_403()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Viewer);
        var res = await CreateAsync(client, NewEscalation());
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
    [InlineData(Roles.Employee)]
    public async Task EscalationUsers_CanList_200(string role)
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
        var res = await client.GetAsync("/api/governance/escalations");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== الإنشاء =====

    [Fact]
    public async Task Employee_Create_ReturnsOpen_AndRaisedBySelf()
    {
        var (client, uid) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await CreateAsync(client, NewEscalation("تصعيد موظّف"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var detail = await res.ReadAsync<GovernanceEscalationDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(GovernanceEscalationStatus.Open, detail!.Item.Status);
        Assert.Equal(uid, detail.Item.RaisedByUserId);
        // الرافع يستطيع التعديل والتعليق، لكن لا يملك الإسناد.
        Assert.True(detail.CanEdit);
        Assert.True(detail.CanComment);
        Assert.False(detail.CanAssign);
        // الخط الزمني يبدأ بحركة إنشاء واحدة.
        Assert.Single(detail.Timeline);
        Assert.Equal(EscalationUpdateType.Created, detail.Timeline[0].UpdateType);
    }

    [Fact]
    public async Task Create_TitleRequired_400()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await CreateAsync(client, NewEscalation(title: "   "));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Create_TargetUser_MissingId_400()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await CreateAsync(client, NewEscalation(targetType: EscalationTargetType.User, targetUserId: null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Create_TargetUser_OutOfScope_NonSensitive_200()
    {
        // (UAT FIX) موظّف عادي يوجّه التصعيد لموظّف آخر خارج نطاقه (رفع متقاطع) — مسموح الآن.
        var (creator, creatorId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, otherId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await CreateAsync(creator,
            NewEscalation(targetType: EscalationTargetType.User, targetUserId: otherId));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var detail = await res.ReadAsync<GovernanceEscalationDetailDto>();
        // الرفع المتقاطع لا يوسّع الرؤية: الرافع يرى تصعيده فقط لأنه رافعه.
        Assert.Equal(creatorId, detail!.Item.RaisedByUserId);
        Assert.Equal(otherId, detail.Item.TargetUserId);
    }

    [Fact]
    public async Task Create_TargetUser_SensitiveAccount_Rejected()
    {
        // موظّف عادي (رؤية غير واسعة) لا يجوز له توجيه التصعيد لحساب حسّاس (Admin) حتى بمعرّف صريح.
        var (creator, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, adminId) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var res = await CreateAsync(creator,
            NewEscalation(targetType: EscalationTargetType.User, targetUserId: adminId));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Create_TargetUser_OutOfScope_StrangerStill404()
    {
        // الرفع المتقاطع لا يوسّع رؤية الغرباء: تصعيد على هدف خارج نطاق المُصعِّد يبقى مُقنَّعًا 404 لغريب.
        var (creator, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, otherId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await (await CreateAsync(creator,
            NewEscalation("رفع متقاطع", targetType: EscalationTargetType.User, targetUserId: otherId)))
            .ReadAsync<GovernanceEscalationDetailDto>();

        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await stranger.GetAsync($"/api/governance/escalations/{created!.Item.Id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== دليل أهداف التصعيد الآمن =====

    [Fact]
    public async Task TargetDirectory_ExcludesSensitive_IncludesRegular()
    {
        // ننشئ موظّفًا عاديًا + حساب أدمن حسّاس، ثم نتحقّق أن الدليل يضمّ العادي ويستبعد الحسّاس.
        var (_, regularId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, adminId) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);

        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await client.GetAsync("/api/governance/escalations/target-directory");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dir = await res.ReadAsync<EscalationTargetDirectoryDto>();
        Assert.NotNull(dir);
        Assert.Contains(dir!.Users, u => u.Id == regularId);
        Assert.DoesNotContain(dir.Users, u => u.Id == adminId);
    }

    [Fact]
    public async Task TargetDirectory_Anonymous_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/governance/escalations/target-directory");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Admin_Create_TargetAnyUser_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, anyId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await CreateAsync(admin,
            NewEscalation(targetType: EscalationTargetType.User, targetUserId: anyId));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== تقنيع 404 + منع IDOR =====

    [Fact]
    public async Task OutOfScopeUser_GetById_404_Masked()
    {
        var (creator, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await (await CreateAsync(creator, NewEscalation("تصعيد خاص"))).ReadAsync<GovernanceEscalationDetailDto>();

        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await stranger.GetAsync($"/api/governance/escalations/{created!.Item.Id}");
        // الرؤية غير المصرّح بها تُقنَّع كـ«غير موجود» 404 لا 403.
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Employee_Assign_403()
    {
        var (creator, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await (await CreateAsync(creator, NewEscalation())).ReadAsync<GovernanceEscalationDetailDto>();
        var res = await creator.PostAsJsonAsync(
            $"/api/governance/escalations/{created!.Item.Id}/assign",
            new AssignGovernanceEscalationRequest(created.Item.RaisedByUserId), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task NonExistent_GetById_404()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var res = await client.GetAsync($"/api/governance/escalations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== دورة حياة كاملة (Admin، رؤية واسعة) =====

    [Fact]
    public async Task Admin_FullLifecycle_Assign_Status_Comment_Close_Reopen()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, assigneeId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var created = await (await CreateAsync(admin, NewEscalation("دورة حياة كاملة", EscalationType.Compliance, EscalationSeverity.High)))
            .ReadAsync<GovernanceEscalationDetailDto>();
        var id = created!.Item.Id;
        Assert.True(created.CanAssign);
        Assert.True(created.CanClose);
        Assert.True(created.CanReopen);

        // إسناد
        var assigned = await admin.PostAsJsonAsync($"/api/governance/escalations/{id}/assign",
            new AssignGovernanceEscalationRequest(assigneeId, "إسناد للمتابعة"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var assignedDto = await assigned.ReadAsync<GovernanceEscalationDetailDto>();
        Assert.Equal(assigneeId, assignedDto!.Item.AssignedToUserId);
        Assert.Equal(GovernanceEscalationStatus.Assigned, assignedDto.Item.Status);

        // تغيير الحالة
        var status = await admin.PostAsJsonAsync($"/api/governance/escalations/{id}/status",
            new ChangeGovernanceEscalationStatusRequest(GovernanceEscalationStatus.UnderReview, "قيد المراجعة"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        // الحالة غير المتغيّرة ⇒ 409
        var unchanged = await admin.PostAsJsonAsync($"/api/governance/escalations/{id}/status",
            new ChangeGovernanceEscalationStatusRequest(GovernanceEscalationStatus.UnderReview), TestJson.Options);
        Assert.Equal(HttpStatusCode.Conflict, unchanged.StatusCode);

        // تعليق
        var comment = await admin.PostAsJsonAsync($"/api/governance/escalations/{id}/comments",
            new AddGovernanceEscalationCommentRequest("تعليق متابعة"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, comment.StatusCode);

        // إغلاق
        var close = await admin.PostAsJsonAsync($"/api/governance/escalations/{id}/close",
            new CloseGovernanceEscalationRequest("تمّت المعالجة", "إغلاق"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var closedDto = await close.ReadAsync<GovernanceEscalationDetailDto>();
        Assert.Equal(GovernanceEscalationStatus.Closed, closedDto!.Item.Status);
        Assert.NotNull(closedDto.ClosedAtUtc);

        // إغلاق مكرّر ⇒ 409
        var closeAgain = await admin.PostAsJsonAsync($"/api/governance/escalations/{id}/close",
            new CloseGovernanceEscalationRequest(), TestJson.Options);
        Assert.Equal(HttpStatusCode.Conflict, closeAgain.StatusCode);

        // إعادة فتح
        var reopen = await admin.PostAsJsonAsync($"/api/governance/escalations/{id}/reopen",
            new ReopenGovernanceEscalationRequest("إعادة فتح للتدقيق"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);
        var reopenedDto = await reopen.ReadAsync<GovernanceEscalationDetailDto>();
        Assert.Equal(GovernanceEscalationStatus.Reopened, reopenedDto!.Item.Status);
        Assert.Null(reopenedDto.ClosedAtUtc);
    }

    [Fact]
    public async Task Reopen_NotClosed_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await CreateAsync(admin, NewEscalation("مفتوح"))).ReadAsync<GovernanceEscalationDetailDto>();
        var res = await admin.PostAsJsonAsync($"/api/governance/escalations/{created!.Item.Id}/reopen",
            new ReopenGovernanceEscalationRequest(), TestJson.Options);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ===== الإسناد إلى مستخدم غير موجود =====

    [Fact]
    public async Task Admin_Assign_NonExistentUser_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await CreateAsync(admin, NewEscalation())).ReadAsync<GovernanceEscalationDetailDto>();
        var res = await admin.PostAsJsonAsync($"/api/governance/escalations/{created!.Item.Id}/assign",
            new AssignGovernanceEscalationRequest(Guid.NewGuid()), TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== الفلترة: mine =====

    [Fact]
    public async Task List_Mine_ReturnsOnlyOwnRaised()
    {
        var (client, uid) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await CreateAsync(client, NewEscalation("تصعيدي 1"));
        await CreateAsync(client, NewEscalation("تصعيدي 2"));
        var res = await client.GetAsync("/api/governance/escalations?mine=true");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.ReadAsync<List<GovernanceEscalationListItemDto>>();
        Assert.NotNull(list);
        Assert.NotEmpty(list!);
        Assert.All(list!, e => Assert.Equal(uid, e.RaisedByUserId));
    }
}
