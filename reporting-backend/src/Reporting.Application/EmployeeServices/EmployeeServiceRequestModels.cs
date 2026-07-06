using Reporting.Domain.Enums;

namespace Reporting.Application.EmployeeServices;

// ===== V1.1 — طلبات الموارد البشرية العامة (خدمات الموظف) =====
// كيان منفصل عن الإجازات. الموظّف ينشئ/يرى/يلغي طلباته؛ الإدارة/HR (HrRequestManagement) تعالج.

/// <summary>طلب HR عام كاملًا مع خطّه الزمني.</summary>
public record EmployeeServiceRequestDto(
    Guid Id,
    Guid RequesterUserId,
    string RequesterName,
    EmployeeServiceRequestType RequestType,
    string Title,
    string? Description,
    PreferredLanguage PreferredLanguage,
    string? DestinationEntity,
    string? AttachmentPath,
    EmployeeServiceRequestStatus Status,
    string? HrComment,
    // ملف الموارد البشرية النهائي — لا يُكشف المسار الداخلي إطلاقًا، فقط حالة/اسم/وقت الرفع.
    bool HasFinalDocument,
    string? FinalDocumentFileName,
    DateTime? FinalDocumentUploadedAt,
    Guid? AssignedToHrUserId,
    string? AssignedToHrName,
    string? RejectionReason,
    DateTime? CompletedAtUtc,
    bool CanCancel,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<EmployeeServiceRequestEventDto> Timeline);

/// <summary>صفّ مختصر لقوائم الطلبات.</summary>
public record EmployeeServiceRequestListItemDto(
    Guid Id,
    Guid RequesterUserId,
    string RequesterName,
    EmployeeServiceRequestType RequestType,
    string Title,
    PreferredLanguage PreferredLanguage,
    EmployeeServiceRequestStatus Status,
    DateTime CreatedAtUtc);

/// <summary>حدث في الخطّ الزمني للطلب.</summary>
public record EmployeeServiceRequestEventDto(
    Guid Id,
    Guid ActorUserId,
    string? ActorName,
    string Action,
    EmployeeServiceRequestStatus FromStatus,
    EmployeeServiceRequestStatus ToStatus,
    string? Comment,
    DateTime AtUtc);

/// <summary>إنشاء طلب (الموظّف لنفسه).</summary>
public record CreateEmployeeServiceRequest(
    EmployeeServiceRequestType RequestType,
    string Title,
    string? Description,
    PreferredLanguage PreferredLanguage,
    string? DestinationEntity,
    string? AttachmentPath);

/// <summary>تعليق HR (لا يغيّر الحالة).</summary>
public record EmployeeServiceRequestCommentRequest(string Comment);

/// <summary>إكمال الطلب + تعليق اختياري. الملف النهائي يُرفع عبر نقطة منفصلة (final-document)، لا عبر مسار نصّي.</summary>
public record EmployeeServiceRequestCompleteRequest(string? HrComment);

/// <summary>رفض الطلب (سبب إلزامي).</summary>
public record EmployeeServiceRequestRejectRequest(string Reason);

/// <summary>
/// حمولة رفع الملف النهائي (تجريد مستقل عن AspNetCore). الكنترولر يبنيها من IFormFile.
/// </summary>
public record FinalDocumentUpload(string FileName, string? ContentType, long Length, Stream Content);

/// <summary>
/// نتيجة طلب تنزيل الملف النهائي. تحمل المسار الداخلي (يُستخدم خادميًّا فقط للبثّ) + نوع المحتوى + اسم التنزيل.
/// لا يُعاد المسار للعميل إطلاقًا.
/// </summary>
public record FinalDocumentDownload(string PhysicalPath, string ContentType, string DownloadFileName);
