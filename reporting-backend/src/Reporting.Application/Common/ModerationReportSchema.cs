namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب «تقرير المديرشن الأسبوعي» وحقوله الرقمية/النصية القابلة للتجميع (Rollup). Business-1D-4.
/// يعتمد على القالب الموجود مسبقًا في TemplateSeeder (لا قالب جديد).
/// المودريشن مختلف عن المحتوى/التصميم/الفيديو: لا يقيس إنتاج قطع بل المتابعة وسرعة وجودة الاستجابة والتصعيد والشكاوى.
/// أُضيفت ثلاثة حقول رقمية تراكميًا فقط (لا تعديل/حذف لحقول قائمة، لا Migration هدّامة): المصعّدة/الشكاوى/الفرص المحوَّلة.
/// </summary>
public static class ModerationReportSchema
{
    public const string TemplateTitle = "تقرير المديرشن الأسبوعي";
    public const string KpiTitle = "مؤشرات أداء المودريشن";

    // ===== حقول رقمية قائمة (ValueNumber) — تُجمَّع =====
    public const string IncomingMessages = "عدد الرسائل الواردة";       // الرسائل الواردة
    public const string AnsweredMessages = "عدد الرسائل المُجاب عليها";  // المُجاب عليها
    public const string AvgResponseMinutes = "متوسط زمن الرد (دقيقة)";   // متوسط سرعة الرد (الأقل أفضل) — يُتوسَّط
    public const string ProblematicComments = "عدد التعليقات الإشكالية"; // التعليقات/الرسائل غير المعالجة الإشكالية

    // ===== حقول رقمية مُضافة تراكميًا (Business-1D-4) — تُجمَّع =====
    public const string Escalations = "عدد الحالات المصعّدة";           // الحالات المصعّدة
    public const string Complaints = "عدد الشكاوى";                      // الشكاوى
    public const string ConvertedOpportunities = "عدد الفرص البيعية المحوَّلة"; // الفرص البيعية المحوَّلة

    // ===== حقول نصية (سياق فقط، لا تُجمَّع رقميًا) =====
    public const string RecurringQuestions = "الأسئلة المتكررة (FAQ)"; // المشكلات/الأسئلة المتكررة
    public const string Recommendations = "توصيات";                     // توصيات الأسبوع القادم / قرارات مطلوبة
}
