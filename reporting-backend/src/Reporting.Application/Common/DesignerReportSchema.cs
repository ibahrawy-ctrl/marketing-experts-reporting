namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب فريق التصميم وحقوله الرقمية/النصية القابلة للتجميع (Rollup). Business-1D-2.
/// يعتمد على القالب الموجود مسبقًا في TemplateSeeder (لا قالب جديد): «تقرير فريق التصميم».
/// القالب أغنى من كاتب المحتوى: يحتوي حقلًا مباشرًا «أعيدت للتعديل» + «بانتظار المراجعة» + «متأخرة»،
/// لذا «المعادة للتعديل» تُقرأ مباشرة (لا تُشتق)، ونسبة الالتزام بالمواعيد تُحتسب من «المتأخرة».
/// </summary>
public static class DesignerReportSchema
{
    public const string TemplateTitle = "تقرير فريق التصميم";
    public const string KpiTitle = "مؤشرات أداء المصمم";

    // ===== حقول رقمية (ValueNumber) — تُجمَّع =====
    public const string RequestedDesigns = "عدد الطلبات المستلمة"; // المطلوبة
    public const string DeliveredDesigns = "عدد التصاميم المسلَّمة"; // المسلَّمة/المنجزة
    public const string ApprovedFirstTime = "معتمدة من أول مرة";    // المعتمدة من أول مرة
    public const string LateDesigns = "متأخرة";                      // المتأخرة
    public const string PendingReview = "بانتظار المراجعة";          // بانتظار المراجعة
    public const string RevisedDesigns = "أعيدت للتعديل";            // المعادة للتعديل (حقل مباشر)
    public const string OutputAchievement = "نسبة تحقيق المخرجات";   // الالتزام بالخطة (٪) — يُتوسَّط

    // ===== حقول نصية (سياق فقط, لا تُجمَّع رقميًا) =====
    public const string DelayReasons = "أسباب التأخير"; // أسباب التأخير المتكررة
    public const string Challenges = "التحديات";         // مخاطر / قرارات مطلوبة / مشاكل الهوية ونقص البريف

    // ===== مفاتيح الحقول الفرعية داخل قسم المشاريع (PRS) لنسخة v5 (KPI مضمَّن داخل كل مشروع) =====
    public static class Prs
    {
        public const string RequestedDesigns = "requested_designs";
        public const string DeliveredDesigns = "delivered_designs";
        public const string ApprovedFirstTime = "approved_first_time";
        public const string LateDesigns = "late_designs";
        public const string PendingReview = "pending_review";
        public const string RevisedDesigns = "revised_designs";
        public const string OutputAchievement = "output_achievement";
        public const string DelayReasons = "delay_reasons";
        public const string Challenges = "challenges";
    }
}
