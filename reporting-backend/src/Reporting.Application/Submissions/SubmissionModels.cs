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

// ===== SUBMITTED-REPORTS-MISSING-EXPECTED-OVERDUE-R1 — العقد الوظيفيّ للصفّ الموحّد =====

/// <summary>
/// نوع صفّ العرض الموحّد في «كل التقارير»:
/// <see cref="ExistingSubmission"/> = تسليم فعليّ قائم (صفّ في report_submissions)،
/// <see cref="ExpectedMissingSubmission"/> = التزام متوقّع لم يُقدَّم إطلاقًا (لا صفّ في القاعدة — مُشتقّ آنيًّا).
/// لا يُكتَب أيّ تسليم صناعيّ إلى القاعدة؛ الصفّ المتوقّع يُبنى قراءةً فقط من IExpectedSubmissionStatusResolver.
/// </summary>
public enum SubmissionRowKind
{
    ExistingSubmission = 0,
    ExpectedMissingSubmission = 1
}

/// <summary>
/// الفلتر السريع الموحّد (يطابق شرائح الواجهة). <see cref="Overdue"/> يُرجِع النوعين معًا
/// (المتأخّر القائم Draft/Returned بعد الموعد + المتوقّع غير المُقدَّم بعد الموعد).
/// </summary>
public enum SubmissionQuickFilter
{
    None = 0,
    Overdue = 1,
    NeedsAction = 2,
    Returned = 3,
    Closed = 4,
    MineApproval = 5
}

/// <summary>
/// فلتر الدورية الصريح للعرض الموحّد (DAILY-REPORTING-APPLICABILITY-R1): الافتراضيّ <see cref="All"/>
/// يشمل اليوميّ والأسبوعيّ معًا؛ <see cref="Daily"/> يقصر على اليوميّة؛ <see cref="Weekly"/> يقصر على الأسبوعيّة.
/// يُطبَّق على التسليمات الفعليّة والصفوف المتوقّعة غير المُقدَّمة على حدٍّ سواء.
/// </summary>
public enum SubmissionCadenceFilter
{
    All = 0,
    Daily = 1,
    Weekly = 2
}

/// <summary>
/// صفّ العرض الموحّد. المفتاح المنطقيّ = ReportTemplateId + SubmitterId + PeriodKey (للتوحيد ومنع التكرار).
/// عقد الصفّ المتوقّع غير المُقدَّم (null-safe): SubmissionId=null، HasSubmission=false،
/// IsExpectedSubmission=true، Status="NotSubmitted"، CurrentApproverId=null، SubmittedAtUtc=null،
/// IsOverdue=true فقط بعد تجاوز حدّ الاستحقاق الصارم.
/// </summary>
public sealed record UnifiedSubmissionRowDto(
    SubmissionRowKind RowKind,
    Guid? SubmissionId,
    Guid? ReportTemplateId,
    string TemplateTitle,
    Guid SubmitterId,
    string SubmitterName,
    Guid? TeamId,
    string? TeamName,
    Guid? DepartmentId,
    string? DepartmentName,
    PeriodType PeriodType,
    string PeriodKey,
    string Status,
    string StatusLabel,
    string Severity,
    DateTime? SubmittedAtUtc,
    Guid? CurrentApproverId,
    DateOnly DueAt,
    bool HasSubmission,
    bool IsExpectedSubmission,
    bool IsOverdue,
    int DelayDays);

/// <summary>
/// فلتر العرض الموحّد. يوسّع فلاتر القائمة القائمة بـ QuickFilter + ReportTemplateId + Search + ترقيم الصفحات.
/// كلّ الفلاتر (عدا الترقيم) تنطبق أيضًا على الصفّ المتوقّع غير المُقدَّم.
/// </summary>
public sealed record UnifiedSubmissionFilter(
    string? PeriodKey = null,
    Guid? SubmitterId = null,
    Guid? TeamId = null,
    Guid? DepartmentId = null,
    Guid? ReportTemplateId = null,
    SubmissionStatus? Status = null,
    string? Search = null,
    SubmissionQuickFilter QuickFilter = SubmissionQuickFilter.None,
    SubmissionCadenceFilter Cadence = SubmissionCadenceFilter.All,
    int Page = 1,
    int PageSize = 200);

/// <summary>
/// عدّادات العرض الموحّد — تُحسَب من نفس المصدر الموحّد بعد النطاق والفلاتر وQuickFilter (قبل الترقيم فقط).
/// القرار الوظيفيّ المعتمد: البطاقات تعكس نفس مجموعة الصفوف بعد QuickFilter (W30 + Overdue ⇒ أرقام متأخّري W30 فقط).
/// OverdueCount = مجموع المتأخّر القائم + المتوقّع المتأخّر (كلاهما).
/// WaitingMyApprovalCount = عدد مميّز للتسليمات الفعليّة (ExistingSubmission بمعرّف) التي معتمِدها الحاليّ = المستخدم المصادَق؛
/// يُحسَب خادميًّا بعد النطاق والفلاتر وQuickFilter وقبل الترقيم، بلا اعتماد على الدور ولا صفوف متوقّعة غير مُقدَّمة.
/// </summary>
public sealed record UnifiedSubmissionSummaryDto(
    string? PeriodKey,
    int Total,
    int OverdueCount,
    int ExistingOverdueCount,
    int MissingOverdueCount,
    int ExpectedMissingCount,
    int NeedsActionCount,
    int ReturnedCount,
    int ClosedCount,
    int WaitingMyApprovalCount,
    IReadOnlyList<StatusCount> ByStatus);

/// <summary>
/// نتيجة العرض الموحّد: صفوف مُرقَّمة + عدّادات على المجموعة بعد QuickFilter (قبل الترقيم فقط).
/// المصدر واحد: التسليمات الفعليّة UNION الالتزامات المتوقّعة غير المُقدَّمة، بعد النطاق ثم الفلاتر ثم QuickFilter؛
/// ثمّ Summary والصفوف كلاهما من نفس المجموعة (TotalCount == Summary.Total دائمًا).
/// </summary>
public sealed record UnifiedSubmissionOverviewDto(
    IReadOnlyList<UnifiedSubmissionRowDto> Items,
    UnifiedSubmissionSummaryDto Summary,
    int Page,
    int PageSize,
    int TotalCount);
