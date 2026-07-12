using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// قناة رقمية للعميل (Client 360 — CPW-R1B): منصّة تواصل اجتماعيّ/إعلانيّة/تحليلات مرتبطة بالعميل.
/// تُخزَّن هنا **معرّفات مرجعية فقط** (Business Manager Id / Ad Account Id / Pixel Id / GA4 / GTM) —
/// **لا تُخزَّن أيّ أسرار أو رموز وصول أو كلمات مرور أو مفاتيح API إطلاقًا**. التعطيل بدل الحذف افتراضيًّا.
/// </summary>
public class ClientDigitalChannel : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    /// <summary>رمز المنصّة من ثوابت الرموز المعتمدة (نصّ فقط — بلا FK).</summary>
    public string PlatformCode { get; set; } = string.Empty;

    /// <summary>الاسم المعروض للقناة (اختياريّ).</summary>
    public string? DisplayName { get; set; }

    /// <summary>اسم المستخدم/المُعرِّف الظاهر (@handle) — ليس سرًّا.</summary>
    public string? UsernameOrHandle { get; set; }

    /// <summary>رابط الملفّ الشخصيّ للقناة (اختياريّ — يُتحقَّق من صحّة الرابط).</summary>
    public string? ProfileUrl { get; set; }

    /// <summary>التعطيل بدل الحذف — نمط تيار العمل. BaseEntity لا يحمل IsActive فيُعرَّف هنا.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>رمز حالة الوصول للقناة من ثوابت الرموز المعتمدة (نصّ فقط — بلا FK).</summary>
    public string? AccessStatusCode { get; set; }

    // معرّفات مرجعية فقط — ليست أسرارًا (لا رموز وصول/كلمات مرور).
    public string? BusinessManagerId { get; set; }
    public string? AdAccountId { get; set; }
    public string? PixelId { get; set; }
    public string? Ga4PropertyId { get; set; }
    public string? GtmContainerId { get; set; }

    public string? Notes { get; set; }
    public int SortOrder { get; set; }
}
