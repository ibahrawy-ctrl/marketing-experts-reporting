using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// نسخة ملفّ من مستند العميل (CPW-R1B2 — خدمة المستندات).
/// كلّ رفع يُنشئ نسخة جديدة؛ النسخ السابقة تبقى قابلة للتنزيل ولا تُحذف ولا تُستبدَل بايتاتها.
/// <para>
/// <b>تحذير أمنيّ:</b> <see cref="StorageKey"/> مفتاح تخزين داخليّ — يُمنع ظهوره في أيّ DTO
/// أو استجابة أو سجلّ تدقيق أو Log. التنزيل يتمّ عبر نقطة نهاية مُصادَقة ومحكومة بالصلاحية فقط
/// (قرار المالك C-02 — لا روابط موقَّعة في هذه المرحلة).
/// </para>
/// </summary>
public class ClientDocumentVersion : BaseEntity
{
    public Guid ClientDocumentId { get; set; }
    public ClientDocument? Document { get; set; }

    /// <summary>رقم النسخة التسلسليّ داخل المستند (يبدأ من 1).</summary>
    public int VersionNo { get; set; }

    /// <summary>اسم الملفّ الأصليّ كما رفعه المستخدم — للعرض والتنزيل فقط، لا يُستخدَم إطلاقًا كمسار على القرص.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// مفتاح التخزين الداخليّ المُولَّد خادميًّا (معرّفات GUID حصرًا).
    /// لا يُشتقّ من اسم الملفّ الأصليّ ولا يُعرَض في أيّ استجابة.
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>نوع المحتوى المُعتمَد بعد التحقّق (Magic Number + قائمة السماح) — لا يُؤخَذ من العميل كما هو.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>بصمة SHA-256 للمحتوى (64 محرفًا hex) — للتحقّق من السلامة وكشف التكرار.</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>
    /// حالة فحص البرمجيّات الخبيثة (قرار المالك C-01).
    /// الافتراضيّ <see cref="DocumentScanStatus.NotScanned"/> — لا يُدّعى أنّ الملفّ نظيف بلا محرّك فعليّ.
    /// </summary>
    public DocumentScanStatus ScanStatus { get; set; } = DocumentScanStatus.NotScanned;

    /// <summary>اسم محرّك الفحص الذي أنتج النتيجة (<c>None</c> حين لا يوجد محرّك مُهيّأ).</summary>
    public string ScanEngine { get; set; } = "None";

    public DateTime? ScannedAtUtc { get; set; }

    /// <summary>تفصيل نتيجة الفحص (رسالة المحرّك) — بلا أسرار.</summary>
    public string? ScanDetail { get; set; }

    public Guid UploadedByUserId { get; set; }

    /// <summary>ملاحظة التغيير التي كتبها الرافع عند إنشاء هذه النسخة.</summary>
    public string? ChangeNote { get; set; }

    /// <summary>هل هذه هي النسخة السارية حاليًّا (نسخة سارية واحدة كحدّ أقصى لكلّ مستند).</summary>
    public bool IsCurrent { get; set; }
}
