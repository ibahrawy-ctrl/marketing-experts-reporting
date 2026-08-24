using System.Text.Json.Serialization;

namespace Reporting.Application.Employee360;

/// <summary>
/// P2-EMP-002 — عرض قراءة (Read Model) لموظّف واحد. لا يملك بيانات ولا يكرّرها؛
/// كلّ قسم إسقاط مباشر من مصدر الحقيقة المالك له (التقارير/KPI/الإجازات/الحوكمة…).
/// **القسم غير المصرَّح به لا يظهر في <see cref="Sections"/> إطلاقًا** — لا مفتاحًا ولا قيمة فارغة.
/// </summary>
public sealed record Employee360Dto(
    Guid SubjectUserId,
    bool IsSelf,
    string ViewerRelation,
    string? PeriodKey,
    IReadOnlyDictionary<string, Employee360SectionDto> Sections);

/// <summary>
/// غلاف موحّد لكلّ قسم: حالته وجودة بياناته وآخر تحديث، ثمّ ملخّصه وعناصره.
/// فشل قسم واحد يبقى محصورًا فيه (<c>Status = Error</c>) ولا يُسقِط بقيّة الصفحة.
/// </summary>
public sealed record Employee360SectionDto(
    string Key,
    string TitleAr,
    string Status,
    string DataQuality,
    DateTime? LastUpdatedAtUtc,
    object? Summary,
    IReadOnlyList<object>? Items,
    string? Reason = null);

/// <summary>حالات القسم كما تظهر للواجهة — نصّ ثابت لا يُترجَم في الخادم.</summary>
public static class Employee360SectionStatus
{
    public const string Ready = "Ready";
    public const string NoData = "NoData";
    public const string Partial = "Partial";
    public const string Error = "Error";
}

/// <summary>جودة بيانات القسم: مكتملة، أو جزئيّة، أو غير متاحة أصلًا في هذا الإصدار.</summary>
public static class Employee360DataQuality
{
    public const string Complete = "Complete";
    public const string Partial = "Partial";
    public const string Unavailable = "Unavailable";
}

// ===== عناصر الأقسام =====

/// <summary>(1) الهويّة وحالة التوظيف — الحقول الحسّاسة لا تُدرَج هنا أصلًا.</summary>
public sealed record Employee360IdentityDto(
    Guid UserId, string FullName, string? Email, string? JobRoleName,
    string? TeamName, string? DepartmentName, string? DirectManagerName,
    bool IsActive, DateTime JoinedAtUtc);

/// <summary>(2) الملخّص التشغيليّ — أعداد لا تفاصيل.</summary>
public sealed record Employee360OperationalSummaryDto(
    int ReportsSubmitted, int ReportsReturned, int ReportsNeedsAction,
    int KpiEvaluationCount, decimal? LastKpiScore, string? LastKpiPeriodKey,
    int OpenLeaveRequests, int OpenServiceRequests, int OpenNotesRequiringAction,
    int OpenGovernanceItems);

/// <summary>(3) تقرير واحد في سجلّ الموظّف.</summary>
public sealed record Employee360ReportDto(
    Guid SubmissionId, string TemplateTitle, string PeriodKey, string PeriodType,
    string Status, DateTime? SubmittedAtUtc, DateTime? ClosedAtUtc);

/// <summary>(4) تقييم KPI واحد ضمن نافذة زمنيّة محدّدة.</summary>
public sealed record Employee360KpiEvaluationDto(
    Guid EvaluationId, string TemplateTitle, string PeriodType, string PeriodKey,
    decimal? TotalScore, string Status, string Trend, DateTime? SubmittedAtUtc);

/// <summary>
/// (4) ملخّص KPI بنوافذ مرحلة 1 المعتمدة. الأسبوعيّ والربعيّ **منفصلان**
/// ولا يُخلَط أحدهما بالآخر، وCoverage يوضّح كم فترة توفّرت فعلًا من المتوقَّع.
/// </summary>
public sealed record Employee360KpiSummaryDto(
    Employee360KpiWindowDto? LastCompletedWeek,
    Employee360KpiWindowDto? PreviousWeek,
    Employee360KpiWindowDto? LastFourWeeks,
    Employee360KpiWindowDto? Month,
    Employee360KpiWindowDto? Quarter,
    Employee360KpiWindowDto? Year,
    string Trend,
    Employee360KpiWindowDto? BestWeek,
    Employee360KpiWindowDto? WorstWeek);

/// <summary>نافذة KPI واحدة: متوسّطها وتغطيتها وعدد التقييمات المعتمدة داخلها.</summary>
public sealed record Employee360KpiWindowDto(
    string WindowKey, string PeriodType, string? PeriodKey,
    decimal? AverageScore, int ApprovedCount, int ExpectedPeriods, decimal Coverage);

/// <summary>
/// (5) إجازة/استئذان. سبب الإجازة مصنَّف <c>HrOnly</c>، وحين لا يُصرَّح به
/// **لا يُسلسَل المفتاح أصلًا** (لا <c>null</c> ولا سلسلة فارغة) — وهو ما تفحصه الاختبارات.
/// </summary>
public sealed record Employee360LeaveDto(
    Guid Id, string Type, DateOnly StartDate, DateOnly EndDate,
    TimeOnly? StartTime, TimeOnly? EndTime, string Status, string CurrentStep,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason,
    DateTime CreatedAtUtc);

/// <summary>(6) طلب خدمة موظّفين.</summary>
public sealed record Employee360ServiceRequestDto(
    Guid Id, string RequestType, string Title, string Status, DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);

/// <summary>(6) رصيد من دفتر الأرصدة — محسوب من الحركات لا من حقل مخزَّن.</summary>
public sealed record Employee360BalanceDto(
    string BalanceType, int Year, decimal Credited, decimal Debited, decimal Net);

/// <summary>(7) حادثة حضور — مبدئيّة أو مؤكَّدة، ولا أثر ماليّ لأيّ منهما.</summary>
public sealed record Employee360AttendanceIncidentDto(
    Guid Id, string TypeCode, string TypeNameAr, DateOnly IncidentDate,
    TimeOnly? StartTime, TimeOnly? ReturnTime, int? DurationMinutes,
    string Status, bool IsConfirmed, DateTime CreatedAtUtc);

/// <summary>(8) ملاحظة إداريّة بعد ترشيح الحسّاسيّة — ما لا يجوز رؤيته لا يُسلسَل أصلًا.</summary>
public sealed record Employee360NoteDto(
    Guid Id, string NoteType, string Status, string Sensitivity,
    string Body, bool RequiresAction, DateTime CreatedAtUtc);

/// <summary>(9) عنصر حوكمة مرتبط بالموظّف.</summary>
public sealed record Employee360GovernanceDto(
    string Kind, Guid Id, string Title, string Status, DateTime CreatedAtUtc);

/// <summary>(10) عنصر تطوير أو تدريب.</summary>
public sealed record Employee360DevelopmentDto(
    string Kind, Guid Id, string Title, string Status, DateTime? DueDateUtc,
    DateTime CreatedAtUtc);

/// <summary>(11) حدث في الخطّ الزمنيّ الموحّد — مصدره مذكور صراحةً للتنقّل.</summary>
public sealed record Employee360TimelineEventDto(
    string Kind, string Source, Guid SourceId, string Label,
    DateTime AtUtc, bool NeedsMyAction);
