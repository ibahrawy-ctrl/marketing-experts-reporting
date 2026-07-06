namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب «تقرير مبيعات B2C حسب الدورة» (ERDS Phase 1 — تجريبي).
/// قالب مُهيكَل يستقبل بيانات رقمية قابلة للتجميع لاحقًا في صورة جدول (صفّ لكل دورة) + حقول نصية داعمة.
/// يستخدمه TemplateSeeder (لإنشاء القالب) والاختبارات (للتحقّق من الأعمدة بالاسم) — حتى لا تتباعد الأسماء.
/// ملاحظة: أعمدة الجدول (TableGrid) خلايا نصّية حرّة على مستوى التخزين؛ الأنواع أدناه توثيقية للمراجعة والتجميع المستقبلي.
/// </summary>
public static class B2cByCourseReportSchema
{
    public const string TemplateTitle = "📊 تقرير مبيعات B2C حسب الدورة";
    public const string Description = "قالب تجريبي مُهيكَل (ERDS Phase 1): جدول أداء المبيعات لكل دورة + حقول نصية داعمة.";

    // اسم الجدول الرئيسي (TableGrid مطلوب) + أعمدته بالترتيب.
    public const string MainTableLabel = "أداء المبيعات لكل دورة";
    public const string ColCourse = "الدورة";          // نص (لا يوجد كتالوج دورات بعد)
    public const string ColWorkHours = "ساعات العمل";   // رقم عشري
    public const string ColLeads = "Leads";             // رقم
    public const string ColContacted = "Contacted";     // رقم
    public const string ColQualified = "Qualified";     // رقم
    public const string ColFollowUps = "Follow-ups";    // رقم
    public const string ColSales = "Sales";             // رقم (صفقات مغلقة)
    public const string ColRevenue = "Revenue";         // عملة/عشري
    public const string ColLost = "Lost";               // رقم (فرص ضائعة)
    public const string ColLostReason = "Lost Reason";  // نص (سبب الضياع)

    public static readonly string[] Columns =
    {
        ColCourse, ColWorkHours, ColLeads, ColContacted, ColQualified,
        ColFollowUps, ColSales, ColRevenue, ColLost, ColLostReason,
    };

    // حقول نصية داعمة بعد الجدول (لا تحلّ محلّ الأرقام).
    public const string TopAchievements = "أهم 3 إنجازات";
    public const string TopChallenges = "أهم 3 تحديات";
    public const string SupportNeeded = "الدعم المطلوب";
    public const string ExceptionalNotes = "ملاحظات استثنائية";
}
