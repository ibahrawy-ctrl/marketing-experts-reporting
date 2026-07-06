using Reporting.Domain.Enums;

namespace Reporting.Application.Leave;

// ===== رقعة V1.0.1 — الإجازات والاستئذانات =====
// كل النواتج مقيَّدة خادميًّا بنطاق المستخدم الحالي ودوره. لا يُصبح الطلب رسميًّا ويؤثّر في التقارير
// إلا عند الحالة HrApproved. هذه الوحدة لا تتضمّن أرصدة إجازات ولا خصومات ولا نظام موارد بشرية كامل.

/// <summary>طلب إجازة/استئذان كاملًا مع خطّه الزمني.</summary>
public record LeaveRequestDto(
    Guid Id,
    Guid RequesterUserId,
    string RequesterName,
    LeaveRequestType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string Reason,
    string? Notes,
    LeaveRequestStatus Status,
    LeaveRequestStep CurrentStep,
    bool IsHrRequest,
    Guid? TeamLeaderReviewerId,
    string? TeamLeaderReviewerName,
    Guid? ManagerReviewerId,
    string? ManagerReviewerName,
    Guid? HrReviewerId,
    string? HrReviewerName,
    DateTime? TeamLeaderDecisionAtUtc,
    DateTime? ManagerDecisionAtUtc,
    DateTime? HrDecisionAtUtc,
    string? RejectionReason,
    string? ReturnReason,
    bool ImpactsReports,
    bool CanCancel,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? CancelledAtUtc,
    // لقطة كفاية رصيد الإجازات (V1.1 — حارس الرصيد). null للطلبات ذات الرصيد الكافي أو للأذونات.
    decimal? BalanceAtRequest,
    int? RequestedLeaveDays,
    decimal? UncoveredLeaveDays,
    bool IsPotentialUnpaidLeave,
    bool EmployeeAcknowledgedUnpaidDeduction,
    DateTime? EmployeeAcknowledgedAtUtc,
    // قرار الموظّف عند تجاوز رصيد الأذونات الشهري (V1.1.1). None للإجازات/الأذونات ضمن الرصيد.
    PermissionShortfallResolution PermissionShortfallResolution,
    IReadOnlyList<LeaveRequestEventDto> Timeline);

/// <summary>صفّ مختصر لقوائم الطلبات (طلباتي / بانتظار قراري).</summary>
public record LeaveRequestListItemDto(
    Guid Id,
    Guid RequesterUserId,
    string RequesterName,
    LeaveRequestType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string Reason,
    LeaveRequestStatus Status,
    LeaveRequestStep CurrentStep,
    bool IsHrRequest,
    bool ImpactsReports,
    bool IsPotentialUnpaidLeave,
    DateTime CreatedAtUtc);

/// <summary>حدث في الخطّ الزمني للطلب.</summary>
public record LeaveRequestEventDto(
    Guid Id,
    Guid ActorUserId,
    string? ActorName,
    string Action,
    LeaveRequestStep Step,
    LeaveRequestStatus FromStatus,
    LeaveRequestStatus ToStatus,
    string? Comment,
    DateTime AtUtc);

/// <summary>
/// إنشاء طلب. الإجازة: Type=Leave مع StartDate/EndDate/Reason. الاستئذان: Type=Permission مع
/// StartDate/StartTime/EndTime/Reason (يوم واحد). الحقول غير المعنيّة تُترك فارغة ويتولّى الخادم التحقق.
/// </summary>
public record CreateLeaveRequestRequest(
    LeaveRequestType Type,
    DateOnly StartDate,
    DateOnly? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string Reason,
    string? Notes,
    // إقرار الموظّف بأن الأيام غير المغطّاة بالرصيد قد تُحتسب إجازةً بدون راتب (V1.1 — حارس الرصيد).
    // إلزامي فقط عند نقص رصيد الإجازات؛ يُتجاهَل عند كفاية الرصيد أو للأذونات.
    bool AcknowledgedUnpaidDeduction = false,
    // قرار الموظّف عند تجاوز رصيد الأذونات الشهري (V1.1.1). إلزامي (غير None) فقط عند نقص الرصيد الشهري
    // للأذونات؛ يُتجاهَل عند كفاية الرصيد أو للإجازات.
    PermissionShortfallResolution PermissionShortfallResolution = PermissionShortfallResolution.None);

/// <summary>قرار اعتماد (تعليق اختياري).</summary>
public record LeaveApproveRequest(string? Comment);

/// <summary>قرار رفض (سبب إلزامي).</summary>
public record LeaveRejectRequest(string Reason);

/// <summary>إعادة للتعديل (سبب إلزامي).</summary>
public record LeaveReturnRequest(string Reason);

/// <summary>
/// إبطال إجازة/إذن معتمَد نهائيًّا (V1.1 — مسار محروس للإدارة/HR). سبب إلزامي. يُنشئ حركة Credit
/// معاكسة (Reversal) للخصم الآلي ولا يحذف الحركة الأصلية، وينقل الطلب HrApproved → Cancelled.
/// </summary>
public record LeaveRevokeRequest(string Reason);

/// <summary>
/// نتيجة معالجة الطلبات العالقة لقادة الفِرق (T-WF1). يُحصي الطلبات المطابقة وعدد ما عولِج فعليًّا.
/// </summary>
public record TeamLeaderStuckRemediationResultDto(
    int MatchedCount,
    int RemediatedCount,
    IReadOnlyList<Guid> RemediatedRequestIds);
