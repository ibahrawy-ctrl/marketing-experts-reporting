using Reporting.Domain.Enums;

namespace Reporting.Application.Governance;

// ===== ورشة الحوكمة العامة (GOV-GOVERNANCE-UX1) =====

/// <summary>عنصر قائمة بند الحوكمة (عرض مختصر).</summary>
public record GovernanceItemListItemDto(
    Guid Id,
    string Title,
    GovernanceCategory Category,
    GovernanceSeverity Severity,
    GovernanceItemStatus Status,
    GovernanceApplicationScope ApplicationScope,
    Guid CreatedById,
    string? CreatedByName,
    Guid? AssignedToUserId,
    string? AssignedToName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? TeamId,
    string? TeamName,
    Guid? RelatedSubmissionId,
    Guid? RelatedUserId,
    string? RelatedUserName,
    DateOnly? DueDate,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>حركة على الخط الزمني لبند الحوكمة.</summary>
public record GovernanceItemUpdateDto(
    Guid Id,
    Guid AuthorId,
    string? AuthorName,
    GovernanceItemUpdateType UpdateType,
    string? Body,
    GovernanceItemStatus? OldStatus,
    GovernanceItemStatus? NewStatus,
    DateTime CreatedAtUtc);

/// <summary>تفاصيل بند الحوكمة مع الخط الزمني.</summary>
public record GovernanceItemDetailDto(
    GovernanceItemListItemDto Item,
    string? Description,
    string? ResolutionSummary,
    DateTime? ClosedAtUtc,
    Guid? ClosedById,
    string? ClosedByName,
    bool CanEdit,
    bool CanChangeStatus,
    IReadOnlyList<GovernanceItemUpdateDto> Timeline);

public record GovernanceItemFilter(
    GovernanceItemStatus? Status = null,
    GovernanceCategory? Category = null,
    GovernanceSeverity? Severity = null,
    Guid? AssignedToUserId = null,
    Guid? DepartmentId = null,
    Guid? TeamId = null,
    bool OpenOnly = false);

public record CreateGovernanceItemRequest(
    string Title,
    string? Description,
    GovernanceCategory Category,
    GovernanceSeverity Severity,
    GovernanceApplicationScope ApplicationScope = GovernanceApplicationScope.User,
    Guid? AssignedToUserId = null,
    Guid? DepartmentId = null,
    Guid? TeamId = null,
    Guid? RelatedSubmissionId = null,
    Guid? RelatedUserId = null,
    DateOnly? DueDate = null);

public record UpdateGovernanceItemRequest(
    string Title,
    string? Description,
    GovernanceCategory Category,
    GovernanceSeverity Severity,
    GovernanceApplicationScope ApplicationScope = GovernanceApplicationScope.User,
    Guid? AssignedToUserId = null,
    Guid? DepartmentId = null,
    Guid? TeamId = null,
    Guid? RelatedSubmissionId = null,
    Guid? RelatedUserId = null,
    DateOnly? DueDate = null);

public record ChangeGovernanceItemStatusRequest(
    GovernanceItemStatus Status,
    string? Note = null,
    string? ResolutionSummary = null);

public record AddGovernanceItemCommentRequest(
    string Body,
    bool IsFollowUp = false);
