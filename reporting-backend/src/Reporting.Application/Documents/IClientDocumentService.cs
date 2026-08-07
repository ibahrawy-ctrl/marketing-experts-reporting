using Reporting.Application.Common;

namespace Reporting.Application.Documents;

/// <summary>
/// خدمة مستندات العميل (CPW-R1B2). القراءة محكومة بنطاق رؤية العميل،
/// والكتابة مسموحة لمدير الحساب للعميل أو لمدير أساسيّ يرى العميل ضمن نطاقه.
/// <para>
/// ثوابت ملزِمة: الحذف Tombstone فقط (لا حذف صفّ ولا ملفّ)؛ القراءة ≠ التنزيل؛
/// معرّف خارج النطاق يُعامَل بـ404 لا 403 لمنع الاستكشاف (IDOR)؛
/// <c>StorageKey</c> لا يعبر إلى أيّ DTO أو تدقيق.
/// </para>
/// </summary>
public interface IClientDocumentService
{
    Task<Result<IReadOnlyList<ClientDocumentDto>>> ListAsync(Guid clientId, ClientDocumentFilter filter, CancellationToken ct = default);

    Task<Result<ClientDocumentDetailDto>> GetAsync(Guid clientId, Guid documentId, CancellationToken ct = default);

    Task<Result<ClientStorageUsageDto>> GetStorageUsageAsync(Guid clientId, CancellationToken ct = default);

    /// <summary>إنشاء مستند جديد بنسخته الأولى (VersionNo = 1).</summary>
    Task<Result<ClientDocumentDetailDto>> CreateAsync(Guid clientId, CreateClientDocumentRequest request, DocumentUploadContent upload, CancellationToken ct = default);

    /// <summary>إضافة نسخة أحدث؛ النسخة السابقة تصبح <c>Superseded</c> ولا تُحذف.</summary>
    Task<Result<ClientDocumentDetailDto>> AddVersionAsync(Guid clientId, Guid documentId, AddDocumentVersionRequest request, DocumentUploadContent upload, CancellationToken ct = default);

    /// <summary>تعديل البيانات الوصفيّة فقط — لا يمسّ أيّ ملفّ.</summary>
    Task<Result<ClientDocumentDto>> UpdateAsync(Guid clientId, Guid documentId, UpdateClientDocumentRequest request, CancellationToken ct = default);

    Task<Result<ClientDocumentDto>> SetArchivedAsync(Guid clientId, Guid documentId, bool isArchived, ArchiveClientDocumentRequest request, CancellationToken ct = default);

    /// <summary>حذف منطقيّ (Tombstone) بسبب إلزاميّ. الصفّ والملفّ يبقيان.</summary>
    Task<Result<bool>> DeleteAsync(Guid clientId, Guid documentId, DeleteClientDocumentRequest request, CancellationToken ct = default);

    /// <summary>تنزيل نسخة محدّدة، أو النسخة السارية عند تمرير <c>null</c>. عمليّة محكومة ومُدقَّقة.</summary>
    Task<Result<DocumentDownload>> DownloadAsync(Guid clientId, Guid documentId, Guid? versionId, CancellationToken ct = default);
}
