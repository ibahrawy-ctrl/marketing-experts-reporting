using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Dashboard;
using Reporting.Application.Positions;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// المناصب المرنة (Phase 1A — رؤية فقط). يتحقّق من:
/// المنصب يوسّع نطاق الرؤية فقط (اتحاد مع نطاق الدور) ولا يمنح أي قدرة اعتماد/إرجاع/تصعيد،
/// ولا يظهر في «بانتظار الاعتماد»، ولا يفتح إدارة (Reset/أرصدة/قوالب). والتعطيل/إلغاء الإسناد يُلغي الأثر.
/// </summary>
[Collection("Integration")]
public class PositionsTests
{
    private readonly CustomWebApplicationFactory _factory;

    public PositionsTests(CustomWebApplicationFactory factory) => _factory = factory;

    // (1) موظّف بلا منصب: السلوك القائم — يرى نفسه فقط ولا يرى تقارير إدارة أخرى.
    [Fact]
    public async Task User_Without_Position_Sees_Only_Own_Scope()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var deptX = await CreateDepartmentAsync("P1X");

        var submitterX = await CreateSubmitterAsync(deptX);
        await SubmitAsync(submitterX.C, templateId, fieldId, TestCalendar.Cycle(1));

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var ids = await SubmitterIdsAsync(viewer.Client);
        Assert.DoesNotContain(submitterX.Id, ids);
    }

    // (2) منصب reports.view على الإدارة X ⇒ يرى تقارير الإدارة X.
    [Fact]
    public async Task Position_ReportsView_On_Department_Sees_That_Department()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var deptX = await CreateDepartmentAsync("P2X");

        var submitterX = await CreateSubmitterAsync(deptX);
        await SubmitAsync(submitterX.C, templateId, fieldId, TestCalendar.Cycle(2));

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetupViewerPositionAsync(admin, viewer.UserId, "P2", deptX, null, null, PositionScopeKind.Department);

        var ids = await SubmitterIdsAsync(viewer.Client);
        Assert.Contains(submitterX.Id, ids);
    }

    // (3) المنصب على الإدارة X لا يكشف الإدارة Y.
    [Fact]
    public async Task Position_On_Department_X_Does_Not_See_Department_Y()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var deptX = await CreateDepartmentAsync("P3X");
        var deptY = await CreateDepartmentAsync("P3Y");

        var submitterX = await CreateSubmitterAsync(deptX);
        var submitterY = await CreateSubmitterAsync(deptY);
        await SubmitAsync(submitterX.C, templateId, fieldId, TestCalendar.Cycle(3));
        await SubmitAsync(submitterY.C, templateId, fieldId, TestCalendar.Cycle(3));

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetupViewerPositionAsync(admin, viewer.UserId, "P3", deptX, null, null, PositionScopeKind.Department);

        var ids = await SubmitterIdsAsync(viewer.Client);
        Assert.Contains(submitterX.Id, ids);
        Assert.DoesNotContain(submitterY.Id, ids);
    }

    // (4) يرى التقرير عبر المنصب لكنه لا يستطيع الاعتماد.
    [Fact]
    public async Task Position_Viewer_Can_See_But_Cannot_Approve()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var deptX = await CreateDepartmentAsync("P4X");
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);

        var submitterX = await CreateSubmitterAsync(deptX, tl.UserId);
        var sub = await SubmitAsync(submitterX.C, templateId, fieldId, TestCalendar.Cycle(4));

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetupViewerPositionAsync(admin, viewer.UserId, "P4", deptX, null, null, PositionScopeKind.Department);

        // يراه ضمن القائمة
        var ids = await SubmitterIdsAsync(viewer.Client);
        Assert.Contains(submitterX.Id, ids);

        var res = await viewer.Client.PostAsJsonAsync($"/api/submissions/{sub.Id}/approve", new ApprovalActionRequest(null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (5) لا يستطيع الإرجاع.
    [Fact]
    public async Task Position_Viewer_Cannot_Return()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var deptX = await CreateDepartmentAsync("P5X");
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);

        var submitterX = await CreateSubmitterAsync(deptX, tl.UserId);
        var sub = await SubmitAsync(submitterX.C, templateId, fieldId, TestCalendar.Cycle(5));

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetupViewerPositionAsync(admin, viewer.UserId, "P5", deptX, null, null, PositionScopeKind.Department);

        var res = await viewer.Client.PostAsJsonAsync($"/api/submissions/{sub.Id}/return", new ApprovalActionRequest("سبب"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (6) لا يستطيع التصعيد (إجراء اعتماد آخر) — المنصب لا يمنح أي قدرة قرار.
    [Fact]
    public async Task Position_Viewer_Cannot_Escalate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var deptX = await CreateDepartmentAsync("P6X");
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);

        var submitterX = await CreateSubmitterAsync(deptX, tl.UserId);
        var sub = await SubmitAsync(submitterX.C, templateId, fieldId, TestCalendar.Cycle(6));

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetupViewerPositionAsync(admin, viewer.UserId, "P6", deptX, null, null, PositionScopeKind.Department);

        var res = await viewer.Client.PostAsJsonAsync($"/api/submissions/{sub.Id}/escalate", new ApprovalActionRequest("سبب"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (7) التقرير لا يظهر في «بانتظار الاعتماد» لصاحب المنصب.
    [Fact]
    public async Task Position_Viewer_Report_Not_In_Pending_Approvals()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var deptX = await CreateDepartmentAsync("P7X");
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);

        var submitterX = await CreateSubmitterAsync(deptX, tl.UserId);
        var sub = await SubmitAsync(submitterX.C, templateId, fieldId, TestCalendar.Cycle(7));

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetupViewerPositionAsync(admin, viewer.UserId, "P7", deptX, null, null, PositionScopeKind.Department);

        var pending = await (await viewer.Client.GetAsync("/api/submissions/pending-approvals"))
            .ReadAsync<List<SubmissionListItemDto>>();
        Assert.DoesNotContain(sub.Id, pending!.Select(p => p.Id));
    }

    // (8) المنصب لا يفتح إعادة تعيين كلمات المرور.
    [Fact]
    public async Task Position_Viewer_Cannot_Reset_Password()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var deptX = await CreateDepartmentAsync("P8X");
        var target = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetupViewerPositionAsync(admin, viewer.UserId, "P8", deptX, null, null, PositionScopeKind.Department);

        var res = await viewer.Client.PostAsJsonAsync(
            $"/api/directory/users/{target.UserId}/reset-password", new { newPassword = "Passw0rd#9" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (9) المنصب لا يفتح إدارة الأرصدة.
    [Fact]
    public async Task Position_Viewer_Cannot_Manage_Balances()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var deptX = await CreateDepartmentAsync("P9X");

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetupViewerPositionAsync(admin, viewer.UserId, "P9", deptX, null, null, PositionScopeKind.Department);

        var res = await viewer.Client.GetAsync("/api/balances/employees");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (10) المنصب لا يفتح إدارة قوالب التقارير.
    [Fact]
    public async Task Position_Viewer_Cannot_Manage_Templates()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var deptX = await CreateDepartmentAsync("P10X");

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetupViewerPositionAsync(admin, viewer.UserId, "P10", deptX, null, null, PositionScopeKind.Department);

        var res = await viewer.Client.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, null, PeriodType.Weekly));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (11) الأدمن لا يتأثّر — يرى تقارير الإدارتين معًا.
    [Fact]
    public async Task Admin_Unaffected_Sees_Everything()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var deptX = await CreateDepartmentAsync("P11X");
        var deptY = await CreateDepartmentAsync("P11Y");

        var submitterX = await CreateSubmitterAsync(deptX);
        var submitterY = await CreateSubmitterAsync(deptY);
        await SubmitAsync(submitterX.C, templateId, fieldId, TestCalendar.Cycle(8));
        await SubmitAsync(submitterY.C, templateId, fieldId, TestCalendar.Cycle(8));

        var ids = await SubmitterIdsAsync(admin);
        Assert.Contains(submitterX.Id, ids);
        Assert.Contains(submitterY.Id, ids);
    }

    // (12) الاتحاد: نطاق الدور (فريق) + نطاق المنصب (إدارة أخرى) يتّحدان دون فقدان.
    [Fact]
    public async Task Scope_Union_Role_And_Position_Without_Loss()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);

        // قائد فريق له مرؤوس مباشر (نطاق الدور)
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var teamMember = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tl.UserId);
        await SubmitAsync(teamMember.Client, templateId, fieldId, TestCalendar.Cycle(9));

        // إدارة منفصلة لا علاقة لها بفريق القائد (نطاق المنصب)
        var deptX = await CreateDepartmentAsync("P12X");
        var deptSubmitter = await CreateSubmitterAsync(deptX);
        await SubmitAsync(deptSubmitter.C, templateId, fieldId, TestCalendar.Cycle(9));

        await SetupViewerPositionAsync(admin, tl.UserId, "P12", deptX, null, null, PositionScopeKind.Department);

        var ids = await SubmitterIdsAsync(tl.Client);
        Assert.Contains(teamMember.UserId, ids);    // نطاق الدور محفوظ
        Assert.Contains(deptSubmitter.Id, ids);     // نطاق المنصب مُضاف
    }

    // (13) تعطيل المنصب يُلغي الأثر فورًا.
    [Fact]
    public async Task Disabling_Position_Removes_Effect()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var deptX = await CreateDepartmentAsync("P13X");

        var submitterX = await CreateSubmitterAsync(deptX);
        await SubmitAsync(submitterX.C, templateId, fieldId, TestCalendar.Cycle(10));

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var pos = await SetupViewerPositionAsync(admin, viewer.UserId, "P13", deptX, null, null, PositionScopeKind.Department);

        Assert.Contains(submitterX.Id, await SubmitterIdsAsync(viewer.Client));

        await admin.PostAsync($"/api/positions/{pos.Id}/disable", null);
        Assert.DoesNotContain(submitterX.Id, await SubmitterIdsAsync(viewer.Client));
    }

    // (14) إلغاء إسناد المنصب عن المستخدم يُلغي الأثر.
    [Fact]
    public async Task Revoking_Position_Removes_Effect()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishWeeklyTemplateAsync(admin);
        var deptX = await CreateDepartmentAsync("P14X");

        var submitterX = await CreateSubmitterAsync(deptX);
        await SubmitAsync(submitterX.C, templateId, fieldId, TestCalendar.Cycle(11));

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var pos = await SetupViewerPositionAsync(admin, viewer.UserId, "P14", deptX, null, null, PositionScopeKind.Department);

        Assert.Contains(submitterX.Id, await SubmitterIdsAsync(viewer.Client));

        await admin.PostAsJsonAsync($"/api/positions/{pos.Id}/revoke", new { userId = viewer.UserId });
        Assert.DoesNotContain(submitterX.Id, await SubmitterIdsAsync(viewer.Client));
    }

    // (15) التدقيق يسجّل الإنشاء/الصلاحية/النطاق/الإسناد/الإلغاء.
    [Fact]
    public async Task Audit_Records_Position_Changes()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var deptX = await CreateDepartmentAsync("P15X");
        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var pos = await SetupViewerPositionAsync(admin, viewer.UserId, "P15", deptX, null, null, PositionScopeKind.Department);

        await admin.PostAsJsonAsync($"/api/positions/{pos.Id}/revoke", new { userId = viewer.UserId });

        await AssertAuditExistsAsync("position.created", pos.Id);
        await AssertAuditExistsAsync("position.permission_changed", pos.Id);
        await AssertAuditExistsAsync("position.scope_changed", pos.Id);
        await AssertAuditExistsAsync("position.assigned", pos.Id);
        await AssertAuditExistsAsync("position.revoked", pos.Id);
    }

    // (16) قائمة مناصب المستخدم تُرجَع بنجاح (حماية من خطأ ترجمة LINQ).
    [Fact]
    public async Task ListForUser_Returns_Assigned_Position()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var deptX = await CreateDepartmentAsync("P16X");
        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var pos = await SetupViewerPositionAsync(admin, viewer.UserId, "P16", deptX, null, null, PositionScopeKind.Department);

        var res = await admin.GetAsync($"/api/users/{viewer.UserId}/positions");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.ReadAsync<List<UserPositionDto>>();
        Assert.Contains(list!, x => x.PositionId == pos.Id && x.PositionIsActive);
    }

    // (17) داشبورد صاحب المنصب يشمل تقارير الإدارة المُسنَدة ضمن نطاقه (اتحاد المنصب يصل للداشبورد).
    [Fact]
    public async Task Position_Viewer_Dashboard_Includes_Scoped_Reports()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var deptX = await CreateDepartmentAsync("P17X");
        var submitterX = await CreateSubmitterAsync(deptX);

        var viewer = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetupViewerPositionAsync(admin, viewer.UserId, "P17", deptX, null, null, PositionScopeKind.Department);

        var dash = await (await viewer.Client.GetAsync("/api/dashboard/me")).ReadAsync<DashboardDto>();
        Assert.Contains(viewer.UserId, dash!.Scope.Ids);   // نطاق الدور محفوظ
        Assert.Contains(submitterX.Id, dash.Scope.Ids);     // نطاق المنصب وصل للداشبورد
    }

    // ── أدوات مساعدة ────────────────────────────────────────────────────

    /// <summary>ينشئ منصبًا فعّالًا بصلاحية reports.view ونطاق، ويُسنده للمستخدم. يعيد المنصب.</summary>
    private async Task<PositionDto> SetupViewerPositionAsync(
        HttpClient admin, Guid userId, string codeTag,
        Guid? departmentId, Guid? teamId, Guid? targetUserId, PositionScopeKind kind)
    {
        var pos = await (await admin.PostAsJsonAsync("/api/positions",
            new CreatePositionRequest($"{codeTag}_{Guid.NewGuid():N}".Substring(0, 18), $"منصب {codeTag}", null)))
            .ReadAsync<PositionDto>();
        await admin.PostAsJsonAsync($"/api/positions/{pos!.Id}/permissions",
            new { permissionKey = PositionPermissions.ReportsView });
        await admin.PostAsJsonAsync($"/api/positions/{pos.Id}/scopes",
            new AddPositionScopeRequest(kind, departmentId, teamId, targetUserId));
        await admin.PostAsJsonAsync($"/api/positions/{pos.Id}/assign", new { userId });
        return pos;
    }

    private async Task<(HttpClient C, Guid Id)> CreateSubmitterAsync(Guid departmentId, Guid? managerId = null)
    {
        var u = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(x => x.Id == u.UserId);
        user.DepartmentId = departmentId;
        await db.SaveChangesAsync();
        return (u.Client, u.UserId);
    }

    private async Task<Guid> CreateDepartmentAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // `NameAr` يأخذ لاحقة جولة كما يأخذها `Code` أصلًا: الاسم صار فريدًا على مستوى القاعدة
        // (DEF-P123-001)، و`reporting_test` قاعدة دائمة تتراكم ⟹ وسمٌ حرفيّ ثابت مثل «P1X» ينجح في
        // أوّل جولة ويصطدم بنفسه في كلّ جولة تالية. لا تأكيد في هذا الصنف يتعلّق بنصّ الاسم.
        var dept = new Department { NameAr = $"إدارة {tag} {Guid.NewGuid():N}", Code = $"{tag}_{Guid.NewGuid():N}".Substring(0, 18) };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        return dept.Id;
    }

    private static async Task<List<Guid>> SubmitterIdsAsync(HttpClient client)
    {
        var list = await (await client.GetAsync("/api/submissions")).ReadAsync<List<SubmissionListItemDto>>();
        return list!.Select(s => s.SubmitterId).ToList();
    }

    private static async Task<SubmissionDto> SubmitAsync(HttpClient c, Guid templateId, Guid fieldId, string periodKey)
    {
        var draft = await (await c.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey)))
            .ReadAsync<SubmissionDto>();
        await c.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 1000m, null, null, null) }));
        return (await (await c.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
    }

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishWeeklyTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"تقرير أسبوعي {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private async Task AssertAuditExistsAsync(string action, Guid entityId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await db.AuditLogs.AnyAsync(a => a.Action == action && a.EntityId == entityId);
        Assert.True(exists, $"توقّعنا سجل تدقيق «{action}» للكيان {entityId}.");
    }
}
