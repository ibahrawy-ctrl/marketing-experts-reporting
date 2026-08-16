using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Auth;
using Reporting.Domain.Entities.Org;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;

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

    /// <summary>
    /// ينشئ مستخدمًا بدور Identity ومسمًّى وظيفيًّا بالرمز المحدّد (get-or-create للمسمّى حسب Code)،
    /// ثم يعيد عميلًا مسجّل الدخول مع معرّف المستخدم. تستخدمه اختبارات تجميع المبيعات:
    /// تقارير SALES_B2C/SALES_B2B تُفرَض «يومية» خادميًّا (ReportCadencePolicy)، فلا تُقبَل التسليمات
    /// اليومية إلا حين يحمل المُسلِّم مسمًّى برمز مبيعات مطابق.
    /// </summary>
    public static async Task<(HttpClient Client, Guid UserId)> CreateUserWithJobRoleCodeAsync(
        CustomWebApplicationFactory factory, string role, string jobRoleCode, Guid? managerId = null)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
        const string password = "Passw0rd#1";
        Guid userId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jobRole = await db.Set<JobRole>().FirstOrDefaultAsync(r => r.Code == jobRoleCode);
            if (jobRole is null)
            {
                jobRole = new JobRole { NameAr = jobRoleCode, Code = jobRoleCode, IsActive = true };
                db.Set<JobRole>().Add(jobRole);
                await db.SaveChangesAsync();
            }

            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = $"مستخدم {role}",
                IsActive = true,
                ManagerId = managerId,
                JobRoleId = jobRole.Id
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

    /// <summary>
    /// ينشئ فريقًا (داخل إدارة حاوية) بقائد فريق محدّد ويُسنِد الأعضاء إليه (TeamId).
    /// تستخدمه اختبارات T-WF2 التي تتطلّب خطوة قائد فريق فعليّة: قائد الفريق وحده يعتمد خطوة قائد الفريق.
    /// </summary>
    public static async Task<Guid> CreateTeamWithLeaderAsync(
        CustomWebApplicationFactory factory, Guid teamLeaderId, params Guid[] memberIds)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dept = new Department { NameAr = $"إدارة اختبار {Guid.NewGuid():N}", IsActive = true };
        db.Set<Department>().Add(dept);
        var team = new Team
        {
            NameAr = $"فريق اختبار {Guid.NewGuid():N}",
            DepartmentId = dept.Id,
            TeamLeaderId = teamLeaderId,
            IsActive = true
        };
        db.Set<Team>().Add(team);

        foreach (var mid in memberIds)
        {
            var u = await db.Users.FirstAsync(x => x.Id == mid);
            u.TeamId = team.Id;
        }
        await db.SaveChangesAsync();
        return team.Id;
    }

    /// <summary>
    /// يضبط علم تجاوز خطوة قائد الفريق (BypassTeamLeaderApproval) للمستخدم مباشرةً عبر AppDbContext.
    /// لا توجد نقطة نهاية API لهذا العلم؛ يُضبط بيانيًّا (كما يُضبط لفاطمة على الإنتاج عبر SQL محكوم).
    /// تستخدمه اختبارات FATMA-DIRECT-REPORTING-OVERRIDE-R1 لمحاكاة موظّف Direct Reporting.
    /// </summary>
    public static async Task SetBypassTeamLeaderApprovalAsync(
        CustomWebApplicationFactory factory, Guid userId, bool value = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = await db.Users.FirstAsync(x => x.Id == userId);
        u.BypassTeamLeaderApproval = value;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// يُضيف عضوية فريق إضافية (ثانوية) للمستخدم في الجدول <c>user_team_memberships</c> دون تغيير فريقه الأساسي.
    /// تستخدمه اختبارات OFFICIAL-LAUNCH-FIX-PACK-R1A للتحقّق من رؤية مشاريع الفرق الإضافية.
    /// </summary>
    public static async Task<Guid> AddExtraTeamMembershipAsync(
        CustomWebApplicationFactory factory, Guid userId, Guid teamId, bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = new UserTeamMembership
        {
            UserId = userId,
            TeamId = teamId,
            IsActive = isActive
        };
        db.Set<UserTeamMembership>().Add(membership);
        await db.SaveChangesAsync();
        return membership.Id;
    }

    /// <summary>
    /// ينشئ إدارة ويُسنِد المستخدمين المحدّدين إليها (DepartmentId).
    /// تستخدمه اختبارات GOV-DIRECTORY-SCOPE-FIX-R1 للتحقّق من نطاق HR (مستخدمو إدارته فقط).
    /// </summary>
    public static async Task<Guid> CreateDepartmentWithUsersAsync(
        CustomWebApplicationFactory factory, params Guid[] memberIds)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dept = new Department { NameAr = $"إدارة اختبار {Guid.NewGuid():N}", IsActive = true };
        db.Set<Department>().Add(dept);

        foreach (var mid in memberIds)
        {
            var u = await db.Users.FirstAsync(x => x.Id == mid);
            u.DepartmentId = dept.Id;
        }
        await db.SaveChangesAsync();
        return dept.Id;
    }
}
