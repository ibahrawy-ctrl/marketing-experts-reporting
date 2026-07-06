namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب «تقرير مبيعات B2B حسب الخدمة» (ERDS Phase 3 — تعميم رقمي متوازٍ).
/// قالب مُهيكَل يستقبل بيانات رقمية قابلة للتجميع لاحقًا في صورة جدول (صفّ لكل خدمة) + حقول نصية داعمة.
/// يستخدمه TemplateSeeder (لإنشاء القالب) والاختبارات (للتحقّق من الأعمدة بالاسم) — حتى لا تتباعد الأسماء.
/// ملاحظة: أعمدة الجدول (TableGrid) خلايا نصّية حرّة على مستوى التخزين؛ الأنواع أدناه توثيقية للمراجعة والتجميع المستقبلي.
/// </summary>
public static class B2bByServiceReportSchema
{
    public const string TemplateTitle = "📈 تقرير مبيعات B2B حسب الخدمة";
    public const string Description = "قالب مُهيكَل (ERDS Phase 3): جدول أداء المبيعات لكل خدمة + حقول نصية داعمة.";

    public const string MainTableLabel = "أداء المبيعات لكل خدمة";
    public const string ColService = "الخدمة";          // نص/Select
    public const string ColWorkHours = "ساعات العمل";   // رقم عشري
    public const string ColLeads = "Leads";             // رقم
    public const string ColMeetings = "Meetings";       // رقم
    public const string ColProposals = "Proposals";     // رقم
    public const string ColNegotiation = "Negotiation"; // رقم
    public const string ColWon = "Won";                 // رقم
    public const string ColLost = "Lost";               // رقم
    public const string ColRevenue = "Revenue";         // عملة/عشري
    public const string ColNextStep = "Next Step";      // نص/Select (اختياري)

    public static readonly string[] Columns =
    {
        ColService, ColWorkHours, ColLeads, ColMeetings, ColProposals,
        ColNegotiation, ColWon, ColLost, ColRevenue, ColNextStep,
    };

    public const string TopAchievements = "أهم 3 إنجازات";
    public const string TopChallenges = "أهم 3 تحديات";
    public const string SupportNeeded = "الدعم المطلوب";
    public const string Notes = "ملاحظات";
}
