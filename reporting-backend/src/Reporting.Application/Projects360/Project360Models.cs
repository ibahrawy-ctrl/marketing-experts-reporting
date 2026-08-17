using Reporting.Domain.Enums;

namespace Reporting.Application.Projects360;

// ======================================================================
// §3 — الاستراتيجيّة (CPW-R3 · D-04)
// ======================================================================

/// <summary>سمة استراتيجيّة مشروطة بنوع الخدمة (زوج رمز/قيمة محكوم بالكتالوج).</summary>
public record ProjectStrategyAttributeDto(
    string FieldCode,
    string? FieldNameAr,
    string? SectionCode,
    string? SectionNameAr,
    string? ValueText,
    int SortOrder);

/// <summary>الاستراتيجيّة النشطة: النواة الثابتة (11 حقلًا) + السمات الديناميكيّة.</summary>
public record ProjectStrategyDto(
    Guid Id,
    Guid ProjectId,
    string? Vision,
    string? StrategySummary,
    string? TargetAudience,
    string? CustomerPersona,
    string? Positioning,
    string? ValueProposition,
    string? Competitors,
    string? ToneOfVoice,
    string? Messaging,
    string? MarketingApproach,
    string? SuccessFactors,
    IReadOnlyList<ProjectStrategyAttributeDto> Attributes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>سمة واردة في طلب الحفظ.</summary>
public record UpsertProjectStrategyAttributeRequest(
    string FieldCode,
    string? SectionCode = null,
    string? ValueText = null,
    int SortOrder = 0);

/// <summary>
/// حفظ (Upsert) الاستراتيجيّة النشطة. النواة كاملة تُستبدَل بالقيم المرسَلة،
/// والسمات تُزامَن تفاضليًّا (إضافة/تحديث/حذف ما لم يُرسَل).
/// </summary>
public record UpsertProjectStrategyRequest(
    string? Vision = null,
    string? StrategySummary = null,
    string? TargetAudience = null,
    string? CustomerPersona = null,
    string? Positioning = null,
    string? ValueProposition = null,
    string? Competitors = null,
    string? ToneOfVoice = null,
    string? Messaging = null,
    string? MarketingApproach = null,
    string? SuccessFactors = null,
    IReadOnlyList<UpsertProjectStrategyAttributeRequest>? Attributes = null);

/// <summary>حقل في مخطَّط الاستراتيجيّة — تبني الواجهة نموذجها منه بلا Hard Coding.</summary>
public record ProjectStrategySchemaFieldDto(
    string FieldCode,
    string NameAr,
    bool IsCore,
    string? SectionCode,
    string? SectionNameAr,
    int SortOrder);

/// <summary>مخطَّط الاستراتيجيّة: النواة الثابتة + الحقول والأقسام المتاحة في الكتالوج.</summary>
public record ProjectStrategySchemaDto(
    IReadOnlyList<ProjectStrategySchemaFieldDto> CoreFields,
    IReadOnlyList<ProjectStrategySchemaFieldDto> DynamicFields,
    IReadOnlyList<ProjectStrategySectionDto> Sections);

/// <summary>قسم عرض من كتالوج <c>strategy_section</c>.</summary>
public record ProjectStrategySectionDto(string Code, string NameAr, int SortOrder);

// ======================================================================
// §4 — الأهداف
// ======================================================================

/// <summary>
/// هدف مشروع. <c>KpiScore</c> و<c>ProgressPercent</c> **مشتقّان** من مؤشّرات الهدف (DN-02)
/// ولا يُخزَّنان في أيّ عمود.
/// </summary>
public record ProjectObjectiveDto(
    Guid Id,
    Guid ProjectId,
    Guid? WorkstreamId,
    string Name,
    string? Description,
    DeliverablePriority Priority,
    decimal Weight,
    decimal NormalizedWeight,
    ProjectObjectiveStatus Status,
    DateOnly? StartDate,
    DateOnly? DueDate,
    Guid? OwnerUserId,
    string? OwnerFullName,
    string? Notes,
    int SortOrder,
    bool IsActive,
    decimal? ProgressPercent,
    int KpiCount,
    int ComputedKpiCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreateProjectObjectiveRequest(
    string Name,
    string? Description = null,
    Guid? WorkstreamId = null,
    DeliverablePriority Priority = DeliverablePriority.Medium,
    decimal Weight = 0m,
    ProjectObjectiveStatus Status = ProjectObjectiveStatus.NotStarted,
    DateOnly? StartDate = null,
    DateOnly? DueDate = null,
    Guid? OwnerUserId = null,
    string? Notes = null,
    int SortOrder = 0);

public record UpdateProjectObjectiveRequest(
    string Name,
    string? Description = null,
    Guid? WorkstreamId = null,
    DeliverablePriority Priority = DeliverablePriority.Medium,
    decimal Weight = 0m,
    DateOnly? StartDate = null,
    DateOnly? DueDate = null,
    Guid? OwnerUserId = null,
    string? Notes = null,
    int SortOrder = 0);

/// <summary>تحديث حالة الهدف فقط — مسموح لقائد الفريق/مدير الحساب المسؤول (D-07).</summary>
public record UpdateProjectObjectiveStatusRequest(ProjectObjectiveStatus Status);

// ======================================================================
// §5 + §6 — المؤشّرات والقراءات
// ======================================================================

public record ProjectKpiDto(
    Guid Id,
    Guid ProjectId,
    Guid ObjectiveId,
    string Name,
    string? Description,
    ProjectKpiCategory Category,
    ProjectKpiUnit Unit,
    string? CustomUnitLabel,
    ProjectKpiDirection Direction,
    ProjectKpiFrequency Frequency,
    decimal? BaselineValue,
    decimal TargetValue,
    decimal? CurrentValue,
    DateOnly? LastReadingDate,
    decimal Weight,
    ProjectKpiSourceType SourceType,
    string? ExternalSourceKey,
    string? ExternalMetricCode,
    DateTime? LastSyncedAtUtc,
    string? Notes,
    int SortOrder,
    bool IsActive,
    decimal? AchievementPercent,
    decimal? Variance,
    ProjectKpiTrend Trend,
    Guid? OwnerUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// إنشاء مؤشّر — **تحت هدف حصرًا** (D-02). لا يوجد ولن يوجد مسار إنشاء مؤشّر مباشرة تحت المشروع.
/// <c>OwnerUserId</c> مقصود غيابه: مالك المؤشّر هو مالك هدفه في W4 (لا عمود مالك على المؤشّر).
/// </summary>
public record CreateProjectKpiRequest(
    string Name,
    decimal TargetValue,
    string? Description = null,
    ProjectKpiCategory Category = ProjectKpiCategory.Custom,
    ProjectKpiUnit Unit = ProjectKpiUnit.Number,
    string? CustomUnitLabel = null,
    ProjectKpiDirection Direction = ProjectKpiDirection.HigherIsBetter,
    ProjectKpiFrequency Frequency = ProjectKpiFrequency.Monthly,
    decimal? BaselineValue = null,
    decimal Weight = 0m,
    string? Notes = null,
    int SortOrder = 0);

public record UpdateProjectKpiRequest(
    string Name,
    decimal TargetValue,
    string? Description = null,
    ProjectKpiCategory Category = ProjectKpiCategory.Custom,
    ProjectKpiUnit Unit = ProjectKpiUnit.Number,
    string? CustomUnitLabel = null,
    ProjectKpiDirection Direction = ProjectKpiDirection.HigherIsBetter,
    ProjectKpiFrequency Frequency = ProjectKpiFrequency.Monthly,
    decimal? BaselineValue = null,
    decimal Weight = 0m,
    string? Notes = null,
    int SortOrder = 0);

public record ProjectKpiReadingDto(
    Guid Id,
    Guid ProjectKpiId,
    DateOnly ReadingDate,
    decimal Value,
    decimal? TargetSnapshot,
    decimal? AchievementSnapshot,
    Guid RecordedByUserId,
    string? RecordedByFullName,
    string? Notes,
    DateTime CreatedAtUtc);

/// <summary>تسجيل قراءة يدويّة — مسموح فقط حين <c>SourceType = Manual</c>.</summary>
public record CreateProjectKpiReadingRequest(
    DateOnly ReadingDate,
    decimal Value,
    string? Notes = null);

/// <summary>تصحيح قراءة قائمة — التصحيح بالتحديث لا بإضافة قراءة ثانية لنفس التاريخ.</summary>
public record UpdateProjectKpiReadingRequest(
    decimal Value,
    string? Notes = null);

// ======================================================================
// §11 — المخرَج التعاقديّ (Contract Deliverable)
// ======================================================================

public record ProjectContractDeliverableDto(
    Guid Id,
    Guid ProjectId,
    Guid? ObjectiveId,
    Guid? WorkstreamId,
    string DeliverableTypeCode,
    string? DeliverableTypeNameAr,
    string Name,
    string? Description,
    int PlannedQuantity,
    int CompletedQuantity,
    ProjectDeliverableStatus Status,
    decimal ProgressPercent,
    /// <summary>وزن المخرَج في تقدّم المشروع (P360-WF-R2 §6-1) — صفر يعني «غير موزون».</summary>
    decimal WeightPercentage,
    DateOnly? StartDate,
    DateOnly? DueDate,
    DateTime? DeliveredAtUtc,
    DeliverablePriority Priority,
    Guid? OwnerUserId,
    string? OwnerFullName,
    string? Notes,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CreateProjectContractDeliverableRequest(
    string DeliverableTypeCode,
    string? Name = null,
    string? Description = null,
    Guid? ObjectiveId = null,
    Guid? WorkstreamId = null,
    int PlannedQuantity = 1,
    DateOnly? StartDate = null,
    DateOnly? DueDate = null,
    DeliverablePriority Priority = DeliverablePriority.Medium,
    Guid? OwnerUserId = null,
    string? Notes = null,
    int SortOrder = 0,
    decimal WeightPercentage = 0m);

/// <summary>تعديل بنيويّ — <c>DeliverableTypeCode</c> **غير قابل للتعديل** (لقطة ثابتة).</summary>
public record UpdateProjectContractDeliverableRequest(
    string Name,
    string? Description = null,
    Guid? ObjectiveId = null,
    Guid? WorkstreamId = null,
    int PlannedQuantity = 1,
    DateOnly? StartDate = null,
    DateOnly? DueDate = null,
    DeliverablePriority Priority = DeliverablePriority.Medium,
    Guid? OwnerUserId = null,
    string? Notes = null,
    int SortOrder = 0,
    decimal WeightPercentage = 0m);

/// <summary>تحديث التقدّم/الحالة — مسموح لقائد الفريق/مدير الحساب المسؤول (D-07).</summary>
public record UpdateProjectContractDeliverableProgressRequest(
    ProjectDeliverableStatus Status,
    decimal ProgressPercent,
    int? CompletedQuantity = null,
    DateTime? DeliveredAtUtc = null);

// ======================================================================
// §14 — لوحة النظرة العامّة (Overview)
// ======================================================================

/// <summary>
/// **العقد الرسميّ الوحيد للصحّة** — يخدم <c>GET /projects/{id}/overview</c> و
/// <c>POST /projects/{id}/health/recompute</c> معًا فلا يوجد شكلان لنفس المفهوم.
///
/// <para>
/// <c>Score</c> و<c>Status</c> و<c>KpiScore</c> و<c>ProgressPercent</c> و<c>ScheduleScore</c>
/// و<c>ReasonCodes</c> كلّها **من <see cref="ProjectHealthPolicy"/> حصرًا** — لا معادلة ثانية
/// في خدمة ولا في Controller ولا في الواجهة.
/// </para>
///
/// <para>
/// **الأسباب مشتقّة ولا تُخزَّن** (W1-A بند 1): <c>ReasonCodes</c> تُشتقّ لحظيًّا من نفس المكوّنات
/// التي أنتجت النتيجة، ولا عمود ولا جدول لها.
/// </para>
///
/// <para>
/// **الفارق بين الختمين مقصود**: <c>LastEvaluatedAtUtc</c> هو لحظة احتساب **هذه** اللقطة
/// (لحظيّة في القراءة)، بينما <c>PersistedAtUtc</c> هو <c>Project.HealthComputedAtUtc</c>
/// **المخزَّن** — و<c>null</c> فيه يعني «لم تُحتسَب الصحّة المخزَّنة بعد» لا «صفر».
/// </para>
/// </summary>
public record ProjectHealthDto(
    decimal? Score,
    ProjectHealthStatus? Status,
    decimal? KpiScore,
    decimal? ProgressPercent,
    decimal? ScheduleScore,
    IReadOnlyList<string> ReasonCodes,
    DateTime? LastEvaluatedAtUtc,
    DateTime? PersistedAtUtc);

public record ProjectOverviewCoreDto(
    Guid Id,
    string Name,
    Guid ClientId,
    string ClientName,
    ServiceType ServiceType,
    ProjectStatus Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal ProgressPercent,
    string? Summary,
    Guid? ProjectOwnerId,
    Guid? TeamLeaderId,
    Guid? AccountManagerId,
    Guid? OwnerTeamId,

    // --- أسماء المسنَدين (§12): الشاشة تعرض بشرًا لا معرّفات ---
    string? ProjectOwnerName = null,
    string? TeamLeaderName = null,
    string? AccountManagerName = null,
    string? OwnerTeamName = null,

    // --- شفافيّة احتساب التقدّم (§6-1): الرقم ومعه كيف حُسِب ومتى وعلى كم مخرَج ---
    // بلا هذه الثلاثة يصير `ProgressPercent` رقمًا بلا نَسَب: لا يُعرَف أوزنَ أم سقط
    // إلى التساوي، ولا متى حُسِب، فتُقرأ ٦٠٪ محسوبة على مخرَج واحد كأنّها ٦٠٪ للمشروع.
    ProjectProgressMode ProgressMode = ProjectProgressMode.NotCalculated,
    DateTime? ProgressCalculatedAtUtc = null,
    int ProgressSourceDeliverableCount = 0);

public record ProjectOverviewObjectivesDto(
    int Total,
    int NotStarted,
    int InProgress,
    int AtRisk,
    int Completed,
    decimal? AverageAchievementPercent,
    IReadOnlyList<ProjectObjectiveDto> Items);

public record ProjectOverviewKpisDto(
    int Total,
    int OnTrack,
    int AtRisk,
    int OffTrack,
    int WithoutReadings,
    decimal? AverageAchievementPercent,
    DateOnly? LastReadingDate);

/// <summary>
/// عدّادات المخرَجات التعاقديّة **بلا نسبة تقدّم خاصّة بها** (GAP-21).
///
/// <para>
/// كان هنا <c>OverallProgressPercent</c> = متوسّط بسيط لنسب المخرَجات النشطة، يُحسَب لحظة
/// القراءة ولا يدخل أيّ معادلة. فكان في الشاشة الواحدة رقما تقدّمٍ متناقضان (٦٢٫٥٠٪ مقابل
/// ٠٫٠٠٪ على TEST) بلا ما يدلّ القارئ أيّهما الرسميّ. **رقم التقدّم الوحيد** هو
/// <c>ProjectOverviewCoreDto.ProgressPercent</c> ومعه <c>ProgressMode</c> — والمخرَجات
/// تُعَدّ هنا ولا تُنسِّب.
/// </para>
/// </summary>
public record ProjectOverviewDeliverablesDto(
    int Total,
    int Completed,
    int Pending,
    int Delayed);

public record ProjectOverviewGovernanceDto(
    int RisksTotal,
    int RisksOpen,
    int DecisionsTotal,
    int DecisionsPending,
    int NotesTotal,
    int NotesOpen);

public record ProjectOverviewStrategyDto(
    bool Exists,
    int FilledCoreFields,
    int TotalCoreFields,
    int AttributesCount);

/// <summary>
/// حصيلة لوحة المشروع في **رحلة تطبيق واحدة** بعدد استعلامات **ثابت** مستقلّ عن عدد الأهداف/المؤشّرات (§15).
/// </summary>
public record ProjectOverviewDto(
    ProjectOverviewCoreDto Project,
    ProjectHealthDto Health,
    ProjectOverviewObjectivesDto Objectives,
    ProjectOverviewKpisDto Kpis,
    ProjectOverviewDeliverablesDto Deliverables,
    ProjectOverviewGovernanceDto Governance,
    ProjectOverviewStrategyDto Strategy,
    ProjectCapabilitiesDto Access);

// ======================================================================
// W5 — قراءات الحوكمة المرشَّحة بالمشروع (CPW-R3 · §17-19)
//
// **صفر تغيير على الحوكمة**: هذه عقود قراءة فقط فوق `Risk` و`Decision` و`ManagementNote`
// القائمة كما هي — لا عمود جديد ولا حالة جديدة ولا سير عمل جديد ولا كتابة من هذا المسار.
// الرؤية تمرّ ببوّابة Project 360 نفسها، فلا تفتح نافذة رؤية جانبيّة على الحوكمة.
// ======================================================================

/// <summary>عنصر مخاطرة مرتبط بالمشروع — قراءة فقط.</summary>
public record ProjectRiskListItemDto(
    Guid Id,
    string Title,
    string? Description,
    RiskSeverity Severity,
    RiskStatus Status,
    Guid OwnerId,
    string? OwnerFullName,
    string? MitigationPlan,
    string? NextAction,
    DateTime? ClosedAtUtc,
    DateTime CreatedAtUtc);

/// <summary>عنصر قرار مرتبط بالمشروع — قراءة فقط.</summary>
public record ProjectDecisionListItemDto(
    Guid Id,
    string Title,
    string? Description,
    DecisionStatus Status,
    Guid MadeById,
    string? MadeByFullName,
    string? NextAction,
    DateTime? DecidedAtUtc,
    DateTime CreatedAtUtc);

/// <summary>ملاحظة إداريّة على المشروع — قراءة فقط.</summary>
public record ProjectNoteListItemDto(
    Guid Id,
    ManagementNoteType NoteType,
    string Body,
    bool RequiresAction,
    ManagementNoteStatus Status,
    Guid AuthorId,
    string? AuthorFullName,
    DateTime? ResolvedAtUtc,
    DateTime CreatedAtUtc);

/// <summary>خيار من كتالوج التصنيفات يُعرَض في قوائم الواجهة (نوع مخرَج تعاقديّ مثلًا).</summary>
public record ProjectCatalogOptionDto(string Code, string NameAr, int SortOrder);
