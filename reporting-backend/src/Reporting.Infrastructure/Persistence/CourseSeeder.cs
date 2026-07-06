using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Domain.Entities.Courses;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// بذر كتالوج الدورات الأولي (قابل للتعديل لاحقًا من شاشة الإدارة). idempotent بمطابقة الاسم (تجاهل الحالة).
/// إضافيّ بحت: لا يحذف/يعدّل القائم، ولا يمسّ أي تقرير/قالب. يعمل في كل البيئات عند الإقلاع.
/// </summary>
public static class CourseSeeder
{
    private record CourseDef(string NameAr, int SortOrder);

    private static readonly CourseDef[] Defs =
    {
        new("الدبلوم الشامل", 10),
        new("الحملات المتقدمة", 20),
        new("Google Ads", 30),
        new("SEO", 40),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();

        var existing = (await db.Courses.Select(c => c.NameAr).ToListAsync())
            .Select(n => n.Trim().ToLowerInvariant()).ToHashSet();

        foreach (var def in Defs)
        {
            if (existing.Contains(def.NameAr.Trim().ToLowerInvariant())) continue;
            db.Courses.Add(new Course { NameAr = def.NameAr, SortOrder = def.SortOrder, IsActive = true });
        }

        await db.SaveChangesAsync();
    }
}
