using Reporting.Application.Common;
using Reporting.Domain.Enums;

namespace Reporting.Application.EmployeeServices;

/// <summary>
/// طلبات الموارد البشرية العامة (V1.1). كل العمليات تفرض الصلاحية خادميًّا: الموظّف ينشئ/يرى/يلغي طلباته فقط؛
/// الإدارة/HR (HrRequestManagement) ترى الكل وتعالج (in-review/تعليق/إكمال/رفض). كل إجراء حسّاس يُسجَّل في Audit.
/// </summary>
public interface IEmployeeServiceRequestService
{
    /// <summary>طلبات المستخدم الحالي (هو صاحبها).</summary>
    Task<Result<IReadOnlyList<EmployeeServiceRequestListItemDto>>> GetMyAsync(CancellationToken ct = default);

    /// <summary>كل الطلبات (تصفية) — HrRequestManagement.</summary>
    Task<Result<IReadOnlyList<EmployeeServiceRequestListItemDto>>> ListAsync(
        EmployeeServiceRequestType? type, EmployeeServiceRequestStatus? status, string? q, Guid? userId, CancellationToken ct = default);

    /// <summary>تفاصيل طلب — للمالك أو لمن له صلاحية المعالجة.</summary>
    Task<Result<EmployeeServiceRequestDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>إنشاء طلب (الموظّف لنفسه).</summary>
    Task<Result<EmployeeServiceRequestDto>> CreateAsync(CreateEmployeeServiceRequest request, CancellationToken ct = default);

    /// <summary>إلغاء الطلب من صاحبه قبل الإكمال.</summary>
    Task<Result<EmployeeServiceRequestDto>> CancelAsync(Guid id, CancellationToken ct = default);

    /// <summary>نقل الطلب إلى «قيد المعالجة» (InReview) — HrRequestManagement.</summary>
    Task<Result<EmployeeServiceRequestDto>> StartReviewAsync(Guid id, CancellationToken ct = default);

    /// <summary>تعليق HR (لا يغيّر الحالة) — HrRequestManagement.</summary>
    Task<Result<EmployeeServiceRequestDto>> CommentAsync(Guid id, EmployeeServiceRequestCommentRequest request, CancellationToken ct = default);

    /// <summary>إكمال الطلب (+ ملف نهائي اختياري) — HrRequestManagement.</summary>
    Task<Result<EmployeeServiceRequestDto>> CompleteAsync(Guid id, EmployeeServiceRequestCompleteRequest request, CancellationToken ct = default);

    /// <summary>رفض الطلب (سبب إلزامي) — HrRequestManagement.</summary>
    Task<Result<EmployeeServiceRequestDto>> RejectAsync(Guid id, EmployeeServiceRequestRejectRequest request, CancellationToken ct = default);

    /// <summary>
    /// رفع الملف النهائي (PDF فقط، ≤ 10MB) — HrRequestManagement. يُخزَّن خارج جذر الويب باسم GUID.
    /// يُسمح بالاستبدال قبل الإكمال فقط (لا بعد Completed). لا يُعاد المسار الداخلي.
    /// </summary>
    Task<Result<EmployeeServiceRequestDto>> UploadFinalDocumentAsync(Guid id, FinalDocumentUpload upload, CancellationToken ct = default);

    /// <summary>
    /// تنزيل الملف النهائي — لصاحب الطلب أو HrRequestManagement فقط. 404 إن لا ملف أو غاب من القرص.
    /// لا يكشف المسار الداخلي ولا يسمح باجتياز المسار (الاسم داخلي GUID).
    /// </summary>
    Task<Result<FinalDocumentDownload>> DownloadFinalDocumentAsync(Guid id, CancellationToken ct = default);
}
