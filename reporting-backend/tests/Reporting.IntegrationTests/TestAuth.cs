using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Auth;
using Reporting.Infrastructure.Identity;

namespace Reporting.IntegrationTests;

/// <summary>أدوات مساعدة لإنشاء مستخدمين وتسجيل الدخول في اختبارات التكامل.</summary>
public static class TestAuth
{
    public static async Task<HttpClient> LoginAsAdminAsync(CustomWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin@marketingexperts.local", "Admin#12345"));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    public static async Task<HttpClient> LoginAsRoleAsync(CustomWebApplicationFactory factory, string role)
        => (await CreateUserAsync(factory, role)).Client;

    /// <summary>ينشئ مستخدمًا بدور (واختياريًا مديرًا مباشرًا) ويعيد عميلًا مسجّل الدخول مع معرّف المستخدم.</summary>
    public static async Task<(HttpClient Client, Guid UserId)> CreateUserAsync(
        CustomWebApplicationFactory factory, string role, Guid? managerId = null)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
        const string password = "Passw0rd#1";
        Guid userId;

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = $"مستخدم {role}",
                IsActive = true,
                ManagerId = managerId
            };
            await users.CreateAsync(user, password);
            await users.AddToRoleAsync(user, role);
            userId = user.Id;
        }

        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, userId);
    }
}
