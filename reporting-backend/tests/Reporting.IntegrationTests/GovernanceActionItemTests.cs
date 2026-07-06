using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1) — كيان مستقلّ يحوّل أيّ تصعيد/بند حوكمة/ملاحظة يدوية إلى إجراء متابَع
/// (مُسنَد إليه + استحقاق + أولوية + حالة + خطّ زمني). لا يمسّ سير اعتماد التقارير. محكوم بسياسة GovernanceActionItemAccess
/// (Viewer مستثنى). الإنشاء: واسع (Admin/CEO/GM/CeoSupport) أو Manager/TeamLeader/HR (لا Employee). الرؤية: المنشئ/المُسنَد إليه/
/// نطاق المدير/الرؤية الواسعة. القراءة غير المصرّح بها تُقنَّع 404 لا 403. الإجراء المنبثق من تصعيد حسّاس فلا يُكشَف مصدره للمُسنَد إليه.
/// «متأخر» تُحسَب ولا تُخزَّن. لا إشعارات/بريد في هذه المرحلة.
/// </summary>
[Collection("Integration")]
public class GovernanceActionItemTests
{
    private readonly CustomWebApplicationFactory _factory;

    public GovernanceActionItemTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static CreateGovernanceActionItemRequest NewItem(
        string title = "إجراء حوكمة اختباري",
        string? description = "وصف الإجراء",
        ActionItemPriority priority = ActionItemPriority.Medium,
        ActionItemSourceType sourceType = ActionItemSourceType.Manual,
        Guid? sourceId = null,
        Guid? assignedToUserId = null,
        DateOnly? dueDate = null)
        => new(title, description, priority, sourceType, sourceId, assignedToUserId, dueDate);

    private static Task<HttpResponseMessage> CreateAsync(HttpClient c, CreateGovernanceActionItemRequest req)
        => c.PostAsJsonAsync("/api/governance/action-items", req, TestJson.Options);

    private static async Task<GovernanceActionItemDetailDto> CreateOkAsync(HttpClient c, CreateGovernanceActionItemRequest req)
    {
        var res = await CreateAsync(c, req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<GovernanceActionItemDetailDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    // ===== 1. الصلاحيات على مستوى السياسة =====

    [Fact]
    public async Task Anonymous_List_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/governance/action-items");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Viewer_List_403()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Viewer);
        var res = await client.GetAsync("/api/governance/action-items");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.TeamLeader)]
    [InlineData(Roles.Hr)]
    [InlineData(Roles.Employee)]
    public async Task ActionItemUsers_CanList_200(string role)
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
        var res = await client.GetAsync("/api/governance/action-items");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 2. الإنشاء بدور مصرّح =====

    [Fact]
    public async Task Admin_Create_ReturnsOpen_CreatedBySelf_TimelineCreated()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var dto = await CreateOkAsync(admin, NewItem("إجراء أدمن", priority: ActionItemPriority.High));
        Assert.Equal(ActionItemStatus.Open, dto.Item.Status);
        Assert.Equal(ActionItemSourceType.Manual, dto.Item.SourceType);
        Assert.True(dto.CanAssign);
        Assert.True(dto.CanChangeDueDate);
        Assert.True(dto.CanCancel);
        Assert.Single(dto.Timeline);
        Assert.Equal(ActionItemUpdateType.Created, dto.Timeline[0].UpdateType);
    }

    [Fact]
    public async Task Manager_Create_200()
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var res = await CreateAsync(client, NewItem("إجراء مدير"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 3. منع الإنشاء غير المصرّح =====

    [Fact]
    public async Task Employee_Create_403()
    {
        // الموظّف ضمن السياسة (يستطيع القراءة) لكن لا يملك إنشاء إجراء حوكمة.
        var (client, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await CreateAsync(client, NewItem());
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Create_TitleRequired_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await CreateAsync(admin, NewItem(title: "   "));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Create_AssigneeNotFound_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await CreateAsync(admin, NewItem(assignedToUserId: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 4. المُسنَد إليه يرى ما أُسنِد إليه =====

    [Fact]
    public async Task Assignee_CanViewAssignedItem_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (assignee, assigneeId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(admin, NewItem("إجراء مُسنَد", assignedToUserId: assigneeId));

        var res = await assignee.GetAsync($"/api/governance/action-items/{created.Item.Id}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<GovernanceActionItemDetailDto>();
        Assert.Equal(assigneeId, dto!.Item.AssignedToUserId);
        // المُسنَد إليه (بلا إدارة) يستطيع تغيير الحالة لكن لا يملك الإسناد/الإلغاء.
        Assert.True(dto.CanChangeStatus);
        Assert.False(dto.CanAssign);
        Assert.False(dto.CanCancel);
    }

    // ===== 5. خارج النطاق يُقنَّع 404 =====

    [Fact]
    public async Task OutOfScopeStranger_GetById_404_Masked()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, assigneeId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(admin, NewItem("إجراء خاص", assignedToUserId: assigneeId));

        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await stranger.GetAsync($"/api/governance/action-items/{created.Item.Id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task NonExistent_GetById_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.GetAsync($"/api/governance/action-items/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== 6. انتقالات حالة المُسنَد إليه =====

    [Fact]
    public async Task Assignee_StatusUpdate_OpenToInProgress_200_InvalidTransition_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (assignee, assigneeId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(admin, NewItem("إجراء تنفيذ", assignedToUserId: assigneeId));
        var id = created.Item.Id;

        // Open → InProgress مسموح للمُسنَد إليه.
        var ok = await assignee.PostAsJsonAsync($"/api/governance/action-items/{id}/status",
            new ChangeGovernanceActionItemStatusRequest(ActionItemStatus.InProgress, "بدأت التنفيذ"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var okDto = await ok.ReadAsync<GovernanceActionItemDetailDto>();
        Assert.Equal(ActionItemStatus.InProgress, okDto!.Item.Status);

        // InProgress → Completed مسموح للمُسنَد إليه.
        var done = await assignee.PostAsJsonAsync($"/api/governance/action-items/{id}/status",
            new ChangeGovernanceActionItemStatusRequest(ActionItemStatus.Completed, CompletionNote: "تمّ"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var doneDto = await done.ReadAsync<GovernanceActionItemDetailDto>();
        Assert.Equal(ActionItemStatus.Completed, doneDto!.Item.Status);
        Assert.NotNull(doneDto.CompletedAtUtc);
    }

    [Fact]
    public async Task Assignee_InvalidTransition_OpenToCompleted_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (assignee, assigneeId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(admin, NewItem("انتقال غير صالح", assignedToUserId: assigneeId));

        // Open → Completed غير مسموح للمُسنَد إليه (يجب المرور بـ InProgress).
        var res = await assignee.PostAsJsonAsync($"/api/governance/action-items/{created.Item.Id}/status",
            new ChangeGovernanceActionItemStatusRequest(ActionItemStatus.Completed), TestJson.Options);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Stranger_StatusUpdate_404_Masked()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, assigneeId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(admin, NewItem(assignedToUserId: assigneeId));

        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await stranger.PostAsJsonAsync($"/api/governance/action-items/{created.Item.Id}/status",
            new ChangeGovernanceActionItemStatusRequest(ActionItemStatus.InProgress), TestJson.Options);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== 7. الإجراء المنبثق من تصعيد لا يكشف مصدره للمُسنَد إليه =====

    [Fact]
    public async Task EscalationSourced_DoesNotLeakSourceToAssignee()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        // تصعيد مصدر (Admin = رؤية واسعة).
        var esc = await (await admin.PostAsJsonAsync("/api/governance/escalations",
            new CreateGovernanceEscalationRequest("تصعيد سرّي", "تفاصيل حسّاسة", EscalationType.Compliance,
                EscalationSeverity.High, EscalationTargetType.Other, null, null, null, null, null), TestJson.Options))
            .ReadAsync<GovernanceEscalationDetailDto>();

        var (assignee, assigneeId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(admin, NewItem(
            "عالج التصعيد", description: "نصّ الإجراء المرئيّ",
            sourceType: ActionItemSourceType.Escalation, sourceId: esc!.Item.Id, assignedToUserId: assigneeId));
        // المنشئ (واسع) يرى المصدر.
        Assert.True(created.SourceVisibleToViewer);
        Assert.Equal(esc.Item.Id, created.Item.SourceId);
        Assert.True(created.Item.IsSensitive);

        // المُسنَد إليه يرى الإجراء لكن لا يُكشَف مصدره (SourceId/SourceTitle مُقنَّعان).
        var seen = await (await assignee.GetAsync($"/api/governance/action-items/{created.Item.Id}"))
            .ReadAsync<GovernanceActionItemDetailDto>();
        Assert.NotNull(seen);
        Assert.False(seen!.SourceVisibleToViewer);
        Assert.Null(seen.Item.SourceId);
        Assert.Null(seen.Item.SourceTitle);
        // لكن نصّ الإجراء نفسه مرئيّ للمُسنَد إليه.
        Assert.Equal("عالج التصعيد", seen.Item.Title);
        Assert.Equal("نصّ الإجراء المرئيّ", seen.Description);
    }

    // ===== 8. «متأخر» محسوبة =====

    [Fact]
    public async Task Overdue_Computed_FromDueDateAndStatus()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var past = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);

        var overdue = await CreateOkAsync(admin, NewItem("متأخر", dueDate: past));
        Assert.True(overdue.Item.IsOverdue);

        var notOverdue = await CreateOkAsync(admin, NewItem("غير متأخر", dueDate: future));
        Assert.False(notOverdue.Item.IsOverdue);
    }

    // ===== 9. الإلغاء =====

    [Fact]
    public async Task Admin_Cancel_200_AlreadyCancelled_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(admin, NewItem("للإلغاء"));
        var id = created.Item.Id;

        var cancel = await admin.PostAsJsonAsync($"/api/governance/action-items/{id}/cancel",
            new CancelGovernanceActionItemRequest("لم يعد مطلوبًا"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelDto = await cancel.ReadAsync<GovernanceActionItemDetailDto>();
        Assert.Equal(ActionItemStatus.Cancelled, cancelDto!.Item.Status);

        var again = await admin.PostAsJsonAsync($"/api/governance/action-items/{id}/cancel",
            new CancelGovernanceActionItemRequest(), TestJson.Options);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Assignee_Cancel_403()
    {
        // المُسنَد إليه (بلا إدارة) لا يملك إلغاء الإجراء.
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (assignee, assigneeId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(admin, NewItem(assignedToUserId: assigneeId));
        var res = await assignee.PostAsJsonAsync($"/api/governance/action-items/{created.Item.Id}/cancel",
            new CancelGovernanceActionItemRequest(), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 10. دورة حياة كاملة + إعادة الفتح =====

    [Fact]
    public async Task Admin_FullLifecycle_Assign_Status_Comment_DueDate_Complete_Reopen()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (_, assigneeId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(admin, NewItem("دورة كاملة"));
        var id = created.Item.Id;

        // إسناد
        var assign = await admin.PostAsJsonAsync($"/api/governance/action-items/{id}/assign",
            new AssignGovernanceActionItemRequest(assigneeId, "للمتابعة"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        Assert.Equal(assigneeId, (await assign.ReadAsync<GovernanceActionItemDetailDto>())!.Item.AssignedToUserId);

        // تغيير تاريخ الاستحقاق
        var due = await admin.PostAsJsonAsync($"/api/governance/action-items/{id}/due-date",
            new ChangeGovernanceActionItemDueDateRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5)), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, due.StatusCode);

        // تعليق
        var comment = await admin.PostAsJsonAsync($"/api/governance/action-items/{id}/updates",
            new AddGovernanceActionItemCommentRequest("متابعة"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, comment.StatusCode);

        // حالة → InProgress → Completed
        await admin.PostAsJsonAsync($"/api/governance/action-items/{id}/status",
            new ChangeGovernanceActionItemStatusRequest(ActionItemStatus.InProgress), TestJson.Options);
        var complete = await admin.PostAsJsonAsync($"/api/governance/action-items/{id}/status",
            new ChangeGovernanceActionItemStatusRequest(ActionItemStatus.Completed, CompletionNote: "أُنجِز"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = await complete.ReadAsync<GovernanceActionItemDetailDto>();
        Assert.Equal(ActionItemStatus.Completed, completed!.Item.Status);
        Assert.True(completed.CanReopen);

        // إعادة الفتح (Completed → InProgress) من صلاحية الإدارة.
        var reopen = await admin.PostAsJsonAsync($"/api/governance/action-items/{id}/status",
            new ChangeGovernanceActionItemStatusRequest(ActionItemStatus.InProgress, "إعادة فتح"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);
        var reopened = await reopen.ReadAsync<GovernanceActionItemDetailDto>();
        Assert.Equal(ActionItemStatus.InProgress, reopened!.Item.Status);
        Assert.Null(reopened.CompletedAtUtc);
    }

    [Fact]
    public async Task Status_Unchanged_409()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(admin, NewItem("بلا تغيير"));
        var res = await admin.PostAsJsonAsync($"/api/governance/action-items/{created.Item.Id}/status",
            new ChangeGovernanceActionItemStatusRequest(ActionItemStatus.Open), TestJson.Options);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_ToCancelled_UseCancelEndpoint_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(admin, NewItem());
        var res = await admin.PostAsJsonAsync($"/api/governance/action-items/{created.Item.Id}/status",
            new ChangeGovernanceActionItemStatusRequest(ActionItemStatus.Cancelled), TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 11. الفلاتر =====

    [Fact]
    public async Task List_AssignedToMe_ReturnsOnlyOwnAssigned()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (assignee, assigneeId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await CreateOkAsync(admin, NewItem("مُسنَد 1", assignedToUserId: assigneeId));
        await CreateOkAsync(admin, NewItem("مُسنَد 2", assignedToUserId: assigneeId));

        var res = await assignee.GetAsync("/api/governance/action-items?assignedToMe=true");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.ReadAsync<List<GovernanceActionItemListItemDto>>();
        Assert.NotEmpty(list!);
        Assert.All(list!, i => Assert.Equal(assigneeId, i.AssignedToUserId));
    }

    // ===== 12. دليل المُسنَد إليهم الآمن =====

    [Fact]
    public async Task AssigneeDirectory_WideViewer_IncludesSensitiveAndRegular()
    {
        // GOV-DIRECTORY-SCOPE-FIX-R1: صاحب الرؤية الواسعة (Admin) يرى الجميع شاملًا الحسّاسين (للإسناد لأي شخص).
        var (_, regularId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, adminId) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.GetAsync("/api/governance/action-items/assignee-directory");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dir = await res.ReadAsync<ActionItemAssigneeDirectoryDto>();
        Assert.Contains(dir!.Users, u => u.Id == regularId);
        Assert.Contains(dir.Users, u => u.Id == adminId);
    }

    [Fact]
    public async Task AssigneeDirectory_Anonymous_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/governance/action-items/assignee-directory");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
