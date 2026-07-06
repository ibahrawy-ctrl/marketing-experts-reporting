namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب «تقرير المشاريع حسب العميل/المشروع» (ERDS Phase 3 — تعميم رقمي متوازٍ).
/// قالب مُهيكَل يستقبل بيانات رقمية قابلة للتجميع لاحقًا في صورة جدول (صفّ لكل مشروع) + حقول نصية داعمة.
/// يستخدمه TemplateSeeder (لإنشاء القالب) والاختبارات (للتحقّق من الأعمدة بالاسم) — حتى لا تتباعد الأسماء.
/// ملاحظة: أعمدة الجدول (TableGrid) خلايا نصّية حرّة على مستوى التخزين؛ الأنواع أدناه توثيقية للمراجعة والتجميع المستقبلي.
/// </summary>
public static class ProjectsByClientReportSchema
{
    public const string TemplateTitle = "🗂️ تقرير المشاريع حسب العميل/المشروع";
    public const string Description = "قالب مُهيكَل (ERDS Phase 3): جدول تقدّم المشاريع لكل عميل/مشروع + حقول نصية داعمة.";

    public const string MainTableLabel = "تقدّم المشاريع لكل عميل/مشروع";
    public const string ColClient = "العميل";               // نص
    public const string ColProject = "المشروع";             // نص
    public const string ColWorkHours = "ساعات العمل";       // رقم عشري
    public const string ColTasksPlanned = "المهام المخططة"; // رقم
    public const string ColTasksDone = "المهام المنجزة";    // رقم
    public const string ColTasksLate = "المهام المتأخرة";   // رقم
    public const string ColTasksBlocked = "المهام المتوقفة Blocked"; // رقم
    public const string ColProgressPct = "نسبة التقدم %";   // نسبة
    public const string ColRiskLevel = "مستوى الخطر";       // نص/Select
    public const string ColSupportNeeded = "الدعم المطلوب"; // نص (اختياري)
    // ERDS Phase 5.5 (Work Unit) — عمود الحالة مُلحق بالنهاية (Project + WorkHours موجودان أصلًا).
    public const string ColStatus = "الحالة";               // نص/Select (اختياري)

    public static readonly string[] Columns =
    {
        ColClient, ColProject, ColWorkHours, ColTasksPlanned, ColTasksDone,
        ColTasksLate, ColTasksBlocked, ColProgressPct, ColRiskLevel, ColSupportNeeded,
        ColStatus,
    };

    public const string TopAchievements = "أهم إنجازات المشروع";
    public const string TopObstacles = "أبرز العوائق";
    public const string DecisionsNeeded = "قرارات مطلوبة";
    public const string Notes = "ملاحظات";
}
