using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Directory;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// GOV-R1 — صلاحيات إدارة المستخدمين للرئيس التنفيذي (CEO).
/// يؤكّد أن CEO يدير المستخدمين كاملًا (قراءة/إنشاء/تعديل/أدوار/حذف + إعادة تعيين كلمة المرور) عبر سياسة
/// UserManagement الجديدة (Admin + CEO) و UserPasswordReset الموسّعة (Admin + CEO + CeoSupport)؛ مع بقاء
/// GM/HR/FinanceManager/Accountant/Manager/Employee ممنوعين (403) — لا تسرّب للصلاحية. ويؤكّد بقاء الحُرّاس
/// الأمنية: حساب Admin لا تُعاد كلمته إلا بفاعل Admin (CEO ممنوع منه)، وأن CeoSupport يبقى كما هو
/// (يُعيد كلمات المرور فقط، ولا يُنشئ/يعدّل/يحذف ولا يبدّل الأدوار).
/// </summary>
[Collection("Integration")]
public class CeoUserManagementTests
{
    private readonly CustomWebApplicationFactory _factory;

    public CeoUserManagementTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string UsersUrl = "/api/directory/users";

    private static CreateUserRequest NewUser(IReadOnlyList<string>? roles = null) =>
        new($"gov-{Guid.NewGuid():N}@test.local", "مستخدم جديد", "Passw0rd#1",
            roles ?? new List<string> { "Employee" }, null, null, null);

    private async Task<Guid> NewTargetAsync()
    {
        var (_, id) = await TestAuth.CreateUserAsync(_factory, "Employee");
        return id;
    }

    // ===== CEO: إدارة كاملة =====

    [Fact]
    public async Task Ceo_ListUsers_200()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        Assert.Equal(HttpStatusCode.OK, (await ceo.GetAsync(UsersUrl)).StatusCode);
    }

    [Fact]
    public async Task Ceo_CreateUser_200()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var created = await (await ceo.PostAsJsonAsync(UsersUrl, NewUser())).ReadAsync<DirectoryUserDto>();
        Assert.NotNull(created);
        Assert.Contains("Employee", created!.Roles);
    }

    [Fact]
    public async Task Ceo_UpdateUser_200()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var targetId = await NewTargetAsync();
        var res = await ceo.PutAsJsonAsync($"{UsersUrl}/{targetId}",
            new UpdateUserRequest("اسم معدّل من CEO", $"ceoupd-{Guid.NewGuid():N}@test.local", true, null, null, null));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Ceo_UpdateRoles_200()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var targetId = await NewTargetAsync();
        var res = await ceo.PutAsJsonAsync($"{UsersUrl}/{targetId}/roles",
            new UpdateUserRolesRequest(new List<string> { "Manager" }));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Ceo_DeleteUser_200()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var targetId = await NewTargetAsync();
        Assert.Equal(HttpStatusCode.OK, (await ceo.DeleteAsync($"{UsersUrl}/{targetId}")).StatusCode);
    }

    [Fact]
    public async Task Ceo_ResetPassword_NormalUser_200()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var targetId = await NewTargetAsync();
        var res = await ceo.PostAsJsonAsync($"{UsersUrl}/{targetId}/reset-password",
            new ResetPasswordRequest("Ceo$Reset9X"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // الحارس الأمني: حساب Admin لا تُعاد كلمته إلا بفاعل Admin ⇒ CEO يُمنع (auth.forbidden / 403).
    [Fact]
    public async Task Ceo_ResetPassword_AdminAccount_403()
    {
        var (ceo, _) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var users = await (await ceo.GetAsync($"{UsersUrl}?includeInactive=true")).ReadAsync<List<DirectoryUserDto>>();
        var adminId = users!.Single(u => u.Email == "admin@marketingexperts.local").Id;
        var res = await ceo.PostAsJsonAsync($"{UsersUrl}/{adminId}/reset-password",
            new ResetPasswordRequest("CeoTryAdmin9X#"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== الأدوار غير المخوّلة تبقى ممنوعة من إدارة المستخدمين (403) =====

    [Theory]
    [InlineData("GeneralManager")]
    [InlineData("HR")]
    [InlineData("FinanceManager")]
    [InlineData("Accountant")]
    [InlineData("Manager")]
    [InlineData("Employee")]
    public async Task NonAuthorizedRole_CreateUser_403(string role)
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(UsersUrl, NewUser())).StatusCode);
    }

    [Theory]
    [InlineData("GeneralManager")]
    [InlineData("HR")]
    [InlineData("FinanceManager")]
    [InlineData("Accountant")]
    public async Task NonAuthorizedRole_UpdateRoles_403(string role)
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
        var targetId = await NewTargetAsync();
        var res = await client.PutAsJsonAsync($"{UsersUrl}/{targetId}/roles",
            new UpdateUserRolesRequest(new List<string> { "TeamLeader" }));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData("GeneralManager")]
    [InlineData("FinanceManager")]
    [InlineData("Accountant")]
    public async Task NonAuthorizedRole_UpdateUser_403(string role)
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
        var targetId = await NewTargetAsync();
        var res = await client.PutAsJsonAsync($"{UsersUrl}/{targetId}",
            new UpdateUserRequest("محاولة", $"x-{Guid.NewGuid():N}@test.local", true, null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData("GeneralManager")]
    [InlineData("FinanceManager")]
    [InlineData("Accountant")]
    public async Task NonAuthorizedRole_DeleteUser_403(string role)
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
        var targetId = await NewTargetAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{UsersUrl}/{targetId}")).StatusCode);
    }

    // المدير المالي / الحسابات / GM ممنوعون من إعادة تعيين كلمة المرور (ليسوا ضمن UserPasswordReset).
    [Theory]
    [InlineData("GeneralManager")]
    [InlineData("FinanceManager")]
    [InlineData("Accountant")]
    public async Task NonAuthorizedRole_ResetPassword_403(string role)
    {
        var (client, _) = await TestAuth.CreateUserAsync(_factory, role);
        var targetId = await NewTargetAsync();
        var res = await client.PostAsJsonAsync($"{UsersUrl}/{targetId}/reset-password",
            new ResetPasswordRequest("Whatever9X#z"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== CeoSupport يبقى كما هو: إعادة تعيين فقط، لا إدارة مستخدمين =====

    [Fact]
    public async Task CeoSupport_ResetPassword_StillSucceeds_200()
    {
        var (ceoSupport, _) = await TestAuth.CreateUserAsync(_factory, "CeoSupport");
        var targetId = await NewTargetAsync();
        var res = await ceoSupport.PostAsJsonAsync($"{UsersUrl}/{targetId}/reset-password",
            new ResetPasswordRequest("Ceo$upport9X"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task CeoSupport_CreateUser_StillForbidden_403()
    {
        var (ceoSupport, _) = await TestAuth.CreateUserAsync(_factory, "CeoSupport");
        Assert.Equal(HttpStatusCode.Forbidden, (await ceoSupport.PostAsJsonAsync(UsersUrl, NewUser())).StatusCode);
    }

    [Fact]
    public async Task CeoSupport_UpdateRoles_StillForbidden_403()
    {
        var (ceoSupport, _) = await TestAuth.CreateUserAsync(_factory, "CeoSupport");
        var targetId = await NewTargetAsync();
        var res = await ceoSupport.PutAsJsonAsync($"{UsersUrl}/{targetId}/roles",
            new UpdateUserRolesRequest(new List<string> { "Manager" }));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CeoSupport_DeleteUser_StillForbidden_403()
    {
        var (ceoSupport, _) = await TestAuth.CreateUserAsync(_factory, "CeoSupport");
        var targetId = await NewTargetAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await ceoSupport.DeleteAsync($"{UsersUrl}/{targetId}")).StatusCode);
    }

    // المجهول ممنوع من إدارة المستخدمين ⇒ 401.
    [Fact]
    public async Task Anonymous_UserManagement_401()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(UsersUrl, NewUser())).StatusCode);
    }
}
