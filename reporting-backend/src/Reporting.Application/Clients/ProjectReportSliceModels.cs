using Reporting.Domain.Enums;

namespace Reporting.Application.Clients;

// ======================================================================
// شريحة المشروع من تسليم تقرير (PROJECT360-PROJECT-SCOPED-REPORT-NAVIGATION-FIX-R1)
//
// **المشكلة التي تعالجها هذه النماذج**: تقرير الموظّف الأسبوعيّ الواحد قد يحمل عمل
// عدّة مشروعات في حقل قسم متكرّر واحد، وكلّ عنصر فيه يحمل `projectId` صريحًا. فتح
// التسليم كاملًا من داخل مشروع معيّن كان يعرض عمل المشروعات الأخرى.
//
// **قرار التصميم**: الشريحة تُبنى **في الخادم** ولا تُرسَل الحمولة الكاملة إطلاقًا.
// ما لا يحمل `projectId` مطابقًا لا يغادر الخادم أصلًا — لا إخفاء في الواجهة.
//
// **لماذا لا يوجد عدّاد إجماليّ للعناصر**: أيّ رقم يكشف كم عنصرًا يخصّ مشروعات
// أخرى هو تسريب بحدّ ذاته، فلا يُعاد سوى ما يخصّ هذا المشروع.
// ======================================================================

/// <summary>
/// حقل قسم متكرّر بعد التصفية: تعريف الحقل (للعرض) + **عناصر هذا المشروع فقط**.
/// <para><c>ConfigJson</c> وصفٌ للقالب لا بيانات تشغيليّة، فهو محايد بين المشروعات
/// وتحتاجه الواجهة لتبني عناوين الحقول الفرعيّة بنفس منطق العرض القائم.</para>
/// </summary>
public sealed record ProjectReportSliceFieldDto(
    Guid TemplateFieldId,
    string Label,
    string? ConfigJson,
    int Order,
    IReadOnlyList<ProjectReportSliceEntryDto> Entries);

/// <summary>
/// عنصر مشروع واحد داخل الشريحة: إجابات مستوى المشروع + بنود العمل المنفَّذة داخله
/// (PROJECT360-MULTI-WORK-ITEMS-AND-REPORT-DISCOVERY-CLOSURE-R2).
/// <para><b>لماذا لم يبقَ العنصر قاموسًا مسطّحًا</b>: المشروع الواحد داخل التقرير الواحد قد يحمل
/// عدّة بنود عمل (كاروسيل + بوست ثابت + ريل…)، والتسطيح كان يفرض تكرار المشروع نفسه — وهو ما
/// يمنعه حارس التفرّد ⇒ كان الموظّف عاجزًا عن تسجيل عمله كاملًا.</para>
/// <para><b>محوِّل القراءة</b>: عنصر قديم (v1) بلا <c>workItems</c> يُعرَض ببندٍ ضمنيّ واحد مشتقّ من
/// <c>answers</c> نفسها — <b>عرضًا فقط، بلا أيّ كتابة على البيانات المخزَّنة</b>.</para>
/// </summary>
public sealed record ProjectReportSliceEntryDto(
    IReadOnlyDictionary<string, string?> Answers,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> WorkItems);

/// <summary>
/// مساهمة تسليم واحد في مشروع واحد — البيانات الوصفيّة اللازمة فقط + الشريحة.
/// </summary>
public sealed record ProjectReportSliceDto(
    Guid SubmissionId,
    Guid ProjectId,
    string ProjectName,
    Guid ClientId,
    string ClientName,
    Guid SubmitterId,
    string? SubmitterName,
    string? TemplateTitle,
    PeriodType PeriodType,
    string PeriodKey,
    SubmissionStatus Status,
    DateTime? SubmittedAtUtc,
    IReadOnlyList<ProjectReportSliceFieldDto> Fields);
