using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        var adminEmail = config["Seed:AdminEmail"] ?? "admin@marketingexperts.local";
        var adminPassword = config["Seed:AdminPassword"] ?? "Admin#12345";

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
