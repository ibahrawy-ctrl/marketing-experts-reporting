namespace Reporting.Application.Reports;

/// <summary>
/// REPORT-EXPECTED-SUBMISSION-STATUS-R1 — مصدر الحقيقة الوحيد للحالة المتوقّعة لدورات التقارير.
/// المبدأ: <b>Users-first</b> (المستخدمون المطالَبون) LEFT JOIN التسليمات — لا Submissions-first.
/// لكلّ (UserId + TemplateId + PeriodKey) يُشتقّ: هل الدورة مطلوبة على أرضيّة الانطباق؟ ثمّ الحالة الموحّدة.
/// لا يكرّر منطق التقويم (<see cref="Common.ReportingCalendarPolicy"/>) ولا منطق الحالة
/// (<see cref="Domain.Common.UnifiedCycleStatusPolicy"/>) — يفوّض إليهما. قراءة فقط: لا تعديل تسليم/سير عمل/هجرة.
/// الوقت يُحقَن عبر <see cref="Common.ISystemClock"/> (اختبارات تثبّت «الآن»).
/// </summary>
public interface IExpectedSubmissionStatusResolver
{
    /// <summary>
    /// يشتقّ كلّ دورات (User × PeriodKey) المتوقّعة للاستعلام، مع بوّابة الانطباق والحالة الموحّدة.
    /// يعيد صفًّا لكلّ (مستخدم، دورة) قابل للتقييم؛ الدورات قبل أرضيّة الانطباق تُوسَم NotApplicable ولا تُعَدّ.
    /// </summary>
    Task<IReadOnlyList<ExpectedCycleResult>> ResolveAsync(ExpectedStatusQuery query, CancellationToken ct = default);

    /// <summary>
    /// عرض المستخدم لنفسه: الدورة الحاليّة (شارة الموظّف) منفصلة عن بنود العمل التاريخيّة (الأحدث أولًا).
    /// الدورة الحاليّة = دورة «اليوم» فقط؛ لا تُخفيها بنود تاريخيّة متأخّرة.
    /// </summary>
    Task<ExpectedSelfStatus> ResolveSelfAsync(Guid userId, IReadOnlyList<string> cycleKeys, Guid? templateId = null, CancellationToken ct = default);

    /// <summary>
    /// إسقاط إداريّ لدورة واحدة عبر نطاق مستخدمين: عدّادات متوقّع/مُسلَّم/متأخّر + بنود الإجراء (users-first).
    /// </summary>
    Task<ExpectedManagementProjection> ResolveManagementAsync(string cycleKey, IReadOnlyCollection<Guid> userIds, Guid? templateId = null, CancellationToken ct = default);
}
