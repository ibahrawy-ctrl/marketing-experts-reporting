using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Directory;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// «دليل الموارد البشرية» المخصّص (قراءة فقط لحزمة HR A) — المسارات الجديدة:
/// GET /api/directory/hr/users | hr/departments | hr/teams | hr/managers، محكومة بسياسة HrDirectoryRead.
/// منفصلة تمامًا عن الدليل العام (لا تغيّر سلوكه ولا تفتحه لـ HR) ولا تستدعي ScopeResolver (قراءة على مستوى الشركة).
/// الحسابات الحسّاسة تظهر مع IsSensitive=true/CanEdit=false ولا يمكن تعديلها (الفرض النهائي في الخدمة).
/// </summary>
[Collection("Integration")]
public class HrDirectoryReadTests
{
    private readonly CustomWebApplicationFactory _factory;

    public HrDirectoryReadTests(CustomWebApplicationFactory factory) => _factory = factory;

    // (1) HR يستطيع قراءة المسارات الأربعة (200).
    [Fact]
    public async Task Hr_CanReadAllHrDirectoryEndpoints_200()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);

        foreach (var path in new[] { "hr/users", "hr/departments", "hr/teams", "hr/managers" })
        {
            var res = await hr.GetAsync($"/api/directory/{path}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
    }

    // (2) HR يرى الموظف العادي على مستوى الشركة عبر hr/users (لا فلترة نطاق) مع CanEdit=true.
    [Fact]
    public async Task Hr_HrUsers_ReturnsCompanyWideNormalEmployee_CanEditTrue()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var users = await (await hr.GetAsync("/api/directory/hr/users")).ReadAsync<List<HrDirectoryUserDto>>();
        Assert.NotNull(users);
        var row = users!.SingleOrDefault(u => u.Id == empId);
        Assert.NotNull(row);                 // مرئيّ على مستوى الشركة (لا نطاق «own»).
        Assert.False(row!.IsSensitive);
        Assert.True(row.CanEdit);
    }

    // (3) الحساب الحسّاس (CEO) يظهر في hr/users مع IsSensitive=true و CanEdit=false.
    [Fact]
    public async Task Hr_HrUsers_SensitiveAccount_FlaggedLockedNotEditable()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, ceoId) = await TestAuth.CreateUserAsync(_factory, Roles.Ceo);

        var users = await (await hr.GetAsync("/api/directory/hr/users")).ReadAsync<List<HrDirectoryUserDto>>();
        var row = users!.SingleOrDefault(u => u.Id == ceoId);
        Assert.NotNull(row);
        Assert.True(row!.IsSensitive);
        Assert.False(row.CanEdit);

        // وفعليًّا لا يمكن تعديله عبر سطح البيانات الأساسية (الفرض النهائي خادمًا) ⇒ 403.
        var edit = await hr.PatchAsJsonAsync($"/api/directory/users/{ceoId}/basic",
            new UpdateUserBasicRequest("اسم"));
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
    }

    // (4) السلوك العام لم يتغيّر: HR على /api/directory/users (العام) لا يرى موظفًا آخر (نطاق «own»)،
    //     بينما يراه على hr/users. أي لم نفتح الدليل العام لـ HR.
    [Fact]
    public async Task Hr_GeneralDirectory_StillScopeFiltered_NotCompanyWide()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, empId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var general = await (await hr.GetAsync("/api/directory/users")).ReadAsync<List<DirectoryUserDto>>();
        Assert.NotNull(general);
        Assert.DoesNotContain(general!, u => u.Id == empId); // الدليل العام ما زال مفلترًا بالنطاق.

        var hrUsers = await (await hr.GetAsync("/api/directory/hr/users")).ReadAsync<List<HrDirectoryUserDto>>();
        Assert.Contains(hrUsers!, u => u.Id == empId);        // الدليل المخصّص على مستوى الشركة.
    }

    // (5) hr/managers يُرجِع المديرين النشطين فقط (يستبعد غير النشط).
    [Fact]
    public async Task Hr_HrManagers_ExcludesInactiveUsers()
    {
        var (hr, _) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (_, activeMgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        var (_, inactiveMgrId) = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        await SetActiveAsync(inactiveMgrId, false);

        var managers = await (await hr.GetAsync("/api/directory/hr/managers")).ReadAsync<List<HrDirectoryUserDto>>();
        Assert.NotNull(managers);
        Assert.Contains(managers!, m => m.Id == activeMgrId);
        Assert.DoesNotContain(managers!, m => m.Id == inactiveMgrId);
        Assert.All(managers!, m => Assert.True(m.IsActive));
    }

    // (6) Admin/CeoSupport/GM/CEO يستطيعون أيضًا قراءة الدليل المخصّص (اتحاد الأدوار المخوّلة).
    [Theory]
    [InlineData(Roles.CeoSupport)]
    [InlineData(Roles.GeneralManager)]
    [InlineData(Roles.Ceo)]
    public async Task AuthorizedRoles_CanReadHrDirectory_200(string role)
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
        var res = await client.GetAsync("/api/directory/hr/users");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // (7) Manager/TeamLeader/Employee محجوبون عن المسارات الأربعة (403).
    [Fact]
    public async Task NonAuthorizedRoles_Forbidden_FromHrDirectory_403()
    {
        foreach (var role in new[] { Roles.Manager, Roles.TeamLeader, Roles.Employee })
        {
            var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
            foreach (var path in new[] { "hr/users", "hr/departments", "hr/teams", "hr/managers" })
            {
                var res = await client.GetAsync($"/api/directory/{path}");
                Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            }
        }
    }

    // (8) المستخدم غير المصادَق = 401 على كل المسارات الأربعة.
    [Fact]
    public async Task Anonymous_401_OnAllHrDirectoryEndpoints()
    {
        var client = _factory.CreateClient();
        foreach (var path in new[] { "hr/users", "hr/departments", "hr/teams", "hr/managers" })
        {
            var res = await client.GetAsync($"/api/directory/{path}");
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }
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
