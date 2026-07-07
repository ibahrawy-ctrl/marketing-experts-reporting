using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Domain.Entities.Services;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// بذر كتالوج خدمات B2B الأولي (قابل للتعديل لاحقًا من شاشة الإدارة). idempotent بمطابقة الاسم (تجاهل الحالة).
/// إضافيّ بحت: لا يحذف/يعدّل القائم، ولا يمسّ أي تقرير/قالب. يعمل في كل البيئات عند الإقلاع.
/// </summary>
public static class ServiceSeeder
{
    private record ServiceDef(string NameAr, int SortOrder);

    private static readonly ServiceDef[] Defs =
    {
        new("تصميم موقع إلكتروني", 10),
        new("متجر إلكتروني", 20),
        new("تطبيق جوال", 30),
        new("إدارة سوشيال ميديا", 40),
        new("حملات إعلانية", 50),
        new("تحسين محركات البحث SEO", 60),
        new("هوية بصرية", 70),
        new("إنتاج محتوى", 80),
        new("فيديو / موشن جرافيك", 90),
        new("استشارات تسويقية", 100),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();

        var existing = (await db.Services.Select(s => s.NameAr).ToListAsync())
            .Select(n => n.Trim().ToLowerInvariant()).ToHashSet();

        foreach (var def in Defs)
        {
            if (existing.Contains(def.NameAr.Trim().ToLowerInvariant())) continue;
            db.Services.Add(new Service { NameAr = def.NameAr, SortOrder = def.SortOrder, IsActive = true });
        }

        await db.SaveChangesAsync();
    }
}
