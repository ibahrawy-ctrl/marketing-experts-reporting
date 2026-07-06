using Reporting.Domain.Enums;

namespace Reporting.Application.Governance;

// ===== التصعيد الفردي (GOV-INDIVIDUAL-ESCALATION1) — كيان مستقلّ عن بنود الحوكمة العامة =====

/// <summary>عنصر قائمة تصعيد فردي (عرض مختصر).</summary>
public record GovernanceEscalationListItemDto(
    Guid Id,
    string Title,
    EscalationType EscalationType,
    EscalationSeverity Severity,
    GovernanceEscalationStatus Status,
    Guid RaisedByUserId,
    string? RaisedByName,
    EscalationTargetType TargetType,
    Guid? TargetUserId,
    string? TargetUserName,
    Guid? TargetDepartmentId,
    string? TargetDepartmentName,
    Guid? TargetTeamId,
    string? TargetTeamName,
    Guid? RelatedSubmissionId,
    Guid? RelatedGovernanceItemId,
    Guid? AssignedToUserId,
    string? AssignedToName,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>حركة على الخط الزمني للتصعيد الفردي.</summary>
public record GovernanceEscalationUpdateDto(
    Guid Id,
    Guid AuthorId,
    string? AuthorName,
    EscalationUpdateType UpdateType,
    string? Body,
    GovernanceEscalationStatus? OldStatus,
    GovernanceEscalationStatus? NewStatus,
    DateTime CreatedAtUtc);

/// <summary>
/// تفاصيل التصعيد الفردي مع الخط الزمني وأعلام الصلاحية المحسوبة وقت الطلب (عرض/تعطيل أزرار الواجهة فقط؛
/// الفرض الحقيقي في الخدمة). أعلام: تعديل، تغيير حالة، إسناد، إغلاق، إعادة فتح، تعليق.
/// </summary>
public record GovernanceEscalationDetailDto(
    GovernanceEscalationListItemDto Item,
    string? Description,
    string? Resolution,
    DateTime? ClosedAtUtc,
    Guid? ClosedByUserId,
    string? ClosedByName,
    bool CanEdit,
    bool CanChangeStatus,
    bool CanAssign,
    bool CanClose,
    bool CanReopen,
    bool CanComment,
    IReadOnlyList<GovernanceEscalationUpdateDto> Timeline);

public record GovernanceEscalationFilter(
    GovernanceEscalationStatus? Status = null,
    EscalationType? Type = null,
    EscalationSeverity? Severity = null,
    EscalationTargetType? TargetType = null,
    Guid? AssignedToUserId = null,
    Guid? RaisedByUserId = null,
    Guid? TargetUserId = null,
    Guid? TargetDepartmentId = null,
    Guid? TargetTeamId = null,
    bool OpenOnly = false,
    bool Mine = false,
    bool AssignedToMe = false);

public record CreateGovernanceEscalationRequest(
    string Title,
    string? Description,
    EscalationType EscalationType,
    EscalationSeverity Severity,
    EscalationTargetType TargetType,
    Guid? TargetUserId = null,
    Guid? TargetDepartmentId = null,
    Guid? TargetTeamId = null,
    Guid? RelatedSubmissionId = null,
    Guid? RelatedGovernanceItemId = null);

public record UpdateGovernanceEscalationRequest(
    string Title,
    string? Description,
    EscalationType EscalationType,
    EscalationSeverity Severity,
    EscalationTargetType TargetType,
    Guid? TargetUserId = null,
    Guid? TargetDepartmentId = null,
    Guid? TargetTeamId = null,
    Guid? RelatedSubmissionId = null,
    Guid? RelatedGovernanceItemId = null);

public record ChangeGovernanceEscalationStatusRequest(
    GovernanceEscalationStatus Status,
    string? Note = null,
    string? Resolution = null);

public record AssignGovernanceEscalationRequest(
    Guid AssignedToUserId,
    string? Note = null);

public record AddGovernanceEscalationCommentRequest(
    string Body);

public record ReopenGovernanceEscalationRequest(
    string? Note = null);

public record CloseGovernanceEscalationRequest(
    string? Resolution = null,
    string? Note = null);

// ===== دليل أهداف التصعيد (آمن، على مستوى الشركة، لا يكشف أيّ تصعيد) =====

public record EscalationTargetUserDto(Guid Id, string FullName, Guid? DepartmentId, Guid? TeamId);
public record EscalationTargetDepartmentDto(Guid Id, string Name);
public record EscalationTargetTeamDto(Guid Id, string Name, Guid? DepartmentId);

/// <summary>قوائم اختيار هدف التصعيد: موظّفون غير حسّاسين + كل الإدارات + كل الفِرق.</summary>
public record EscalationTargetDirectoryDto(
    IReadOnlyList<EscalationTargetUserDto> Users,
    IReadOnlyList<EscalationTargetDepartmentDto> Departments,
    IReadOnlyList<EscalationTargetTeamDto> Teams);
