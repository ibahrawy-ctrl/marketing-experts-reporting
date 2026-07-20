namespace Reporting.Application.Dashboard;

/// <summary>عقد البيانات الموحّد للداشبورد — مصدر الحقيقة لنوع الواجهة والنطاق والصلاحيات (handoff §10–§11).</summary>
public record DashboardDto(
    string DashboardType,
    DashboardPeriodDto Period,
    DashboardUserDto User,
    DashboardScopeDto Scope,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<SummaryCardDto> SummaryCards,
    IReadOnlyList<DashboardWidgetDto> Widgets,
    IReadOnlyList<DashboardActionDto> Actions);

public record DashboardPeriodDto(string PeriodKey, string Label);

public record DashboardUserDto(Guid Id, string Name, string Role);

/// <summary>النطاق الذي تُحسب منه الأرقام — own/team/department/company/governance — مع معرّفات المستخدمين داخله.</summary>
public record DashboardScopeDto(string Type, IReadOnlyList<Guid> Ids);

public record SummaryCardDto(string Key, string Title, decimal? Value, string Status, string? DrilldownKey);

public record DashboardWidgetDto(string Key, string Type, string Title, object? Data);

public record DashboardActionDto(string Key, string Label, string Permission);

// ===== نقاط الـDrill-down (§4–§8) — كلها محصورة بنطاق المستخدم =====

/// <summary>نقطة في منحنى اتجاه KPI لفترة واحدة.</summary>
public record KpiTrendPointDto(string PeriodKey, decimal Score);

/// <summary>اتجاه KPI لشخص محدد عبر آخر الفترات.</summary>
public record KpiTrendDto(Guid SubjectId, string SubjectName, IReadOnlyList<KpiTrendPointDto> Points);

/// <summary>أداء عضو داخل النطاق: متوسط KPI واتجاهه + اكتمال تقاريره للفترة.</summary>
public record MemberPerformanceDto(
    Guid UserId, string Name, decimal? KpiAverage, string KpiTrend, int ReportsTotal, int ReportsCompleted);

/// <summary>عنصر في خلاصة الأنشطة — أحدث التسليمات داخل النطاق.</summary>
public record ActivityItemDto(
    Guid SubmissionId, string SubmitterName, string TemplateTitle, string Status, string PeriodKey, DateTime? SubmittedAtUtc);

/// <summary>
/// بند تقرير يحتاج إجراءً داخل النطاق للفترة (Users-first): متأخّر بلا تسليم (non-starter، <see cref="SubmissionId"/>=null)،
/// أو مسودّة متأخّرة، أو مُعاد للتعديل، أو مُصعَّد. <see cref="Status"/> يحمل اسم <c>ExpectedSubmissionStatus</c>
/// و<see cref="StatusLabel"/>/<see cref="Severity"/> يأتيان جاهزَين من مصدر الحقيقة (لا حساب في الواجهة).
/// </summary>
public record PendingReportDto(
    Guid? SubmissionId, Guid SubmitterId, string SubmitterName, string TemplateTitle, string Status, string PeriodKey)
{
    public string StatusLabel { get; init; } = "";
    public string Severity { get; init; } = "info";
    public bool HasSubmission { get; init; }
}

// ===== ملف أداء الموظف (Phase 3) — صفحة موحّدة محصورة بنطاق المستخدم =====

/// <summary>الملف الموحّد لأداء موظّف واحد: رأس + ملخّص + تقارير + تقييمات + حوكمة + خط زمني.</summary>
public record EmployeeProfileDto(
    EmployeeProfileHeaderDto Header,
    EmployeeProfileSummaryDto Summary,
    IReadOnlyList<EmployeeProfileReportDto> Reports,
    IReadOnlyList<EmployeeProfileKpiDto> KpiEvaluations,
    IReadOnlyList<EmployeeProfileGovernanceDto> GovernanceItems,
    IReadOnlyList<EmployeeProfileTimelineDto> Timeline,
    // الإجازات والاستئذانات الأخيرة (V1.0.1) — عرض فقط، لا إدارة من هنا.
    IReadOnlyList<EmployeeProfileLeaveDto> LeaveRequests);

/// <summary>رأس الملف: البيانات الأساسية + حالة مختصرة (جيد/يحتاج متابعة/متأخر/لا توجد بيانات كافية).</summary>
public record EmployeeProfileHeaderDto(
    Guid UserId, string FullName, string? Email, string? JobRoleName,
    string? TeamName, string? DepartmentName, string? DirectManagerName,
    bool IsActive, string StatusKey, string StatusLabel);

/// <summary>ملخّص الأداء — يُبنى من البيانات المتاحة فقط (لا تجميع KPI دوريّ جديد).</summary>
public record EmployeeProfileSummaryDto(
    decimal? LastKpiScore, string? LastKpiPeriod, string LastKpiTrend,
    decimal? AverageKpi, int KpiCount,
    int ReportsSubmitted, int ReportsReturned, int ReportsNeedsAction,
    int OpenNotesRequiringAction);

/// <summary>تقرير حديث للموظّف.</summary>
public record EmployeeProfileReportDto(
    Guid SubmissionId, string TemplateTitle, string PeriodKey, string Status,
    DateTime? SubmittedAtUtc, string? CurrentApproverName);

/// <summary>تقييم KPI حديث للموظّف.</summary>
public record EmployeeProfileKpiDto(
    Guid EvaluationId, string TemplateTitle, string PeriodKey, decimal? TotalScore, string Status, string Trend);

/// <summary>بند حوكمة مرتبط بالموظّف (مخاطرة بصفته موضوعًا، أو تصعيد موجَّه إليه).</summary>
public record EmployeeProfileGovernanceDto(
    string Kind, Guid Id, string Title, string Status, DateTime CreatedAtUtc);

/// <summary>حدث مبسّط في الخط الزمني للموظّف (تسليم/إعادة/تقييم/ملاحظة/تصعيد).</summary>
public record EmployeeProfileTimelineDto(string Kind, string Label, DateTime AtUtc);

/// <summary>إجازة/استئذان حديث للموظّف — يوضّح النوع والمدّة والحالة والمعتمِد وأثره في التقارير.</summary>
public record EmployeeProfileLeaveDto(
    Guid Id, string Type, DateOnly StartDate, DateOnly EndDate,
    TimeOnly? StartTime, TimeOnly? EndTime, string Status,
    string? FinalApproverName, bool ImpactsReports, DateTime CreatedAtUtc);
