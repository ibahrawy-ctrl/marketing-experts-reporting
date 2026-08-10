using Reporting.Domain.Enums;

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
    /// <summary>تصنيفات المستندات المعتمَدة (CPW-R2: أُضيفت الأربعة الأخيرة، ولم يُحذَف أيّ تصنيف قائم).</summary>
    public static readonly IReadOnlySet<string> DocumentCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Contract", "Proposal", "Invoice", "Quotation", "BrandAsset", "Logo",
        "Presentation", "Report", "Legal", "Identity", "MeetingMinutes",
        "Creative", "Media", "Other",
        "FinancialProposal", "TechnicalProposal", "MarketingPlan", "NDA"
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

    /// <summary>
    /// خريطة سياسة الرؤية الافتراضيّة لكلّ تصنيف (CPW-R2).
    /// <b>تُطبَّق عند الإنشاء فقط</b> وحين لا يُرسِل الطالب سياسة صريحة؛
    /// تغيير التصنيف لاحقًا لا يُعيد ضبط سياسة اختارها المستخدم.
    /// التصنيف غير المذكور هنا ⇒ <c>ClientScoped</c>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, DocumentVisibilityType> DefaultVisibilityByCategory =
        new Dictionary<string, DocumentVisibilityType>(StringComparer.OrdinalIgnoreCase)
        {
            ["Contract"] = DocumentVisibilityType.ManagementAndFinance,
            ["Invoice"] = DocumentVisibilityType.ManagementAndFinance,
            ["Quotation"] = DocumentVisibilityType.ManagementAndFinance,
            ["FinancialProposal"] = DocumentVisibilityType.ManagementAndFinance,

            ["TechnicalProposal"] = DocumentVisibilityType.ProjectTeam,
            ["MarketingPlan"] = DocumentVisibilityType.ProjectTeam,
            ["MeetingMinutes"] = DocumentVisibilityType.ProjectTeam,
            ["BrandAsset"] = DocumentVisibilityType.ProjectTeam,
            ["Logo"] = DocumentVisibilityType.ProjectTeam,
            ["Creative"] = DocumentVisibilityType.ProjectTeam,
            ["Media"] = DocumentVisibilityType.ProjectTeam,

            ["NDA"] = DocumentVisibilityType.ManagementOnly,
            ["Legal"] = DocumentVisibilityType.ManagementOnly,
            ["Identity"] = DocumentVisibilityType.ManagementOnly,

            ["Proposal"] = DocumentVisibilityType.ClientScoped,
            ["Report"] = DocumentVisibilityType.ClientScoped,
            ["Presentation"] = DocumentVisibilityType.ClientScoped,
            ["Other"] = DocumentVisibilityType.ClientScoped,
        };

    /// <summary>السياسة الافتراضيّة للتصنيف (<c>ClientScoped</c> لأيّ تصنيف غير مُعرَّف في الخريطة).</summary>
    public static DocumentVisibilityType DefaultVisibilityFor(string? categoryCode)
        => !string.IsNullOrWhiteSpace(categoryCode)
           && DefaultVisibilityByCategory.TryGetValue(categoryCode.Trim(), out var v)
            ? v
            : DocumentVisibilityType.ClientScoped;

    public static bool IsValidDocumentCategory(string? code) => Required(DocumentCategories, code);
    public static bool IsValidLinkCategory(string? code) => Required(LinkCategories, code);
    public static bool IsValidConfidentiality(string? code) => Optional(ConfidentialityLevels, code);

    private static bool Required(IReadOnlySet<string> set, string? code)
        => !string.IsNullOrWhiteSpace(code) && set.Contains(code.Trim());

    private static bool Optional(IReadOnlySet<string> set, string? code)
        => string.IsNullOrWhiteSpace(code) || set.Contains(code.Trim());
}
