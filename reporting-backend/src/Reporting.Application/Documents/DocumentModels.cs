using Reporting.Domain.Enums;

namespace Reporting.Application.Documents;

// ===== Client Documents (CPW-R1B2 — خدمة المستندات) =====
// قاعدة ملزِمة: لا يظهر StorageKey في أيّ DTO أو سجلّ أو تدقيق إطلاقًا.

/// <summary>عنصر في قائمة مستندات العميل (بلا مسار تخزين).</summary>
public record ClientDocumentDto(
    Guid Id,
    Guid ClientId,
    string Title,
    string? Description,
    string CategoryCode,
    string? Tags,
    string? ConfidentialityCode,
    DocumentLifecycleStatus LifecycleStatus,
    string ApprovalStatusCode,
    int VersionCount,
    Guid UploadedByUserId,
    string? UploadedByName,
    bool IsArchived,
    DateTime? ArchivedAtUtc,
    string? ArchiveReason,
    // ملخّص النسخة السارية — بلا StorageKey.
    Guid? CurrentVersionId,
    int? CurrentVersionNo,
    string? CurrentFileName,
    string? CurrentContentType,
    long? CurrentSizeBytes,
    DocumentScanStatus? CurrentScanStatus,
    bool CurrentForceAttachment,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>نسخة ملفّ واحدة (بلا مسار تخزين).</summary>
public record ClientDocumentVersionDto(
    Guid Id,
    Guid ClientDocumentId,
    int VersionNo,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DocumentScanStatus ScanStatus,
    string ScanEngine,
    DateTime? ScannedAtUtc,
    string? ScanDetail,
    Guid UploadedByUserId,
    string? UploadedByName,
    string? ChangeNote,
    bool IsCurrent,
    DateTime CreatedAtUtc);

/// <summary>تفصيل المستند مع كامل تاريخ النسخ.</summary>
public record ClientDocumentDetailDto(
    ClientDocumentDto Document,
    IReadOnlyList<ClientDocumentVersionDto> Versions);

/// <summary>مرشّحات قائمة المستندات.</summary>
public record ClientDocumentFilter(
    string? CategoryCode = null,
    string? ConfidentialityCode = null,
    DocumentLifecycleStatus? LifecycleStatus = null,
    string? Search = null,
    bool IncludeArchived = false);

/// <summary>بيانات إنشاء مستند (تُرفَق مع الملفّ في نفس الطلب).</summary>
public record CreateClientDocumentRequest(
    string Title,
    string CategoryCode,
    string? Description = null,
    string? Tags = null,
    string? ConfidentialityCode = null,
    string? ChangeNote = null);

/// <summary>بيانات إضافة نسخة جديدة لمستند قائم.</summary>
public record AddDocumentVersionRequest(string? ChangeNote = null);

/// <summary>تعديل البيانات الوصفيّة فقط (لا يمسّ الملفّات).</summary>
public record UpdateClientDocumentRequest(
    string Title,
    string CategoryCode,
    string? Description = null,
    string? Tags = null,
    string? ConfidentialityCode = null,
    DocumentLifecycleStatus? LifecycleStatus = null,
    string? ApprovalStatusCode = null);

/// <summary>سبب الأرشفة أو إلغائها.</summary>
public record ArchiveClientDocumentRequest(string? Reason = null);

/// <summary>سبب الحذف المنطقيّ (Tombstone) — إلزاميّ.</summary>
public record DeleteClientDocumentRequest(string Reason);

/// <summary>
/// حمولة ملفّ مرفوع بصيغة محايدة عن ASP.NET (كي تبقى طبقة Application بلا اعتماد على IFormFile).
/// المتحكّم هو من يحوّل <c>IFormFile</c> إلى هذا النوع.
/// </summary>
public record DocumentUploadContent(
    Stream Content,
    string FileName,
    string? DeclaredContentType,
    long Length);

/// <summary>
/// نتيجة طلب تنزيل: تدفّق + نوع محتوى مُعاد اشتقاقه من الخادم + اسم ملفّ مُطهَّر.
/// <c>ForceAttachment</c> يفرض <c>Content-Disposition: attachment</c> (SVG/ZIP/CSV/TXT/HTML).
/// </summary>
public record DocumentDownload(
    Stream Content,
    string ContentType,
    string FileName,
    long SizeBytes,
    bool ForceAttachment);

/// <summary>استهلاك التخزين لمورد واحد (عميل) مقابل الحصّة — C-04.</summary>
public record ClientStorageUsageDto(
    Guid ClientId,
    long UsedBytes,
    long QuotaBytes,
    long RemainingBytes,
    int DocumentCount,
    int VersionCount,
    long MaxUploadSizeBytes,
    IReadOnlyList<string> AllowedExtensions,
    string ScanEngine,
    bool ScannerConfigured);
