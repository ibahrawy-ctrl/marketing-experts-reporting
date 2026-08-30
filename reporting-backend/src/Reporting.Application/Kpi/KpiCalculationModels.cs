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

/// <summary>
/// DEC-01/18 — حالة رحلة KPI للموظّف داخل الفترة. حالات صريحة ومتباينة: لا تُخلَط «لا بيانات»
/// بـ«صفر»، ولا «قيد الاستكمال» بـ«تغطية غير كافية»، ولا «مُعفى» بـ«لم يبدأ».
/// </summary>
public enum KpiJourneyState
{
    /// <summary>لا تواتر ولا قالب فعّال (DEC-01/5) ⇒ لا يُحسب متوقَّع ولا تغطية ولا درجة.</summary>
    CadenceNotConfigured = 0,
    /// <summary>كل الالتزامات داخل الفترة مُعفاة (إجازة معتمَدة/استثناء إداريّ/خارج نافذة الخدمة) ⇒ المقام صفر.</summary>
    Exempt = 1,
    /// <summary>عليه التزامات ولم يكتمل له أيّ تقييم بعد. ليس صفرًا في المتوسّط (DEC-01/10).</summary>
    NotStarted = 2,
    /// <summary>الفترة ما زالت مفتوحة وله تقييم مكتمل واحد على الأقلّ والتغطية دون 100%.</summary>
    InProgress = 3,
    /// <summary>التغطية ≥ الحدّ الأدنى المعتمَد (DEC-01/13) ⇒ نتيجة ربعيّة نهائيّة تدخل المتوسّط الرسميّ.</summary>
    CompleteEligible = 4,
    /// <summary>الفترة انتهت والتغطية دون الحدّ الأدنى (DEC-01/14) ⇒ درجة مؤقّتة خارج المتوسّط الرسميّ.</summary>
    InsufficientCoverage = 5
}

/// <summary>
/// DEC-01/5 — التواتر الفعّال لموظّف واحد ومصدره. <c>Cadence = null</c> ⇒ «التواتر غير مُهيّأ»:
/// لا اختيار ولا <c>fallback</c> صامت.
/// </summary>
public sealed record KpiEffectiveCadence(
    Guid UserId,
    KpiCadence? Cadence,
    /// <summary>employeeAssignment | teamAssignment | jobRole | departmentAssignment | generalTemplate | notConfigured.</summary>
    string Source,
    IReadOnlyCollection<Guid> TemplateIds);

/// <summary>
/// OBS-R5-01 — المساران معًا لموظّف واحد: <b>نبض الأسبوع</b> و<b>التقييم الربعيّ الرسميّ</b>.
/// المساران متزامنان لا متبادلان: سلّم الأولويّة (موظّف ← فريق ← مسمّى ← إدارة ← عامّ) يُطبَّق
/// <b>داخل كلّ مسار على حدة</b>، فلا يُخفي فوزُ مستوًى في أحدهما المسارَ الآخر. غياب التهيئة في
/// مسار حالةٌ مسمّاة لذلك المسار وحده (<c>Cadence = null</c>) ولا يمسّ المسار المقابل.
/// </summary>
public sealed record KpiEffectiveTracks(
    Guid UserId,
    KpiEffectiveCadence WeeklyPulse,
    KpiEffectiveCadence Quarterly)
{
    public KpiEffectiveCadence For(KpiCadence cadence) =>
        cadence == KpiCadence.Quarterly ? Quarterly : WeeklyPulse;

    /// <summary>
    /// المسار الأوّليّ حين لا يطلب المستهلك مسارًا بعينه (نداءات التحليلات بلا <c>cadence</c>).
    ///
    /// OBS-R5-01/2 — لا يجوز تفضيل مسار <b>لنوعه</b> هنا: موظّف قالبه الأسبوعيّ مُسنَد إلى مسمّاه
    /// بينما مساره الربعيّ يأتي من قالب <b>عامّ</b> ⟹ تفضيل الربعيّ لنوعه يُلغي أخصّ إسناد قُصِد به
    /// فعلًا ويحسب أداءه بقالب عامّ. لذلك القاعدة: <b>الأخصّ إسنادًا يفوز</b> بنفس سلّم DEC-01،
    /// وعند تساوي المستوى يفوز الربعيّ الرسميّ لأنّه المسار الحاسم في المتوسّط والمكافآت.
    /// المسار غير المُهيّأ لا ينافس أصلًا. والمساران يبقيان متاحين صراحةً عبر <see cref="For"/>.
    /// </summary>
    public KpiEffectiveCadence Primary =>
        Quarterly.Cadence is null ? WeeklyPulse
        : WeeklyPulse.Cadence is null ? Quarterly
        : KpiCadenceSources.Specificity(WeeklyPulse.Source) < KpiCadenceSources.Specificity(Quarterly.Source)
            ? WeeklyPulse
            : Quarterly;

    public static KpiEffectiveTracks NotConfigured(Guid userId) => new(
        userId,
        new KpiEffectiveCadence(userId, null, KpiCadenceSources.NotConfigured, Array.Empty<Guid>()),
        new KpiEffectiveCadence(userId, null, KpiCadenceSources.NotConfigured, Array.Empty<Guid>()));
}

/// <summary>مصادر التواتر الفعّال كما تُسلَّم في العقد — نصوص ثابتة لا تُترجَم في الخادم.</summary>
public static class KpiCadenceSources
{
    public const string EmployeeAssignment = "employeeAssignment";
    public const string TeamAssignment = "teamAssignment";
    public const string JobRole = "jobRole";
    public const string DepartmentAssignment = "departmentAssignment";
    public const string GeneralTemplate = "generalTemplate";
    public const string NotConfigured = "notConfigured";
    /// <summary>الكادنس أتى صراحةً من الطلب (مسار «النبض الأسبوعيّ» المنفصل — DEC-01/3).</summary>
    public const string ExplicitRequest = "explicitRequest";

    /// <summary>
    /// درجة أخصّية مصدر الحسم — الأصغر أخصّ، بنفس سلّم DEC-01: موظّف ← فريق ← مسمّى ← إدارة ← عامّ.
    /// مصدر واحد للسلّم يقرأه حاسم المسار ومنتقي المسار الأوّليّ معًا، فلا يتكرّر السلّم بصيغتين
    /// قابلتين للتباعد (وهو بعينه العيب الذي عولج في <c>KpiTemplateService</c>).
    /// </summary>
    public static int Specificity(string source) => source switch
    {
        EmployeeAssignment => 1,
        TeamAssignment => 2,
        JobRole => 3,
        DepartmentAssignment => 4,
        GeneralTemplate => 5,
        _ => 6
    };
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
    /// <summary><c>EligibleEvaluationCount / AdjustedExpectedCount</c> كنسبة (0..1)؛ <c>null</c> إذا المقام صفر.</summary>
    decimal? Coverage,
    /// <summary>التزامات متوقَّعة بلا درجة معتمَدة = max(0, المعدَّل − المؤهَّل). ليست صفرًا في المتوسّط.</summary>
    int MissingCount,
    /// <summary>تقييمات موجودة لكنّها خارج حالات الاكتمال (Draft/InProgress/Submitted/UnderReview/NeedsRevision/Rejected).</summary>
    int ExcludedByStatusCount,
    KpiDataQuality DataQuality,
    decimal? PreviousValue,
    decimal? Delta,
    KpiTrend Trend,
    /// <summary>
    /// DEC-01/12 — <c>Completed ÷ AdjustedExpected × 100</c> مقرَّبة إلى منزلتين، وهي **الرقم المعروض**
    /// (مثال العقد الحاكم: 1 من 9 ⇒ 11.11). <c>Coverage</c> أعلاه يبقى نسبةً للمقارنات الداخليّة.
    /// </summary>
    decimal? CoveragePercent = null,
    /// <summary>DEC-01/14 — درجة محسوبة لكنّ تغطيتها دون الحدّ الأدنى ⇒ «مؤقّتة»، خارج المتوسّط الرسميّ والتصدير المالي.</summary>
    bool IsProvisional = false,
    /// <summary>DEC-01/18 — حالة الرحلة الصريحة.</summary>
    KpiJourneyState JourneyState = KpiJourneyState.NotStarted);

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
    string ThresholdSource,
    /// <summary>DEC-01/5 — التواتر الفعّال لهذا الموظّف؛ <c>null</c> ⇒ «التواتر غير مُهيّأ».</summary>
    KpiCadence? EffectiveCadence = null,
    /// <summary>DEC-01/5 — مصدر التواتر (انظر <see cref="KpiCadenceSources"/>) — شفافيّة الحسم لا صندوق أسود.</summary>
    string CadenceSource = KpiCadenceSources.NotConfigured);

/// <summary>درجة مجموعة (شركة/إدارة/فريق) محسوبة بمتوسّط متوسّطات الأعضاء — لا متوسّط خام (B-2).</summary>
public sealed record KpiGroupScoreDto(
    string GroupType,
    Guid? GroupId,
    string? GroupName,
    KpiMeasureDto Measure,
    /// <summary>أعضاء لهم درجة معتمَدة داخل الفترة (دخلوا المرحلة الثانية من التوسيط).</summary>
    int ScoredMemberCount,
    /// <summary>أعضاء النطاق المنتمون للمجموعة إجمالًا (بمن فيهم بلا بيانات).</summary>
    int TotalMemberCount,
    /// <summary>DEC-01/16 — الأعضاء المؤهّلون الذين دخلوا المتوسّط الرسميّ فعلًا (تغطية ≥ الحدّ الأدنى).</summary>
    int QualifiedMemberCount = 0,
    /// <summary>
    /// DEC-01/17 — الأعضاء ذوو الدرجة المستبعَدون من المتوسّط الرسميّ لضعف التغطية:
    /// **لا يختفون** بل تُعرَض أسماؤهم وحالة نقصهم منفصلةً عن المتوسّط.
    /// </summary>
    IReadOnlyList<KpiEmployeeScoreDto>? ExcludedForInsufficientCoverage = null);

/// <summary>العقد التنظيميّ الموحّد: شركة + إدارات + فرق + موظّفون، كلّها بنفس الفترة والكادنس والنطاق.</summary>
public sealed record KpiPerformanceDto(
    KpiPeriodResolvedDto PeriodResolved,
    KpiPeriodResolvedDto PreviousPeriodResolved,
    /// <summary>الكادنس المطلوب صراحةً؛ <c>null</c> ⇒ الوضع التلقائيّ بتواتر كلّ موظّف (DEC-01/2+5).</summary>
    KpiCadence? Cadence,
    string ScopeType,
    KpiGroupScoreDto Company,
    IReadOnlyList<KpiGroupScoreDto> Departments,
    IReadOnlyList<KpiGroupScoreDto> Teams,
    IReadOnlyList<KpiEmployeeScoreDto> Employees,
    DateTime CalculatedAtUtc);

/// <summary>ترتيب الأفضل/المحتاجين للدعم — صفّ واحد لكلّ موظّف بعد تطبيق شرط التغطية (B-5/§5.7).</summary>
public sealed record KpiRankingsDto(
    KpiPeriodResolvedDto PeriodResolved,
    KpiCadence? Cadence,
    string ScopeType,
    IReadOnlyList<KpiEmployeeScoreDto> TopPerformers,
    IReadOnlyList<KpiEmployeeScoreDto> NeedsSupport,
    /// <summary>عدد الموظّفين الذين لديهم درجة لكن أُخرِجوا من الترتيب لضعف التغطية (شفافيّة لا إخفاء).</summary>
    int ExcludedForInsufficientCoverage,
    decimal MinimumCoverage,
    DateTime CalculatedAtUtc,
    /// <summary>DEC-01/17 — أسماء المستبعَدين وحالة نقصهم، لا العدد وحده.</summary>
    IReadOnlyList<KpiEmployeeScoreDto>? ExcludedEmployees = null,
    /// <summary>DEC-01/5+18 — موظّفون بلا تواتر/قالب فعّال: يظهرون بحالتهم ولا يُحسبون ناقصي تغطية.</summary>
    IReadOnlyList<KpiEmployeeScoreDto>? CadenceNotConfiguredEmployees = null);

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

/// <summary>
/// DEC-01/18 — فترة مصدريّة واحدة داخل نافذة التحليل: التزام متوقَّع ومآله.
/// وجودها هو ما يجعل <c>Missing</c> فترةً ناقصة محدَّدة الاسم لا رقمًا مجرَّدًا ولا صفرًا.
/// </summary>
public sealed record KpiSourcePeriodDto(
    string PeriodKey,
    DateOnly Start,
    DateOnly End,
    string Label,
    /// <summary>هل اكتمل تقييم لهذه الفترة (Approved أو Closed)؟</summary>
    bool IsCompleted,
    /// <summary>هل أُعفيت هذه الفترة (إجازة معتمَدة/استثناء إداريّ/خارج نافذة الخدمة)؟ ⇒ خارج المقام.</summary>
    bool IsExempt,
    /// <summary>سبب الإعفاء عند وجوده: approvedLeave | administrativeExemption | beforeHireDate | afterExitDate.</summary>
    string? ExemptReason,
    decimal? Score);

/// <summary>تفصيل رقم KPI إلى الصفوف التي بنته، بنفس النطاق والسياسة تمامًا.</summary>
public sealed record KpiDrilldownDto(
    KpiPeriodResolvedDto PeriodResolved,
    KpiCadence? Cadence,
    Guid? SubjectUserId,
    /// <summary>المتوسّط المُعاد حسابه من الصفوف المُعادة — يجب أن يطابق الرقم المعروض.</summary>
    decimal? RecomputedValue,
    int RowCount,
    IReadOnlyList<KpiDrilldownRowDto> Rows,
    DateTime CalculatedAtUtc,
    /// <summary>
    /// DEC-01/18 — المقاس الكامل للنطاق المطلوب: Expected · AdjustedExpected · Completed · Missing · Coverage.
    /// </summary>
    KpiMeasureDto? Measure = null,
    /// <summary>DEC-01/18 — الفترات المصدريّة؛ تُملأ عند تحديد موظّف بعينه.</summary>
    IReadOnlyList<KpiSourcePeriodDto>? SourcePeriods = null,
    /// <summary>DEC-01/5 — التواتر الفعّال للموظّف المحدَّد ومصدره.</summary>
    KpiCadence? EffectiveCadence = null,
    string CadenceSource = KpiCadenceSources.NotConfigured);

/// <summary>
/// استعلام تحليلات KPI.
/// <para>
/// DEC-01/2+5 — <c>Cadence</c> **اختياريّ**: تركُه فارغًا لا يعني سقوطًا صامتًا إلى النبض الأسبوعيّ،
/// بل يعني أنّ الخادم يحسم تواتر **كلّ موظّف** من قالبه الفعّال بترتيب أولويّة معلَن، ويسمّي الحالة
/// <c>CadenceNotConfigured</c> صراحةً حين لا يوجد قالب فعّال. تحديدُه صراحةً يفصل المسارين
/// (نبض أسبوعيّ / تقييم ربعيّ رسميّ) بلا مزج — وهو ما كانت تحميه B-3 قبل R5.
/// </para>
/// النطاق يُفرَض خادميًّا مهما كانت هذه القيم.
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
