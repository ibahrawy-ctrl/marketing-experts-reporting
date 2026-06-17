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
    string? Notes);

/// <summary>قرار اعتماد (تعليق اختياري).</summary>
public record LeaveApproveRequest(string? Comment);

/// <summary>قرار رفض (سبب إلزامي).</summary>
public record LeaveRejectRequest(string Reason);

/// <summary>إعادة للتعديل (سبب إلزامي).</summary>
public record LeaveReturnRequest(string Reason);
