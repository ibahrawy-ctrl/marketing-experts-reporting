namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب كاتب المحتوى وحقوله الرقمية/النصية القابلة للتجميع (Rollup). Business-1D-1.
/// يعتمد على القالب الموجود مسبقًا في TemplateSeeder (لا قالب جديد): «تقرير كاتب المحتوى الأسبوعي».
/// التجميع يقرأ القيم بالاسم حتى لا تتباعد الأسماء عن الـ Seeder.
/// المشتقات (المعادة للتعديل / نسبة الاعتماد من أول مرة / نسبة التعديلات / تسليم المحتوى) تُحتسب آليًا من هذه الحقول.
/// </summary>
public static class ContentWriterReportSchema
{
    public const string TemplateTitle = "تقرير كاتب المحتوى الأسبوعي";
    public const string KpiTitle = "مؤشرات أداء كاتب المحتوى";

    // ===== حقول رقمية (ValueNumber) — تُجمَّع =====
    public const string RequiredPieces = "عدد القطع المطلوبة";   // المطلوبة
    public const string DeliveredPieces = "عدد القطع المسلَّمة";  // المكتوبة/المنجزة
    public const string ApprovedFirstTime = "معتمدة من أول مرة";  // المعتمدة من أول مرة
    public const string LatePieces = "متأخرة";                    // المتأخرة
    public const string OutputAchievement = "نسبة تحقيق المخرجات"; // الالتزام بالخطة (٪) — يُتوسَّط

    // ===== حقول نصية (سياق فقط، لا تُجمَّع رقميًا) =====
    public const string DelayReasons = "أسباب التأخير";  // أسباب التأخير المتكررة
    public const string Challenges = "التحديات";          // مخاطر / قرارات مطلوبة

    // ===== مفاتيح الحقول الفرعية داخل قسم المشاريع (PRS) لنسخة v5 (KPI مضمَّن داخل كل مشروع) =====
    public static class Prs
    {
        public const string RequiredPieces = "required_pieces";
        public const string DeliveredPieces = "delivered_pieces";
        public const string ApprovedFirstTime = "approved_first_time";
        public const string LatePieces = "late_pieces";
        public const string OutputAchievement = "output_achievement";
        public const string DelayReasons = "delay_reasons";
        public const string Challenges = "challenges";
    }
}
