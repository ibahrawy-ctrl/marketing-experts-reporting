namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب «تقرير الفيديو Video Production» (ERDS Phase 3 — تعميم رقمي متوازٍ).
/// قالب مُهيكَل يستقبل بيانات رقمية قابلة للتجميع لاحقًا في صورة جدول (صفّ لكل عميل) + حقول نصية داعمة.
/// يستخدمه TemplateSeeder (لإنشاء القالب) والاختبارات (للتحقّق من الأعمدة بالاسم) — حتى لا تتباعد الأسماء.
/// ملاحظة: أعمدة الجدول (TableGrid) خلايا نصّية حرّة على مستوى التخزين؛ الأنواع أدناه توثيقية للمراجعة والتجميع المستقبلي.
/// </summary>
public static class VideoProductionReportSchema
{
    public const string TemplateTitle = "🎬 تقرير الفيديو Video Production";
    public const string Description = "قالب مُهيكَل (ERDS Phase 3): جدول إنتاج الفيديو لكل عميل + حقول نصية داعمة.";

    public const string MainTableLabel = "إنتاج الفيديو لكل عميل";
    public const string ColClient = "العميل";              // نص
    public const string ColRequired = "فيديوهات مطلوبة";   // رقم
    public const string ColFilmed = "مصورة";               // رقم
    public const string ColEdited = "مونتاج";              // رقم
    public const string ColApproved = "معتمدة";            // رقم
    public const string ColPublished = "منشورة";           // رقم
    public const string ColLate = "متأخرة";                // رقم
    public const string ColLateReason = "سبب التأخير";     // نص (اختياري)
    // ERDS Phase 5.5 (Work Unit) — أعمدة مُلحقة بالنهاية للحفاظ على فهارس القراءة القديمة (التوافق الخلفي).
    public const string ColProject = "المشروع";            // نص (Work Unit)
    public const string ColWorkHours = "ساعات العمل";      // رقم عشري (Work Unit)
    public const string ColStatus = "الحالة";              // نص/Select (اختياري)

    public static readonly string[] Columns =
    {
        ColClient, ColRequired, ColFilmed, ColEdited, ColApproved,
        ColPublished, ColLate, ColLateReason,
        ColProject, ColWorkHours, ColStatus,
    };

    public const string BestVideos = "أفضل فيديوهات الأسبوع";
    public const string TopChallenges = "أبرز التحديات";
    public const string SupportNeeded = "الدعم المطلوب";
    public const string Notes = "ملاحظات";
}
