using Reporting.Application.Common;

namespace Reporting.Application.Attendance;

/// <summary>
/// P2-ATT-006 — خدمة وقائع الحضور.
///
/// <para><b>ضوابط مُلزِمة على أيّ تنفيذ:</b></para>
/// <list type="bullet">
/// <item>كلّ نقطة تعيد <c>attendance.not_found</c> (404) خارج النطاق — لا 403 ولا قائمة فارغة كاشفة.</item>
/// <item>كلّ كتابة تمرّ بجدول الانتقالات ومُخوِّل الفاعل معًا، ثمّ تُلحِق حدثًا غير قابل للتعديل.</item>
/// <item>لا انتقال هنا — ولا أيّ تركيبة منها — يُنشئ خصمًا أو حركة رصيد أو أثرًا على الرواتب.</item>
/// <item>التزامن متفائل: تصادم <c>ConcurrencyStamp</c> ⇒ <c>attendance.conflict</c> (409).</item>
/// </list>
/// </summary>
public interface IAttendanceService
{
    /// <summary>كتالوج الأنواع الفعّالة مرتّبًا.</summary>
    Task<Result<IReadOnlyList<AttendanceTypeDto>>> ListTypesAsync(CancellationToken ct = default);

    /// <summary>قائمة الوقائع داخل نطاق المستخدم حصرًا، مُرشَّحة ومُصفَّحة.</summary>
    Task<Result<AttendancePagedDto>> ListAsync(AttendanceListFilter filter, CancellationToken ct = default);

    /// <summary>تفاصيل واقعة بعد ترشيح الحسّاسيّة، مع الخطّ الزمنيّ والمرفقات والقدرات المتاحة.</summary>
    Task<Result<AttendanceIncidentDetailDto>> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// إنشاء بلاغ (مسودّة أو إرسال فوريّ).
    /// <paramref name="idempotencyKey"/> يجعل إعادة إرسال نفس الطلب شبكيًّا لا تُنشئ بلاغًا ثانيًا.
    /// </summary>
    Task<Result<AttendanceIncidentDetailDto>> CreateAsync(
        CreateAttendanceIncidentRequest request, string? idempotencyKey, CancellationToken ct = default);

    Task<Result<AttendanceIncidentDetailDto>> UpdateDraftAsync(
        Guid id, UpdateAttendanceDraftRequest request, CancellationToken ct = default);

    Task<Result<AttendanceIncidentDetailDto>> SubmitAsync(Guid id, int concurrencyStamp, CancellationToken ct = default);

    /// <summary>إلغاء مسودّة لم تُرسَل — الحذف الوحيد المسموح في دورة الحياة.</summary>
    Task<Result> CancelDraftAsync(Guid id, int concurrencyStamp, CancellationToken ct = default);

    /// <summary>سحب بلاغ مُرسَل من مُنشِئه قبل ردّ الموظّف، بسبب موثَّق في الخطّ الزمنيّ.</summary>
    Task<Result<AttendanceIncidentDetailDto>> WithdrawAsync(
        Guid id, AttendanceReasonRequest request, CancellationToken ct = default);

    // ===== حقّ الموظّف =====

    Task<Result<AttendanceIncidentDetailDto>> AcknowledgeAsync(
        Guid id, EmployeeResponseRequest request, CancellationToken ct = default);

    Task<Result<AttendanceIncidentDetailDto>> DisputeAsync(
        Guid id, EmployeeResponseRequest request, CancellationToken ct = default);

    // ===== مراجعة الموارد البشريّة =====

    /// <summary>تأكيد/رفض/تصحيح/مصالحة/إبطال في نقطة واحدة محكومة بآلة الحالات.</summary>
    Task<Result<AttendanceIncidentDetailDto>> HrReviewAsync(
        Guid id, HrReviewRequest request, CancellationToken ct = default);

    Task<Result<AttendanceIncidentDetailDto>> EscalateAsync(
        Guid id, AttendanceReasonRequest request, CancellationToken ct = default);

    Task<Result<AttendanceIncidentDetailDto>> CloseAsync(
        Guid id, AttendanceReasonRequest request, CancellationToken ct = default);

    // ===== الأدلّة والخطّ الزمنيّ =====

    Task<Result<IReadOnlyList<AttendanceEventDto>>> ListEventsAsync(Guid id, CancellationToken ct = default);

    Task<Result<AttendanceAttachmentDto>> UploadAttachmentAsync(
        Guid id, string fileName, string contentType, long sizeBytes, Stream content, CancellationToken ct = default);

    Task<Result<AttendanceFileDownload>> DownloadAttachmentAsync(
        Guid id, Guid attachmentId, CancellationToken ct = default);

    /// <summary>اقتراحات مصالحة مع إجازة/استئذان معتمد — للاطّلاع فقط، بلا أيّ تغيير حالة.</summary>
    Task<Result<IReadOnlyList<AttendanceReconciliationSuggestionDto>>> SuggestReconciliationAsync(
        Guid id, CancellationToken ct = default);

    /// <summary>
    /// كنس SLA: إشعار الموظّفين، وإنهاء النوافذ المنقضية، والإحالة إلى الموارد البشريّة.
    /// إجراء **نظام** لا مستخدم، ولا يُنتج أيّ أثر ماليّ.
    /// </summary>
    Task<Result<AttendanceSlaSweepResult>> RunSlaSweepAsync(CancellationToken ct = default);
}
