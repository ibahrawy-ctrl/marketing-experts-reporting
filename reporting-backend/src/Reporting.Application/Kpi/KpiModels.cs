using Reporting.Domain.Enums;

namespace Reporting.Application.Kpi;

// ===== قوالب KPI =====

public record KpiMetricDto(
    Guid Id,
    string Name,
    string? Description,
    int Order,
    decimal Weight,
    decimal? TargetValue,
    string? Unit,
    KpiCalcMethod CalcMethod,
    string? CalcConfigJson);

public record KpiTemplateVersionDto(
    Guid Id,
    int VersionNumber,
    bool IsPublished,
    DateTime? PublishedAtUtc,
    decimal TotalWeight,
    IReadOnlyList<KpiMetricDto> Metrics);

public record KpiTemplateDto(
    Guid Id,
    string Title,
    string? Description,
    Guid? JobRoleId,
    KpiCadence Cadence,
    TemplateStatus Status,
    Guid OwnerId,
    bool IsActive,
    int LatestVersionNumber,
    int MetricCount);

public record KpiTemplateDetailDto(
    Guid Id,
    string Title,
    string? Description,
    Guid? JobRoleId,
    KpiCadence Cadence,
    TemplateStatus Status,
    Guid OwnerId,
    bool IsActive,
    IReadOnlyList<KpiTemplateVersionDto> Versions);

public record CreateKpiTemplateRequest(string Title, string? Description, Guid? JobRoleId, KpiCadence Cadence);
public record UpdateKpiTemplateRequest(string Title, string? Description, Guid? JobRoleId, KpiCadence Cadence);

public record UpsertKpiMetricRequest(
    string Name,
    string? Description,
    decimal Weight,
    decimal? TargetValue,
    string? Unit,
    KpiCalcMethod CalcMethod,
    string? CalcConfigJson);

// SubjectUserId = تصفية قوالب الـ KPI بحسب المسمّى الوظيفي للموظّف المُختار في نموذج التقييم،
// فلا يظهر للمدير إلا القوالب العامّة أو المربوطة بدور هذا الموظّف. لا يؤثّر إن كان null.
public record KpiTemplateFilter(
    Guid? JobRoleId = null,
    KpiCadence? Cadence = null,
    TemplateStatus? Status = null,
    bool? IsActive = null,
    Guid? SubjectUserId = null);

// ===== تقييمات KPI =====

public record KpiResultDto(
    Guid KpiMetricId,
    string MetricName,
    decimal Weight,
    decimal? TargetValue,
    string? Unit,
    decimal? RawValue,
    decimal? Score,
    string? Note);

public record KpiEvaluationDto(
    Guid Id,
    Guid KpiTemplateVersionId,
    string TemplateTitle,
    KpiCadence Cadence,
    Guid SubjectUserId,
    string SubjectName,
    Guid? EvaluatorId,
    string? EvaluatorName,
    Guid? TeamId,
    Guid? DepartmentId,
    PeriodType PeriodType,
    string PeriodKey,
    KpiEvaluationStatus Status,
    decimal? TotalScore,
    KpiTrend Trend,
    bool IsBelowTarget,
    DateTime? SubmittedAtUtc,
    bool CanEdit,
    IReadOnlyList<KpiResultDto> Results);

public record KpiEvaluationListItemDto(
    Guid Id,
    string TemplateTitle,
    Guid SubjectUserId,
    string SubjectName,
    Guid? EvaluatorId,
    PeriodType PeriodType,
    string PeriodKey,
    KpiEvaluationStatus Status,
    decimal? TotalScore,
    KpiTrend Trend);

public record CreateKpiEvaluationRequest(Guid KpiTemplateId, Guid SubjectUserId, PeriodType PeriodType, string PeriodKey);

// نطاق إنشاء التقييم أضيق من نطاق العرض: الأشخاص الذين يحقّ للمستخدم الحالي تقييمهم.
// = مرؤوسوه المباشرون فقط (ManagerId == المُقيّم)، أو كل الموظّفين إن كان أدمن (IsAdminOverride).
public record EvaluatableSubjectDto(Guid Id, string FullName, string Email);
public record EvaluatableSubjectsDto(bool IsAdminOverride, IReadOnlyList<EvaluatableSubjectDto> Subjects);

public record KpiResultInput(Guid KpiMetricId, decimal? RawValue, decimal? Score, string? Note);

public record SaveKpiResultsRequest(IReadOnlyList<KpiResultInput> Results);

public record KpiEvaluationFilter(
    Guid? SubjectUserId = null,
    Guid? EvaluatorId = null,
    Guid? TeamId = null,
    Guid? DepartmentId = null,
    string? PeriodKey = null,
    KpiEvaluationStatus? Status = null);

// ===== تجميع KPI الدوري (Phase 5 §8) — الأسبوع وحدة الأساس، والمتوسطات تُحسب منه =====
// المتوسط الشهري = متوسط أسابيع الشهر، والربع سنوي = متوسط أسابيع الربع، والسنوي = متوسط أسابيع السنة،
// والمخصّص = متوسط الأسابيع داخل المدى. يُفرض النطاق خادميًّا (لا تصفية من الواجهة فقط).
public record KpiAggregateRequest(
    string Granularity,            // "Monthly" | "Quarterly" | "Yearly" | "Custom"
    string? PeriodKey = null,      // "2026-06" / "2026-Q2" / "2026"
    DateOnly? From = null,         // للمدى المخصّص
    DateOnly? To = null,
    Guid? SubjectUserId = null,
    Guid? TeamId = null,
    Guid? DepartmentId = null);

public record KpiWeeklyPointDto(
    string PeriodKey, DateOnly WeekStart, DateOnly WeekEnd, decimal Score, int EvaluationsCount);

public record KpiAggregateDto(
    string Granularity,
    string PeriodLabel,
    DateOnly RangeStart,
    DateOnly RangeEnd,
    decimal? Average,
    int WeeksCount,
    int EvaluationsCount,
    string ScopeType,
    bool CanViewRows,
    IReadOnlyList<KpiWeeklyPointDto> Weeks);
