using Reporting.Domain.Enums;

namespace Reporting.Application.Governance;

// ===== Risk =====
public record RiskDto(
    Guid Id,
    string Title,
    string? Description,
    RiskSeverity Severity,
    RiskStatus Status,
    Guid OwnerId,
    string? OwnerName,
    Guid? DepartmentId,
    string? MitigationPlan,
    DateTime? ClosedAtUtc,
    DateTime CreatedAtUtc,
    Guid? RelatedSubmissionId = null,
    Guid? RelatedKpiEvaluationId = null,
    Guid? SubjectUserId = null,
    Guid? TeamId = null,
    string? NextAction = null,
    Guid? ClientId = null,
    Guid? ProjectId = null);

public record CreateRiskRequest(
    string Title,
    string? Description,
    RiskSeverity Severity,
    Guid? OwnerId,
    Guid? DepartmentId,
    string? MitigationPlan,
    Guid? RelatedSubmissionId = null,
    Guid? RelatedKpiEvaluationId = null,
    Guid? SubjectUserId = null,
    Guid? TeamId = null,
    string? NextAction = null,
    Guid? ClientId = null,
    Guid? ProjectId = null);

public record UpdateRiskRequest(
    string Title,
    string? Description,
    RiskSeverity Severity,
    RiskStatus Status,
    string? MitigationPlan,
    string? NextAction = null);

public record RiskFilter(
    RiskStatus? Status = null,
    RiskSeverity? Severity = null,
    Guid? DepartmentId = null,
    Guid? OwnerId = null);

// ===== Escalation =====
public record EscalationDto(
    Guid Id,
    Guid RaisedById,
    string? RaisedByName,
    Guid TargetUserId,
    string? TargetName,
    string Reason,
    EscalationStatus Status,
    Guid? ReportSubmissionId,
    Guid? RiskId,
    DateTime? ResolvedAtUtc,
    string? Resolution,
    DateTime CreatedAtUtc,
    Guid? KpiEvaluationId = null,
    string? NextAction = null);

public record CreateEscalationRequest(
    Guid TargetUserId,
    string Reason,
    Guid? ReportSubmissionId,
    Guid? RiskId,
    Guid? KpiEvaluationId = null);

public record ResolveEscalationRequest(
    EscalationStatus Status,
    string? Resolution,
    string? NextAction = null);

public record EscalationFilter(
    EscalationStatus? Status = null,
    Guid? TargetUserId = null,
    Guid? RaisedById = null);

// ===== Decision =====
public record DecisionDto(
    Guid Id,
    string Title,
    string? Description,
    Guid MadeById,
    string? MadeByName,
    DecisionStatus Status,
    Guid? RelatedSubmissionId,
    Guid? RelatedRiskId,
    Guid? RelatedEscalationId,
    DateTime? DecidedAtUtc,
    DateTime CreatedAtUtc,
    Guid? RelatedKpiEvaluationId = null,
    string? NextAction = null,
    // الربط بالمشروع كان يُكتَب ولا يُقرأ: يُنشئ المستخدم قرارًا على مشروع ثمّ لا يجد في أيّ
    // استجابة ما يدلّ على أنّه ارتبط به. ربطٌ لا يُقرأ لا يمكن التحقّق منه ولا تصحيحه.
    Guid? ProjectId = null);

public record CreateDecisionRequest(
    string Title,
    string? Description,
    Guid? RelatedSubmissionId,
    Guid? RelatedRiskId,
    Guid? RelatedEscalationId,
    Guid? RelatedKpiEvaluationId = null,
    Guid? ProjectId = null);

public record UpdateDecisionRequest(
    string Title,
    string? Description,
    DecisionStatus Status,
    string? NextAction = null);

public record DecisionFilter(DecisionStatus? Status = null);
