using Reporting.Domain.Enums;

namespace Reporting.Application.Submissions;

public record SubmissionFieldValueDto(
    Guid TemplateFieldId,
    string Label,
    FieldType FieldType,
    string? ValueText,
    decimal? ValueNumber,
    DateTime? ValueDate,
    bool? ValueBool,
    string? ValueJson,
    bool IsRequired,
    string? HelpText,
    string? ConfigJson);

public record ApprovalStepDto(
    int Level,
    Guid ApproverId,
    string? ApproverName,
    ApprovalStatus Status,
    string? Comment,
    DateTime? DecidedAtUtc);

public record SubmissionDto(
    Guid Id,
    Guid ReportTemplateVersionId,
    string TemplateTitle,
    Guid SubmitterId,
    string SubmitterName,
    Guid? TeamId,
    Guid? DepartmentId,
    PeriodType PeriodType,
    string PeriodKey,
    SubmissionStatus Status,
    DateTime? SubmittedAtUtc,
    DateTime? ClosedAtUtc,
    Guid? CurrentApproverId,
    bool CanEdit,
    IReadOnlyList<SubmissionFieldValueDto> FieldValues,
    IReadOnlyList<ApprovalStepDto> ApprovalSteps,
    Guid? ClientId = null,
    string? ClientName = null,
    Guid? ProjectId = null,
    string? ProjectName = null);

public record SubmissionListItemDto(
    Guid Id,
    string TemplateTitle,
    Guid SubmitterId,
    string SubmitterName,
    Guid? TeamId,
    Guid? DepartmentId,
    PeriodType PeriodType,
    string PeriodKey,
    SubmissionStatus Status,
    DateTime? SubmittedAtUtc,
    Guid? CurrentApproverId);

public record CreateSubmissionRequest(Guid ReportTemplateId, PeriodType PeriodType, string PeriodKey, Guid? ProjectId = null);

public record FieldValueInput(
    Guid TemplateFieldId,
    string? ValueText,
    decimal? ValueNumber,
    DateTime? ValueDate,
    bool? ValueBool,
    string? ValueJson);

public record SaveFieldValuesRequest(IReadOnlyList<FieldValueInput> Values);

public record ApprovalActionRequest(string? Comment);

/// <summary>طلب حذف إداريّ ناعم لتقرير مُسلَّم (ADMIN-GOVERNANCE-R1): السبب إلزاميّ ويُحفَظ في الأثر التدقيقيّ.</summary>
public record AdminDeleteRequest(string? Reason);

public record SubmissionFilter(
    SubmissionStatus? Status = null,
    string? PeriodKey = null,
    Guid? SubmitterId = null,
    Guid? TeamId = null,
    Guid? DepartmentId = null);

public record StatusCount(SubmissionStatus Status, int Count);

public record SubmissionSummaryDto(
    string? PeriodKey,
    int Total,
    IReadOnlyList<StatusCount> ByStatus);
