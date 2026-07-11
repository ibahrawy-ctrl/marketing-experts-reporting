namespace Reporting.Application.Common;

/// <summary>
/// RC-4 Task 4 (Path A) — عناوين قوالب التنفيذ Project-First والمفاتيح الرقمية القياسية داخل ProjectRepeatableSection.
/// مصدر الحقيقة الموحّد بين البذر (TemplateSeeder v2) ومحرّك التجميع الجديد (ProjectFirstExecutionAggregationService).
/// كل الأرقام التشغيلية تُخزَّن داخل كل مشروع في answers لقسم المشاريع المتكرّر (لا top-level).
/// </summary>
public static class ProjectFirstExecutionSchema
{
    public const string ContentTitle = "تقرير كاتب المحتوى الأسبوعي";
    public const string DesignTitle = "تقرير فريق التصميم";
    public const string VideoTitle = "تقرير فريق الفيديو";
    public const string ModerationTitle = "تقرير المديرشن الأسبوعي";

    /// <summary>عناوين قوالب التنفيذ الأربعة التي نُقلت أرقامها داخل المشاريع (Path A).</summary>
    public static readonly string[] ExecutionTemplateTitles = { ContentTitle, DesignTitle, VideoTitle, ModerationTitle };

    // مفاتيح الإنتاج (محتوى/تصميم/فيديو) — كلها داخل المشروع.
    public const string KeyPlanned = "planned";
    public const string KeyCompleted = "completed";
    public const string KeyApproved = "approved";
    public const string KeyRevisions = "revisions";
    public const string KeyPublished = "published";
    public const string KeyDelayed = "delayed";

    // مفاتيح المديرشن — داخل المشروع.
    public const string KeyMessagesIn = "messages_in";
    public const string KeyResponses = "responses";
    public const string KeyIssueComments = "issue_comments_count";
    public const string KeyEscalations = "escalations";
}
