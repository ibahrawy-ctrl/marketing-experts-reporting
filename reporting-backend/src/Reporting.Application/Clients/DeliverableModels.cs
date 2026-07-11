using Reporting.Domain.Enums;

namespace Reporting.Application.Clients;

// ===== Workstream Deliverable (P2 — خطّة إنتاج تيار العمل) =====
public record WorkstreamDeliverableDto(
    Guid Id,
    Guid WorkstreamId,
    string DeliverableTypeCode,
    string? DeliverableTypeNameAr,
    string? UsageContextCode,
    string? UsageContextNameAr,
    string Name,
    int PlannedQuantity,
    decimal? EstimatedHours,
    DateOnly? StartDate,
    DateOnly? DueDate,
    DeliverablePriority Priority,
    Guid? ResponsibleUserId,
    string? ResponsibleUserName,
    string? Notes,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreateWorkstreamDeliverableRequest(
    string DeliverableTypeCode,
    string? UsageContextCode = null,
    string? Name = null,
    int PlannedQuantity = 1,
    decimal? EstimatedHours = null,
    DateOnly? StartDate = null,
    DateOnly? DueDate = null,
    DeliverablePriority Priority = DeliverablePriority.Medium,
    Guid? ResponsibleUserId = null,
    string? Notes = null,
    int SortOrder = 0);

// نوع المخرَج (DeliverableTypeCode) لا يتغيّر بعد الإنشاء (لقطة ثابتة) — كنمط نوع تيار العمل.
public record UpdateWorkstreamDeliverableRequest(
    string? UsageContextCode = null,
    string? Name = null,
    int PlannedQuantity = 1,
    decimal? EstimatedHours = null,
    DateOnly? StartDate = null,
    DateOnly? DueDate = null,
    DeliverablePriority Priority = DeliverablePriority.Medium,
    Guid? ResponsibleUserId = null,
    string? Notes = null,
    int SortOrder = 0);
