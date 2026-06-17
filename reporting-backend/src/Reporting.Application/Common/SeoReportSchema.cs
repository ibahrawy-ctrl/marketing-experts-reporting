namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قوالب SEO وحقولها الرقمية/النصية القابلة للتجميع (Rollup). Business-1C.
/// يعتمد على القوالب الموجودة مسبقًا في TemplateSeeder (لا قالب جديد):
///   - «🔍 تقرير فريق SEO» (أخصائي SEO): الكلمات/المهام/المشاكل/الفهرسة/Organic.
///   - «تقرير متابعة مقالات SEO الأسبوعي»: أعداد المقالات.
/// التجميع يقرأ القيم بالاسم حتى لا تتباعد الأسماء عن الـ Seeder. لا تكامل خارجي (GSC/GA) في هذه المرحلة.
/// </summary>
public static class SeoReportSchema
{
    // القالبان المصدر للتجميع + قالب القائد + عنوان قالب الـ KPI.
    public const string TeamTemplateTitle = "🔍 تقرير فريق SEO";
    public const string ArticlesTemplateTitle = "تقرير متابعة مقالات SEO الأسبوعي";
    public const string LeaderTemplateTitle = "تقرير قائد فريق SEO";
    public const string KpiTitle = "مؤشرات أداء SEO";

    // ===== حقول «🔍 تقرير فريق SEO» الرقمية (ValueNumber) — تُجمَّع =====
    public const string ImprovedKeywords = "كلمات تحسّنت";
    public const string DeclinedKeywords = "كلمات تراجعت";
    public const string OrganicTraffic = "Organic Traffic";   // يدوي — يُعرض ويُجمَّع لكنه ليس مصدرًا آليًا دقيقًا
    public const string IndexedPages = "الصفحات المفهرسة";
    public const string TasksDone = "المهام المنفّذة";
    public const string TechnicalIssues = "المشاكل التقنية";

    // ===== حقول «تقرير متابعة مقالات SEO الأسبوعي» الرقمية — تُجمَّع =====
    public const string ArticlesPlanned = "عدد المقالات المخطط لها";
    public const string ArticlesPublished = "عدد المقالات المنشورة";
    public const string ArticlesLate = "عدد المقالات المتأخرة";

    // ===== حقول نصية (سياق فقط، لا تُجمَّع رقميًا) =====
    public const string DecisionsNeeded = "قرارات مطلوبة";   // من قالب الفريق
    public const string Recommendations = "اقتراحات";          // من قالب المقالات
}
