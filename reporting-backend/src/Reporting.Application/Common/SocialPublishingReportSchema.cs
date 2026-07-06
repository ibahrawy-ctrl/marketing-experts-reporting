namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب «تقرير النشر والسوشيال ميديا Publishing/Social Media» (ERDS Phase 3 — تعميم رقمي متوازٍ).
/// قالب مُهيكَل يستقبل بيانات رقمية قابلة للتجميع لاحقًا في صورة جدول (صفّ لكل عميل) + حقول نصية داعمة.
/// يستخدمه TemplateSeeder (لإنشاء القالب) والاختبارات (للتحقّق من الأعمدة بالاسم) — حتى لا تتباعد الأسماء.
/// ملاحظة: أعمدة الجدول (TableGrid) خلايا نصّية حرّة على مستوى التخزين؛ الأنواع أدناه توثيقية للمراجعة والتجميع المستقبلي.
/// </summary>
public static class SocialPublishingReportSchema
{
    public const string TemplateTitle = "📣 تقرير النشر والسوشيال ميديا";
    public const string Description = "قالب مُهيكَل (ERDS Phase 3): جدول النشر والتفاعل لكل عميل + حقول نصية داعمة.";

    public const string MainTableLabel = "النشر والتفاعل لكل عميل";
    public const string ColClient = "العميل";                    // نص
    public const string ColPostsScheduled = "Posts Scheduled";   // رقم
    public const string ColPostsPublished = "Posts Published";   // رقم
    public const string ColStories = "Stories";                  // رقم
    public const string ColReels = "Reels";                      // رقم
    public const string ColMissedPosts = "Missed Posts";         // رقم
    public const string ColComments = "Comments";                // رقم
    public const string ColEngagementNotes = "Engagement Notes"; // نص (اختياري)
    // ERDS Phase 5.5 (Work Unit) — أعمدة مُلحقة بالنهاية للحفاظ على فهارس القراءة القديمة (التوافق الخلفي).
    public const string ColProject = "المشروع";                  // نص (Work Unit)
    public const string ColWorkHours = "ساعات العمل";            // رقم عشري (Work Unit)
    public const string ColStatus = "الحالة";                    // نص/Select (اختياري)

    public static readonly string[] Columns =
    {
        ColClient, ColPostsScheduled, ColPostsPublished, ColStories, ColReels,
        ColMissedPosts, ColComments, ColEngagementNotes,
        ColProject, ColWorkHours, ColStatus,
    };

    public const string PublishingConsistency = "انتظام النشر";
    public const string TopPublishingIssues = "أبرز مشاكل النشر";
    public const string SupportNeeded = "الدعم المطلوب";
    public const string Notes = "ملاحظات";
}
