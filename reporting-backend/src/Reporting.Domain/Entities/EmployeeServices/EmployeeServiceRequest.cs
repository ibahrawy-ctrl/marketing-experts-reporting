using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.EmployeeServices;

/// <summary>
/// طلب موارد بشرية عام (خطاب تعريف، شهادة راتب/خبرة، خطاب بنك/سفارة، تحديث بيانات…). كيان منفصل
/// تمامًا عن الإجازات/الأذونات (ليس Leave ولا Permission). دورة حياة: Submitted → InReview → Completed/Rejected/Cancelled.
/// </summary>
public class EmployeeServiceRequest : BaseEntity
{
    /// <summary>الموظّف صاحب الطلب (= منشئه).</summary>
    public Guid RequesterUserId { get; set; }

    public EmployeeServiceRequestType RequestType { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>ملاحظات الموظّف.</summary>
    public string? Description { get; set; }

    public PreferredLanguage PreferredLanguage { get; set; } = PreferredLanguage.Arabic;

    /// <summary>الجهة الموجَّه إليها الخطاب (اختياري).</summary>
    public string? DestinationEntity { get; set; }

    /// <summary>مرفق الموظّف (اختياري) — تخزين محلي خارج جذر الويب.</summary>
    public string? AttachmentPath { get; set; }

    public EmployeeServiceRequestStatus Status { get; set; } = EmployeeServiceRequestStatus.Submitted;

    public string? HrComment { get; set; }

    /// <summary>
    /// المسار الداخلي للملف النهائي (PDF) المخزَّن خارج جذر الويب — يُدار آليًّا (اسم GUID).
    /// لا يُعرض للعميل إطلاقًا؛ الوصول حصرًا عبر نقطة تنزيل محروسة بالصلاحية.
    /// </summary>
    public string? HrAttachmentPath { get; set; }

    /// <summary>اسم الملف الأصلي وقت الرفع (لاسم التنزيل فقط، لا يُستخدم في المسار).</summary>
    public string? HrAttachmentOriginalFileName { get; set; }

    /// <summary>نوع المحتوى (application/pdf).</summary>
    public string? HrAttachmentContentType { get; set; }

    /// <summary>حجم الملف بالبايت.</summary>
    public long? HrAttachmentSizeBytes { get; set; }

    /// <summary>وقت رفع الملف النهائي (UTC).</summary>
    public DateTime? HrAttachmentUploadedAtUtc { get; set; }

    /// <summary>معرّف موظّف الموارد البشرية الذي رفع الملف النهائي.</summary>
    public Guid? HrAttachmentUploadedByUserId { get; set; }

    /// <summary>تعيين اختياري لموظّف موارد بشرية.</summary>
    public Guid? AssignedToHrUserId { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}
