using Reporting.Domain.Enums;

namespace Reporting.Application.Governance;

// ===== إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1) — كيان مستقلّ يحوّل أي ملاحظة/تصعيد إلى إجراء متابَع =====

/// <summary>عنصر قائمة إجراء حوكمة (عرض مختصر). IsOverdue محسوبة (غير مخزَّنة).</summary>
public record GovernanceActionItemListItemDto(
    Guid Id,
    string Title,
    ActionItemSourceType SourceType,
    Guid? SourceId,
    string? SourceTitle,
    ActionItemPriority Priority,
    ActionItemStatus Status,
    bool IsOverdue,
    DateOnly? DueDate,
    Guid? AssignedToUserId,
    string? AssignedToName,
    Guid CreatedByUserId,
    string? CreatedByName,
    bool IsSensitive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>حركة على الخط الزمني لإجراء الحوكمة.</summary>
public record GovernanceActionItemUpdateDto(
    Guid Id,
    Guid AuthorId,
    string? AuthorName,
    ActionItemUpdateType UpdateType,
    string? Body,
    ActionItemStatus? OldStatus,
    ActionItemStatus? NewStatus,
    DateTime CreatedAtUtc);

/// <summary>
/// تفاصيل إجراء الحوكمة مع الخط الزمني وأعلام الصلاحية المحسوبة وقت الطلب (عرض/تعطيل أزرار الواجهة فقط؛
/// الفرض الحقيقي في الخدمة). أعلام: تغيير حالة (المُسنَد إليه)، إسناد/تغيير استحقاق/إلغاء/إعادة فتح (المنشئ/الإدارة)، تعليق.
/// SourceVisibleToViewer: هل يحقّ للمشاهِد رؤية تفاصيل المصدر الحسّاس؛ إن false لا يُكشَف SourceId/SourceTitle.
/// </summary>
public record GovernanceActionItemDetailDto(
    GovernanceActionItemListItemDto Item,
    string? Description,
    string? CompletionNote,
    DateTime? CompletedAtUtc,
    Guid? CompletedByUserId,
    string? CompletedByName,
    Guid? AssignedByUserId,
    string? AssignedByName,
    bool SourceVisibleToViewer,
    bool CanChangeStatus,
    bool CanAssign,
    bool CanChangeDueDate,
    bool CanCancel,
    bool CanReopen,
    bool CanComment,
    IReadOnlyList<GovernanceActionItemUpdateDto> Timeline);

public record GovernanceActionItemFilter(
    ActionItemStatus? Status = null,
    Guid? AssignedToUserId = null,
    ActionItemSourceType? SourceType = null,
    Guid? SourceId = null,
    ActionItemPriority? Priority = null,
    DateOnly? DueFrom = null,
    DateOnly? DueTo = null,
    bool OverdueOnly = false,
    bool MineOnly = false,
    bool AssignedToMe = false);

public record CreateGovernanceActionItemRequest(
    string Title,
    string? Description,
    ActionItemPriority Priority,
    ActionItemSourceType SourceType = ActionItemSourceType.Manual,
    Guid? SourceId = null,
    Guid? AssignedToUserId = null,
    DateOnly? DueDate = null);

public record ChangeGovernanceActionItemStatusRequest(
    ActionItemStatus Status,
    string? Note = null,
    string? CompletionNote = null);

public record AssignGovernanceActionItemRequest(
    Guid AssignedToUserId,
    string? Note = null);

public record ChangeGovernanceActionItemDueDateRequest(
    DateOnly? DueDate,
    string? Note = null);

public record AddGovernanceActionItemCommentRequest(
    string Body);

public record CancelGovernanceActionItemRequest(
    string? Note = null);

// ===== دليل المُسنَد إليهم (آمن، على مستوى الشركة، لا يكشف أيّ إجراء) =====

public record ActionItemAssigneeDto(Guid Id, string FullName, Guid? DepartmentId, Guid? TeamId);

/// <summary>قائمة اختيار المُسنَد إليه: موظّفون غير حسّاسين على مستوى الشركة.</summary>
public record ActionItemAssigneeDirectoryDto(
    IReadOnlyList<ActionItemAssigneeDto> Users);
