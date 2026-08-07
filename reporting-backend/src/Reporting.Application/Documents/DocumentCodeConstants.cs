namespace Reporting.Application.Documents;

/// <summary>
/// ثوابت رموز خدمة المستندات (CPW-R1B2). رموز نصّية مُتحقَّق منها بلا جدول كتالوج،
/// على نمط <c>ClientCodeConstants</c> المعتمَد في CPW-R1B.
/// <para>
/// <b>ملاحظة أمنيّة مُلزِمة:</b> لا يوجد — ولن يوجد — تصنيف لحفظ الاعتمادات
/// (كلمات مرور/رموز وصول/مفاتيح API). النظام لا يخزّن أسرارًا إطلاقًا.
/// </para>
/// </summary>
public static class DocumentCodeConstants
{
    /// <summary>تصنيفات المستندات المعتمَدة.</summary>
    public static readonly IReadOnlySet<string> DocumentCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Contract", "Proposal", "Invoice", "Quotation", "BrandAsset", "Logo",
        "Presentation", "Report", "Legal", "Identity", "MeetingMinutes",
        "Creative", "Media", "Other"
    };

    /// <summary>تصنيفات الروابط الخارجيّة المعتمَدة.</summary>
    public static readonly IReadOnlySet<string> LinkCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Drive", "Website", "SocialProfile", "AdsAccount", "Analytics",
        "DesignBoard", "ProjectBoard", "Documentation", "Other"
    };

    /// <summary>درجات السرّية المعتمَدة (وصفيّة — لا تُغني عن ضوابط الصلاحية).</summary>
    public static readonly IReadOnlySet<string> ConfidentialityLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Public", "Internal", "Confidential", "Restricted"
    };

    public static bool IsValidDocumentCategory(string? code) => Required(DocumentCategories, code);
    public static bool IsValidLinkCategory(string? code) => Required(LinkCategories, code);
    public static bool IsValidConfidentiality(string? code) => Optional(ConfidentialityLevels, code);

    private static bool Required(IReadOnlySet<string> set, string? code)
        => !string.IsNullOrWhiteSpace(code) && set.Contains(code.Trim());

    private static bool Optional(IReadOnlySet<string> set, string? code)
        => string.IsNullOrWhiteSpace(code) || set.Contains(code.Trim());
}
