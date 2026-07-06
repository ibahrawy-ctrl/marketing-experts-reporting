namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب «تقرير مبيعات B2C — بيانات جديدة/قديمة» (Phase 7 — فصل جودة البيانات).
/// قالب مُهيكَل بجدولين منفصلين: (1) أداء البيانات الجديدة New Leads، (2) أداء بيانات CRM القديمة Old CRM Data.
/// كلا الجدولين يستخدم منتقي «الدورة» من كتالوج الدورات (لا إدخال يدوي حرّ).
///
/// إضافة بحتة (additive): قالب جديد بعنوان مستقلّ ⇒ TemplateSeeder (idempotent-by-title) يُنشئه من جديد
/// دون المساس بقالب B2C أحادي الجدول القديم (B2cByCourseReportSchema)، الذي يبقى Legacy قابلًا للقراءة كما هو.
/// التقارير القديمة لا تُحوَّل تلقائيًّا. أعمدة الجدولين متطابقة بالبنية بالفهرس مع القالب القديم عدا التسميتين
/// في الفهرس 2 (New Leads / Old Leads Worked) والفهرس 4 (Qualified / Requalified) — ما يبسّط التحقّق والتجميع.
/// </summary>
public static class B2cNewOldReportSchema
{
    public const string TemplateTitle = "📊 تقرير مبيعات B2C — بيانات جديدة/قديمة";
    public const string Description = "قالب مُهيكَل (Phase 7): جدولان منفصلان لأداء البيانات الجديدة New Leads وبيانات CRM القديمة Old CRM Data + حقول نصية داعمة. الدورة من الكتالوج.";

    // ===== أعمدة مشتركة بالاسم بين الجدولين =====
    public const string ColCourse = "الدورة";           // نص (من كتالوج الدورات)
    public const string ColWorkHours = "ساعات العمل";    // رقم عشري
    public const string ColContacted = "Contacted";      // رقم
    public const string ColFollowUps = "Follow-ups";     // رقم
    public const string ColSales = "Sales";              // رقم (صفقات مغلقة)
    public const string ColRevenue = "Revenue";          // عملة/عشري
    public const string ColLost = "Lost";                // رقم (فرص ضائعة)
    public const string ColLostReason = "Lost Reason";   // نص (سبب الضياع)

    // ===== جدول 1: أداء البيانات الجديدة New Leads =====
    public const string NewLeadsTableLabel = "أداء البيانات الجديدة New Leads";
    public const string ColNewLeads = "New Leads";       // رقم (Leads جديدة)
    public const string ColQualified = "Qualified";      // رقم

    // Part 7.1: يتوقّف الجدول عند Revenue — لا Lost ولا Lost Reason (الثابتان مُبقيان لأنّ AccumBucket يقرأهما بالاسم؛ Array.IndexOf سيُرجِع -1 بأمان).
    public static readonly string[] NewLeadsColumns =
    {
        ColCourse, ColWorkHours, ColNewLeads, ColContacted, ColQualified,
        ColFollowUps, ColSales, ColRevenue,
    };

    // ===== جدول 2: أداء بيانات CRM القديمة Old CRM Data =====
    public const string OldCrmTableLabel = "أداء بيانات CRM القديمة Old CRM Data";
    public const string ColOldLeadsWorked = "Old Leads Worked"; // رقم (بيانات قديمة تمّت معالجتها)
    public const string ColRequalified = "Requalified";         // رقم

    // Part 7.1: يتوقّف الجدول عند Revenue — لا Lost ولا Lost Reason.
    public static readonly string[] OldCrmColumns =
    {
        ColCourse, ColWorkHours, ColOldLeadsWorked, ColContacted, ColRequalified,
        ColFollowUps, ColSales, ColRevenue,
    };

    // حقول نصية داعمة (لا تحلّ محلّ الأرقام).
    public const string TopAchievements = "أهم 3 إنجازات";
    public const string TopChallenges = "أهم 3 تحديات";
    public const string SupportNeeded = "الدعم المطلوب";
    public const string ExceptionalNotes = "ملاحظات استثنائية";
}
