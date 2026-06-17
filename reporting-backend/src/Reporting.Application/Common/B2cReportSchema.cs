namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب «تقرير مندوب مبيعات B2C الفردي» وحقوله الرقمية القابلة للتجميع (Rollup).
/// يستخدمه كل من TemplateSeeder (لإنشاء القالب) وخدمة التجميع (لقراءة القيم بالاسم) — حتى لا تتباعد الأسماء.
/// </summary>
public static class B2cReportSchema
{
    public const string TemplateTitle = "📇 تقرير مندوب مبيعات B2C الفردي";
    public const string KpiTitle = "مؤشرات مندوب مبيعات B2C";

    // حقول رقمية قابلة للتجميع
    public const string Leads = "عدد الليدز";
    public const string Calls = "عدد المكالمات";
    public const string FollowUps = "عدد المتابعات";
    public const string Registrations = "عدد التسجيلات";
    public const string ClosedDeals = "الصفقات المغلقة";
    public const string TargetRegistrations = "تارجت التسجيلات للأسبوع";

    // حقول نصية (لا تُجمَّع رقميًا، تُستخدم للسياق)
    public const string LostReasons = "أسباب عدم الإغلاق";
    public const string DataQuality = "تحديث CRM / جودة البيانات";
    public const string Notes = "ملاحظات";
    public const string NextActions = "الخطوات القادمة";
}
