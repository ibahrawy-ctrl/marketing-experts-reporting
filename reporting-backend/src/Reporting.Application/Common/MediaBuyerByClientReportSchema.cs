namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب «تقرير Media Buyer» (ERDS Phase 3 — تعميم رقمي متوازٍ).
/// قالب مُهيكَل يستقبل بيانات رقمية قابلة للتجميع لاحقًا في صورة جدول (صفّ لكل عميل) + حقول نصية داعمة.
/// يستخدمه TemplateSeeder (لإنشاء القالب) والاختبارات (للتحقّق من الأعمدة بالاسم) — حتى لا تتباعد الأسماء.
/// ملاحظة: أعمدة الجدول (TableGrid) خلايا نصّية حرّة على مستوى التخزين؛ الأنواع أدناه توثيقية للمراجعة والتجميع المستقبلي.
/// </summary>
public static class MediaBuyerByClientReportSchema
{
    public const string TemplateTitle = "📊 تقرير Media Buyer حسب العميل";
    public const string Description = "قالب مُهيكَل (ERDS Phase 3): جدول أداء الحملات لكل عميل + حقول نصية داعمة.";

    public const string MainTableLabel = "أداء الحملات لكل عميل";
    public const string ColClient = "العميل";        // نص
    public const string ColPlatform = "المنصة";      // نص/Select
    public const string ColSpend = "Spend";          // عملة
    public const string ColLeads = "Leads";          // رقم
    public const string ColCpl = "CPL";              // عملة (اختياري)
    public const string ColPurchases = "Purchases";  // رقم
    public const string ColCpa = "CPA";              // عملة (اختياري)
    public const string ColRevenue = "Revenue";      // عملة (اختياري)
    public const string ColRoas = "ROAS";            // رقم (اختياري)
    public const string ColNotes = "ملاحظات";        // نص (اختياري)
    // ERDS Phase 5.5 (Work Unit) — أعمدة مُلحقة بالنهاية للحفاظ على فهارس القراءة القديمة (التوافق الخلفي).
    public const string ColProject = "المشروع";      // نص (Work Unit)
    public const string ColWorkHours = "ساعات العمل"; // رقم عشري (Work Unit)
    public const string ColStatus = "الحالة";        // نص/Select (اختياري)

    public static readonly string[] Columns =
    {
        ColClient, ColPlatform, ColSpend, ColLeads, ColCpl,
        ColPurchases, ColCpa, ColRevenue, ColRoas, ColNotes,
        ColProject, ColWorkHours, ColStatus,
    };

    public const string BestCampaign = "أفضل حملة";
    public const string WeakestCampaign = "أضعف حملة";
    public const string ImprovementOrDeclineReasons = "أسباب التحسن أو الانخفاض";
    public const string SupportNeeded = "الدعم المطلوب";
}
