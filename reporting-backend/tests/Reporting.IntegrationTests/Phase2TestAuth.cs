using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Auth;
using Reporting.Application.Security;
using Reporting.Infrastructure.Identity;

namespace Reporting.IntegrationTests;

/// <summary>
/// أدوات المرحلة الثانية لإنشاء مستخدمين يحملون **أذونات دقيقة** (<c>perm</c> claims).
/// الأذونات تُمنَح لمستخدم اختباريّ مؤقّت فقط — لا تُمنَح لأيّ دور مخزَّن ولا تُغيَّر أيّ صلاحيّة قائمة (§6/P2-HR-009).
/// </summary>
public static class Phase2TestAuth
{
    public static async Task<(HttpClient Client, Guid UserId)> CreateUserAsync(
        Phase2WebApplicationFactory factory,
        string role,
        Guid? managerId = null,
        Guid? teamId = null,
        Guid? departmentId = null,
        params string[] permissions)
    {
        var email = $"p2-{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
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
                FullName = $"مستخدم مرحلة 2 {role}",
                IsActive = true,
                ManagerId = managerId,
                TeamId = teamId,
                DepartmentId = departmentId
            };
            await users.CreateAsync(user, password);
            await users.AddToRoleAsync(user, role);
            foreach (var permission in permissions.Distinct())
                await users.AddClaimAsync(user, new Claim(AppPermissions.ClaimType, permission));
            userId = user.Id;
        }

        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, userId);
    }
}
