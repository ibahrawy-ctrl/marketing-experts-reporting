using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Reporting.Application.Auth;
using Xunit;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class AuthTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static LoginRequest AdminLogin => new("admin@marketingexperts.local", "Admin#12345");

    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsTokensAndAdminRole()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", AdminLogin);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Contains("Admin", body.Roles);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin@marketingexperts.local", "WrongPass#1"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_WithToken_ReturnsCurrentUser()
    {
        var client = _factory.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/auth/login", AdminLogin))
            .Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        var res = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var me = await res.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal("admin@marketingexperts.local", me!.Email);
        Assert.True(me.IsActive);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndOldTokenIsRevoked()
    {
        var client = _factory.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/auth/login", AdminLogin))
            .Content.ReadFromJsonAsync<AuthResponse>();

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login!.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        // الرمز القديم أُبطل: محاولة إعادة استخدامه تفشل.
        var reused = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }
}
