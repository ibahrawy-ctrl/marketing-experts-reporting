using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Auth;
using Reporting.Application.Common;
using Reporting.Application.Directory;
using Reporting.Application.Security;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// مسار منح/إلغاء مفاتيح الصلاحيّات الدقيقة (<c>perm</c>) عبر المنتج نفسه.
///
/// <para>
/// السبب الجذريّ لتعطّل 8 سيناريوهات في UAT: المفاتيح تُقرأ من <c>AspNetUserClaims</c> في
/// <c>AuthService</c> وتُحقن في الرمز، لكن **لم يكن هناك أيّ سطح منتج يكتبها** — الكتابة الوحيدة كانت
/// في مساعدات الاختبار. النتيجة: <c>AspNetUserClaims = 0</c> على TEST و<c>permissions: []</c> للجميع.
/// </para>
/// <para>
/// المبدأ المحكوم (<c>AppPermissions.cs:6-9</c>): لا يكتسب أيّ دور هذه المفاتيح ضمنًا — **ولا Admin**.
/// لذلك لا يوجد أيّ ربط دور↔مفتاح، والمنح فرديّ صريح ومُدقَّق وقابل للإلغاء.
/// </para>
/// </summary>
/// <para>
/// يعمل على مصنع المرحلة الثانية لا على المصنع المشترك لسبب واحد: سطح <c>hr-operations</c> محكوم بعلم
/// <c>Phase2:HrOperationsEnabled</c> الذي يعيد 404 وهو مطفأ، فيبتلع **مسار السماح** ويجعل إثبات المنح
/// مستحيلًا. رفع العلم ليس تفويضًا — فحوص <c>perm</c> تعمل كاملة تحته، وحالات المنع في هذا الملفّ نفسه
/// هي البرهان (المنع يقع في طبقة التخويل قبل بلوغ العلم، فيبقى 403 لا 404).
/// </para>
[Collection("Phase2")]
public class PermissionGrantPathTests
{
    private const string TestPassword = "Passw0rd#1";
    private readonly Phase2WebApplicationFactory _factory;

    public PermissionGrantPathTests(Phase2WebApplicationFactory factory) => _factory = factory;

    /// <summary>يعيد تسجيل الدخول لالتقاط رمز جديد — المفاتيح محقونة في JWT فلا تسري إلّا بعده.</summary>
    private async Task<HttpClient> ReLoginAsync(Guid userId)
    {
        string email;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            email = await db.Users.Where(u => u.Id == userId).Select(u => u.Email!).FirstAsync();
        }

        var client = _factory.CreateClient();
        var auth = await (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, TestPassword))).Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> SetPermissionsAsync(
        HttpClient actor, Guid targetUserId, params string[] permissions) =>
        actor.PutAsJsonAsync($"/api/directory/users/{targetUserId}/permissions",
            new SetUserPermissionsRequest(permissions.ToList()));

    private static async Task<string[]> ReadMePermissionsAsync(HttpClient client)
    {
        var me = await (await client.GetAsync("/api/auth/me")).ReadAsync<MeResponse>();
        return me!.Permissions!.ToArray();
    }

    [Fact]
    public async Task NoRole_IncludingAdmin_GetsHrOperationsKeysImplicitly()
    {
        // AppPermissions.cs:6-9 حرفيًّا: «حتّى Admin لا يكتسبها ضمنًا». هذا حارس ضدّ انزلاق مستقبليّ
        // نحو ربط دور↔مفتاح، وهو ما يمنعه أمر المعالجة صراحةً.
        foreach (var role in new[] { Roles.Admin, Roles.Ceo, Roles.GeneralManager, Roles.Hr, Roles.Manager, Roles.Employee })
        {
            var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
            Assert.Empty(await ReadMePermissionsAsync(client));

            var res = await client.GetAsync("/api/hr-operations/dashboard");
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
    }

    [Fact]
    public async Task GrantThenReLogin_UnlocksHrOperations_AndRevokeRelocksIt()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var (target, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);

        // قبل المنح: محجوب.
        Assert.Equal(HttpStatusCode.Forbidden, (await target.GetAsync("/api/hr-operations/dashboard")).StatusCode);

        var grant = await SetPermissionsAsync(admin, targetId, AppPermissions.HrOperationsView);
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);

        // الرمز القديم لا يزال بلا المفتاح — المطالبات محقونة في JWT وقت الإصدار.
        Assert.Equal(HttpStatusCode.Forbidden, (await target.GetAsync("/api/hr-operations/dashboard")).StatusCode);

        var refreshed = await ReLoginAsync(targetId);
        Assert.Equal(new[] { AppPermissions.HrOperationsView }, await ReadMePermissionsAsync(refreshed));
        Assert.Equal(HttpStatusCode.OK, (await refreshed.GetAsync("/api/hr-operations/dashboard")).StatusCode);

        // الإلغاء = إرسال المجموعة النهائيّة بلا المفتاح.
        Assert.Equal(HttpStatusCode.OK, (await SetPermissionsAsync(admin, targetId)).StatusCode);

        var afterRevoke = await ReLoginAsync(targetId);
        Assert.Empty(await ReadMePermissionsAsync(afterRevoke));
        Assert.Equal(HttpStatusCode.Forbidden, (await afterRevoke.GetAsync("/api/hr-operations/dashboard")).StatusCode);
    }

    [Fact]
    public async Task ViewKey_DoesNotGrantExportKey()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);

        await SetPermissionsAsync(admin, targetId, AppPermissions.HrOperationsView);
        var client = await ReLoginAsync(targetId);

        // فصل الواجبات: التصدير مفتاح مستقلّ تمامًا عن الرؤية.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/hr-operations/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/hr-operations/queues/probation/export")).StatusCode);
    }

    [Fact]
    public async Task UnknownPermissionKey_IsRejected_AndNothingIsPersisted()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await SetPermissionsAsync(admin, targetId, "Totally.Made.Up.Key");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("user.permissions.invalid", await res.Content.ReadAsStringAsync());

        var after = await (await admin.GetAsync($"/api/directory/users/{targetId}/permissions"))
            .ReadAsync<UserPermissionsDto>();
        Assert.Empty(after!.Permissions);
    }

    [Fact]
    public async Task SettingTheSameSet_IsIdempotent_AndWritesNoAuditEvent()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);

        await SetPermissionsAsync(admin, targetId, AppPermissions.HrOperationsView);
        var auditsAfterFirst = await CountPermissionAuditsAsync(targetId);

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK,
                (await SetPermissionsAsync(admin, targetId, AppPermissions.HrOperationsView)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // صفّ واحد فقط للمطالبة مهما تكرّر النداء، ولا سجلّ تدقيق زائف عن «تغيير» لم يحدث.
        Assert.Equal(1, await db.UserClaims.CountAsync(
            c => c.UserId == targetId && c.ClaimType == AppPermissions.ClaimType
                 && c.ClaimValue == AppPermissions.HrOperationsView));
        Assert.Equal(auditsAfterFirst, await CountPermissionAuditsAsync(targetId));
    }

    [Fact]
    public async Task GrantAndRevoke_AreAudited_WithActorAndBeforeAfter()
    {
        var (admin, adminId) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Hr);

        await SetPermissionsAsync(admin, targetId, AppPermissions.HrOperationsView, AppPermissions.AttendanceReview);
        await SetPermissionsAsync(admin, targetId, AppPermissions.HrOperationsView);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await db.AuditLogs
            .Where(a => a.Action == "user.permissions.changed" && a.EntityId == targetId)
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, logs.Count);
        Assert.All(logs, l => Assert.Equal(adminId, l.ActorId));
        Assert.Contains(AppPermissions.AttendanceReview, logs[0].DataJson);
        Assert.Contains("\"revoked\": [\"" + AppPermissions.AttendanceReview + "\"]", logs[1].DataJson);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("HR")]
    [InlineData("TeamLeader")]
    [InlineData("Employee")]
    [InlineData("GeneralManager")]
    public async Task NonUserManagers_CannotGrantPermissions_NorEscalateThemselves(string role)
    {
        var (actor, actorId) = await TestAuth.CreateUserAsync(_factory, role);

        // لا على غيره ولا على نفسه: سلطة المنح محصورة في Policies.UserManagement (Admin + CEO).
        var (_, victimId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SetPermissionsAsync(actor, victimId, AppPermissions.HrOperationsView)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SetPermissionsAsync(actor, actorId, AppPermissions.HrOperationsView)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await actor.GetAsync($"/api/directory/users/{victimId}/permissions")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.UserClaims.CountAsync(
            c => (c.UserId == actorId || c.UserId == victimId) && c.ClaimType == AppPermissions.ClaimType));
    }

    [Fact]
    public async Task Anonymous_CannotReadOrWritePermissions()
    {
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync($"/api/directory/users/{targetId}/permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SetPermissionsAsync(anon, targetId, AppPermissions.HrOperationsView)).StatusCode);
    }

    [Fact]
    public async Task GrantingToUnknownUser_Returns404_WithoutLeakingExistence()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var ghost = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await SetPermissionsAsync(admin, ghost, AppPermissions.HrOperationsView)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/directory/users/{ghost}/permissions")).StatusCode);
    }

    [Fact]
    public async Task MultiRoleUser_GetsUnionOfExplicitKeysOnly_NoRoleDerivedKeys()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, Roles.Admin);
        var (_, targetId) = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);

        // دور مركّب (قائد فريق + موارد بشريّة) كحالة `multi` في UAT.
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(
            $"/api/directory/users/{targetId}/roles",
            new UpdateUserRolesRequest(new List<string> { Roles.TeamLeader, Roles.Hr }))).StatusCode);

        var beforeGrant = await ReLoginAsync(targetId);
        // تعدّد الأدوار لا يولّد مفتاحًا واحدًا.
        Assert.Empty(await ReadMePermissionsAsync(beforeGrant));

        await SetPermissionsAsync(admin, targetId, AppPermissions.AttendanceReview, AppPermissions.HrOperationsView);
        var afterGrant = await ReLoginAsync(targetId);

        var perms = await ReadMePermissionsAsync(afterGrant);
        Assert.Equal(2, perms.Length);
        Assert.Contains(AppPermissions.AttendanceReview, perms);
        Assert.Contains(AppPermissions.HrOperationsView, perms);
    }

    private async Task<int> CountPermissionAuditsAsync(Guid targetId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AuditLogs.CountAsync(a => a.Action == "user.permissions.changed" && a.EntityId == targetId);
    }
}
