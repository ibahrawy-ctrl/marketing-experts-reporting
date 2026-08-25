namespace Reporting.Application.HrOperations;

/// <summary>
/// P2-HR-009 — الطوابير الأحد عشر للوحة العمليّات. الترقيم مستقرّ لأنّه يظهر في الروابط والتصدير.
/// </summary>
public enum HrOperationsQueue
{
    /// <summary>تقارير مطلوبة ولم تُقدَّم حتّى الآن.</summary>
    ReportsMissing = 1,

    /// <summary>تقارير تجاوزت موعدها (ناقصة أو مُقدَّمة متأخّرة).</summary>
    ReportsLate = 2,

    /// <summary>تقييمات أداء مطلوبة وغير مكتملة.</summary>
    KpiEvaluationsMissing = 3,

    /// <summary>تقييمات أُرسِلت وتنتظر اعتماد المراجِع.</summary>
    KpiEvaluationsAwaitingApproval = 4,

    /// <summary>موظّفون بلا تغطية قوالب أداء كافية ⇒ لا يمكن تقييمهم أصلًا.</summary>
    KpiCoverageInsufficient = 5,

    /// <summary>وقائع حضور بانتظار ردّ الموظّف ضمن نافذته.</summary>
    AttendanceAwaitingEmployee = 6,

    /// <summary>وقائع انقضت نافذة ردّ الموظّف عليها ولم يُردّ.</summary>
    AttendanceEmployeeSlaBreached = 7,

    /// <summary>وقائع بانتظار مراجعة الموارد البشريّة.</summary>
    AttendanceAwaitingHr = 8,

    /// <summary>وقائع تجاوزت مهلة مراجعة الموارد البشريّة.</summary>
    AttendanceHrSlaBreached = 9,

    /// <summary>إجازات واستئذانات وطلبات خدمة بانتظار إجراء.</summary>
    RequestsAwaitingAction = 10,

    /// <summary>خطط تحسين وعناصر حوكمة/امتثال تحتاج متابعة.</summary>
    FollowUpItems = 11
}

/// <summary>
/// صفّ موحَّد لكلّ الطوابير. الشكل واحد عمدًا كي تعمل عليه عدسة واحدة في الواجهة والتصدير معًا.
/// <para>
/// <b>لا يحمل هذا الصفّ أيّ نصّ حرّ حسّاس</b> (وصف واقعة، ردّ موظّف، ملاحظة موارد بشريّة، سبب رفض):
/// السطر عنوانٌ ومسار، والتفصيل يُفتَح من مصدره حيث تُفرَض رؤية الحقل كاملةً.
/// </para>
/// </summary>
public sealed record HrOperationsRowDto(
    HrOperationsQueue Queue,
    Guid EntityId,
    string EntityType,
    Guid SubjectUserId,
    string SubjectFullName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? TeamId,
    string? TeamName,
    string TitleAr,
    string TypeAr,
    string StatusAr,
    string? PeriodKey,
    DateOnly? DueAt,
    DateTime? SlaDueAtUtc,
    bool SlaBreached,
    int AgeingDays,
    Guid? OwnerUserId,
    string? OwnerFullName,
    string NextActionAr,
    DateTime? LastActionAtUtc);

/// <summary>
/// بطاقة طابور. <see cref="Count"/> ليس رقمًا مستقلًّا: هو <b>عدد صفوف الطابور نفسه</b>
/// تحت المرشِّح نفسه، محسوبًا من المجموعة ذاتها ⇒ لا يمكن للبطاقة أن تخالف تفصيلها بنيويًّا.
/// </summary>
public sealed record HrOperationsCardDto(
    HrOperationsQueue Queue,
    string Key,
    string TitleAr,
    string GroupAr,
    int Count,
    int BreachedCount,
    int MaxAgeingDays,
    string SeverityAr);

/// <summary>نطاق المُشاهِد كما حُسِب خادميًّا — يُعرَض كي لا يُقرَأ رقمٌ خارج سياقه.</summary>
public sealed record HrOperationsScopeDto(string ScopeType, int UserCount);

public sealed record HrOperationsDashboardDto(
    IReadOnlyList<string> PeriodKeys,
    HrOperationsScopeDto Scope,
    IReadOnlyList<HrOperationsCardDto> Cards);

public sealed record HrOperationsQueueDto(
    HrOperationsQueue Queue,
    string Key,
    string TitleAr,
    int TotalCount,
    int BreachedCount,
    int Page,
    int PageSize,
    IReadOnlyList<HrOperationsRowDto> Rows);

/// <summary>ناتج تصدير جاهز للحافّة — البايتات والاسم فقط، بلا أيّ قرار HTTP هنا.</summary>
public sealed record HrOperationsExportDto(
    string FileName,
    string ContentType,
    byte[] Content,
    int RowCount);

/// <summary>
/// مرشِّح موحَّد لكلّ الطوابير. <b>لا يوسّع النطاق أبدًا</b> — يضيّقه فقط داخل ما يراه المُشاهِد.
/// </summary>
public sealed record HrOperationsFilter(
    int? RecentCycles = null,
    string? FromCycleKey = null,
    string? ToCycleKey = null,
    Guid? DepartmentId = null,
    Guid? TeamId = null,
    Guid? UserId = null,
    string? Type = null,
    string? Status = null,
    bool OverdueOnly = false);

/// <summary>
/// فهرس الطوابير: المفتاح النصّيّ والعنوان والمجموعة. مصدر واحد للتسمية تقرؤه البطاقة
/// وصفحة التفصيل والتصدير معًا فلا تتفرّق الأسماء بينها.
/// </summary>
public static class HrOperationsCatalog
{
    public static IReadOnlyList<HrOperationsQueue> All { get; } =
        Enum.GetValues<HrOperationsQueue>().OrderBy(q => (int)q).ToList();

    public static string Key(HrOperationsQueue q) => q switch
    {
        HrOperationsQueue.ReportsMissing => "reports-missing",
        HrOperationsQueue.ReportsLate => "reports-late",
        HrOperationsQueue.KpiEvaluationsMissing => "kpi-missing",
        HrOperationsQueue.KpiEvaluationsAwaitingApproval => "kpi-awaiting-approval",
        HrOperationsQueue.KpiCoverageInsufficient => "kpi-coverage-gap",
        HrOperationsQueue.AttendanceAwaitingEmployee => "attendance-awaiting-employee",
        HrOperationsQueue.AttendanceEmployeeSlaBreached => "attendance-employee-sla-breached",
        HrOperationsQueue.AttendanceAwaitingHr => "attendance-awaiting-hr",
        HrOperationsQueue.AttendanceHrSlaBreached => "attendance-hr-sla-breached",
        HrOperationsQueue.RequestsAwaitingAction => "requests-awaiting-action",
        HrOperationsQueue.FollowUpItems => "follow-up-items",
        _ => "unknown"
    };

    public static HrOperationsQueue? FromKey(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : All.Cast<HrOperationsQueue?>()
                 .FirstOrDefault(q => string.Equals(Key(q!.Value), key, StringComparison.OrdinalIgnoreCase));

    public static string TitleAr(HrOperationsQueue q) => q switch
    {
        HrOperationsQueue.ReportsMissing => "تقارير مطلوبة لم تُقدَّم",
        HrOperationsQueue.ReportsLate => "تقارير متأخّرة",
        HrOperationsQueue.KpiEvaluationsMissing => "تقييمات مطلوبة غير مكتملة",
        HrOperationsQueue.KpiEvaluationsAwaitingApproval => "تقييمات بانتظار الاعتماد",
        HrOperationsQueue.KpiCoverageInsufficient => "موظّفون بلا تغطية تقييم كافية",
        HrOperationsQueue.AttendanceAwaitingEmployee => "وقائع بانتظار ردّ الموظّف",
        HrOperationsQueue.AttendanceEmployeeSlaBreached => "وقائع تجاوزت مهلة ردّ الموظّف",
        HrOperationsQueue.AttendanceAwaitingHr => "وقائع بانتظار مراجعة الموارد البشريّة",
        HrOperationsQueue.AttendanceHrSlaBreached => "وقائع تجاوزت مهلة المراجعة",
        HrOperationsQueue.RequestsAwaitingAction => "طلبات بانتظار إجراء",
        HrOperationsQueue.FollowUpItems => "بنود تحتاج متابعة",
        _ => "غير معروف"
    };

    public static string GroupAr(HrOperationsQueue q) => q switch
    {
        HrOperationsQueue.ReportsMissing or HrOperationsQueue.ReportsLate => "التقارير",
        HrOperationsQueue.KpiEvaluationsMissing
            or HrOperationsQueue.KpiEvaluationsAwaitingApproval
            or HrOperationsQueue.KpiCoverageInsufficient => "الأداء",
        HrOperationsQueue.AttendanceAwaitingEmployee
            or HrOperationsQueue.AttendanceEmployeeSlaBreached
            or HrOperationsQueue.AttendanceAwaitingHr
            or HrOperationsQueue.AttendanceHrSlaBreached => "الحضور والالتزام",
        HrOperationsQueue.RequestsAwaitingAction => "الطلبات",
        _ => "المتابعة"
    };

    /// <summary>
    /// شدّة البطاقة من واقع صفوفها لا من تقدير: خرقُ مهلةٍ واحد يكفي لرفعها.
    /// </summary>
    public static string Severity(int count, int breachedCount) =>
        count == 0 ? "سليم"
        : breachedCount > 0 ? "حرِج"
        : "يحتاج متابعة";
}
