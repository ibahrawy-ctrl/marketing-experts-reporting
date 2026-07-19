using Reporting.Domain.Enums;

namespace Reporting.Application.Reports;

/// <summary>
/// REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — العقد الموحّد لحالة دورة تقرير واحدة (§5 من تقرير التصميم).
/// كلّ الحقول المشتقّة (unifiedStatus/isLate/delayDays/severity/availableActions/statusLabel/statusDescription/
/// isCurrentPriority) تُحسَب في الخادم عبر <see cref="Reporting.Domain.Common.UnifiedCycleStatusPolicy"/>.
/// الواجهة تعرض فقط ولا تحسب أيّ حالة أو تأخّر.
/// </summary>
public sealed record UnifiedReportCycleStatusDto(
    // تعريف الدورة (من التقويم)
    Guid? TemplateId,
    Guid? TemplateVersionId,
    string TemplateName,
    PeriodType PeriodType,
    string PeriodKey,
    string CycleLabel,
    DateOnly CycleStartDate,
    DateOnly CycleEndDate,
    DateOnly DueAt,
    // الإسناد
    Guid? AssignmentId,
    bool IsAssigned,
    // التسليم
    Guid? SubmissionId,
    SubmissionStatus? SubmissionStatus,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? ClosedAt,
    bool HasSubmission,
    // الاشتقاق الزمنيّ
    bool IsLate,
    int DelayDays,
    // الحالة الموحّدة (كلّها من الخادم)
    UnifiedCycleStatus UnifiedStatus,
    string StatusLabel,
    string StatusDescription,
    string Severity,
    IReadOnlyList<string> AvailableActions,
    string ActionUrl,
    bool IsCurrentPriority);
