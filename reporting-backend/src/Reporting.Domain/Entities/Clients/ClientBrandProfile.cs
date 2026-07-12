namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// ملفّ البراند للعميل (Client 360 — CPW-R1B): علاقة 1:0..1 مع العميل.
/// لا يرث BaseEntity — مفتاحه الأساسيّ هو ClientId نفسه (سجلّ واحد لكلّ عميل كحدّ أقصى).
/// حقول وصفيّة للهوية والمحتوى فقط — لا أسرار.
/// </summary>
public class ClientBrandProfile
{
    /// <summary>المفتاح الأساسيّ = معرّف العميل (1:0..1).</summary>
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    /// <summary>نبذة عن نشاط العميل.</summary>
    public string? BusinessOverview { get; set; }

    /// <summary>المنتجات والخدمات.</summary>
    public string? ProductsAndServices { get; set; }

    /// <summary>الجمهور المستهدف.</summary>
    public string? TargetAudience { get; set; }

    /// <summary>المناطق الجغرافية المستهدفة.</summary>
    public string? TargetLocations { get; set; }

    /// <summary>القيمة/الميزة التنافسية الفريدة (USP).</summary>
    public string? UniqueSellingProposition { get; set; }

    /// <summary>نقاط القوّة.</summary>
    public string? Strengths { get; set; }

    /// <summary>المنافسون.</summary>
    public string? Competitors { get; set; }

    /// <summary>نبرة الصوت.</summary>
    public string? ToneOfVoice { get; set; }

    /// <summary>الرسائل المفضَّلة.</summary>
    public string? PreferredMessages { get; set; }

    /// <summary>الرسائل الممنوعة.</summary>
    public string? ProhibitedMessages { get; set; }

    /// <summary>رابط دليل البراند (اختياريّ — يُتحقَّق من صحّة الرابط).</summary>
    public string? BrandGuidelinesUrl { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
