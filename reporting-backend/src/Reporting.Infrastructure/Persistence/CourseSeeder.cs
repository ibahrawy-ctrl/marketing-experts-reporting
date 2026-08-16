using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Courses;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// بذر كتالوج الدورات الأولي (قابل للتعديل لاحقًا من شاشة الإدارة). idempotent بمطابقة مفتاح التجميع الموحّد.
/// إضافيّ بحت: لا يحذف/يعدّل القائم، لا يُعيد تفعيل مؤرشفًا، لا يمسّ أي تقرير/قالب. يعمل في كل البيئات عند الإقلاع.
///
/// COURSE-DUPLICATE-MERGE-R1: أُزيل الاسم القديم «الدبلوم الشامل» واستُبدل بالاسم الموحّد الرسميّ
/// (<see cref="CourseNamePolicy.CanonicalDigitalDiploma"/>). المطابقة الآن عبر مفتاح التجميع الموحّد
/// (لا التطابق النصّيّ الحرفيّ) ⇒ لا يُعيد إنشاء الاسم المهجور ولا ينشئ صفًّا ثالثًا حتى لو كان الناجي
/// يحمل حاليًّا الاسم الانتقاليّ ذا «ال» الزائدة — فأيّ اسمٍ بديلٍ قائمٍ يُطابَق كتغطية للدورة الموحّدة.
/// </summary>
public static class CourseSeeder
{
    private record CourseDef(string NameAr, int SortOrder);

    private static readonly CourseDef[] Defs =
    {
        new(CourseNamePolicy.CanonicalDigitalDiploma, 10),
        new("الحملات المتقدمة", 20),
        new("Google Ads", 30),
        new("SEO", 40),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();

        // مفاتيح التجميع الموحّدة للأسماء القائمة: كلّ الأسماء البديلة المعتمَدة تعود لمفتاح واحد
        // ⇒ وجود أيّ اسم بديل (الناجي بالاسم الانتقاليّ أو المكرَّر «الدبلوم الشامل») يمنع إضافة صفّ جديد.
        var existingKeys = (await db.Courses.Select(c => c.NameAr).ToListAsync())
            .Select(CourseNamePolicy.NormalizeForGrouping).ToHashSet();

        var added = false;
        foreach (var def in Defs)
        {
            if (existingKeys.Contains(CourseNamePolicy.NormalizeForGrouping(def.NameAr))) continue;
            db.Courses.Add(new Course { NameAr = def.NameAr, SortOrder = def.SortOrder, IsActive = true });
            added = true;
        }

        if (added) await db.SaveChangesAsync();
    }
}
