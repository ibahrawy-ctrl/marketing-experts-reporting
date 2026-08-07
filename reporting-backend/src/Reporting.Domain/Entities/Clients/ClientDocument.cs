using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// مستند مرتبط بالعميل (CPW-R1B2 — خدمة المستندات، أوّل ارتباط مورد Resource Binding).
/// الكيان يحمل بيانات التعريف فقط؛ البايتات في نسخ منفصلة (<see cref="ClientDocumentVersion"/>).
/// لا يُخزَّن هنا أيّ مسار تخزين ولا أيّ سرّ (كلمة مرور/رمز وصول/مفتاح API/كوكي/رمز استرداد).
/// الحذف تشاهُديّ (Tombstone) فقط — لا حذف فعليّ للبيانات ولا للملفّات من هذا المسار.
/// </summary>
public class ClientDocument : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    /// <summary>عنوان المستند كما يعرضه المستخدم (لا يُستخدَم إطلاقًا كاسم ملفّ على القرص).</summary>
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>رمز التصنيف من ثوابت الرموز المعتمَدة (نصّ فقط — بلا FK).</summary>
    public string CategoryCode { get; set; } = string.Empty;

    /// <summary>وسوم حرّة مفصولة بفواصل (تُطبَّع وتُقصّ خادميًّا).</summary>
    public string? Tags { get; set; }

    /// <summary>رمز مستوى السرّية من ثوابت الرموز المعتمَدة (نصّ فقط — بلا FK).</summary>
    public string? ConfidentialityCode { get; set; }

    public DocumentLifecycleStatus LifecycleStatus { get; set; } = DocumentLifecycleStatus.Draft;

    /// <summary>
    /// رمز حالة الاعتماد — مؤجَّل في هذه المرحلة، القيمة الافتراضيّة <c>NotApplicable</c>.
    /// موجود من البداية كي لا تحتاج دورة الاعتماد المستقبليّة إلى تعديل سكيمة مُتلِف.
    /// </summary>
    public string ApprovalStatusCode { get; set; } = "NotApplicable";

    /// <summary>النسخة السارية. Nullable لكسر دورة المفاتيح الأجنبيّة (تُضبَط بعد إدراج أوّل نسخة داخل معاملة واحدة).</summary>
    public Guid? CurrentVersionId { get; set; }
    public ClientDocumentVersion? CurrentVersion { get; set; }

    public int VersionCount { get; set; }

    public Guid UploadedByUserId { get; set; }

    /// <summary>الأرشفة (تُخفي المستند من العرض الافتراضيّ ولا تحذف شيئًا).</summary>
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public Guid? ArchivedByUserId { get; set; }
    public string? ArchiveReason { get; set; }

    /// <summary>الحذف التشاهُديّ (Tombstone) — Admin/CEO/GM فقط بسبب إلزاميّ. لا يُحذف صفّ ولا ملفّ.</summary>
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public string? DeleteReason { get; set; }

    public ICollection<ClientDocumentVersion> Versions { get; set; } = new List<ClientDocumentVersion>();
}
