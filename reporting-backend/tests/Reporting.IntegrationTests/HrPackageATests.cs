using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Directory;
using Reporting.Application.Reports;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// حزمة A لدور الموارد البشرية (HR):
/// (1) شاشة متابعة التزام التسليم (per-person) — التزام فقط بلا أيّ محتوى تقرير،
/// (2) تعديل البيانات الأساسية غير الحسّاسة للموظف (الاسم فقط)،
/// (3) تعديل الانتماء التنظيمي (الإدارة/الفريق/المدير) مع قيود أمان صارمة + Audit.
/// تتحقّق هذه الاختبارات من السماح والحجب والحواجز والتدقيق دون توسيع أيّ صلاحية حسّاسة.
/// </summary>
[Collection("Integration")]
public class HrPackageATests
{
    private readonly CustomWebApplicationFactory _factory;

    public HrPackageATests(CustomWebApplicationFactory factory) => _factory = factory;

    // (1) HR يرى قائمة متابعة الالتزام per-person (الموظف المتوقَّع يظهر، الحالة «لم يسلّم»).
    [Fact]
    public async Task Hr_SeesPerPersonComplianceList_200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var roleId = await CreateWeeklyReportingRoleAsync(admin, "HRC1");
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, roleId);

        var res = await hr.GetAsync("/api/reports/submission-compliance");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var report = await res.ReadAsync<SubmissionComplianceReport>();
        Assert.NotNull(report);
        var row = report!.Rows.SingleOrDefault(r => r.UserId == empId);
        Assert.NotNull(row);
        Assert.False(row!.Submitted);
        Assert.Equal("لم يسلّم", row.StatusLabel);
    }

    // (2) شاشة المتابعة لا تكشف أيّ محتوى تقرير: قيمة حقل سرّية لا تظهر في استجابة الالتزام.
    [Fact]
    public async Task Hr_ComplianceList_ContainsNoReportContent()
    {
        const string secret = "SECRET_CONTENT_DO_NOT_LEAK_7f3a";
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (roleId, templateId, fieldId) = await CreateWeeklyTemplateWithTextFieldAsync(admin, "HRC2");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, roleId);

        // أسبوع منطبق (في/بعد أرضيّة الإطلاق الأسبوعيّ 2026-07-04 = 2026-W28) كي يظهر الموظّف في قائمة الالتزام.
        var weekKey = "2026-W28";
        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, weekKey))).ReadAsync<SubmissionDto>();
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, secret, null, null, null, null) }));
        var submitted = await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);

        var res = await hr.GetAsync($"/api/reports/submission-compliance?weekKey={weekKey}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var raw = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, raw);            // لا قيمة الحقل.
        Assert.DoesNotContain(fieldId.ToString(), raw); // لا أيّ مرجع لحقول التقرير.

        var report = await res.ReadAsync<SubmissionComplianceReport>();
        var row = report!.Rows.Single(r => r.UserId == empId);
        Assert.True(row.Submitted); // الالتزام فقط: سلّم — دون كشف ما سلّمه.
    }

    // (3) HR لا يستطيع فتح تقرير خارج نطاقه (لا قراءة محتوى عبر مسار التسليمات).
    [Fact]
    public async Task Hr_CannotOpenSubmissionOutsideScope_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, templateId, fieldId) = await CreateWeeklyTemplateWithTextFieldAsync(admin, "HRC3");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, roleId);
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W26"))).ReadAsync<SubmissionDto>();
        _ = fieldId;

        var res = await hr.GetAsync($"/api/submissions/{draft!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (4) HR لا يستطيع الاعتماد/الإرجاع لتقرير (ليس الموافِق الحالي).
    [Fact]
    public async Task Hr_CannotApproveOrReturn_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (roleId, templateId, fieldId) = await CreateWeeklyTemplateWithTextFieldAsync(admin, "HRC4");
        var (emp, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SetJobRoleAsync(empId, roleId);
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);

        var draft = await (await emp.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W27"))).ReadAsync<SubmissionDto>();
        await emp.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, "x", null, null, null, null) }));
        await emp.PostAsync($"/api/submissions/{draft.Id}/submit", null);

        var approve = await hr.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve", new ApprovalActionRequest(null));
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);

        var ret = await hr.PostAsJsonAsync($"/api/submissions/{draft.Id}/return", new ApprovalActionRequest("سبب"));
        Assert.Equal(HttpStatusCode.Forbidden, ret.StatusCode);
    }

    // (5) HR لا يستطيع إعادة تعيين كلمة المرور (سياسة Admin/CeoSupport فقط).
    [Fact]
    public async Task Hr_CannotResetPassword_403()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await hr.PostAsJsonAsync($"/api/directory/users/{targetId}/reset-password",
            new ResetPasswordRequest("NewPassw0rd#9"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (6) HR لا يستطيع إنشاء مستخدم (AdminOnly).
    [Fact]
    public async Task Hr_CannotCreateUser_403()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var res = await hr.PostAsJsonAsync("/api/directory/users",
            new CreateUserRequest($"new-{Guid.NewGuid():N}@test.local", "موظف جديد", "Passw0rd#1",
                new[] { Roles.Employee }, null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (7) HR لا يستطيع تعطيل مستخدم (التعطيل عبر PUT users/{id} = AdminOnly).
    [Fact]
    public async Task Hr_CannotDeactivateUser_403()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await hr.PutAsJsonAsync($"/api/directory/users/{targetId}",
            new UpdateUserRequest("اسم", $"x-{Guid.NewGuid():N}@test.local", false, null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (8) HR لا يستطيع تعديل الأدوار (AdminOnly).
    [Fact]
    public async Task Hr_CannotUpdateRoles_403()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await hr.PutAsJsonAsync($"/api/directory/users/{targetId}/roles",
            new UpdateUserRolesRequest(new[] { Roles.Manager }));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // (9) HR لا يستطيع تعديل حساب إداري/تنفيذي حسّاس (CEO) عبر سطح البيانات الأساسية.
    [Fact]
    public async Task Hr_CannotEditSensitiveAccount_403()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);

        var res = await hr.PatchAsJsonAsync($"/api/directory/users/{ceoId}/basic",
            new UpdateUserBasicRequest("اسم جديد للرئيس"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode); // auth.forbidden ⇒ 403
    }

    // (10) HR يستطيع تعديل البيانات الأساسية المسموحة فقط (الاسم) لموظف عادي.
    [Fact]
    public async Task Hr_CanEditBasicData_200()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await hr.PatchAsJsonAsync($"/api/directory/users/{targetId}/basic",
            new UpdateUserBasicRequest("الاسم المُحدَّث"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var dto = await res.ReadAsync<DirectoryUserDto>();
        Assert.Equal("الاسم المُحدَّث", dto!.FullName);
    }

    // (11) HR يستطيع تعديل الإدارة/الفريق/المدير لموظف عادي.
    [Fact]
    public async Task Hr_CanEditOrgAssignment_200()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (deptId, teamId) = await CreateDeptTeamAsync();
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);

        var res = await hr.PatchAsJsonAsync($"/api/directory/users/{targetId}/org-assignment",
            new UpdateUserOrgAssignmentRequest(deptId, teamId, managerId, "نقل تنظيمي"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var dto = await res.ReadAsync<DirectoryUserDto>();
        Assert.Equal(deptId, dto!.DepartmentId);
        Assert.Equal(teamId, dto.TeamId);
        Assert.Equal(managerId, dto.ManagerId);
    }

    // (12) منع جعل المستخدم مديرًا لنفسه.
    [Fact]
    public async Task Hr_OrgAssignment_SelfManager_409()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await hr.PatchAsJsonAsync($"/api/directory/users/{targetId}/org-assignment",
            new UpdateUserOrgAssignmentRequest(null, null, targetId, null));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // (13) منع علاقة مدير دائرية (A مدير B ثم محاولة جعل B مديرًا لـ A).
    [Fact]
    public async Task Hr_OrgAssignment_CircularManager_409()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, aId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, bId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: aId); // B مديره A

        // محاولة جعل B مديرًا لـ A ⇒ دائرة.
        var res = await hr.PatchAsJsonAsync($"/api/directory/users/{aId}/org-assignment",
            new UpdateUserOrgAssignmentRequest(null, null, bId, null));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // (14) منع تعيين مدير غير نشط.
    [Fact]
    public async Task Hr_OrgAssignment_InactiveManager_409()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, mgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        await SetActiveAsync(mgrId, false);

        var res = await hr.PatchAsJsonAsync($"/api/directory/users/{targetId}/org-assignment",
            new UpdateUserOrgAssignmentRequest(null, null, mgrId, null));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // (15) تعديل البيانات الأساسية يكتب Audit = user.basic.updated.
    [Fact]
    public async Task Hr_BasicUpdate_WritesAudit()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        await hr.PatchAsJsonAsync($"/api/directory/users/{targetId}/basic",
            new UpdateUserBasicRequest("اسم مُدقَّق", "سبب التعديل"));

        var logs = await (await admin.GetAsync($"/api/audit-logs?entityId={targetId}")).ReadAsync<List<AuditLogDto>>();
        Assert.NotNull(logs);
        Assert.Contains(logs!, l => l.Action == "user.basic.updated");
    }

    // (16) تعديل الانتماء التنظيمي يكتب Audit = user.org.changed.
    [Fact]
    public async Task Hr_OrgUpdate_WritesAudit()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (deptId, _) = await CreateDeptTeamAsync();

        await hr.PatchAsJsonAsync($"/api/directory/users/{targetId}/org-assignment",
            new UpdateUserOrgAssignmentRequest(deptId, null, null, "نقل إدارة"));

        var logs = await (await admin.GetAsync($"/api/audit-logs?entityId={targetId}")).ReadAsync<List<AuditLogDto>>();
        Assert.NotNull(logs);
        Assert.Contains(logs!, l => l.Action == "user.org.changed");
    }

    // (17) Manager/TeamLeader/Employee محجوبون عن المسارات الجديدة (basic + org)، والموظف محجوب عن المتابعة.
    [Fact]
    public async Task NonAuthorizedRoles_Forbidden_FromNewEndpoints_403()
    {
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        foreach (var role in new[] { Roles.Manager, Roles.TeamLeader, Roles.Employee })
        {
            var (client, _) = await TestAuth.CreateUserAsync(_factory, role);

            var basic = await client.PatchAsJsonAsync($"/api/directory/users/{targetId}/basic",
                new UpdateUserBasicRequest("محاولة"));
            Assert.Equal(HttpStatusCode.Forbidden, basic.StatusCode);

            var org = await client.PatchAsJsonAsync($"/api/directory/users/{targetId}/org-assignment",
                new UpdateUserOrgAssignmentRequest(null, null, null, null));
            Assert.Equal(HttpStatusCode.Forbidden, org.StatusCode);
        }

        // الموظف محجوب عن شاشة متابعة الالتزام (ليس ضمن CompletionMonitors).
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var compliance = await employee.GetAsync("/api/reports/submission-compliance");
        Assert.Equal(HttpStatusCode.Forbidden, compliance.StatusCode);
    }

    // (18) المستخدم غير المصادَق = 401 على كل المسارات الجديدة.
    [Fact]
    public async Task Anonymous_401()
    {
        var client = _factory.CreateClient();
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var basic = await client.PatchAsJsonAsync($"/api/directory/users/{targetId}/basic",
            new UpdateUserBasicRequest("x"));
        Assert.Equal(HttpStatusCode.Unauthorized, basic.StatusCode);

        var org = await client.PatchAsJsonAsync($"/api/directory/users/{targetId}/org-assignment",
            new UpdateUserOrgAssignmentRequest(null, null, null, null));
        Assert.Equal(HttpStatusCode.Unauthorized, org.StatusCode);

        var compliance = await client.GetAsync("/api/reports/submission-compliance");
        Assert.Equal(HttpStatusCode.Unauthorized, compliance.StatusCode);
    }

    // ===== أدوات مساعدة =====

    // مسمّى وظيفي أسبوعي (Code غير مبيعات) + قالب أساسي منشور مرتبط به ⇒ حامله متوقَّع منه تقرير أسبوعي.
    private async Task<Guid> CreateWeeklyReportingRoleAsync(HttpClient admin, string tag)
    {
        var roleId = await CreateJobRoleAsync(tag);
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
                new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, roleId, PeriodType.Weekly, TemplateClassification.Primary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return roleId;
    }

    private async Task<(Guid RoleId, Guid TemplateId, Guid FieldId)> CreateWeeklyTemplateWithTextFieldAsync(
        HttpClient admin, string tag)
    {
        var roleId = await CreateJobRoleAsync(tag);
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
                new CreateTemplateRequest($"قالب {Guid.NewGuid():N}", null, roleId, PeriodType.Weekly, TemplateClassification.Primary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
                new UpsertFieldRequest("ملاحظة", "note", FieldType.ShortText, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (roleId, created.Id, field!.Id);
    }

    private async Task<Guid> CreateJobRoleAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new JobRole { NameAr = $"دور {tag}", Code = $"{tag}_{Guid.NewGuid():N}".Substring(0, 18) };
        db.JobRoles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task<(Guid DeptId, Guid TeamId)> CreateDeptTeamAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dept = new Department { NameAr = $"إدارة {Guid.NewGuid():N}".Substring(0, 16) };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        var team = new Team { NameAr = $"فريق {Guid.NewGuid():N}".Substring(0, 16), DepartmentId = dept.Id };
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return (dept.Id, team.Id);
    }

    private async Task SetJobRoleAsync(Guid userId, Guid? jobRoleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRoleId;
        await db.SaveChangesAsync();
    }

    private async Task SetActiveAsync(Guid userId, bool active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.IsActive = active;
        await db.SaveChangesAsync();
    }
}
