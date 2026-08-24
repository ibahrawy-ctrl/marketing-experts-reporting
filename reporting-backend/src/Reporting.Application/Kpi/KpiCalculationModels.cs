using Reporting.Application.Periods;
using Reporting.Domain.Enums;

namespace Reporting.Application.Kpi;

// =====================================================================================
// P1-KPI-003/004 — عقود القراءة الموحّدة لتحليلات KPI (v2).
// كل رقم مؤسّسيّ هنا مبنيّ على تقييمات Approved فقط، داخل الفترة والكادنس المحدَّدين،
// وبحبيبيّة موظّف/فترة، ويحمل بياناته الوصفيّة الكاملة (§5.5) — لا رقم عارٍ بلا تغطية.
// التعدادات تُسلسَل نصًّا (JsonStringEnumConverter مُفعَّل في Program.cs).
// =====================================================================================

/// <summary>جودة بيانات الرقم المعروض (§5.5). <c>NoData</c> ≠ صفر، و<c>InsufficientCoverage</c> يُستبعَد من الترتيب.</summary>
public enum KpiDataQuality
{
    /// <summary>لا تقييم معتمَد واحد داخل الفترة ⇒ لا رقم إطلاقًا (وليس صفرًا).</summary>
    NoData = 0,
    /// <summary>التغطية اكتملت: عدد التقييمات المعتمدة ≥ المتوقَّع المعدَّل.</summary>
    Complete = 1,
    /// <summary>تغطية جزئيّة مقبولة (≥ الحدّ الأدنى للترتيب) لكنّها ناقصة.</summary>
    Partial = 2,
    /// <summary>تغطية دون الحدّ الأدنى (B-5 = 75%) ⇒ تُعرَض فرديًّا بشارة وتُستبعَد من الترتيب والمقارنة الرسميّة.</summary>
    InsufficientCoverage = 3
}

/// <summary>الفترة المحلولة كما تُسلَّم في العقد (§5.5 <c>periodResolved</c>).</summary>
public sealed record KpiPeriodResolvedDto(
    string Type,
    string Key,
    DateOnly Start,
    DateOnly End,
    string Timezone,
    bool IsOpen,
    string Label)
{
    public static KpiPeriodResolvedDto From(ResolvedPeriod p) =>
        new(p.Type, p.Key, p.Start, p.End, p.TimeZone, p.IsOpen, p.Label);
}

/// <summary>
/// رقم KPI واحد مع بياناته الوصفيّة الكاملة (§5.5). العدّادات مسمّاة بأسمائها الصريحة
/// (لا <c>numerator/denominator</c> غامضين). <c>Value</c> = <c>null</c> يعني «لا بيانات» لا صفرًا.
/// </summary>
public sealed record KpiMeasureDto(
    decimal? Value,
    /// <summary>تقييمات Approved ذات درجة غير فارغة داخل الفترة والكادنس.</summary>
    int EligibleEvaluationCount,
    /// <summary>الالتزامات المتوقَّعة قبل خصم الإعفاءات (= عدد الدورات داخل الفترة للنبض الأسبوعيّ).</summary>
    int ExpectedEvaluationCount,
    /// <summary>المتوقَّع بعد خصم الإجازات/الإعفاءات المعتمدة — مقام التغطية (B-5).</summary>
    int AdjustedExpectedCount,
    /// <summary><c>EligibleEvaluationCount / AdjustedExpectedCount</c>؛ <c>null</c> إذا المقام صفر.</summary>
    decimal? Coverage,
    /// <summary>التزامات متوقَّعة بلا درجة معتمَدة = max(0, المعدَّل − المؤهَّل). ليست صفرًا في المتوسّط.</summary>
    int MissingCount,
    /// <summary>تقييمات موجودة لكنّها خارج <c>Approved</c> (Draft/InProgress/UnderReview/NeedsRevision/Rejected/Closed).</summary>
    int ExcludedByStatusCount,
    KpiDataQuality DataQuality,
    decimal? PreviousValue,
    decimal? Delta,
    KpiTrend Trend);

/// <summary>درجة موظّف واحد داخل فترة وكادنس — صفّ واحد لكلّ موظّف دائمًا (§5.7).</summary>
public sealed record KpiEmployeeScoreDto(
    Guid UserId,
    string FullName,
    Guid? TeamId,
    string? TeamName,
    Guid? DepartmentId,
    string? DepartmentName,
    KpiMeasureDto Measure,
    /// <summary>هل يدخل الترتيب والمقارنة الرسميّة؟ (تغطية ≥ الحدّ الأدنى **و** تقييم معتمَد واحد على الأقلّ).</summary>
    bool EligibleForRanking,
    /// <summary>دون العتبة المعتمَدة؛ <c>null</c> إذا لا توجد درجة (لا يُفترَض صفر).</summary>
    bool? IsBelowTarget,
    /// <summary>العتبة المطبَّقة فعلًا ومصدرها (إصدار القالب أوّلًا ثمّ الإعداد المركزيّ) — B-6.</summary>
    decimal AppliedBelowTargetThreshold,
    string ThresholdSource);

/// <summary>درجة مجموعة (شركة/إدارة/فريق) محسوبة بمتوسّط متوسّطات الأعضاء — لا متوسّط خام (B-2).</summary>
public sealed record KpiGroupScoreDto(
    string GroupType,
    Guid? GroupId,
    string? GroupName,
    KpiMeasureDto Measure,
    /// <summary>أعضاء لهم درجة معتمَدة داخل الفترة (دخلوا المرحلة الثانية من التوسيط).</summary>
    int ScoredMemberCount,
    /// <summary>أعضاء النطاق المنتمون للمجموعة إجمالًا (بمن فيهم بلا بيانات).</summary>
    int TotalMemberCount);

/// <summary>العقد التنظيميّ الموحّد: شركة + إدارات + فرق + موظّفون، كلّها بنفس الفترة والكادنس والنطاق.</summary>
public sealed record KpiPerformanceDto(
    KpiPeriodResolvedDto PeriodResolved,
    KpiPeriodResolvedDto PreviousPeriodResolved,
    KpiCadence Cadence,
    string ScopeType,
    KpiGroupScoreDto Company,
    IReadOnlyList<KpiGroupScoreDto> Departments,
    IReadOnlyList<KpiGroupScoreDto> Teams,
    IReadOnlyList<KpiEmployeeScoreDto> Employees,
    DateTime CalculatedAtUtc);

/// <summary>ترتيب الأفضل/المحتاجين للدعم — صفّ واحد لكلّ موظّف بعد تطبيق شرط التغطية (B-5/§5.7).</summary>
public sealed record KpiRankingsDto(
    KpiPeriodResolvedDto PeriodResolved,
    KpiCadence Cadence,
    string ScopeType,
    IReadOnlyList<KpiEmployeeScoreDto> TopPerformers,
    IReadOnlyList<KpiEmployeeScoreDto> NeedsSupport,
    /// <summary>عدد الموظّفين الذين لديهم درجة لكن أُخرِجوا من الترتيب لضعف التغطية (شفافيّة لا إخفاء).</summary>
    int ExcludedForInsufficientCoverage,
    decimal MinimumCoverage,
    DateTime CalculatedAtUtc);

/// <summary>صفّ تقييم فعليّ دخل في بناء الرقم — يسمح بإعادة إنتاج المتوسّط يدويًّا.</summary>
public sealed record KpiDrilldownRowDto(
    Guid EvaluationId,
    Guid SubjectUserId,
    string SubjectName,
    string TemplateTitle,
    KpiCadence Cadence,
    PeriodType PeriodType,
    string PeriodKey,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    KpiEvaluationStatus Status,
    decimal? TotalScore,
    DateTime? SubmittedAtUtc);

/// <summary>تفصيل رقم KPI إلى الصفوف التي بنته، بنفس النطاق والسياسة تمامًا.</summary>
public sealed record KpiDrilldownDto(
    KpiPeriodResolvedDto PeriodResolved,
    KpiCadence Cadence,
    Guid? SubjectUserId,
    /// <summary>المتوسّط المُعاد حسابه من الصفوف المُعادة — يجب أن يطابق الرقم المعروض.</summary>
    decimal? RecomputedValue,
    int RowCount,
    IReadOnlyList<KpiDrilldownRowDto> Rows,
    DateTime CalculatedAtUtc);

/// <summary>
/// استعلام تحليلات KPI. <c>Cadence</c> **إلزاميّ صراحةً** (B-3): لا مزج ولا سقوط صامت
/// بين النبض الأسبوعيّ والتقييم الربع سنويّ. النطاق يُفرَض خادميًّا مهما كانت هذه القيم.
/// </summary>
public sealed record KpiAnalyticsQuery(
    string PeriodType,
    KpiCadence? Cadence,
    string? PeriodKey = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? DepartmentId = null,
    Guid? TeamId = null,
    Guid? SubjectUserId = null);
