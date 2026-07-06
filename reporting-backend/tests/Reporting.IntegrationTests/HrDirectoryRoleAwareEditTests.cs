using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Common;
using Reporting.Application.Directory;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// «دليل الموارد البشرية» — الفرق بين Admin و HR/CeoSupport في تعديل الحسابات الحسّاسة (إصلاح Bug ما بعد النشر):
/// • HR/CeoSupport: CanEdit=false للحسّاس، ويُرفض تعديله (403) — لم يتغيّر.
/// • Admin: CanEdit=true لكل الحسابات (بما فيها الحسّاسة)، ويستطيع تعديل الاسم/التنظيم من هذا السطح (الأدوار/كلمة المرور/التعطيل تبقى في إدارة المستخدمين).
/// لا تغيير على ScopeResolver/Workflow/الهجرات؛ لا توسيع لصلاحيات HR.
/// </summary>
[Collection("Integration")]
public class HrDirectoryRoleAwareEditTests
{
    private readonly CustomWebApplicationFactory _factory;

    public HrDirectoryRoleAwareEditTests(CustomWebApplicationFactory factory) => _factory = factory;

    // (1) Admin يرى الحساب الحسّاس (CEO) مع IsSensitive=true لكن CanEdit=true (ليس Locked).
    [Fact]
    public async Task Admin_HrUsers_SensitiveAccount_IsSensitiveTrue_ButCanEditTrue()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);

        var users = await (await admin.GetAsync("/api/directory/hr/users")).ReadAsync<List<HrDirectoryUserDto>>();
        var row = users!.Single(u => u.Id == ceoId);
        Assert.True(row.IsSensitive);
        Assert.True(row.CanEdit);
    }

    // (2) Admin يستطيع تعديل الاسم لحساب حسّاس (CEO) من سطح HR — 200.
    [Fact]
    public async Task Admin_CanEditBasic_OfSensitiveAccount_200()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);

        var res = await admin.PatchAsJsonAsync($"/api/directory/users/{ceoId}/basic",
            new UpdateUserBasicRequest("اسم محدّث من الأدمن"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // (3) Admin يستطيع تعديل التنظيم الوظيفي لحساب حسّاس (CEO) — 200.
    [Fact]
    public async Task Admin_CanEditOrgAssignment_OfSensitiveAccount_200()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);

        var res = await admin.PatchAsJsonAsync($"/api/directory/users/{ceoId}/org-assignment",
            new UpdateUserOrgAssignmentRequest(null, null, managerId));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // (4) Admin يستطيع تعديل الموظف العادي أيضًا (CanEdit=true) — لم يتغيّر.
    [Fact]
    public async Task Admin_NormalEmployee_CanEditTrue()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var users = await (await admin.GetAsync("/api/directory/hr/users")).ReadAsync<List<HrDirectoryUserDto>>();
        var row = users!.Single(u => u.Id == empId);
        Assert.False(row.IsSensitive);
        Assert.True(row.CanEdit);
    }

    // (5) HR لا يأخذ تجاوز الأدمن: الحساب الحسّاس يبقى CanEdit=false ويُرفض تعديله (403).
    [Fact]
    public async Task Hr_DoesNotGetAdminBypass_SensitiveLocked_AndForbidden()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, gmId) = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);

        var users = await (await hr.GetAsync("/api/directory/hr/users")).ReadAsync<List<HrDirectoryUserDto>>();
        var row = users!.Single(u => u.Id == gmId);
        Assert.True(row.IsSensitive);
        Assert.False(row.CanEdit);

        var basic = await hr.PatchAsJsonAsync($"/api/directory/users/{gmId}/basic",
            new UpdateUserBasicRequest("اسم"));
        Assert.Equal(HttpStatusCode.Forbidden, basic.StatusCode);

        var org = await hr.PatchAsJsonAsync($"/api/directory/users/{gmId}/org-assignment",
            new UpdateUserOrgAssignmentRequest(null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, org.StatusCode);
    }

    // (6) CeoSupport (غير أدمن، ضمن سياسات HR) لا يأخذ تجاوز الأدمن: تعديل الحسّاس يُرفض (403).
    [Fact]
    public async Task CeoSupport_DoesNotGetAdminBypass_SensitiveForbidden()
    {
        var (cs, _) = await TestAuth.CreateUserAsync(_factory, Roles.CeoSupport);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);

        var users = await (await cs.GetAsync("/api/directory/hr/users")).ReadAsync<List<HrDirectoryUserDto>>();
        var row = users!.Single(u => u.Id == ceoId);
        Assert.True(row.IsSensitive);
        Assert.False(row.CanEdit);

        var basic = await cs.PatchAsJsonAsync($"/api/directory/users/{ceoId}/basic",
            new UpdateUserBasicRequest("اسم"));
        Assert.Equal(HttpStatusCode.Forbidden, basic.StatusCode);
    }

    // (7) HR يبقى قادرًا على تعديل الموظف العادي (لا تراجع في وظيفة HR) — 200.
    [Fact]
    public async Task Hr_CanStillEditNormalEmployee_200()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await hr.PatchAsJsonAsync($"/api/directory/users/{empId}/basic",
            new UpdateUserBasicRequest("اسم موظف محدّث"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
