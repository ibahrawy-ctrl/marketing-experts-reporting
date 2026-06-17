using Reporting.Domain.Enums;

namespace Reporting.Application.Development;

// ===== Training Need =====
public record TrainingNeedDto(
    Guid Id,
    Guid SubjectUserId,
    string? SubjectName,
    Guid RaisedById,
    string? RaisedByName,
    string Title,
    string? Description,
    string? Source,
    TrainingNeedStatus Status,
    Guid? RelatedKpiEvaluationId,
    DateTime CreatedAtUtc);

public record CreateTrainingNeedRequest(
    Guid SubjectUserId,
    string Title,
    string? Description,
    string? Source,
    Guid? RelatedKpiEvaluationId);

public record UpdateTrainingNeedRequest(
    string Title,
    string? Description,
    TrainingNeedStatus Status);

public record TrainingNeedFilter(
    Guid? SubjectUserId = null,
    TrainingNeedStatus? Status = null);

// ===== Improvement Plan =====
public record ImprovementPlanDto(
    Guid Id,
    Guid SubjectUserId,
    string? SubjectName,
    Guid OwnerId,
    string? OwnerName,
    string Title,
    string? Description,
    ImprovementPlanStatus Status,
    DateTime? DueDateUtc,
    Guid? RelatedTrainingNeedId,
    DateTime CreatedAtUtc);

public record CreateImprovementPlanRequest(
    Guid SubjectUserId,
    string Title,
    string? Description,
    DateTime? DueDateUtc,
    Guid? RelatedTrainingNeedId);

public record UpdateImprovementPlanRequest(
    string Title,
    string? Description,
    ImprovementPlanStatus Status,
    DateTime? DueDateUtc);

public record ImprovementPlanFilter(
    Guid? SubjectUserId = null,
    ImprovementPlanStatus? Status = null);
