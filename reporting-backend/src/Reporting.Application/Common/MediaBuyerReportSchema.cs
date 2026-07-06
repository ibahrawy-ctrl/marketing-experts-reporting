namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب «تقرير النمو والأداء — Media Buyer» وحقوله الرقمية القابلة للتجميع (Rollup).
/// يعتمد على القالب الموجود مسبقًا في TemplateSeeder (لا قالب جديد) — تستخدمه خدمة التجميع لقراءة القيم بالاسم
/// حتى لا تتباعد الأسماء عن الـ Seeder. Business-1B.
/// </summary>
public static class MediaBuyerReportSchema
{
    public const string TemplateTitle = "📈 تقرير النمو والأداء — Media Buyer";
    public const string KpiTitle = "مؤشرات مشتري الإعلانات";

    // حقول رقمية قابلة للتجميع (ValueNumber)
    public const string Spend = "إجمالي الإنفاق";                       // الإنفاق — يُجمَّع
    public const string Leads = "عدد العملاء المحتملين (Leads)";        // الليدز — يُجمَّع
    public const string Cpl = "تكلفة العميل المحتمل (CPL)";            // مُبلَّغ عنه؛ الإجمالي يُحتسب آليًا = الإنفاق/الليدز
    public const string Ctr = "معدل النقر (CTR)";                       // نسبة — متوسط
    public const string ConversionRate = "معدل التحويل";               // نسبة — متوسط

    // حقول نصية (سياق فقط، لا تُجمَّع رقميًا)
    public const string IssueCause = "سبب المشكلة الأساسي";
    public const string DecisionsNeeded = "قرارات مطلوبة";
    public const string BestContent = "الرسائل والمحتوى الأفضل أداءً";

    // ===== مفاتيح الحقول الفرعية داخل قسم المشاريع (PRS) لنسخة v5 (KPI مضمَّن داخل كل مشروع) =====
    public static class Prs
    {
        public const string Spend = "spend";
        public const string Leads = "mb_leads";
        public const string Ctr = "ctr";
        public const string ConversionRate = "conv_rate";
        public const string IssueCause = "issue_cause";
        public const string DecisionsNeeded = "decisions_needed";
    }
}
