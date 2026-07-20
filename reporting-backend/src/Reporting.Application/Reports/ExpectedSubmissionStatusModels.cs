using Reporting.Application.Common;
using Reporting.Domain.Enums;

namespace Reporting.Application.Reports;

/// <summary>
/// REPORT-EXPECTED-SUBMISSION-STATUS-R1 — الحالة المتوقّعة الموحّدة لدورة (User + Template + PeriodKey).
/// تُشتقّ من التزامات متوقّعة (Expected obligations) LEFT JOIN تسليمات، بعد بوّابة أرضيّة الانطباق.
/// لا تُحسَب في الواجهة إطلاقًا — الواجهة تعرض القيمة فقط.
/// </summary>
public enum ExpectedSubmissionStatus
{
    /// <summary>
    /// الدورة غير منطبِقة — لا تدخل العدّادات. يشمل «ما قبل أرضيّة الانطباق» و«الإعفاء/الإجازة».
    /// السبب المُصنَّف يُميَّز عبر <see cref="CycleExclusionReason"/> على الحقل ExclusionReasonCode
    /// (الثابت العموميّ لا يتغيّر تفاديًا لأثر الـ API؛ التمييز في نموذج القراءة).
    /// </summary>
    NotApplicable = 0,

    /// <summary>لا تسليم، ما زال ضمن المهلة (اليوم ≤ الموعد) — مهمّة قائمة لا تأخّر.</summary>
    NotStartedWithinDeadline = 1,

    /// <summary>مسودّة ضمن المهلة (قبل الموعد).</summary>
    DraftWithinDeadline = 2,

    /// <summary>تجاوز الموعد بلا تسليم صالح — جوهر الإصلاح.</summary>
    OverdueNotSubmitted = 3,

    /// <summary>مسودّة بعد الموعد.</summary>
    OverdueDraft = 4,

    /// <summary>مُعاد للتعديل — يحتاج إجراء الموظّف.</summary>
    ReturnedActionRequired = 5,

    /// <summary>مُصعَّد — يحتاج إجراء معتمِد.</summary>
    EscalatedActionRequired = 6,

    /// <summary>مُسلَّم (في الموعد أو متأخّرًا؛ يميّزه علم IsLate) — لا إجراء على الموظّف.</summary>
    Submitted = 7,

    /// <summary>اكتمل الاعتماد — لا إجراء.</summary>
    Approved = 8,

    /// <summary>مُغلَق (طرفيّ) — لا إجراء.</summary>
    Closed = 9,

    /// <summary>الموظّف غير نشط — يُستبعَد من التزامات المستقبل.</summary>
    InactiveUser = 10,

    /// <summary>حالة تاريخيّة يتعذّر تحديدها بثقة — تُستخدَم فقط عند الضرورة القصوى.</summary>
    HistoricalUnknown = 11
}

/// <summary>
/// REPORT-EXPECTED-SUBMISSION-STATUS-R1 — السبب المُصنَّف لاستبعاد دورة من التزامات العمل (نموذج قراءة صريح).
/// يفصل صراحةً «ما قبل أرضيّة الانطباق» (لم يكن التقرير مطلوبًا بعد) عن «الإعفاء/الإجازة المعتمَدة»،
/// حتى لا تُعرَض دورة سابقة لأرضيّة الانطباق كإعفاء موظّف. قيمة قراءة داخليّة (لا تُسلسَل عبر أيّ عقد API).
/// </summary>
public enum CycleExclusionReason
{
    /// <summary>لا استبعاد — الدورة مطلوبة (منطبِقة).</summary>
    None = 0,

    /// <summary>الدورة تبدأ قبل أرضيّة انطباق الموظّف/القالب — لم يكن التقرير مطلوبًا بعد (ليس إعفاءً ولا إجازة).</summary>
    BeforeApplicabilityFloor = 1,

    /// <summary>الموظّف غير نشط وقت الاستحقاق.</summary>
    InactiveUser = 2,

    /// <summary>إعفاء صريح أو إجازة معتمَدة تغطّي الدورة — منفصل تمامًا عن «ما قبل الأرضيّة».</summary>
    ExemptOrOnLeave = 3,

    /// <summary>لا قالب مطالبة مُسنَد للموظّف.</summary>
    NotAssigned = 4
}

/// <summary>
/// REPORT-EXPECTED-SUBMISSION-STATUS-R1 — نتيجة اشتقاق حالة دورة متوقّعة واحدة.
/// المفتاح المنطقيّ = UserId + TemplateId + PeriodKey. قيمة قراءة نقيّة بلا آثار جانبية.
/// </summary>
public sealed record ExpectedCycleResult(
    // الهوية
    Guid UserId,
    string UserFullName,
    Guid? TemplateId,
    string TemplateName,
    string PeriodKey,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueAt,
    // الانطباق
    bool IsExpected,
    DateOnly ApplicabilityFloor,
    ApplicabilitySource ApplicabilitySource,
    ApplicabilityConfidence ApplicabilityConfidence,
    // التسليم
    Guid? SubmissionId,
    SubmissionStatus? SubmissionStatus,
    bool HasSubmission,
    bool HasStarted,
    // الاشتقاق الزمنيّ
    bool IsWithinDeadline,
    bool IsLate,
    int DelayDays,
    bool IsActionable,
    bool IsHistorical,
    // الحالة الموحّدة + الرتبة
    ExpectedSubmissionStatus Status,
    UnifiedCycleStatus UnifiedStatus,
    int ActionRequiredRank,
    string? ExclusionReason,
    // العرض
    string StatusLabel,
    string Severity)
{
    /// <summary>
    /// السبب المُصنَّف لاستبعاد الدورة — يميّز «ما قبل الأرضيّة» عن «الإعفاء/الإجازة» (نموذج قراءة صريح).
    /// ExclusionReason النصّيّ يبقى للعرض البشريّ؛ هذا الحقل للتمييز البرمجيّ القاطع.
    /// </summary>
    public CycleExclusionReason ExclusionReasonCode { get; init; }

    /// <summary>الدور التنظيميّ (Department) للمالك — يملؤه المُجمِّع الإداريّ فقط عند الحاجة.</summary>
    public Guid? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public Guid? TeamId { get; init; }
    public string? TeamName { get; init; }
    public string? PrimaryRole { get; init; }
}

/// <summary>
/// عرض المستخدم لنفسه: الدورة الحاليّة منفصلة عن بنود العمل التاريخيّة.
/// شارة الموظّف = الدورة الحاليّة فقط؛ التاريخيّة قائمة مستقلّة (الأحدث أولًا).
/// </summary>
public sealed record ExpectedSelfStatus(
    ExpectedCycleResult? CurrentCycle,
    IReadOnlyList<ExpectedCycleResult> HistoricalActionItems);

/// <summary>إسقاط إداريّ لدورة واحدة عبر نطاق مستخدمين (users-first, LEFT JOIN submissions).</summary>
public sealed record ExpectedManagementProjection(
    string PeriodKey,
    string CycleLabel,
    int Expected,
    int Submitted,
    int NotStartedWithinDeadline,
    int OverdueNotSubmitted,
    int OverdueDraft,
    int ReturnedActionRequired,
    int EscalatedActionRequired,
    IReadOnlyList<ExpectedCycleResult> ActionItems);

/// <summary>استعلام الاشتقاق — يحدّد المستخدمين، مفاتيح الدورات، وفلتر القالب الاختياريّ.</summary>
public sealed record ExpectedStatusQuery(
    IReadOnlyCollection<Guid> UserIds,
    IReadOnlyList<string> CycleKeys,
    Guid? TemplateId);
