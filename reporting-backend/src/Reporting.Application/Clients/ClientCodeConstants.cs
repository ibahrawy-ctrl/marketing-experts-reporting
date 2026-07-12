namespace Reporting.Application.Clients;

/// <summary>
/// ثوابت الرموز المعتمدة لملف العميل الموسَّع (Client 360 — CPW-R1B).
/// رموز نصّية مُتحقَّق منها بلا نظام كتالوج/جدول قاعدة بيانات (بقرار المالك).
/// أيّ رمز يُرسَل خارج المجموعة المعتمدة يُرفَض عند التحقّق في طبقة الخدمة.
/// </summary>
public static class ClientCodeConstants
{
    /// <summary>رموز أنواع العميل المعتمدة.</summary>
    public static readonly IReadOnlySet<string> ClientTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Company", "Individual", "Government", "NonProfit", "Startup", "Agency", "Other"
    };

    /// <summary>رموز القطاعات/الصناعات المعتمدة.</summary>
    public static readonly IReadOnlySet<string> Sectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Retail", "Ecommerce", "Realestate", "Healthcare", "Education", "Food",
        "Technology", "Finance", "Travel", "Automotive", "Beauty", "Fashion",
        "Sports", "Entertainment", "Industrial", "Services", "Other"
    };

    /// <summary>رموز مصادر العلاقة/الاكتساب المعتمدة.</summary>
    public static readonly IReadOnlySet<string> Sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Referral", "Website", "SocialMedia", "ColdOutreach", "Event", "Partner",
        "Advertisement", "Existing", "Other"
    };

    /// <summary>رموز طرق التواصل المفضَّلة المعتمدة.</summary>
    public static readonly IReadOnlySet<string> ContactMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Email", "Phone", "WhatsApp", "InPerson", "Other"
    };

    /// <summary>رموز المنصّات الرقمية المعتمدة.</summary>
    public static readonly IReadOnlySet<string> Platforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Facebook", "Instagram", "X", "LinkedIn", "TikTok", "Snapchat", "YouTube",
        "GoogleAds", "GoogleAnalytics", "GoogleTagManager", "MetaBusiness",
        "Website", "WhatsApp", "Pinterest", "Other"
    };

    /// <summary>رموز حالة الوصول للقناة المعتمدة.</summary>
    public static readonly IReadOnlySet<string> AccessStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "FullAccess", "PartialAccess", "Requested", "Pending", "NoAccess", "Revoked"
    };

    public static bool IsValidClientType(string? code) => IsValid(ClientTypes, code);
    public static bool IsValidSector(string? code) => IsValid(Sectors, code);
    public static bool IsValidSource(string? code) => IsValid(Sources, code);
    public static bool IsValidContactMethod(string? code) => IsValid(ContactMethods, code);
    public static bool IsValidPlatform(string? code) => IsValid(Platforms, code);
    public static bool IsValidAccessStatus(string? code) => IsValid(AccessStatuses, code);

    /// <summary>الرمز الفارغ/غير المحدَّد مقبول (اختياريّ)؛ خلاف ذلك يجب أن يكون ضمن المجموعة.</summary>
    private static bool IsValid(IReadOnlySet<string> set, string? code)
        => string.IsNullOrWhiteSpace(code) || set.Contains(code.Trim());
}
