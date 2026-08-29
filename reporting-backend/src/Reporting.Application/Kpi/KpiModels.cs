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

// ===== إسناد قوالب KPI (Phase T1) — رؤية/اختيار قالب فقط =====
// يحاكي إسناد قوالب التقارير: مستويات Employee/JobRole/Team/Department + Include/Exclude،
// مع أولوية موحَّدة: استثناء موظّف > إسناد موظّف > مسمّى > فريق > إدارة > عام (JobRoleId == null).
// لا يمسّ التقييمات القائمة (مرتبطة بنسخة مجمّدة) ولا منطق الاعتماد/الاحتساب.

// مستخدم ضمن تغطية قالب KPI: مرتبط (Matched) أو مستثنى (Excluded) مع سبب الربط أو الاستثناء.
public record KpiTemplateAssignmentUserDto(
    Guid UserId,
    string FullName,
    string? Email,
    Guid? JobRoleId,
    string? JobRoleName,
    bool IsActive,
    // أسباب الاستثناء: excludedBecauseInactive / excludedBecauseMoreSpecificTemplateExists /
    // excludedBecauseTemplateNotAssignable / excludedManually
    string? ExclusionReason,
    // سبب الربط للمرتبطين: matchedByUser / matchedByJobRole / matchedByTeam / matchedByDepartment / matchedByGeneral
    string? MatchReason = null,
    // انتماء تنظيمي (قراءة فقط) لتمكين أزرار الاستثناء السريع على مستوى الفريق/الإدارة في الواجهة.
    Guid? TeamId = null,
    string? TeamName = null,
    Guid? DepartmentId = null,
    string? DepartmentName = null);

// صفّ إسناد/استثناء صريح لقالب KPI (للعرض والإدارة في تبويب الإسناد).
public record KpiTemplateAssignmentRowDto(
    Guid Id,
    TemplateAssignmentScope ScopeType,
    Guid ScopeId,
    string? ScopeName,
    TemplateAssignmentKind Kind,
    string? Notes,
    bool IsActive,
    DateTime CreatedAtUtc,
    // DEC-01/6 — تاريخا سريان الإسناد؛ null = ساري بلا حدّ (سلوك ما قبل R5 حرفيًّا).
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null);

// تغطية قالب KPI: المعلومات + المرتبطون + المستثنون بأسبابهم + الإسنادات/الاستثناءات الصريحة.
// تطبّق نفس أولوية الاختيار بالخادم (Employee→JobRole→Team→Department→General، Exclude يتفوّق)
// ضمن نفس الدورية (Cadence) عند موازنة الأخصّية عبر القوالب.
public record KpiTemplateAssignmentsDto(
    Guid TemplateId,
    string Title,
    Guid? JobRoleId,
    string? JobRoleName,
    KpiCadence Cadence,
    TemplateStatus Status,
    bool IsActive,
    // قابل للاختيار فعليًّا في إنشاء التقييمات (منشور ونشط).
    bool IsAssignable,
    // قالب متخصص مربوط بمسمّى وظيفي؟ (عكسه: قالب عام بلا مسمّى).
    bool IsRoleSpecific,
    IReadOnlyList<KpiTemplateAssignmentUserDto> MatchedUsers,
    IReadOnlyList<KpiTemplateAssignmentUserDto> ExcludedUsers,
    IReadOnlyList<KpiTemplateAssignmentRowDto> Assignments);

// إنشاء إسناد/استثناء صريح لقالب KPI على مستوى (موظّف/مسمّى/فريق/إدارة).
// DEC-01/6 — لكلّ تغيير تواتر/قالب تاريخُ سريان: الفترات التاريخيّة لا يُعاد تفسيرها بإعداد لاحق.
// DEC-01/8 — الاستثناء (Exclude) المؤقَّت بتاريخَي سريان هو «الإعفاء الإداريّ المسجَّل» الذي يخفض
// المقام (AdjustedExpected) بدل أن يُعاقِب الموظّف. الحدّان الفارغان ⇒ سلوك ما قبل R5 حرفيًّا.
public record CreateKpiAssignmentRequest(
    TemplateAssignmentScope ScopeType,
    Guid ScopeId,
    TemplateAssignmentKind Kind,
    string? Notes = null,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null);

// تعطيل/تفعيل إسناد قائم + تعديل الملاحظة.
public record UpdateKpiAssignmentRequest(bool IsActive, string? Notes = null);

// ===== تقييمات KPI =====

public record KpiResultDto(
    Guid KpiMetricId,
    string MetricName,
    decimal Weight,
    decimal? TargetValue,
    string? Unit,
    KpiCalcMethod CalcMethod,
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
    IReadOnlyList<KpiResultDto> Results,
    // مراجعة حوكميّة (ADMIN-GOVERNANCE-R1) — المُراجِع المعيَّن وقراره.
    Guid? ReviewerId = null,
    string? ReviewerName = null,
    DateTime? ReviewedAtUtc = null,
    string? ReviewNote = null,
    // القدرات السياقيّة للمستخدم الحالي على هذا التقييم (لإظهار/إخفاء أزرار الواجهة؛ الفرض النهائيّ خادميّ).
    bool CanReview = false,
    bool CanFlag = false,
    bool CanAdminDelete = false,
    bool CanReopen = false);

/// <summary>طلب إجراء مراجعة على تقييم KPI: السبب إلزاميّ لطلب التعديل/الرفض/التعليق/الإشارة/طلب إعادة الفتح/إعادة الفتح/الحذف.</summary>
public record KpiReviewActionRequest(string? Reason = null);

/// <summary>عنصر في سجلّ حوكمة مراجعة تقييم KPI (شاشة الخط الزمنيّ في الحوكمة).</summary>
public record KpiEvaluationReviewEventDto(
    Guid Id,
    string Action,
    Guid ActorId,
    string? ActorName,
    string? FromStatus,
    string? ToStatus,
    string? Reason,
    DateTime CreatedAtUtc);

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

// KPI-REVIEWER-OVERRIDE-R1 — بحث قرائيّ صرف عن تقييم قائم (الموظّف + القالب/الإصدار + مفتاح الفترة).
// لا يُنشئ سجلًّا إطلاقًا ولا يُعدّل أيّ حقل؛ Found=false تعني عدم وجود تقييم مطابق.
public record KpiEvaluationLookupQuery(
    Guid SubjectUserId,
    string PeriodKey,
    Guid? KpiTemplateId = null,
    Guid? KpiTemplateVersionId = null);

public record KpiEvaluationLookupDto(bool Found, KpiEvaluationDto? Evaluation);

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
    Guid? DepartmentId = null,
    // P1-KPI-007 (B-3): مُدخَل إضافيّ لا يكسر المستهلكين القائمين. غيابه ليس خلطًا صامتًا؛ هذه النقطة
    // أسبوعيّة النطاق بحكم عقدها (PeriodType.Weekly ونقاط أسبوعيّة)، فتُطبَّق WeeklyPulse صراحةً
    // ويُعاد الكادنس المطبَّق في AppliedCadence ليراه العميل.
    KpiCadence? Cadence = null);

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
    IReadOnlyList<KpiWeeklyPointDto> Weeks,
    // P1-KPI-007: الكادنس المطبَّق فعليًّا — حقل إضافيّ لا يكسر أيّ مستهلك قائم.
    KpiCadence AppliedCadence = KpiCadence.WeeklyPulse,
    // B-2: عدد الموظّفين الذين دخلوا التوسيط ذا المرحلتين — حقل إضافيّ.
    int EmployeesCount = 0,
    // B-6: العتبة المطبَّقة (نسخة القالب المنشورة أوّلًا ثمّ الإعداد المركزيّ) — حقل إضافيّ حتّى لا
    // تبقى ثوابت 60/85 متناثرة في الواجهة. `null` = لا حكم، وليس «افترض 60».
    decimal? AppliedBelowTargetThreshold = null);

// ===== تصدير KPI للمالية (KPI-FIN1) — قراءة/تصدير فقط على مستوى الشركة =====
// صفّ لكل تقييم KPI أسبوعي معتمَد يقع داخل الربع المختار (لا متوسط ربع سنوي). إعلامي بحت:
// لا يحسب أو يصرف أيّ مستحقات، ولا يغيّر حالة أيّ تقييم. النطاق مفروض بالسياسة (بلا ScopeResolver).
// «تاريخ آخر تحديث/اعتماد» = UpdatedAtUtc (لا يوجد ApprovedAtUtc بعد؛ تاريخ الاعتماد الدقيق يحتاج مرحلة لاحقة).

/// <summary>مُرشِّحات تصدير KPI للمالية: السنة والربع إلزاميان، والإدارة/الفريق/الحالة اختيارية.</summary>
public record KpiFinanceExportFilter(
    int Year,
    int Quarter,
    Guid? DepartmentId = null,
    Guid? TeamId = null,
    // الحالة المسموح تصديرها: Approved افتراضيًّا، أو Closed. أيّ حالة أخرى تُرفَض (kpi_finance.status_invalid).
    KpiEvaluationStatus? Status = null);

/// <summary>صفّ تصدير KPI للمالية: تقييم أسبوعي معتمَد واحد داخل الربع (لا تجميع).</summary>
public record KpiFinanceExportRowDto(
    Guid EvaluationId,
    Guid SubjectUserId,
    string EmployeeName,
    string? DepartmentName,
    string? TeamName,
    string? JobRoleName,
    PeriodType PeriodType,
    string PeriodKey,
    int Year,
    int Quarter,
    string TemplateTitle,
    decimal? TotalScore,
    KpiEvaluationStatus Status,
    // UpdatedAtUtc — يُعرَض في الواجهة/الـCSV بعنوان «تاريخ آخر تحديث / اعتماد».
    DateTime LastUpdatedAtUtc);

/// <summary>نتيجة معاينة تصدير KPI للمالية: وصف الفترة + عدد الصفوف + الصفوف.</summary>
public record KpiFinanceExportDto(
    int Year,
    int Quarter,
    string PeriodLabel,
    DateOnly RangeStart,
    DateOnly RangeEnd,
    KpiEvaluationStatus Status,
    int RowCount,
    IReadOnlyList<KpiFinanceExportRowDto> Rows);
