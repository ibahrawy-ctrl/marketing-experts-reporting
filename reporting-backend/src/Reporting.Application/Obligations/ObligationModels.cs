namespace Reporting.Application.Obligations;

/// <summary>
/// P2-HR-008 — نوع الالتزام. القائمة مغلقة عمدًا: كلّ نوع جديد يستلزم مصدر اشتقاق موثَّقًا،
/// ولا يُضاف نوع «محسوب في الواجهة» إطلاقًا.
/// </summary>
public enum ObligationKind
{
    /// <summary>تقرير دوريّ مُسنَد عبر <c>ReportTemplateAssignment</c>.</summary>
    Report = 1,

    /// <summary>تقييم KPI مُسنَد عبر <c>KpiTemplateAssignment</c>.</summary>
    KpiEvaluation = 2
}

/// <summary>
/// P2-HR-008 — حالة الالتزام الواحد. حالة واحدة حصريّة لا تتقاطع مع غيرها.
/// <para><b>Missing ليس Zero</b>: الالتزام غير المنطبِق يُصنَّف <see cref="Exempt"/> أو
/// <see cref="NotApplicable"/> ولا يدخل عدّاد النقص إطلاقًا.</para>
/// </summary>
public enum ObligationState
{
    /// <summary>لا إسناد ⇒ لا التزام أصلًا. لا يُعَدّ ناقصًا ولا متأخّرًا تحت أيّ ظرف.</summary>
    NotApplicable = 0,

    /// <summary>مطلوب وما زال ضمن المهلة (لم يُنجَز بعد ولم يتجاوز <c>DueAt</c>).</summary>
    Pending = 1,

    /// <summary>مُنجَز (سُلّم/اعتُمِد/أُغلِق) — قد يكون متأخّرًا، ويميّزه <c>Late</c>.</summary>
    Fulfilled = 2,

    /// <summary>مطلوب، وتجاوز <c>DueAt</c>، ولم يُنجَز. هذا وحده «النقص».</summary>
    Missing = 3,

    /// <summary>مُعفى: إجازة معتمَدة تغطّي المهلة، أو موظّف غير نشط، أو ما قبل أرضيّة الانطباق.</summary>
    Exempt = 4
}

/// <summary>
/// P2-HR-008 — سبب الإعفاء/عدم الانطباق المُصنَّف. يمنع خلط «لم يكن مطلوبًا بعد» بـ«معفى بإجازة».
/// </summary>
public enum ObligationExemptionReason
{
    /// <summary>لا إعفاء — الالتزام مطلوب فعلًا.</summary>
    None = 0,

    /// <summary>لا قالب مُسنَد للموظّف في هذه الفترة.</summary>
    NotAssigned = 1,

    /// <summary>الموظّف غير نشط.</summary>
    InactiveUser = 2,

    /// <summary>الفترة تسبق أرضيّة انطباق الموظّف/القالب — لم يكن مطلوبًا بعد.</summary>
    BeforeApplicabilityFloor = 3,

    /// <summary>إجازة معتمَدة من الموارد البشريّة تغطّي كامل المدى حتّى موعد الاستحقاق.</summary>
    ApprovedLeave = 4
}

/// <summary>
/// P2-HR-008 — التزام واحد مُشتقّ. <b>قيمة قراءة نقيّة</b>: لا يُخزَّن في أيّ جدول موازٍ،
/// ويُعاد اشتقاقه في كلّ نداء من مصادره الأصليّة (الإسناد + التقويم + التسليم/التقييم + الإجازات).
/// </summary>
/// <param name="Kind">نوع الالتزام.</param>
/// <param name="SubjectUserId">الموظّف موضوع الالتزام (صاحب التقرير أو المُقيَّم).</param>
/// <param name="SubjectFullName">اسم الموظّف موضوع الالتزام.</param>
/// <param name="OwnerUserId">
/// <b>المالك = من يقع عليه الفعل</b>. في التقرير هو الموظّف نفسه؛ في تقييم KPI هو المُقيِّم
/// (تجاوز المراجِع إن وُجد وإلّا المدير المباشر)، وقد يكون <c>null</c> إن تعذّر تحديده.
/// </param>
/// <param name="OwnerFullName">اسم المالك، أو null.</param>
/// <param name="SourceKind">اسم الكيان المصدر (<c>ReportTemplateAssignment</c>/<c>KpiTemplateAssignment</c>).</param>
/// <param name="SourceId">معرّف القالب المصدر.</param>
/// <param name="SourceName">عنوان القالب المصدر.</param>
/// <param name="PeriodKey">مفتاح الفترة (<c>YYYY-Www</c> أو <c>YYYY-Qn</c>).</param>
/// <param name="PeriodStart">بداية الفترة بتوقيت الرياض.</param>
/// <param name="PeriodEnd">نهاية الفترة بتوقيت الرياض.</param>
/// <param name="DueAt">تاريخ الاستحقاق المشتقّ من التقويم ودور المالك.</param>
/// <param name="Expected">هل الالتزام مطلوب فعلًا؟ (false ⇒ لا يدخل أيّ عدّاد نقص/تأخّر).</param>
/// <param name="Fulfilled">هل أُنجِز؟</param>
/// <param name="Missing">هل مطلوب وتجاوز المهلة بلا إنجاز؟</param>
/// <param name="Late">هل تأخّر (أُنجِز بعد المهلة، أو ما زال ناقصًا بعدها)؟</param>
/// <param name="LateByDays">مقدار التأخّر بالأيّام (0 إن لم يتأخّر).</param>
/// <param name="State">الحالة الحصريّة.</param>
/// <param name="ExemptionReason">سبب الإعفاء المُصنَّف.</param>
/// <param name="StateLabel">تسمية عربيّة للعرض — تُحسَب خادميًّا ولا تُشتقّ في الواجهة.</param>
/// <param name="FulfilledAtUtc">لحظة الإنجاز إن وُجدت.</param>
/// <param name="ReferenceId">معرّف التسليم/التقييم المُنجِز إن وُجد (للتنقّل إلى المصدر).</param>
public sealed record ObligationDto(
    ObligationKind Kind,
    Guid SubjectUserId,
    string SubjectFullName,
    Guid? OwnerUserId,
    string? OwnerFullName,
    string SourceKind,
    Guid? SourceId,
    string SourceName,
    string PeriodKey,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueAt,
    bool Expected,
    bool Fulfilled,
    bool Missing,
    bool Late,
    int LateByDays,
    ObligationState State,
    ObligationExemptionReason ExemptionReason,
    string StateLabel,
    DateTime? FulfilledAtUtc,
    Guid? ReferenceId);

/// <summary>P2-HR-008 — عدّادات مجمَّعة. <c>Expected = Fulfilled + Pending + Missing</c> دائمًا.</summary>
public sealed record ObligationSummaryDto(
    int Expected,
    int Fulfilled,
    int Pending,
    int Missing,
    int Late,
    int Exempt);

/// <summary>P2-HR-008 — ناتج الاستعلام: المدى الزمنيّ + العدّادات + البنود.</summary>
public sealed record ObligationsResultDto(
    IReadOnlyList<string> PeriodKeys,
    ObligationSummaryDto Summary,
    IReadOnlyList<ObligationDto> Items);

/// <summary>
/// P2-HR-008 — استعلام الاشتقاق الداخليّ. <b>النطاق مفروض قبل الوصول إلى هنا</b>:
/// هذا النوع لا يُبنى إلّا من معرّفات تحقّق منها المتّصل مسبقًا.
/// </summary>
public sealed record ObligationQuery(
    IReadOnlyCollection<Guid> UserIds,
    IReadOnlyList<string> CycleKeys,
    ObligationKind? Kind = null);

/// <summary>
/// P2-HR-008 — مرشِّح نقطة النهاية العامّة. <c>UserId</c> خارج نطاق المُشاهِد ⇒ 404 لا 403.
/// </summary>
/// <param name="UserId">حصر النتيجة بموظّف واحد (اختياريّ).</param>
/// <param name="RecentCycles">عدد الدورات الأخيرة (1..26، الافتراضيّ 8) حين لا يُحدَّد مدى صريح.</param>
/// <param name="FromCycleKey">أوّل دورة في المدى الصريح.</param>
/// <param name="ToCycleKey">آخر دورة في المدى الصريح.</param>
/// <param name="Kind">حصر النتيجة بنوع التزام واحد.</param>
/// <param name="OnlyActionable">إعادة ما يحتاج إجراءً فقط (Pending/Missing).</param>
public sealed record ObligationsFilter(
    Guid? UserId = null,
    int? RecentCycles = null,
    string? FromCycleKey = null,
    string? ToCycleKey = null,
    ObligationKind? Kind = null,
    bool OnlyActionable = false);
