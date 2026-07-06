using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reporting.Application.Common;

namespace Reporting.Infrastructure.Identity;

/// <summary>تهيئة الأدوار الثمانية + مستخدم مدير أولي عند الإقلاع.</summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var env = services.GetRequiredService<IHostEnvironment>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole
                {
                    Name = role,
                    DisplayNameAr = Roles.DisplayAr(role)
                });
            }
        }

        // أمان: في الإنتاج لا يُنشأ مدير نظام افتراضي ولا تُستخدم قيم افتراضية للبريد/كلمة المرور
        // إطلاقًا. لا يُبذَر مدير إلا بقيم صريحة وآمنة عبر متغيّرات البيئة (Seed:AdminEmail و
        // Seed:AdminPassword). خارج الإنتاج (Development/Testing) تبقى القيم الافتراضية لتسهيل
        // الاختبارات والتطوير. هذا الفرع لا يمسّ حسابًا قائمًا — يُنشئ فقط عند الغياب.
        var configuredEmail = config["Seed:AdminEmail"];
        var configuredPassword = config["Seed:AdminPassword"];

        var adminEmail = string.IsNullOrWhiteSpace(configuredEmail)
            ? (env.IsProduction() ? null : "admin@marketingexperts.local")
            : configuredEmail;
        var adminPassword = string.IsNullOrWhiteSpace(configuredPassword)
            ? (env.IsProduction() ? null : "Admin#12345")
            : configuredPassword;

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            // الإنتاج بلا بذور صريحة: لا تُنشئ مدير نظام افتراضيًا (يُنشأ المديرون يدويًا).
            logger.LogWarning(
                "IdentitySeeder: تخطّي إنشاء مدير نظام افتراضي في {Environment} لعدم توفّر Seed:AdminEmail/Seed:AdminPassword صريحة.",
                env.EnvironmentName);
            return;
        }

        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "مدير النظام",
                IsActive = true
            };
            var created = await userManager.CreateAsync(admin, adminPassword);
            if (created.Succeeded)
                await userManager.AddToRoleAsync(admin, Roles.Admin);
        }
    }
}
