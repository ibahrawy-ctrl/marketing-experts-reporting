namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة أسماء قالب «تقرير مبيعات B2B — حسب مصدر البيانات» (RC-3 — فصل مصدر بيانات B2B).
/// قالب مُهيكَل بجدولين منفصلين: (1) New Leads (عملاء محتملون جدد)، (2) Data Scraping (سحب/تجميع بيانات).
/// كلا الجدولين يستخدم منتقي «الخدمة» من كتالوج الخدمات (لا إدخال يدوي حرّ؛ المعطّلة تُخفى للتقارير الجديدة؛ القيم القديمة Legacy تبقى مقروءة).
///
/// إضافة بحتة (additive): قالب جديد بعنوان مستقلّ ⇒ TemplateSeeder (idempotent-by-title) يُنشئه من جديد
/// دون المساس بقالب B2B أحادي الجدول السابق (B2bByServiceReportSchema)، الذي يبقى قابلًا للقراءة والتجميع كما هو.
/// التقارير القديمة لا تُحوَّل تلقائيًّا. الفهرس 0 في كلا الجدولين = «الخدمة» (Select) ما يبسّط التحقّق والتجميع.
/// </summary>
public static class B2bBySourceReportSchema
{
    public const string TemplateTitle = "📊 تقرير مبيعات B2B — حسب مصدر البيانات";
    public const string Description = "قالب مُهيكَل (RC-3): جدولان منفصلان — New Leads (عملاء محتملون جدد) وData Scraping (سحب بيانات) + حقول نصية داعمة. الخدمة من الكتالوج.";

    // ===== أعمدة مشتركة بالاسم بين الجدولين =====
    public const string ColService = "الخدمة";           // نص/Select (من كتالوج الخدمات)
    public const string ColWorkHours = "ساعات العمل";    // رقم عشري
    public const string ColContacted = "Contacted";      // رقم
    public const string ColMeetings = "Meetings";        // رقم
    public const string ColProposals = "Proposals";      // رقم
    public const string ColNegotiation = "Negotiation";  // رقم
    public const string ColWon = "Won";                  // رقم
    public const string ColRevenue = "Revenue";          // عملة/عشري

    // ===== جدول 1: New Leads (عملاء محتملون جدد) =====
    public const string NewLeadsTableLabel = "أداء العملاء المحتملين الجدد New Leads";
    public const string ColNewLeads = "New Leads";       // رقم (Leads جديدة)

    public static readonly string[] NewLeadsColumns =
    {
        ColService, ColWorkHours, ColNewLeads, ColContacted, ColMeetings,
        ColProposals, ColNegotiation, ColWon, ColRevenue,
    };

    // ===== جدول 2: Data Scraping (سحب/تجميع بيانات) =====
    public const string DataScrapingTableLabel = "أداء سحب البيانات Data Scraping";
    public const string ColScrapedLeads = "Scraped Leads"; // رقم (Leads تم سحبها)
    public const string ColValidLeads = "Valid Leads";     // رقم (صالحة بعد التصفية)

    public static readonly string[] DataScrapingColumns =
    {
        ColService, ColWorkHours, ColScrapedLeads, ColValidLeads, ColContacted,
        ColMeetings, ColProposals, ColNegotiation, ColWon, ColRevenue,
    };

    // حقول نصية داعمة (لا تحلّ محلّ الأرقام).
    public const string TopAchievements = "أهم 3 إنجازات";
    public const string TopChallenges = "أهم 3 تحديات";
    public const string SupportNeeded = "الدعم المطلوب";
    public const string Notes = "ملاحظات";
}
