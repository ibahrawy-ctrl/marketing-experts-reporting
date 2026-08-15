using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// عميل/حساب تتعامل معه الوكالة (Phase 6 — بُعد العميل والمشروع).
/// يربط الأداء التشغيلي بأداء العميل عبر المشاريع المرتبطة به.
/// </summary>
public class Client : BaseEntity
{
    /// <summary>الاسم التجاري/المعروض للعميل بالعربية (المصدر الأوحد للاسم العربي).</summary>
    public string Name { get; set; } = string.Empty;
    public ClientStatus Status { get; set; } = ClientStatus.Active;

    /// <summary>مدير الحساب المسؤول (مرجع مستخدم — ليس دورًا نظاميًا).</summary>
    public Guid? AccountManagerId { get; set; }

    public string? MainContactName { get; set; }
    public string? MainContactInfo { get; set; }
    public string? Notes { get; set; }

    // === Client 360 Foundation (CPW-R1B) — حقول ملف العميل الموسَّع (كلها اختيارية) ===

    /// <summary>الاسم التجاري بالإنجليزية (اختياريّ).</summary>
    public string? TradeNameEn { get; set; }

    /// <summary>الاسم القانونيّ الرسميّ للكيان (اختياريّ).</summary>
    public string? LegalName { get; set; }

    /// <summary>رمز نوع العميل من ثوابت الرموز المعتمدة (نصّ فقط — بلا FK).</summary>
    public string? ClientTypeCode { get; set; }

    /// <summary>رمز القطاع/الصناعة من ثوابت الرموز المعتمدة (نصّ فقط — بلا FK).</summary>
    public string? SectorCode { get; set; }

    /// <summary>الدولة (اختياريّة).</summary>
    public string? Country { get; set; }

    /// <summary>المدينة (اختياريّة).</summary>
    public string? City { get; set; }

    /// <summary>الموقع الإلكترونيّ الرسميّ (اختياريّ — يُتحقَّق من صحّة الرابط).</summary>
    public string? Website { get; set; }

    /// <summary>رمز مصدر العلاقة/الاكتساب من ثوابت الرموز المعتمدة (نصّ فقط — بلا FK).</summary>
    public string? SourceCode { get; set; }

    /// <summary>تاريخ بدء العلاقة مع العميل (اختياريّ).</summary>
    public DateOnly? RelationshipStartDate { get; set; }

    public ICollection<Project> Projects { get; set; } = new List<Project>();

    /// <summary>جهات الاتصال لدى العميل (1:N).</summary>
    public ICollection<ClientContact> Contacts { get; set; } = new List<ClientContact>();

    /// <summary>القنوات الرقمية للعميل (1:N).</summary>
    public ICollection<ClientDigitalChannel> DigitalChannels { get; set; } = new List<ClientDigitalChannel>();

    /// <summary>ملفّ البراند (1:0..1).</summary>
    public ClientBrandProfile? BrandProfile { get; set; }

    // === CPW-R1B2 — خدمة المستندات (أوّل ارتباط مورد) ===

    /// <summary>مستندات العميل (1:N).</summary>
    public ICollection<ClientDocument> Documents { get; set; } = new List<ClientDocument>();

    /// <summary>الروابط الخارجيّة المهمّة للعميل (1:N).</summary>
    public ICollection<ClientExternalLink> ExternalLinks { get; set; } = new List<ClientExternalLink>();
}
