namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب فريق الفيديو وحقوله الرقمية/النصية القابلة للتجميع (Rollup). Business-1D-3.
/// يعتمد على القالب الموجود مسبقًا في TemplateSeeder (لا قالب جديد): «تقرير فريق الفيديو».
/// القالب مطابق بنيويًا لقالب التصميم: يحتوي حقلًا مباشرًا «أعيدت للتعديل» + «بانتظار المراجعة» + «متأخرة»،
/// لذا «المعادة للتعديل» تُقرأ مباشرة (لا تُشتق)، ونسبة الالتزام بالمواعيد تُحتسب من «المتأخرة».
/// </summary>
public static class VideoReportSchema
{
    public const string TemplateTitle = "تقرير فريق الفيديو";
    public const string KpiTitle = "مؤشرات أداء الفيديو";

    // ===== حقول رقمية (ValueNumber) — تُجمَّع =====
    public const string RequestedVideos = "عدد الطلبات المستلمة";   // المطلوبة
    public const string DeliveredVideos = "عدد الفيديوهات المسلَّمة"; // المسلَّمة/المنجزة
    public const string ApprovedFirstTime = "معتمدة من أول مرة";     // المعتمدة من أول مرة
    public const string LateVideos = "متأخرة";                        // المتأخرة
    public const string PendingReview = "بانتظار المراجعة";           // بانتظار المراجعة
    public const string RevisedVideos = "أعيدت للتعديل";             // المعادة للتعديل (حقل مباشر)
    public const string OutputAchievement = "نسبة تحقيق المخرجات";    // الالتزام بالخطة (٪) — يُتوسَّط

    // ===== حقول نصية (سياق فقط, لا تُجمَّع رقميًا) =====
    public const string DelayReasons = "أسباب التأخير"; // أسباب التأخير المتكررة
    public const string Challenges = "التحديات";         // مخاطر / قرارات / نقص المواد أو البريف / مشاكل التصوير أو المونتاج

    // ===== مفاتيح الحقول الفرعية داخل قسم المشاريع (PRS) لنسخة v5 (KPI مضمَّن داخل كل مشروع) =====
    public static class Prs
    {
        public const string RequestedVideos = "requested_videos";
        public const string DeliveredVideos = "delivered_videos";
        public const string ApprovedFirstTime = "approved_first_time";
        public const string LateVideos = "late_videos";
        public const string PendingReview = "pending_review";
        public const string RevisedVideos = "revised_videos";
        public const string OutputAchievement = "output_achievement";
        public const string DelayReasons = "delay_reasons";
        public const string Challenges = "challenges";
    }
}
