namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب «تقرير المحتوى Content Production» (ERDS Phase 3 — تعميم رقمي متوازٍ).
/// قالب مُهيكَل يستقبل بيانات رقمية قابلة للتجميع لاحقًا في صورة جدول (صفّ لكل عميل) + حقول نصية داعمة.
/// يستخدمه TemplateSeeder (لإنشاء القالب) والاختبارات (للتحقّق من الأعمدة بالاسم) — حتى لا تتباعد الأسماء.
/// ملاحظة: أعمدة الجدول (TableGrid) خلايا نصّية حرّة على مستوى التخزين؛ الأنواع أدناه توثيقية للمراجعة والتجميع المستقبلي.
/// </summary>
public static class ContentProductionReportSchema
{
    public const string TemplateTitle = "✍️ تقرير المحتوى Content Production";
    public const string Description = "قالب مُهيكَل (ERDS Phase 3): جدول إنتاج المحتوى لكل عميل + حقول نصية داعمة.";

    public const string MainTableLabel = "إنتاج المحتوى لكل عميل";
    public const string ColClient = "العميل";                  // نص
    public const string ColPiecesRequired = "قطع محتوى مطلوبة"; // رقم
    public const string ColIdeas = "أفكار";                    // رقم
    public const string ColCaptions = "كابشنات";               // رقم
    public const string ColScripts = "سكربتات";                // رقم
    public const string ColReels = "Reels";                    // رقم
    public const string ColApproved = "معتمد";                 // رقم
    public const string ColLate = "متأخر";                     // رقم
    public const string ColLateReason = "سبب التأخير";         // نص (اختياري)
    // ERDS Phase 5.5 (Work Unit) — أعمدة مُلحقة بالنهاية للحفاظ على فهارس القراءة القديمة (التوافق الخلفي).
    public const string ColProject = "المشروع";                // نص (Work Unit)
    public const string ColWorkHours = "ساعات العمل";          // رقم عشري (Work Unit)
    public const string ColStatus = "الحالة";                  // نص/Select (اختياري)

    public static readonly string[] Columns =
    {
        ColClient, ColPiecesRequired, ColIdeas, ColCaptions, ColScripts,
        ColReels, ColApproved, ColLate, ColLateReason,
        ColProject, ColWorkHours, ColStatus,
    };

    public const string BestContent = "أفضل محتوى تم إنتاجه";
    public const string TopChallenges = "أهم التحديات";
    public const string SupportNeeded = "الدعم المطلوب";
    public const string Notes = "ملاحظات";
}
